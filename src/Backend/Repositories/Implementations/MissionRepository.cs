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
    public class MissionRepository : GenericRepository<Mission>, IMissionRepository
    {
        public MissionRepository(PickleVerseContext context) : base(context)
        {
        }

        public async Task<List<Mission>> GetAllActiveMissionsAsync()
        {
            return await _context.Missions.ToListAsync();
        }

        public async Task<List<Playermission>> GetPlayerMissionsAsync(Guid playerId)
        {
            return await _context.Playermissions
                .Include(pm => pm.Mission)
                .Where(pm => pm.Playerid == playerId)
                .ToListAsync();
        }

        public async Task<Playermission?> GetPlayerMissionAsync(Guid playerId, int missionId)
        {
            return await _context.Playermissions
                .Include(pm => pm.Mission)
                .OrderByDescending(pm => pm.Periodkey)
                .FirstOrDefaultAsync(pm => pm.Playerid == playerId && pm.Missionid == missionId);
        }

        public async Task AddPlayerMissionAsync(Playermission pm)
        {
            await _context.Playermissions.AddAsync(pm);
        }

        public Task UpdatePlayerMissionAsync(Playermission pm)
        {
            _context.Playermissions.Update(pm);
            return Task.CompletedTask;
        }

        public async Task<int> CreateMissionAsync(Mission mission)
        {
            await _context.Missions.AddAsync(mission);
            await _context.SaveChangesAsync();
            return mission.Missionid;
        }
    }
}
