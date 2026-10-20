using System;
using System.Threading.Tasks;
using Services.DTOs;

namespace Services.Interfaces
{
    public interface IReplayService
    {
        Task<(bool Success, string Message, string? ReplayUrl)> SaveReplayAsync(Guid matchId, string replayJsonData);
        Task<MatchReplayResponse?> GetReplayAsync(Guid matchId);
    }
}
