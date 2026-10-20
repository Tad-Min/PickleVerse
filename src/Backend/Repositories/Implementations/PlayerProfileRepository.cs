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
    public class PlayerProfileRepository : GenericRepository<Playerprofile>, IPlayerProfileRepository
    {
        public PlayerProfileRepository(PickleVerseContext context) : base(context)
        {
        }

        public async Task<Playerprofile?> GetByUserIdAsync(Guid userId)
        {
            return await _context.Playerprofiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Userid == userId);
        }

        public async Task<List<Playerprofile>> GetLeaderboardAsync(int top = 50)
        {
            return await _context.Playerprofiles
                .Include(p => p.User)
                .OrderByDescending(p => p.Elo)
                .ThenByDescending(p => p.Wincount)
                .Take(top)
                .ToListAsync();
        }

        public async Task AddProfileAsync(Playerprofile profile)
        {
            await _context.Playerprofiles.AddAsync(profile);
        }

        public Task UpdateProfileAsync(Playerprofile profile)
        {
            _context.Playerprofiles.Update(profile);
            return Task.CompletedTask;
        }
    }
}
