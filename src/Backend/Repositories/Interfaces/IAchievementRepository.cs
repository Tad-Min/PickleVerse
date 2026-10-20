using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Entities.Models;
using Repositories.Base;

namespace Repositories.Interfaces
{
    public interface IAchievementRepository : IGenericRepository<Achievement>
    {
        Task<List<Achievement>> GetAllAchievementsAsync();
        Task<List<Playerachievement>> GetPlayerAchievementsAsync(Guid playerId);
        Task<Playerachievement?> GetPlayerAchievementAsync(Guid playerId, int achievementId);
        Task AddPlayerAchievementAsync(Playerachievement pa);
        Task UpdatePlayerAchievementAsync(Playerachievement pa);
    }
}
