using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Entities.Models;
using Repositories.Base;

namespace Repositories.Interfaces
{
    public interface IEconomyRepository : IGenericRepository<Currency>
    {
        Task<List<Currency>> GetAllCurrenciesAsync();
        Task<List<Playercurrency>> GetPlayerCurrenciesAsync(Guid playerId);
        Task<Playercurrency?> GetPlayerCurrencyAsync(Guid playerId, int currencyId);
        Task AddPlayerCurrencyAsync(Playercurrency pc);
        Task UpdatePlayerCurrencyAsync(Playercurrency pc);
        Task<List<Cosmetic>> GetShopCosmeticsAsync(string? type);
        Task<Cosmetic?> GetCosmeticByIdAsync(int cosmeticId);
        Task<List<Playercosmetic>> GetPlayerCosmeticsAsync(Guid playerId);
        Task<Playercosmetic?> GetPlayerCosmeticAsync(Guid playerId, int cosmeticId);
        Task AddPlayerCosmeticAsync(Playercosmetic pc);
        Task UpdatePlayerCosmeticAsync(Playercosmetic pc);
        Task<int> CreateCosmeticAsync(Cosmetic item);
    }
}
