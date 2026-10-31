using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Entities.Models;
using Repositories.UnitOfWork;
using Entities.DTOs;
using Services.Interfaces;

namespace Services.Implementations
{
    public class AiCoachingService : IAiCoachingService
    {
        private readonly IUnitOfWork _unitOfWork;

        public AiCoachingService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<MasteryDto>> GetMasteryStatusAsync(Guid playerId)
        {
            var techniques = await _unitOfWork.AiCoaching.GetAllTechniquesAsync();
            var masteries = await _unitOfWork.AiCoaching.GetPlayerMasteriesAsync(playerId);

            var result = new List<MasteryDto>();

            foreach (var t in techniques)
            {
                var m = masteries.FirstOrDefault(p => p.Techniqueid == t.Techniqueid);
                result.Add(new MasteryDto
                {
                    TechniqueId = t.Techniqueid,
                    TechniqueName = t.Techniquename,
                    MasteryScore = m?.Masteryscore ?? 0,
                    TotalAttempts = m?.Totalattempts ?? 0,
                    TotalErrors = m?.Totalerrors ?? 0
                });
            }

            return result;
        }

        public async Task<RecommendationResponse> GetRecommendedDrillsAsync(Guid playerId)
        {
            var masteries = await GetMasteryStatusAsync(playerId);

            // Tìm kỹ thuật có điểm thành thạo thấp nhất
            var weakest = masteries.OrderBy(m => m.MasteryScore).FirstOrDefault();

            if (weakest == null)
            {
                return new RecommendationResponse
                {
                    CoachMessage = "Bạn chưa có dữ liệu kỹ thuật. Hãy bắt đầu từ Bài học số 1!",
                    RecommendedDrills = new List<DrillDto>()
                };
            }

            var lessons = await _unitOfWork.Training.GetAllLessonsWithDrillsAsync();
            var matchedDrills = lessons
                .SelectMany(l => l.Lessondrills)
                .Where(ld => ld.Drill.Techniqueid == weakest.TechniqueId)
                .Take(3)
                .Select(ld => new DrillDto
                {
                    DrillId = ld.Drillid,
                    TechniqueId = ld.Drill.Techniqueid,
                    TechniqueName = weakest.TechniqueName,
                    DrillName = ld.Drill.Drillname,
                    Description = ld.Drill.Description,
                    Target = ld.Target
                }).ToList();

            string coachMessage = weakest.MasteryScore < 50
                ? $"Kỹ năng {weakest.TechniqueName} của bạn đang ở mức yếu ({weakest.MasteryScore}%). Huấn luyện viên khuyên bạn nên tập trung cải thiện ngay hôm nay!"
                : $"Bạn đang có phong độ tốt ở kỹ năng {weakest.TechniqueName} ({weakest.MasteryScore}%). Hãy tiếp tục hoàn thành các bài tập nâng cao để đạt điểm tuyệt đối!";

            return new RecommendationResponse
            {
                WeakestTechniqueName = weakest.TechniqueName,
                CurrentMasteryScore = weakest.MasteryScore,
                CoachMessage = coachMessage,
                RecommendedDrills = matchedDrills
            };
        }

        public async Task<bool> LogTechniqueErrorAsync(Guid playerId, LogTechniqueErrorRequest request)
        {
            // 1. Lưu bản ghi lỗi
            var errorEntity = new Playertechniqueserror
            {
                Playerid = playerId,
                Techniqueid = request.TechniqueId,
                Matchid = request.MatchId,
                Lessonid = request.LessonId,
                Errortype = request.ErrorType,
                Coachingtip = request.CoachingTip,
                Createdat = DateTime.UtcNow
            };

            await _unitOfWork.AiCoaching.AddTechniqueErrorAsync(errorEntity);

            // 2. Cập nhật số lần thử và số lỗi trong bảng Mastery
            var mastery = await _unitOfWork.AiCoaching.GetPlayerMasteryAsync(playerId, request.TechniqueId);

            if (mastery == null)
            {
                mastery = new Playertechniquemastery
                {
                    Playerid = playerId,
                    Techniqueid = request.TechniqueId,
                    Totalattempts = 1,
                    Totalerrors = 1,
                    Lastupdatedat = DateTime.UtcNow
                };
                await _unitOfWork.AiCoaching.AddPlayerMasteryAsync(mastery);
            }
            else
            {
                mastery.Totalattempts += 1;
                mastery.Totalerrors += 1;
                mastery.Lastupdatedat = DateTime.UtcNow;
                await _unitOfWork.AiCoaching.UpdatePlayerMasteryAsync(mastery);
            }

            await _unitOfWork.SaveChangesAsync();
            return true;
        }
    }
}
