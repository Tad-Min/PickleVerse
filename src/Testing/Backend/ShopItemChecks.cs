using System.Net;
using System.Text.Json;
using Entities.DTOs;
using Entities.Exceptions;
using Entities.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Repositories.Context;
using Services.Implementations;

/// <summary>Trang quản trị đăng bán, sửa giá và ngừng bán vật phẩm; game phải hiện và bán đúng như vậy.</summary>
static class ShopItemChecks
{
    const string Dragon = "paddle.dragonruby", Phoenix = "paddle.bluephoenix";
    /// <summary>Skins that have a model in the game but are not in Database.sql: an administrator publishes them.</summary>
    static readonly string[] Unpublished = [Phoenix];
    static SaveShopItemRequest Request(string code = Dragon, int tokenpad = 0, int elite = 800, string title = "DRAGON RUBY", string description = "Rồng phương Đông.",
        string collection = "BỘ SƯU TẬP TINH ANH") => new()
        { Code = code, Title = title, Description = description, Collection = collection, TokenpadPrice = tokenpad, ElitePrice = elite, Premium = true };

    /// <summary>Quy tắc thuần, không cần database.</summary>
    public static void Rules(Action<bool, string> check)
    {
        void Invalid(SaveShopItemRequest? request, bool creating, string label)
        {
            HttpStatusCode? status = null;
            try { ShopItemRules.Validate(request, creating); } catch (AppException e) { status = e.StatusCode; }
            check(status == HttpStatusCode.BadRequest, "shop item rejected: " + label);
        }
        Invalid(null, true, "no body");
        Invalid(Request("PADDLE_CYBER_2026"), true, "code the game never reads");
        Invalid(Request("paddle.unknown"), true, "code without a model in the game");
        Invalid(Request(PaddleSkins.Default), true, "default paddle");
        Invalid(Request(title: " "), true, "empty name");
        Invalid(Request(title: new string('a', ShopItemRules.MaxTitle + 1)), true, "name too long");
        Invalid(Request(description: ""), true, "empty description");
        Invalid(Request(description: new string('a', ShopItemRules.MaxDescription + 1)), true, "description longer than its column");
        Invalid(Request(collection: ""), true, "empty collection");
        Invalid(Request(elite: -1), true, "negative price");
        Invalid(Request(elite: ShopItemRules.MaxPrice + 1), true, "price above the limit");
        Invalid(Request(tokenpad: 0, elite: 0), true, "publishing without a price");
        check(ShopItemRules.Validate(Request(tokenpad: 0, elite: 0), false).ElitePrice == 0, "an existing item may lose both prices, which stops its sale");
        check(ShopItemRules.Validate(Request(title: "  DRAGON RUBY "), true).Title == "DRAGON RUBY", "shop text is trimmed");
        foreach (var skin in PaddleSkins.All)
            check(ShopItemRules.Validate(Request(skin.Code, title: skin.Title, description: skin.Description, collection: skin.Collection), true).Code == skin.Code
                && skin.Code.StartsWith(PaddleSkins.Prefix, StringComparison.Ordinal), "suggested content of " + skin.Code + " is valid as it stands");
        check(PaddleSkins.All.Select(s => s.Code).Distinct().Count() == PaddleSkins.All.Count && PaddleSkins.Find(PaddleSkins.Default) == null, "skin codes are unique and exclude the default paddle");

        var offer = ShopItemRules.Offer(Request(tokenpad: 900, elite: 250), 7);
        check(offer.id == Dragon && offer.tokenpadPrice == 900 && offer.elitePrice == 250 && offer.releaseOrder == 7 && offer.gemstone, "game configuration carries the published item");
        ShopItemState State(CosmeticOffer o, int tokenpad, int elite, string code = Dragon) => ShopItemRules.State(code, o, tokenpad, elite).state;
        check(State(offer, 900, 250) == ShopItemState.OnSale, "item with matching prices is on sale");
        check(State(ShopItemRules.Offer(Request(tokenpad: 0, elite: 0), 7), 0, 0) == ShopItemState.Stopped, "item without prices is no longer sold");
        check(State(offer, 900, 0) == ShopItemState.Incomplete, "shown price that cannot be paid is reported");
        check(State(new CosmeticOffer(), 900, 250) == ShopItemState.Incomplete, "item without game configuration is reported");
        check(State(offer, 900, 250, "paddle.thot") == ShopItemState.Incomplete, "configuration of another item is reported");
        check(State(offer, 900, 250, "PADDLE_CYBER_2026") == ShopItemState.Incomplete, "code the game never reads is reported");
        check(State(new CosmeticOffer(), 0, 0, PaddleSkins.Default) == ShopItemState.Default, "default paddle is not a sale item");
        foreach (var config in new[] { "{}", "null", "not json", "", null })
            check(string.IsNullOrEmpty(ShopItemRules.ReadOffer(config).id), "game configuration reads as an empty item: " + (config ?? "missing"));
        check(ShopItemRules.CanDelete("PADDLE_CYBER_2026", new CosmeticOffer(), 0) && ShopItemRules.CanDelete("paddle.cyber", new CosmeticOffer(), 0), "item the game does not read can be removed");
        check(!ShopItemRules.CanDelete("PADDLE_CYBER_2026", new CosmeticOffer(), 1) && !ShopItemRules.CanDelete(Dragon, offer, 0)
            && !ShopItemRules.CanDelete(PaddleSkins.Default, new CosmeticOffer(), 0), "owned items, items of the game and the default paddle cannot be removed");
        string page = File.ReadAllText(Path.Combine(TestSettings.Root, "src/Frontend/src/features/content/pages/shopItems.ts"));
        foreach (var (name, limit) in new[] { ("MAX_PRICE", ShopItemRules.MaxPrice), ("MAX_TITLE", ShopItemRules.MaxTitle), ("MAX_DESCRIPTION", ShopItemRules.MaxDescription), ("MAX_COLLECTION", ShopItemRules.MaxCollection) })
            check(page.Replace("_000", "000").Contains($"{name} = {limit};"), "the admin page uses the server limit " + name);
        foreach (var state in Enum.GetValues<ShopItemState>())
            check(JsonSerializer.Serialize(state, GameJson.Options) == $"\"{state}\"" && page.Contains(state + ": '"), "the admin page knows the state " + state);
    }

    public static async Task Run(DbContextOptions<PickleVerseContext> options, Action<bool, string> check)
    {
        async Task<T> Shop<T>(Func<ShopItemService, Task<T>> action)
        { using var db = new PickleVerseContext(options); using var unit = new UnitOfWork(db); return await action(new ShopItemService(unit)); }
        Task Delete(int id) => Shop(async s => { await s.Delete(id); return true; });
        async Task<T> Game<T>(Func<OnlineGameService, Task<T>> action)
        {
            using var db = new PickleVerseContext(options); using var unit = new UnitOfWork(db); var settings = TestSettings.Load();
            return await action(new OnlineGameService(unit, new GameCatalogService(unit, settings), TimeProvider.System, settings,
                TestRedis.State, TestRedis.Leaderboard, new MatchReplayService(unit, TestReplayStorage.Shared, NullLogger<MatchReplayService>.Instance)));
        }
        async Task<HttpStatusCode?> Status(Func<Task> action)
        { try { await action(); return null; } catch (AppException e) { return e.StatusCode; } }
        async Task Reject(Func<Task> action, HttpStatusCode expected, string label) => check(await Status(action) == expected, label);
        async Task<CosmeticOffer?> InGame(string code)
        { using var db = new PickleVerseContext(options); using var unit = new UnitOfWork(db); return (await unit.Game.Catalog()).cosmetics.SingleOrDefault(c => c.id == code); }
        async Task<Dictionary<string, int>> Prices(string code)
        {
            using var db = new PickleVerseContext(options);
            return await (from p in db.CosmeticPrices join c in db.Cosmetics on p.CosmeticId equals c.Cosmeticid join u in db.Currencies on p.CurrencyId equals u.Currencyid
                          where c.Itemcode == code select new { u.Currencyname, p.Price }).ToDictionaryAsync(x => x.Currencyname, x => x.Price);
        }
        async Task<Guid> Player(int tokenpad, int elite)
        {
            var id = Guid.NewGuid();
            using (var db = new PickleVerseContext(options))
            {
                db.Users.Add(new User { Userid = id, Email = id.ToString("N") + "@example.invalid", Passwordhash = "test-only",
                    Roleid = await db.Roles.Where(r => r.Rolename == "Player").Select(r => r.Roleid).SingleAsync(), Status = UserStatus.Active, Createdat = DateTime.UtcNow });
                db.Playerprofiles.Add(new Playerprofile { Userid = id, Playername = "shop" + id.ToString("N")[..12], Avatar = "initial", Level = 1, Elo = 2000 });
                await db.SaveChangesAsync();
            }
            await Game(s => s.Read(id));
            using (var db = new PickleVerseContext(options))
                foreach (var (name, amount) in new[] { ("TOKENPAD", tokenpad), ("ELITE TOKEN", elite) })
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE playercurrencies SET amount={amount} WHERE playerid={id} AND currencyid=(SELECT currencyid FROM currencies WHERE currencyname={name})");
            return id;
        }
        GameCommand Buy(string currency, string code = Dragon) => new() { RequestId = Guid.NewGuid().ToString("N"), Code = code, Currency = currency };

        var seeded = await Shop(s => s.List());
        check(PaddleSkins.All.Where(skin => !Unpublished.Contains(skin.Code)).All(skin => seeded.Items.Single(i => i.Code == skin.Code) is { State: ShopItemState.OnSale, Issue: null, CanEdit: true, CanDelete: false }),
            "every seeded skin is on sale and its shown price equals its purchase price");
        check(seeded.Items.Single(i => i.Code == PaddleSkins.Default) is { State: ShopItemState.Default, CanEdit: false, CanDelete: false }, "default paddle is listed read-only");
        check(seeded.AvailableSkins.Select(s => s.Code).SequenceEqual(Unpublished) && seeded.Items.All(i => !Unpublished.Contains(i.Code)),
            "on a fresh database only the Blue Phoenix waits for an administrator to publish it");
        check(seeded.AvailableSkins.Single(s => s.Code == Phoenix) is { Title: "BLUE PHOENIX", Premium: true } phoenixSkin && phoenixSkin.Description.Contains("Phượng hoàng"),
            "the publishing form is filled in with the Blue Phoenix text");
        check(await InGame(Phoenix) == null, "the game does not sell the Blue Phoenix before it is published");
        await Reject(() => Shop(s => s.Create(Request())), HttpStatusCode.Conflict, "an item cannot be published twice");

        // A database that existed before the skin (staging): the administrator publishes it on the web page.
        using (var db = new PickleVerseContext(options))
            await db.Cosmetics.Where(c => c.Itemcode == Dragon).ExecuteDeleteAsync();
        check((await Shop(s => s.List())).AvailableSkins.Select(s => s.Code).Order().SequenceEqual(new[] { Dragon, Phoenix }.Order()) && await InGame(Dragon) == null,
            "a skin missing from the shop is offered for publishing");
        foreach (var bad in new[] { Request("paddle.unknown"), Request("PADDLE_CYBER_2026"), Request(PaddleSkins.Default), Request(elite: -5), Request(elite: 0), Request(title: "") })
            await Reject(() => Shop(s => s.Create(bad)), HttpStatusCode.BadRequest, "invalid item is not published: " + bad.Code + " " + bad.Title + " " + bad.ElitePrice);
        using (var db = new PickleVerseContext(options))
            check(!await db.Cosmetics.AnyAsync(c => c.Itemcode == Dragon || c.Itemcode == "PADDLE_CYBER_2026" || c.Itemcode == "paddle.unknown"), "rejected items leave no row behind");
        var racing = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Status(() => Shop(s => s.Create(Request())))));
        check(racing.Count(r => r == null) == 1 && racing.Count(r => r == HttpStatusCode.Conflict) == 1, "two administrators publishing at once create one item");
        var created = (await Shop(s => s.List())).Items.Single(i => i.Code == Dragon);
        check(created is { State: ShopItemState.OnSale, Issue: null, TokenpadPrice: 0, ElitePrice: 800, Premium: true, Owners: 0 }
            && created.ReleaseOrder > seeded.Items.Where(i => i.Code != Dragon).Max(i => i.ReleaseOrder), "published item is on sale and the newest of the shop");
        check(await InGame(Dragon) is { title: "DRAGON RUBY", tokenpadPrice: 0, elitePrice: 800, gemstone: true } sent && sent.releaseOrder == created.ReleaseOrder, "the game receives the published item");
        check(await Prices(Dragon) is { Count: 1 } price && price["ELITE TOKEN"] == 800, "only the published currency has a purchase price");

        var player = await Player(5000, 1000);
        await Reject(() => Game(s => s.Purchase(player, Buy("Tokenpad"))), HttpStatusCode.BadRequest, "item cannot be bought with a currency without a price");
        var bought = await Game(s => s.Purchase(player, Buy("EliteToken")));
        check(bought.inventory.owned.Contains(Dragon) && bought.player.gems == 200 && bought.player.coins == 5000, "a player buys the published item for 800 ELITE TOKEN");
        check((await Shop(s => s.List())).Items.Single(i => i.Code == Dragon).Owners == 1, "owners are counted");

        var edit = Request("paddle.thot", 900, 750, "RỒNG RUBY", "Mô tả mới.");
        var changed = await Shop(s => s.Update(created.CosmeticId, edit));
        check(changed is { Code: Dragon, Title: "RỒNG RUBY", TokenpadPrice: 900, ElitePrice: 750, State: ShopItemState.OnSale } && changed.ReleaseOrder == created.ReleaseOrder,
            "text and prices change while the code and the release order stay");
        check(await InGame(Dragon) is { title: "RỒNG RUBY", tokenpadPrice: 900, elitePrice: 750 } && await Prices(Dragon) is { Count: 2 } both && both["TOKENPAD"] == 900 && both["ELITE TOKEN"] == 750,
            "the game shows the prices that are charged");
        await Reject(() => Shop(s => s.Update(created.CosmeticId, Request(elite: -1))), HttpStatusCode.BadRequest, "invalid change is rejected");
        await Reject(() => Shop(s => s.Update(int.MaxValue, Request())), HttpStatusCode.NotFound, "unknown item cannot be changed");
        check((await Prices(Dragon))["ELITE TOKEN"] == 750, "a rejected change keeps the previous prices");

        var stopped = await Shop(s => s.Update(created.CosmeticId, Request(tokenpad: 0, elite: 0)));
        check(stopped.State == ShopItemState.Stopped && (await Prices(Dragon)).Count == 0 && await InGame(Dragon) is { tokenpadPrice: 0, elitePrice: 0 },
            "a stopped item has no price but stays known to the game");
        var late = await Player(5000, 5000);
        await Reject(() => Game(s => s.Purchase(late, Buy("EliteToken"))), HttpStatusCode.BadRequest, "a stopped item cannot be bought");
        var equipped = await Game(s => s.Equip(player, Buy("EliteToken")));
        check(equipped.inventory.equippedPaddle == Dragon, "owners keep using a stopped item");
        check((await Shop(s => s.Update(created.CosmeticId, Request()))).State == ShopItemState.OnSale && (await Prices(Dragon))["ELITE TOKEN"] == 800, "a stopped item can be sold again");

        // The administrator publishes the Blue Phoenix from the form the page fills in, as for the Dragon Ruby.
        var skin = PaddleSkins.Find(Phoenix)!;
        var phoenix = await Shop(s => s.Create(Request(Phoenix, 0, 900, skin.Title, skin.Description, skin.Collection)));
        check(phoenix is { Code: Phoenix, State: ShopItemState.OnSale, Issue: null, ElitePrice: 900, TokenpadPrice: 0, Premium: true }
            && phoenix.ReleaseOrder > created.ReleaseOrder, "the Blue Phoenix is published and leads the shop");
        check(await InGame(Phoenix) is { title: "BLUE PHOENIX", elitePrice: 900, tokenpadPrice: 0, gemstone: true } && (await Prices(Phoenix))["ELITE TOKEN"] == 900,
            "the game receives the Blue Phoenix with the price that is charged");
        var fan = await Player(0, 1000);
        var owned = await Game(s => s.Purchase(fan, Buy("EliteToken", Phoenix)));
        check(owned.inventory.owned.Contains(Phoenix) && owned.player.gems == 100, "a player buys the Blue Phoenix for 900 ELITE TOKEN");
        check((await Game(s => s.Equip(fan, Buy("EliteToken", Phoenix)))).inventory.equippedPaddle == Phoenix, "and plays with it");
        check(!(await Shop(s => s.List())).AvailableSkins.Any(), "nothing is left to publish");

        // Rows of the old form: neither game configuration nor purchase prices.
        using (var db = new PickleVerseContext(options))
        {
            int currency = await db.Currencies.OrderBy(c => c.Currencyid).Select(c => c.Currencyid).FirstAsync();
            db.Cosmetics.Add(new Cosmetic { Itemcode = "PADDLE_CYBER_2026", Cosmeticname = "Vợt Cyber", Description = "Form cũ.", Price = 100, Type = "PADDLE", Currencyid = currency });
            await db.SaveChangesAsync();
            int id = await db.Cosmetics.Where(c => c.Itemcode == "paddle.gemstone").Select(c => c.Cosmeticid).SingleAsync();
            await db.Cosmetics.Where(c => c.Cosmeticid == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Config, "{}"));
            await db.CosmeticPrices.Where(p => p.CosmeticId == id).ExecuteDeleteAsync();
        }
        var old = (await Shop(s => s.List())).Items;
        var cyber = old.Single(i => i.Code == "PADDLE_CYBER_2026");
        var gemstone = old.Single(i => i.Code == "paddle.gemstone");
        check(cyber is { State: ShopItemState.Incomplete, CanEdit: false, CanDelete: true } && !string.IsNullOrEmpty(cyber.Issue), "item of the old form is reported with a reason");
        check(gemstone is { State: ShopItemState.Incomplete, CanEdit: true } && !string.IsNullOrEmpty(gemstone.Issue), "item without game configuration can be repaired");
        check((await Shop(s => s.Update(gemstone.CosmeticId, Request("paddle.gemstone", 0, 650, "GEMSTONE")))).State == ShopItemState.OnSale
            && await InGame("paddle.gemstone") is { elitePrice: 650 }, "saving an incomplete item puts it on sale");
        await Delete(cyber.CosmeticId);
        check((await Shop(s => s.List())).Items.All(i => i.Code != "PADDLE_CYBER_2026"), "item the game does not read is removed");
        await Reject(() => Delete(created.CosmeticId), HttpStatusCode.BadRequest, "item of the game cannot be removed");
        await Reject(() => Delete(cyber.CosmeticId), HttpStatusCode.NotFound, "removed item is gone");
    }
}
