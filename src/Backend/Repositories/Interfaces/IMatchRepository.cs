using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Entities.Models;
using Repositories.Base;

namespace Repositories.Interfaces
{
    public interface IMatchRepository : IGenericRepository<Gamematch>
    {
        Task<Gamematch?> GetMatchByIdAsync(Guid matchId);
        Task<List<Gamematch>> GetPlayerMatchesAsync(Guid playerId, int limit = 20);
        Task<List<Gamematch>> GetRecentMatchesAsync(int limit = 50);
        Task AddMatchAsync(Gamematch match);
        Task AddParticipantAsync(Gamematchplayer participant);
        Task<Matchhistoryreplay?> GetTelemetryReplayAsync(Guid matchId);
        Task AddTelemetryReplayAsync(Matchhistoryreplay replay);
        Task UpdateTelemetryReplayAsync(Matchhistoryreplay replay);
        Task AddReportAsync(Reportplayerinmatch report);
        Task<List<Reportplayerinmatch>> GetReportsAsync(ReportStatus? status);
        Task<Reportplayerinmatch?> GetReportAsync(Guid reporterId, Guid reportedPlayerId, Guid matchId);
        Task UpdateReportAsync(Reportplayerinmatch report);
    }
}
