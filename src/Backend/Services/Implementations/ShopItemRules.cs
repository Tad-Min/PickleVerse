using Entities.DTOs;
using Entities.Exceptions;
using Entities.Game;

namespace Services.Implementations;

/// <summary>Quy tắc của vật phẩm cửa hàng, không chạm database.</summary>
public static class ShopItemRules
{
    public const int MaxPrice = 1_000_000, MaxTitle = 40, MaxDescription = 254, MaxCollection = 60;

    /// <summary>Chuẩn hóa và kiểm tra dữ liệu nhập; <paramref name="creating"/> thì phải có ít nhất một giá.</summary>
    public static SaveShopItemRequest Validate(SaveShopItemRequest? request, bool creating)
    {
        if (request == null)
            throw new BadRequestException("Thiếu dữ liệu vật phẩm.");
        var r = new SaveShopItemRequest
        {
            Code = (request.Code ?? "").Trim(),
            Title = (request.Title ?? "").Trim(),
            Description = (request.Description ?? "").Trim(),
            Collection = (request.Collection ?? "").Trim(),
            TokenpadPrice = request.TokenpadPrice,
            ElitePrice = request.ElitePrice,
            Premium = request.Premium
        };
        if (r.Code == PaddleSkins.Default)
            throw new BadRequestException("Vợt mặc định luôn miễn phí, không đăng bán hay sửa ở đây.");
        if (PaddleSkins.Find(r.Code) == null)
            throw new BadRequestException($"Game chưa có model cho mã \"{r.Code}\". Hãy chọn một vợt trong danh sách.");
        if (r.Title.Length is < 2 or > MaxTitle)
            throw new BadRequestException($"Tên vật phẩm phải có từ 2 đến {MaxTitle} ký tự.");
        if (r.Description.Length is < 1 or > MaxDescription)
            throw new BadRequestException($"Mô tả phải có từ 1 đến {MaxDescription} ký tự.");
        if (r.Collection.Length is < 1 or > MaxCollection)
            throw new BadRequestException($"Bộ sưu tập phải có từ 1 đến {MaxCollection} ký tự.");
        if (r.TokenpadPrice is < 0 or > MaxPrice || r.ElitePrice is < 0 or > MaxPrice)
            throw new BadRequestException($"Giá phải từ 0 đến {MaxPrice:N0}.");
        if (creating && r.TokenpadPrice == 0 && r.ElitePrice == 0)
            throw new BadRequestException("Nhập ít nhất một giá lớn hơn 0 để đăng bán.");
        return r;
    }

    /// <summary>Cấu hình gửi cho game (cột Config); giá ở đây chỉ để hiển thị.</summary>
    public static CosmeticOffer Offer(SaveShopItemRequest r, int releaseOrder) => new()
    {
        id = r.Code, title = r.Title, description = r.Description, collection = r.Collection,
        tokenpadPrice = r.TokenpadPrice, elitePrice = r.ElitePrice, gemstone = r.Premium, releaseOrder = releaseOrder
    };

    public static CosmeticOffer ReadOffer(string? config)
    {
        try { return string.IsNullOrWhiteSpace(config) ? new CosmeticOffer() : GameJson.Read<CosmeticOffer>(config); }
        catch (Exception e) when (e is System.Text.Json.JsonException or InvalidOperationException) { return new CosmeticOffer(); }
    }

    /// <summary>Game có hiện và cho mua vật phẩm không; so cấu hình với bảng giá thật.</summary>
    public static (ShopItemState state, string? issue) State(string code, CosmeticOffer offer, int tokenpad, int elite)
    {
        if (code == PaddleSkins.Default)
            return (ShopItemState.Default, null);
        if (!code.StartsWith(PaddleSkins.Prefix, StringComparison.Ordinal))
            return (ShopItemState.Incomplete, $"Mã không bắt đầu bằng \"{PaddleSkins.Prefix}\" nên game không đọc.");
        if (string.IsNullOrEmpty(offer.id))
            return (ShopItemState.Incomplete, "Thiếu cấu hình game nên không hiện trong cửa hàng.");
        if (offer.id != code)
            return (ShopItemState.Incomplete, "Cấu hình game thuộc về mã khác.");
        if (PaddleSkins.Find(code) == null)
            return (ShopItemState.Incomplete, "Game chưa có model cho mã này.");
        if (offer.tokenpadPrice != tokenpad || offer.elitePrice != elite)
            return (ShopItemState.Incomplete, "Giá hiển thị khác giá bán. Lưu lại để đồng bộ.");
        return tokenpad == 0 && elite == 0 ? (ShopItemState.Stopped, null) : (ShopItemState.OnSale, null);
    }

    /// <summary>Chỉ xóa được vật phẩm game không đọc và chưa ai sở hữu.</summary>
    public static bool CanDelete(string code, CosmeticOffer offer, int owners) => owners == 0 && code != PaddleSkins.Default &&
        (!code.StartsWith(PaddleSkins.Prefix, StringComparison.Ordinal) || string.IsNullOrEmpty(offer.id));
}
