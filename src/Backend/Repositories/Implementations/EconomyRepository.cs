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

        public Task<Cosmetic?> GetCosmeticByCodeAsync(string code)
        {
            return _context.Cosmetics.FirstOrDefaultAsync(c => c.Itemcode == code);
        }

        public async Task AddCosmeticAsync(Cosmetic item)
        {
            await _context.Cosmetics.AddAsync(item);
        }

        public void RemoveCosmetic(Cosmetic item)
        {
            _context.Cosmetics.Remove(item);
        }

        public Task<List<CosmeticPrice>> GetCosmeticPricesAsync()
        {
            return _context.CosmeticPrices.AsNoTracking().ToListAsync();
        }

        public async Task SetCosmeticPricesAsync(int cosmeticId, IReadOnlyDictionary<int, int> prices)
        {
            var current = await _context.CosmeticPrices.Where(p => p.CosmeticId == cosmeticId).ToListAsync();
            _context.CosmeticPrices.RemoveRange(current.Where(p => !prices.ContainsKey(p.CurrencyId)));
            foreach (var (currency, price) in prices)
            {
                var row = current.FirstOrDefault(p => p.CurrencyId == currency);
                if (row != null)
                    row.Price = price;
                else
                    await _context.CosmeticPrices.AddAsync(new CosmeticPrice { CosmeticId = cosmeticId, CurrencyId = currency, Price = price });
            }
        }

        public Task<Dictionary<int, int>> GetCosmeticOwnersAsync()
        {
            return _context.Playercosmetics.GroupBy(p => p.Cosmeticid).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count);
        }
    }
}
