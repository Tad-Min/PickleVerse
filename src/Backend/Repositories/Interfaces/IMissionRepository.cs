using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Entities.Models;
using Repositories.Base;

namespace Repositories.Interfaces
{
    public interface IMissionRepository : IGenericRepository<Mission>
    {
        Task<List<Mission>> GetAllActiveMissionsAsync();
        Task<List<Playermission>> GetPlayerMissionsAsync(Guid playerId);
        Task<Playermission?> GetPlayerMissionAsync(Guid playerId, int missionId);
        Task AddPlayerMissionAsync(Playermission pm);
        Task UpdatePlayerMissionAsync(Playermission pm);
        Task<int> CreateMissionAsync(Mission mission);
    }
}
