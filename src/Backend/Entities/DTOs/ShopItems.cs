using System.Text.Json.Serialization;

namespace Entities.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<ShopItemState>))]
public enum ShopItemState
{
    /// <summary>Vợt mặc định, ai cũng có.</summary>
    Default,
    /// <summary>Đang bán: game hiện vật phẩm và mua được.</summary>
    OnSale,
    /// <summary>Ngừng bán: không mua được, người đã sở hữu vẫn dùng.</summary>
    Stopped,
    /// <summary>Thiếu dữ liệu nên game không hiện hoặc không mua được.</summary>
    Incomplete
}

public sealed class ShopItemDto
{
    public int CosmeticId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Collection { get; set; } = string.Empty;
    /// <summary>Giá thật khi mua; 0 nghĩa là không bán bằng loại tiền này.</summary>
    public int TokenpadPrice { get; set; }
    public int ElitePrice { get; set; }
    public int ReleaseOrder { get; set; }
    public bool Premium { get; set; }
    public int Owners { get; set; }
    public ShopItemState State { get; set; }
    /// <summary>Lý do khi trạng thái là Incomplete.</summary>
    public string? Issue { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}

/// <summary>Vợt có model trong game, kèm nội dung gợi ý cho form đăng bán.</summary>
public sealed class ShopSkinDto
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Collection { get; set; } = string.Empty;
    public bool Premium { get; set; }
}

public sealed class ShopItemsDto
{
    public List<ShopItemDto> Items { get; set; } = [];
    /// <summary>Vợt có model nhưng chưa có trong cửa hàng.</summary>
    public List<ShopSkinDto> AvailableSkins { get; set; } = [];
}

public sealed class SaveShopItemRequest
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Collection { get; set; } = string.Empty;
    public int TokenpadPrice { get; set; }
    public int ElitePrice { get; set; }
    public bool Premium { get; set; }
}
