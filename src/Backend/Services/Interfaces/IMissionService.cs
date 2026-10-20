using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Services.DTOs;

namespace Services.Interfaces
{
    public interface IMissionService
    {
        Task<List<MissionDto>> GetMyMissionsAsync(Guid playerId);
        Task<bool> IncrementProgressAsync(Guid playerId, int missionId, int amount = 1);
        Task<(bool Success, string Message, int ExpGained)> ClaimRewardAsync(Guid playerId, int missionId);
    }
}
