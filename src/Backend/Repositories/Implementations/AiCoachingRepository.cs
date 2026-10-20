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
    public class AiCoachingRepository : GenericRepository<Technique>, IAiCoachingRepository
    {
        public AiCoachingRepository(PickleVerseContext context) : base(context)
        {
        }

        public async Task<List<Technique>> GetAllTechniquesAsync()
        {
            return await _context.Techniques.ToListAsync();
        }

        public async Task<Technique?> GetTechniqueByIdAsync(int techniqueId)
        {
            return await _context.Techniques.FindAsync(techniqueId);
        }

        public async Task<List<Playertechniquemastery>> GetPlayerMasteriesAsync(Guid playerId)
        {
            return await _context.Playertechniquemasteries
                .Include(ptm => ptm.Technique)
                .Where(ptm => ptm.Playerid == playerId)
                .ToListAsync();
        }

        public async Task<Playertechniquemastery?> GetPlayerMasteryAsync(Guid playerId, int techniqueId)
        {
            return await _context.Playertechniquemasteries
                .FirstOrDefaultAsync(ptm => ptm.Playerid == playerId && ptm.Techniqueid == techniqueId);
        }

        public async Task AddPlayerMasteryAsync(Playertechniquemastery m)
        {
            await _context.Playertechniquemasteries.AddAsync(m);
        }

        public Task UpdatePlayerMasteryAsync(Playertechniquemastery m)
        {
            _context.Playertechniquemasteries.Update(m);
            return Task.CompletedTask;
        }

        public async Task AddTechniqueErrorAsync(Playertechniqueserror err)
        {
            await _context.Playertechniqueserrors.AddAsync(err);
        }

        public async Task<List<Playertechniqueserror>> GetRecentErrorsAsync(Guid playerId, int limit = 20)
        {
            return await _context.Playertechniqueserrors
                .Include(pte => pte.Technique)
                .Where(pte => pte.Playerid == playerId)
                .OrderByDescending(pte => pte.Createdat)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<Playertechniqueserror>> GetAllErrorsAsync()
        {
            return await _context.Playertechniqueserrors
                .Include(pte => pte.Technique)
                .ToListAsync();
        }
    }
}
