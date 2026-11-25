using Entities.DTOs;

namespace Services.Interfaces;

/// <summary>Quản trị vật phẩm bán trong cửa hàng của game.</summary>
public interface IShopItemService
{
    Task<ShopItemsDto> List();
    Task<ShopItemDto> Create(SaveShopItemRequest request);
    Task<ShopItemDto> Update(int cosmeticId, SaveShopItemRequest request);
    Task Delete(int cosmeticId);
}
