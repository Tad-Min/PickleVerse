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
    public class EconomyRepository : GenericRepository<Currency>, IEconomyRepository
    {
        public EconomyRepository(PickleVerseContext context) : base(context)
        {
        }

        public async Task<List<Currency>> GetAllCurrenciesAsync()
        {
            return await _context.Currencies.ToListAsync();
        }

        public async Task<List<Playercurrency>> GetPlayerCurrenciesAsync(Guid playerId)
        {
            return await _context.Playercurrencies
                .Where(pc => pc.Playerid == playerId)
                .ToListAsync();
        }

        public async Task<Playercurrency?> GetPlayerCurrencyAsync(Guid playerId, int currencyId)
        {
            return await _context.Playercurrencies
                .FirstOrDefaultAsync(pc => pc.Playerid == playerId && pc.Currencyid == currencyId);
        }

        public async Task AddPlayerCurrencyAsync(Playercurrency pc)
        {
            await _context.Playercurrencies.AddAsync(pc);
        }

        public Task UpdatePlayerCurrencyAsync(Playercurrency pc)
        {
            _context.Playercurrencies.Update(pc);
            return Task.CompletedTask;
        }

        public async Task<List<Cosmetic>> GetShopCosmeticsAsync(string? type)
        {
            var query = _context.Cosmetics
                .Include(c => c.Currency)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(type))
            {
                var normType = type.ToLower();
                query = query.Where(c => c.Type.ToLower() == normType);
            }

            return await query.ToListAsync();
        }

        public async Task<Cosmetic?> GetCosmeticByIdAsync(int cosmeticId)
        {
            return await _context.Cosmetics
                .Include(c => c.Currency)
                .FirstOrDefaultAsync(c => c.Cosmeticid == cosmeticId);
        }

        public async Task<List<Playercosmetic>> GetPlayerCosmeticsAsync(Guid playerId)
        {
            return await _context.Playercosmetics
                .Include(pc => pc.Cosmetic)
                .Where(pc => pc.Playerid == playerId)
                .ToListAsync();
        }

        public async Task<Playercosmetic?> GetPlayerCosmeticAsync(Guid playerId, int cosmeticId)
        {
            return await _context.Playercosmetics
                .Include(pc => pc.Cosmetic)
                .FirstOrDefaultAsync(pc => pc.Playerid == playerId && pc.Cosmeticid == cosmeticId);
        }

        public async Task AddPlayerCosmeticAsync(Playercosmetic pc)
        {
            await _context.Playercosmetics.AddAsync(pc);
        }

        public Task UpdatePlayerCosmeticAsync(Playercosmetic pc)
        {
            _context.Playercosmetics.Update(pc);
            return Task.CompletedTask;
        }

        public async Task<int> CreateCosmeticAsync(Cosmetic item)
        {
            await _context.Cosmetics.AddAsync(item);
            await _context.SaveChangesAsync();
            return item.Cosmeticid;
        }
    }
}
