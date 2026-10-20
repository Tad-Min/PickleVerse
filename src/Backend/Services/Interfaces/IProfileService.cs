using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Services.DTOs;

namespace Services.Interfaces
{
    public interface IProfileService
    {
        Task<PlayerProfileResponse?> GetMyProfileAsync(Guid userId);
        Task<PlayerProfileResponse?> GetProfileByIdAsync(Guid playerId);
        Task<bool> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
        Task<bool> SyncTimePlayedAsync(Guid userId, long playedSeconds);
        Task<List<PlayerProfileResponse>> GetLeaderboardAsync(int top = 50);
    }
}
