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
    public class AchievementRepository : GenericRepository<Achievement>, IAchievementRepository
    {
        public AchievementRepository(PickleVerseContext context) : base(context)
        {
        }

        public async Task<List<Achievement>> GetAllAchievementsAsync()
        {
            return await _context.Achievements.ToListAsync();
        }

        public async Task<List<Playerachievement>> GetPlayerAchievementsAsync(Guid playerId)
        {
            return await _context.Playerachievements
                .Include(pa => pa.Achievement)
                .Where(pa => pa.Playerid == playerId)
                .ToListAsync();
        }

        public async Task<Playerachievement?> GetPlayerAchievementAsync(Guid playerId, int achievementId)
        {
            return await _context.Playerachievements
                .Include(pa => pa.Achievement)
                .FirstOrDefaultAsync(pa => pa.Playerid == playerId && pa.Achievementid == achievementId);
        }

        public async Task AddPlayerAchievementAsync(Playerachievement pa)
        {
            await _context.Playerachievements.AddAsync(pa);
        }

        public Task UpdatePlayerAchievementAsync(Playerachievement pa)
        {
            _context.Playerachievements.Update(pa);
            return Task.CompletedTask;
        }
    }
}
