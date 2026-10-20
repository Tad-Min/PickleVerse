using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Entities.Models;
using Repositories.UnitOfWork;
using Services.DTOs;
using Services.Interfaces;

namespace Services.Implementations
{
    public class AchievementService : IAchievementService
    {
        private readonly IUnitOfWork _unitOfWork;

        public AchievementService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<AchievementDto>> GetMyAchievementsAsync(Guid playerId)
        {
            var achievements = await _unitOfWork.Achievements.GetAllAchievementsAsync();
            var playerAchievements = await _unitOfWork.Achievements.GetPlayerAchievementsAsync(playerId);

            var result = new List<AchievementDto>();
            foreach (var item in achievements)
            {
                var pa = playerAchievements.FirstOrDefault(x => x.Achievementid == item.Achievementid);
                var progress = pa?.Progress ?? 0;
                result.Add(new AchievementDto
                {
                    AchievementId = item.Achievementid,
                    AchievementCode = item.Achievementcode,
                    AchievementName = item.Achievementname,
                    Description = item.Description,
                    Type = item.Type,
                    Target = item.Target,
                    Reward = item.Reward,
                    CurrentProgress = progress,
                    IsCompleted = progress >= item.Target,
                    IsClaimed = pa?.Isclaimed ?? false
                });
            }

            return result;
        }

        public async Task<bool> IncrementProgressAsync(Guid playerId, int achievementId, int amount = 1)
        {
            var pa = await _unitOfWork.Achievements.GetPlayerAchievementAsync(playerId, achievementId);

            if (pa == null)
            {
                pa = new Playerachievement
                {
                    Playerid = playerId,
                    Achievementid = achievementId,
                    Progress = amount,
                    Isclaimed = false
                };
                await _unitOfWork.Achievements.AddPlayerAchievementAsync(pa);
            }
            else
            {
                pa.Progress += amount;
                await _unitOfWork.Achievements.UpdatePlayerAchievementAsync(pa);
            }

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<(bool Success, string Message, int ExpGained)> ClaimRewardAsync(Guid playerId, int achievementId)
        {
            var achievement = await _unitOfWork.Achievements.GetByIdAsync(achievementId);
            if (achievement == null) return (false, "Thành tựu không tồn tại.", 0);

            var pa = await _unitOfWork.Achievements.GetPlayerAchievementAsync(playerId, achievementId);

            if (pa == null || pa.Progress < achievement.Target)
            {
                return (false, "Chưa hoàn thành chỉ tiêu thành tựu.", 0);
            }

            if (pa.Isclaimed)
            {
                return (false, "Phần thưởng thành tựu này đã được nhận rồi.", 0);
            }

            pa.Isclaimed = true;
            pa.Claimedat = DateTime.UtcNow;
            await _unitOfWork.Achievements.UpdatePlayerAchievementAsync(pa);

            var profile = await _unitOfWork.Profiles.GetByUserIdAsync(playerId);
            int exp = 100;
            if (profile != null)
            {
                profile.Exp += exp;
                if (profile.Exp >= profile.Level * 100)
                {
                    profile.Level += 1;
                    profile.Exp = 0;
                }
                await _unitOfWork.Profiles.UpdateProfileAsync(profile);
            }

            await _unitOfWork.SaveChangesAsync();
            return (true, $"Mở khóa thành công danh hiệu và phần thưởng: {achievement.Reward}!", exp);
        }
    }
}
