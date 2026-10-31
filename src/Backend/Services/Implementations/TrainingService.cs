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
    public class TrainingService : ITrainingService
    {
        private readonly IUnitOfWork _unitOfWork;

        public TrainingService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<LessonDto>> GetLessonsAsync(Guid playerId)
        {
            var lessons = await _unitOfWork.Training.GetAllLessonsWithDrillsAsync();
            var playerDrills = await _unitOfWork.Training.GetAllPlayerDrillsAsync(playerId);

            var result = new List<LessonDto>();
            bool previousLessonCompleted = true;

            foreach (var l in lessons)
            {
                var lessonDto = new LessonDto
                {
                    LessonId = l.Lessonid,
                    LessonName = l.Lessonname,
                    Description = l.Description
                };

                // Trạng thái mở khóa
                if (previousLessonCompleted)
                {
                    lessonDto.Status = "UNLOCKED";
                }
                else
                {
                    lessonDto.Status = "LOCKED";
                }

                bool allDrillsPassed = l.Lessondrills.Any();

                foreach (var ld in l.Lessondrills)
                {
                    var pd = playerDrills.FirstOrDefault(p => p.Lessonid == l.Lessonid && p.Drillid == ld.Drillid);
                    var bestScore = pd?.Score ?? 0;

                    if (bestScore <= 0)
                    {
                        allDrillsPassed = false;
                    }

                    lessonDto.Drills.Add(new DrillDto
                    {
                        DrillId = ld.Drillid,
                        TechniqueId = ld.Drill.Techniqueid,
                        TechniqueName = ld.Drill.Technique?.Techniquename ?? string.Empty,
                        DrillName = ld.Drill.Drillname,
                        Description = ld.Drill.Description,
                        Target = ld.Target,
                        BestScore = bestScore,
                        IsClaimedReward = pd?.Isclaimedreward ?? false
                    });
                }

                if (allDrillsPassed && l.Lessondrills.Any())
                {
                    lessonDto.Status = "COMPLETED";
                    previousLessonCompleted = true;
                }
                else
                {
                    previousLessonCompleted = false;
                }

                result.Add(lessonDto);
            }

            return result;
        }

        public async Task<SubmitDrillResponse> SubmitDrillResultAsync(Guid playerId, SubmitDrillRequest request)
        {
            var record = await _unitOfWork.Training.GetPlayerLessonDrillAsync(playerId, request.LessonId, request.DrillId);

            int expGained = 0;
            bool levelUp = false;
            var profile = await _unitOfWork.Profiles.GetByUserIdAsync(playerId);

            if (record == null)
            {
                record = new Playerlessondrill
                {
                    Playerid = playerId,
                    Lessonid = request.LessonId,
                    Drillid = request.DrillId,
                    Score = request.Score,
                    Isclaimedreward = request.Score > 0
                };

                if (request.Score > 0)
                {
                    expGained = request.Score * 50;
                }

                await _unitOfWork.Training.AddPlayerLessonDrillAsync(record);
            }
            else
            {
                if (request.Score > record.Score)
                {
                    record.Score = request.Score;
                }

                if (!record.Isclaimedreward && request.Score > 0)
                {
                    record.Isclaimedreward = true;
                    expGained = request.Score * 50;
                }

                await _unitOfWork.Training.UpdatePlayerLessonDrillAsync(record);
            }

            // Cộng Exp và tính Level Up
            if (expGained > 0 && profile != null)
            {
                profile.Exp += expGained;
                int expNeeded = profile.Level * 200;
                if (profile.Exp >= expNeeded)
                {
                    profile.Level += 1;
                    profile.Exp -= expNeeded;
                    levelUp = true;
                }
                await _unitOfWork.Profiles.UpdateProfileAsync(profile);
            }

            await _unitOfWork.SaveChangesAsync();

            return new SubmitDrillResponse
            {
                Success = true,
                Message = "Ghi nhận kết quả bài tập thành công!",
                ExpGained = expGained,
                LevelUp = levelUp,
                NewLevel = profile?.Level ?? 1,
                NewExp = profile?.Exp ?? 0
            };
        }
    }
}
