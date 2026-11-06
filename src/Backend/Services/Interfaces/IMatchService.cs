using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Entities.DTOs;

namespace Services.Interfaces
{
    public interface IMatchService
    {
        Task<Guid> FinishMatchAsync(FinishMatchRequest request);
        Task<List<MatchItemDto>> GetMyMatchHistoryAsync(Guid playerId, int take = 20);
        Task<bool> ReportPlayerAsync(Guid reporterId, ReportPlayerRequest request);
        Task<List<ReportTargetDto>> ReportTargets(Guid player, Guid match);
    }
}
