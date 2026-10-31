using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Entities.DTOs;

namespace Services.Interfaces
{
    public interface IAchievementService
    {
        Task<List<AchievementDto>> GetMyAchievementsAsync(Guid playerId);
        Task<bool> IncrementProgressAsync(Guid playerId, int achievementId, int amount = 1);
        Task<(bool Success, string Message, int ExpGained)> ClaimRewardAsync(Guid playerId, int achievementId);
    }
}
