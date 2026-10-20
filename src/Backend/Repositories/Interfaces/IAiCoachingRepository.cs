using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Entities.Models;
using Repositories.Base;

namespace Repositories.Interfaces
{
    public interface IAiCoachingRepository : IGenericRepository<Technique>
    {
        Task<List<Technique>> GetAllTechniquesAsync();
        Task<Technique?> GetTechniqueByIdAsync(int techniqueId);
        Task<List<Playertechniquemastery>> GetPlayerMasteriesAsync(Guid playerId);
        Task<Playertechniquemastery?> GetPlayerMasteryAsync(Guid playerId, int techniqueId);
        Task AddPlayerMasteryAsync(Playertechniquemastery m);
        Task UpdatePlayerMasteryAsync(Playertechniquemastery m);
        Task AddTechniqueErrorAsync(Playertechniqueserror err);
        Task<List<Playertechniqueserror>> GetRecentErrorsAsync(Guid playerId, int limit = 20);
        Task<List<Playertechniqueserror>> GetAllErrorsAsync();
    }
}
