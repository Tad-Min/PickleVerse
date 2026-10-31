using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Entities.DTOs;

namespace Services.Interfaces
{
    public interface IEconomyService
    {
        Task<List<CurrencyDto>> GetCurrenciesBalanceAsync(Guid playerId);
        Task<List<CosmeticShopItemDto>> GetShopItemsAsync(Guid playerId, string? type);
        Task<(bool Success, string Message)> BuyCosmeticAsync(Guid playerId, int cosmeticId);
        Task<List<PlayerCosmeticDto>> GetMyCosmeticsAsync(Guid playerId);
        Task<(bool Success, string Message)> EquipCosmeticAsync(Guid playerId, int cosmeticId, bool equip);
    }
}
