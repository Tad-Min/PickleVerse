using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Entities.Models;
using Microsoft.Extensions.Logging;
using Repositories.UnitOfWork;
using Services.DTOs;
using Services.Exceptions;
using Services.Interfaces;

namespace Services.Implementations
{
    public class EconomyService : IEconomyService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<EconomyService> _logger;

        public EconomyService(IUnitOfWork unitOfWork, ILogger<EconomyService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<List<CurrencyDto>> GetCurrenciesBalanceAsync(Guid playerId)
        {
            try
            {
                var allCurrencies = await _unitOfWork.Economy.GetAllCurrenciesAsync();
                var playerCurrencies = await _unitOfWork.Economy.GetPlayerCurrenciesAsync(playerId);

                var result = new List<CurrencyDto>();
                foreach (var currency in allCurrencies)
                {
                    var pc = playerCurrencies.FirstOrDefault(x => x.Currencyid == currency.Currencyid);
                    result.Add(new CurrencyDto
                    {
                        CurrencyId = currency.Currencyid,
                        CurrencyName = currency.Currencyname,
                        Description = currency.Description,
                        Amount = pc?.Amount ?? 0
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy số dư tiền tệ cho PlayerId {PlayerId}", playerId);
                throw new AppException("Không thể tải số dư tiền tệ.", ex);
            }
        }

        public async Task<List<CosmeticShopItemDto>> GetShopItemsAsync(Guid playerId, string? type)
        {
            try
            {
                var cosmetics = await _unitOfWork.Economy.GetShopCosmeticsAsync(type);
                var ownedItems = await _unitOfWork.Economy.GetPlayerCosmeticsAsync(playerId);

                return cosmetics.Select(c =>
                {
                    var owned = ownedItems.FirstOrDefault(x => x.Cosmeticid == c.Cosmeticid);
                    return new CosmeticShopItemDto
                    {
                        CosmeticId = c.Cosmeticid,
                        ItemCode = c.Itemcode,
                        CosmeticName = c.Cosmeticname,
                        Description = c.Description,
                        Price = c.Price,
                        Type = c.Type,
                        CurrencyId = c.Currencyid,
                        CurrencyName = c.Currency?.Currencyname ?? string.Empty,
                        IsOwned = owned != null,
                        IsEquipped = owned?.Equipped ?? false
                    };
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách vật phẩm Cửa hàng Shop cho PlayerId {PlayerId}", playerId);
                throw new AppException("Không thể tải danh sách cửa hàng vật phẩm.", ex);
            }
        }

        public async Task<(bool Success, string Message)> BuyCosmeticAsync(Guid playerId, int cosmeticId)
        {
            try
            {
                var cosmetic = await _unitOfWork.Economy.GetCosmeticByIdAsync(cosmeticId);
                if (cosmetic == null)
                {
                    throw new NotFoundException("Vật phẩm không tồn tại trong cửa hàng.");
                }

                var existing = await _unitOfWork.Economy.GetPlayerCosmeticAsync(playerId, cosmeticId);
                if (existing != null)
                {
                    throw new BadRequestException("Bạn đã sở hữu vật phẩm này rồi.");
                }

                var playerCurrency = await _unitOfWork.Economy.GetPlayerCurrencyAsync(playerId, cosmetic.Currencyid);
                if (playerCurrency == null || playerCurrency.Amount < cosmetic.Price)
                {
                    throw new BadRequestException("Số dư tiền tệ không đủ để mua vật phẩm này.");
                }

                // Trừ tiền
                playerCurrency.Amount -= cosmetic.Price;
                await _unitOfWork.Economy.UpdatePlayerCurrencyAsync(playerCurrency);

                // Thêm vào kho đồ
                var newPlayerCosmetic = new Playercosmetic
                {
                    Playerid = playerId,
                    Cosmeticid = cosmeticId,
                    Equipped = false
                };

                await _unitOfWork.Economy.AddPlayerCosmeticAsync(newPlayerCosmetic);
                await _unitOfWork.SaveChangesAsync();

                return (true, $"Mua thành công '{cosmetic.Cosmeticname}'!");
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi PlayerId {PlayerId} mua CosmeticId {CosmeticId}", playerId, cosmeticId);
                throw new AppException("Giao dịch mua vật phẩm thất bại.", ex);
            }
        }

        public async Task<List<PlayerCosmeticDto>> GetMyCosmeticsAsync(Guid playerId)
        {
            try
            {
                var items = await _unitOfWork.Economy.GetPlayerCosmeticsAsync(playerId);

                return items.Select(pc => new PlayerCosmeticDto
                {
                    CosmeticId = pc.Cosmeticid,
                    ItemCode = pc.Cosmetic.Itemcode,
                    CosmeticName = pc.Cosmetic.Cosmeticname,
                    Type = pc.Cosmetic.Type,
                    Equipped = pc.Equipped
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy kho đồ cá nhân của PlayerId {PlayerId}", playerId);
                throw new AppException("Không thể tải kho đồ cá nhân.", ex);
            }
        }

        public async Task<(bool Success, string Message)> EquipCosmeticAsync(Guid playerId, int cosmeticId, bool equip)
        {
            try
            {
                var item = await _unitOfWork.Economy.GetPlayerCosmeticAsync(playerId, cosmeticId);
                if (item == null)
                {
                    throw new NotFoundException("Bạn chưa sở hữu vật phẩm này.");
                }

                if (equip)
                {
                    var allOwned = await _unitOfWork.Economy.GetPlayerCosmeticsAsync(playerId);
                    var sameTypeItems = allOwned.Where(pc => pc.Cosmetic.Type == item.Cosmetic.Type).ToList();

                    foreach (var other in sameTypeItems)
                    {
                        other.Equipped = false;
                        await _unitOfWork.Economy.UpdatePlayerCosmeticAsync(other);
                    }

                    item.Equipped = true;
                }
                else
                {
                    item.Equipped = false;
                }

                await _unitOfWork.Economy.UpdatePlayerCosmeticAsync(item);
                await _unitOfWork.SaveChangesAsync();
                return (true, equip ? $"Đã trang bị '{item.Cosmetic.Cosmeticname}'" : $"Đã tháo trang bị '{item.Cosmetic.Cosmeticname}'");
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi trang bị vật phẩm CosmeticId {CosmeticId} cho PlayerId {PlayerId}", cosmeticId, playerId);
                throw new AppException("Thao tác trang bị vật phẩm thất bại.", ex);
            }
        }
    }
}
