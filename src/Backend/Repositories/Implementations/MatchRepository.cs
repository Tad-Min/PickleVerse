using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Entities.Models;
using Microsoft.EntityFrameworkCore;
using Repositories.Base;
using Repositories.Context;
using Repositories.Interfaces;

namespace Repositories.Implementations
{
    public class MatchRepository : GenericRepository<Gamematch>, IMatchRepository
    {
        public MatchRepository(PickleVerseContext context) : base(context)
        {
        }

        public async Task<Gamematch?> GetMatchByIdAsync(Guid matchId)
        {
            return await _context.Gamematches
                .Include(m => m.Gamematchplayers)
                    .ThenInclude(p => p.Player)
                .FirstOrDefaultAsync(m => m.Matchid == matchId && m.Status == MatchStatus.Finished && m.Endedat != null);
        }

        public async Task<List<Gamematch>> GetPlayerMatchesAsync(Guid playerId, int limit = 20)
        {
            return await _context.Gamematches
                .Include(m => m.Gamematchplayers).Include(m => m.Ratingledgers).Include(m => m.Matchscores).AsSplitQuery()
                .Where(m => m.Status == MatchStatus.Finished && m.Endedat != null && m.Gamematchplayers.Any(p => p.Playerid == playerId && p.Finalizedat != null))
                .Where(m => m.Status == MatchStatus.Finished && m.Endedat != null)
                .OrderByDescending(m => m.Endedat).ThenByDescending(m => m.Sequence)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<Gamematch>> GetRecentMatchesAsync(int limit = 50)
        {
            return await _context.Gamematches
                .Include(m => m.Gamematchplayers)
                    .ThenInclude(p => p.Player)
                .Include(m => m.Matchscores).AsSplitQuery()
                .Where(m => m.Status == MatchStatus.Finished && m.Endedat != null)
                .OrderByDescending(m => m.Endedat).ThenByDescending(m => m.Sequence)
                .Take(limit)
                .ToListAsync();
        }

        public async Task AddMatchAsync(Gamematch match)
        {
            await _context.Gamematches.AddAsync(match);
        }

        public async Task AddParticipantAsync(Gamematchplayer participant)
        {
            await _context.Gamematchplayers.AddAsync(participant);
        }

        public async Task<Matchhistoryreplay?> GetTelemetryReplayAsync(Guid matchId)
        {
            return await _context.Matchhistoryreplays
                .FirstOrDefaultAsync(r => r.Matchid == matchId);
        }

        public async Task AddTelemetryReplayAsync(Matchhistoryreplay replay)
        {
            await _context.Matchhistoryreplays.AddAsync(replay);
        }

        public Task UpdateTelemetryReplayAsync(Matchhistoryreplay replay)
        {
            _context.Matchhistoryreplays.Update(replay);
            return Task.CompletedTask;
        }

        public async Task AddReportAsync(Reportplayerinmatch report)
        {
            await _context.Reportplayerinmatches.AddAsync(report);
        }

        public async Task<List<Reportplayerinmatch>> GetReportsAsync(ReportStatus? status)
        {
            var query = _context.Reportplayerinmatches
                .Include(r => r.Reporter)
                .Include(r => r.Reportedplayer)
                .AsQueryable();

            if (status.HasValue)
            {
                var norm = status.Value;
                query = query.Where(r => r.Status == norm);
            }

            return await query.OrderByDescending(r => r.Createdat).ToListAsync();
        }

        public async Task<Reportplayerinmatch?> GetReportAsync(Guid reporterId, Guid reportedPlayerId, Guid matchId)
        {
            return await _context.Reportplayerinmatches
                .Include(r => r.Reporter)
                .Include(r => r.Reportedplayer)
                .FirstOrDefaultAsync(r => r.Reporterid == reporterId && r.Reportedplayerid == reportedPlayerId && r.Matchid == matchId);
        }

        public Task UpdateReportAsync(Reportplayerinmatch report)
        {
            _context.Reportplayerinmatches.Update(report);
            return Task.CompletedTask;
        }
    }
}
