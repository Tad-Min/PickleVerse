using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Entities.Models;
using Repositories.Base;

namespace Repositories.Interfaces
{
    public interface IMatchRepository : IGenericRepository<Matchhistory>
    {
        Task<Matchhistory?> GetMatchByIdAsync(Guid matchId);
        Task<List<Matchhistory>> GetPlayerMatchesAsync(Guid playerId, int limit = 20);
        Task<List<Matchhistory>> GetRecentMatchesAsync(int limit = 50);
        Task AddMatchAsync(Matchhistory match);
        Task AddParticipantAsync(Playermatchhistory participant);
        Task<Matchhistoryreplay?> GetTelemetryReplayAsync(Guid matchId);
        Task AddTelemetryReplayAsync(Matchhistoryreplay replay);
        Task UpdateTelemetryReplayAsync(Matchhistoryreplay replay);
        Task AddReportAsync(Reportplayerinmatch report);
        Task<List<Reportplayerinmatch>> GetReportsAsync(string? status);
        Task<Reportplayerinmatch?> GetReportAsync(Guid reporterId, Guid reportedPlayerId, Guid matchId);
        Task UpdateReportAsync(Reportplayerinmatch report);
    }
}
