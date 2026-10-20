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
    public class MatchRepository : GenericRepository<Matchhistory>, IMatchRepository
    {
        public MatchRepository(PickleVerseContext context) : base(context)
        {
        }

        public async Task<Matchhistory?> GetMatchByIdAsync(Guid matchId)
        {
            return await _context.Matchhistories
                .Include(m => m.Playermatchhistories)
                    .ThenInclude(p => p.Player)
                .FirstOrDefaultAsync(m => m.Matchid == matchId);
        }

        public async Task<List<Matchhistory>> GetPlayerMatchesAsync(Guid playerId, int limit = 20)
        {
            return await _context.Matchhistories
                .Include(m => m.Playermatchhistories)
                .Where(m => m.Playermatchhistories.Any(p => p.Playerid == playerId))
                .OrderByDescending(m => m.Createdat)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<Matchhistory>> GetRecentMatchesAsync(int limit = 50)
        {
            return await _context.Matchhistories
                .Include(m => m.Playermatchhistories)
                    .ThenInclude(p => p.Player)
                .OrderByDescending(m => m.Createdat)
                .Take(limit)
                .ToListAsync();
        }

        public async Task AddMatchAsync(Matchhistory match)
        {
            await _context.Matchhistories.AddAsync(match);
        }

        public async Task AddParticipantAsync(Playermatchhistory participant)
        {
            await _context.Playermatchhistories.AddAsync(participant);
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

        public async Task<List<Reportplayerinmatch>> GetReportsAsync(string? status)
        {
            var query = _context.Reportplayerinmatches
                .Include(r => r.Reporter)
                .Include(r => r.Reportedplayer)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                var norm = status.ToLower();
                query = query.Where(r => r.Status.ToLower() == norm);
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
