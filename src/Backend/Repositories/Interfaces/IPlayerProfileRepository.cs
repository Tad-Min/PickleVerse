using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Entities.Models;
using Repositories.Base;

namespace Repositories.Interfaces
{
    public interface IPlayerProfileRepository : IGenericRepository<Playerprofile>
    {
        Task<Playerprofile?> GetByUserIdAsync(Guid userId);
        Task<List<Playerprofile>> GetLeaderboardAsync(int top = 50);
        Task AddProfileAsync(Playerprofile profile);
        Task UpdateProfileAsync(Playerprofile profile);
    }
}
