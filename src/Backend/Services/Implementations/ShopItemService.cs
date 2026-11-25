using System.Net;
using Entities.DTOs;
using Entities.Exceptions;
using Entities.Game;
using Entities.Models;
using Repositories.UnitOfWork;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>
/// Đăng bán và sửa vật phẩm cửa hàng. Một vật phẩm chỉ bán được khi có đủ ba phần: dòng Cosmetics, cấu hình game (Config) và
/// bảng giá (CosmeticPrices); dịch vụ ghi cả ba trong một giao dịch nên game và giá mua luôn khớp nhau.
/// </summary>
public sealed class ShopItemService(IUnitOfWork unit) : IShopItemService
{
    const string Tokenpad = "TOKENPAD", Elite = "ELITE TOKEN", Locked = "shop-items";

    public async Task<ShopItemsDto> List()
    {
        var items = await Items();
        var known = items.Select(i => i.Code).ToHashSet();
        return new ShopItemsDto
        {
            Items = items,
            AvailableSkins = PaddleSkins.All.Where(s => !known.Contains(s.Code)).Select(s => new ShopSkinDto
            { Code = s.Code, Title = s.Title, Description = s.Description, Collection = s.Collection, Premium = s.Premium }).ToList()
        };
    }

    public async Task<ShopItemDto> Create(SaveShopItemRequest request)
    {
        var r = ShopItemRules.Validate(request, true);
        await using var tx = await unit.Game.BeginTransactionAsync();
        await unit.Game.Lock(Locked);
        if (await unit.Economy.GetCosmeticByCodeAsync(r.Code) != null)
            throw new AppException($"Cửa hàng đã có mã \"{r.Code}\". Hãy sửa vật phẩm đó.", HttpStatusCode.Conflict);
        var row = new Cosmetic { Itemcode = r.Code };
        await unit.Economy.AddCosmeticAsync(row);
        await Save(row, r, await NextReleaseOrder());
        await tx.CommitAsync();
        return await Single(row.Cosmeticid);
    }

    public async Task<ShopItemDto> Update(int cosmeticId, SaveShopItemRequest request)
    {
        await using var tx = await unit.Game.BeginTransactionAsync();
        await unit.Game.Lock(Locked);
        var row = await unit.Economy.GetCosmeticByIdAsync(cosmeticId) ?? throw new NotFoundException("Không tìm thấy vật phẩm.");
        if (request != null)
            request.Code = row.Itemcode;                       // mã là khóa của game, không đổi được
        var r = ShopItemRules.Validate(request, false);
        var current = ShopItemRules.ReadOffer(row.Config);
        await Save(row, r, current.id == row.Itemcode ? current.releaseOrder : await NextReleaseOrder());
        await tx.CommitAsync();
        return await Single(cosmeticId);
    }

    public async Task Delete(int cosmeticId)
    {
        await using var tx = await unit.Game.BeginTransactionAsync();
        await unit.Game.Lock(Locked);
        var row = await unit.Economy.GetCosmeticByIdAsync(cosmeticId) ?? throw new NotFoundException("Không tìm thấy vật phẩm.");
        var owners = await unit.Economy.GetCosmeticOwnersAsync();
        if (!ShopItemRules.CanDelete(row.Itemcode, ShopItemRules.ReadOffer(row.Config), owners.GetValueOrDefault(cosmeticId)))
            throw new BadRequestException("Chỉ xóa được vật phẩm game không đọc và chưa ai sở hữu. Với vật phẩm khác, hãy ngừng bán.");
        unit.Economy.RemoveCosmetic(row);
        await unit.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <summary>Ghi đủ dòng vật phẩm, cấu hình game và bảng giá; giá 0 nghĩa là không bán bằng loại tiền đó.</summary>
    async Task Save(Cosmetic row, SaveShopItemRequest r, int releaseOrder)
    {
        var currencies = await Currencies();
        bool elite = r.ElitePrice > 0 && r.TokenpadPrice == 0;
        row.Cosmeticname = r.Title;
        row.Description = r.Description;
        row.Type = "Paddle";
        row.Price = elite ? r.ElitePrice : r.TokenpadPrice;
        row.Currencyid = currencies[elite ? Elite : Tokenpad];
        row.Config = GameJson.Write(ShopItemRules.Offer(r, releaseOrder));
        await unit.SaveChangesAsync();
        var prices = new Dictionary<int, int>();
        if (r.TokenpadPrice > 0)
            prices[currencies[Tokenpad]] = r.TokenpadPrice;
        if (r.ElitePrice > 0)
            prices[currencies[Elite]] = r.ElitePrice;
        await unit.Economy.SetCosmeticPricesAsync(row.Cosmeticid, prices);
        await unit.SaveChangesAsync();
    }

    async Task<int> NextReleaseOrder() => (await unit.Economy.GetShopCosmeticsAsync(null))
        .Select(c => ShopItemRules.ReadOffer(c.Config).releaseOrder).DefaultIfEmpty(0).Max() + 1;

    async Task<Dictionary<string, int>> Currencies()
    {
        var ids = (await unit.Economy.GetAllCurrenciesAsync()).ToDictionary(c => c.Currencyname, c => c.Currencyid);
        if (!ids.ContainsKey(Tokenpad) || !ids.ContainsKey(Elite))
            throw new AppException("Database thiếu loại tiền TOKENPAD hoặc ELITE TOKEN.", HttpStatusCode.InternalServerError);
        return ids;
    }

    async Task<ShopItemDto> Single(int cosmeticId) => (await Items()).Single(i => i.CosmeticId == cosmeticId);

    async Task<List<ShopItemDto>> Items()
    {
        var currencies = await Currencies();
        var prices = (await unit.Economy.GetCosmeticPricesAsync()).ToDictionary(p => (p.CosmeticId, p.CurrencyId), p => p.Price);
        var owners = await unit.Economy.GetCosmeticOwnersAsync();
        return (await unit.Economy.GetShopCosmeticsAsync(null)).OrderBy(c => c.Cosmeticid).Select(c =>
        {
            var offer = ShopItemRules.ReadOffer(c.Config);
            int tokenpad = prices.GetValueOrDefault((c.Cosmeticid, currencies[Tokenpad])), elite = prices.GetValueOrDefault((c.Cosmeticid, currencies[Elite]));
            int owned = owners.GetValueOrDefault(c.Cosmeticid);
            var (state, issue) = ShopItemRules.State(c.Itemcode, offer, tokenpad, elite);
            return new ShopItemDto
            {
                CosmeticId = c.Cosmeticid, Code = c.Itemcode,
                Title = string.IsNullOrEmpty(offer.title) ? c.Cosmeticname : offer.title,
                Description = string.IsNullOrEmpty(offer.description) ? c.Description : offer.description,
                Collection = string.IsNullOrEmpty(offer.collection) ? PaddleSkins.Find(c.Itemcode)?.Collection ?? "" : offer.collection,
                TokenpadPrice = tokenpad, ElitePrice = elite, ReleaseOrder = offer.releaseOrder, Premium = offer.gemstone, Owners = owned,
                State = state, Issue = issue,
                CanEdit = PaddleSkins.Find(c.Itemcode) != null,
                CanDelete = ShopItemRules.CanDelete(c.Itemcode, offer, owned)
            };
        }).ToList();
    }
}
