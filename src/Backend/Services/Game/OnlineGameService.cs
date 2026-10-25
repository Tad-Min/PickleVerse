using System.Net;
using System.Security.Cryptography;
using System.Text;
using Entities.Models;
using Microsoft.EntityFrameworkCore;
using PickleVerse.Progression;
using Repositories.Context;
using Services.Exceptions;
using Services.Configuration;
using static Services.Game.GameSql;

namespace Services.Game;

public sealed partial class OnlineGameService(PickleVerseContext db, GameSql sql, GameCatalogService catalogs, TimeProvider clock, ServerSettings settings, RedisGameState matchmaking, LeaderboardCache leaderboard)
{
    sealed class StoredState
    {
        public PlayerProfile player = new();
        public TutorialState tutorial = new();
    }
    sealed class Session
    {
        public Playerprofile profile = null!;
        public StoredState stored = new();
        public OnlineInventory inventory = new();
        public GameCatalog catalog = new();
        public long revision, beforeCoins, beforeElite;
        public PlayerProfile Player => stored.player;
        public ProgressionEngine Engine => new(catalog.Rules());
        public GameSnapshot Snapshot() => new() { player = Player, inventory = inventory, catalog = catalog, isMale = profile.Ismale, revision = revision, tutorial = stored.tutorial };
    }
    DateTime Now => clock.GetUtcNow().UtcDateTime;
    static AppException Bad(string message) => new(message, HttpStatusCode.BadRequest);
    static AppException Conflict(string message) => new(message, HttpStatusCode.Conflict);

    async Task<Session> Load(Guid id, GameCatalog catalog)
    {
        var profile = await db.Playerprofiles.FromSqlInterpolated($"SELECT * FROM playerprofiles WHERE userid={id} FOR UPDATE").SingleOrDefaultAsync()
            ?? throw new AppException("Không tìm thấy hồ sơ.", HttpStatusCode.NotFound);
        var saved = await sql.One("SELECT data::text,revision FROM gameprogress WHERE playerid=@id", r => new { data = r.GetString(0), revision = r.GetInt64(1) }, P("id", id));
        var stored = saved == null ? new StoredState() : GameJson.Read<StoredState>(saved.data);
        var p = stored.player;
        p.online = true;
        p.id = id.ToString("N");
        p.playerName = profile.Playername;
        p.avatarId = profile.Avatar;
        p.elo = profile.Elo;
        p.level = profile.Level;
        p.levelXp = profile.Exp;
        p.wins = profile.Wincount;
        p.losses = profile.Losecount;
        p.activeSeconds = profile.Timeplayedseconds;
        if (saved == null)
        {
            p.totalXp = 100L * p.level * (p.level - 1) + p.levelXp;
            p.lifetime.matches = p.wins + p.losses;
            p.lifetime.wins = p.wins;
            p.lifetime.streak = p.lifetime.bestStreak = profile.Winstreak;
            p.achievementClaims = await sql.Query("SELECT a.achievementcode FROM playerachievements p JOIN achievements a USING(achievementid) WHERE p.playerid=@id AND p.isclaimed", r => r.GetString(0), P("id", id));
        }
        await sql.Run("INSERT INTO playercurrencies(playerid,currencyid,amount) SELECT @id,currencyid,0 FROM currencies WHERE currencyname IN ('TOKENPAD','ELITE TOKEN') ON CONFLICT DO NOTHING", P("id", id));
        var balances = await sql.Query("SELECT c.currencyname,p.amount FROM playercurrencies p JOIN currencies c USING(currencyid) WHERE p.playerid=@id", r => new { name = r.GetString(0), amount = (long)r.GetInt32(1) }, P("id", id));
        p.coins = balances.FirstOrDefault(b => b.name == "TOKENPAD")?.amount ?? 0;
        p.gems = balances.FirstOrDefault(b => b.name == "ELITE TOKEN")?.amount ?? 0;
        await sql.Run("INSERT INTO playercosmetics(playerid,cosmeticid,equipped) SELECT @id,cosmeticid,NOT EXISTS(SELECT 1 FROM playercosmetics WHERE playerid=@id AND equipped) FROM cosmetics WHERE itemcode='paddle.default' ON CONFLICT DO NOTHING", P("id", id));
        var items = await sql.Query("SELECT c.itemcode,p.equipped FROM playercosmetics p JOIN cosmetics c USING(cosmeticid) WHERE p.playerid=@id ORDER BY c.cosmeticid", r => new { code = r.GetString(0), equipped = r.GetBoolean(1) }, P("id", id));
        var inventory = new OnlineInventory { tokenpad = p.coins, elite = p.gems, owned = items.Select(i => i.code).ToList(), equippedPaddle = items.FirstOrDefault(i => i.equipped)?.code ?? "paddle.default" };
        return new Session { profile = profile, stored = stored, inventory = inventory, catalog = catalog, revision = saved?.revision ?? 0, beforeCoins = p.coins, beforeElite = p.gems };
    }

    async Task Persist(Session s, Guid id, string command, string reason)
    {
        var p = s.Player;
        if (p.coins < 0 || p.gems < 0 || p.coins > int.MaxValue || p.gems > int.MaxValue)
            throw Bad("Số dư vượt giới hạn hỗ trợ.");
        s.profile.Level = p.level;
        s.profile.Exp = p.levelXp;
        s.profile.Elo = p.elo;
        s.profile.Wincount = checked((int)p.wins);
        s.profile.Losecount = checked((int)p.losses);
        s.profile.Winstreak = p.lifetime.streak;
        s.profile.Daystreak = p.dailyStreak?.current ?? 0;
        s.profile.Timeplayedseconds = (long)p.activeSeconds;
        foreach (var currency in new[] { (name: "TOKENPAD", amount: p.coins, before: s.beforeCoins), (name: "ELITE TOKEN", amount: p.gems, before: s.beforeElite) })
        {
            await sql.Run("UPDATE playercurrencies SET amount=@amount WHERE playerid=@id AND currencyid=(SELECT currencyid FROM currencies WHERE currencyname=@currency)", P("amount", (int)currency.amount), P("id", id), P("currency", currency.name));
            if (currency.amount != currency.before)
                await sql.Run("INSERT INTO currencytransactions(playerid,currencyid,commandid,delta,balanceafter,reason) SELECT @id,currencyid,@command,@delta,@balance,@reason FROM currencies WHERE currencyname=@currency", P("id", id), P("command", command), P("delta", currency.amount - currency.before), P("balance", currency.amount), P("reason", reason), P("currency", currency.name));
        }
        s.inventory.tokenpad = p.coins;
        s.inventory.elite = p.gems;
        p.shopData = GameJson.Write(s.inventory);
        foreach (var def in s.catalog.definitions.Where(d => d.scope == ProgressScope.Achievement))
            await sql.Run("INSERT INTO playerachievements(playerid,achievementid,progress,isclaimed,claimedat) SELECT @id,achievementid,@progress,@claimed,CASE WHEN @claimed THEN CURRENT_TIMESTAMP ELSE NULL END FROM achievements WHERE achievementcode=@code ON CONFLICT(playerid,achievementid) DO UPDATE SET progress=EXCLUDED.progress,isclaimed=EXCLUDED.isclaimed,claimedat=COALESCE(playerachievements.claimedat,EXCLUDED.claimedat)", P("id", id), P("progress", (int)Math.Min(int.MaxValue, s.Engine.Progress(p, def))), P("claimed", p.achievementClaims.Contains(def.id)), P("code", def.id));
        foreach (var def in s.catalog.definitions.Where(d => d.scope != ProgressScope.Achievement))
        {
            var period = def.scope == ProgressScope.Daily ? p.daily : p.weekly;
            await sql.Run("INSERT INTO playermissions(playerid,missionid,periodkey,progress,isclaimed,claimedat) SELECT @id,missionid,@period,@progress,@claimed,CASE WHEN @claimed THEN CURRENT_TIMESTAMP ELSE NULL END FROM missions WHERE code=@code ON CONFLICT(playerid,missionid) DO UPDATE SET periodkey=EXCLUDED.periodkey,progress=EXCLUDED.progress,isclaimed=EXCLUDED.isclaimed,claimedat=CASE WHEN NOT EXCLUDED.isclaimed THEN NULL WHEN playermissions.periodkey=EXCLUDED.periodkey THEN COALESCE(playermissions.claimedat,EXCLUDED.claimedat) ELSE EXCLUDED.claimedat END",
                P("id", id), P("period", period.key), P("progress", (int)Math.Min(int.MaxValue, s.Engine.Progress(p, def))), P("claimed", period.claimed.Contains(def.id)), P("code", def.id));
        }
        await db.SaveChangesAsync();
        s.revision = await sql.One("INSERT INTO gameprogress(playerid,data,revision) VALUES(@id,@data::jsonb,1) ON CONFLICT(playerid) DO UPDATE SET data=EXCLUDED.data,revision=gameprogress.revision+1,updatedat=CURRENT_TIMESTAMP RETURNING revision", r => r.GetInt64(0), P("id", id), P("data", GameJson.Write(s.stored)));
    }

    async Task<GameSnapshot> Execute(Guid id, string? requestId, string kind, object? payload, Func<Session, Task> operation)
    {
        var catalog = await catalogs.Load();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var session = await Load(id, catalog);
        string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(GameJson.Payload(payload))));
        if (requestId != null)
        {
            if (requestId.Length < 8 || requestId.Length > 64)
                throw Bad("Mã yêu cầu không hợp lệ.");
            var previous = await sql.One("SELECT kind,fingerprint FROM gamecommands WHERE playerid=@id AND commandid=@command", r => new { kind = r.GetString(0), fingerprint = r.GetString(1) }, P("id", id), P("command", requestId));
            if (previous != null)
            {
                if (previous.kind != kind || previous.fingerprint != fingerprint)
                    throw Conflict("Mã yêu cầu đã được dùng cho nội dung khác.");
                await transaction.CommitAsync();
                session.Player.shopData = GameJson.Write(session.inventory);
                return session.Snapshot();
            }
        }
        DailyStreakEngine.Ensure(session.Player, Now);
        session.Engine.RefreshPeriods(session.Player, Now);
        DailyStreakEngine.Refresh(session.Player, Now);
        await operation(session);
        var command = requestId ?? Guid.NewGuid().ToString("N");
        await Persist(session, id, command, kind);
        if (requestId != null)
            await sql.Run("INSERT INTO gamecommands(playerid,commandid,kind,fingerprint,result) VALUES(@id,@command,@kind,@fingerprint,@result::jsonb)", P("id", id), P("command", requestId), P("kind", kind), P("fingerprint", fingerprint), P("result", GameJson.Write(new { session.revision })));
        await transaction.CommitAsync();
        return session.Snapshot();
    }

    public async Task<GameSnapshot> Read(Guid id)
    {
        await ReconcileOnline(id);
        // Reconciliation uses its own transaction and may track several profiles. Read the
        // subsequent locked snapshot afresh, including results committed by another request.
        db.ChangeTracker.Clear();
        return await Execute(id, null, "refresh", null, _ => Task.CompletedTask);
    }
    public Task<GameSnapshot> Claim(Guid id, GameCommand request) => Execute(id, request.RequestId, "claim", request, s =>
    {
        if (!s.Engine.Claim(s.Player, request.Code, Now))
            throw Bad("Chưa đủ điều kiện nhận thưởng hoặc đã nhận.");
        return Task.CompletedTask;
    });
    public Task<GameSnapshot> Tutorial(Guid id, TutorialUpdateRequest request) => Execute(id, request.RequestId, "tutorial", request, s =>
    {
        if (request.Key == "completed")
            s.stored.tutorial.completed = true;
        else if (request.Key == "skipped")
            s.stored.tutorial.skipped = true;
        else throw Bad("Trạng thái hướng dẫn không hợp lệ.");
        return Task.CompletedTask;
    });
    public Task<GameSnapshot> Purchase(Guid id, GameCommand request) => Execute(id, request.RequestId, "purchase", request, async s =>
    {
        if (s.inventory.owned.Contains(request.Code))
            throw Bad("Bạn đã sở hữu vật phẩm này.");
        string currency = request.Currency switch { "Tokenpad" => "TOKENPAD", "EliteToken" => "ELITE TOKEN", _ => throw Bad("Loại tiền không hợp lệ.") };
        var offer = await sql.One("SELECT p.price,c.cosmeticid FROM cosmeticprices p JOIN cosmetics c USING(cosmeticid) JOIN currencies u ON u.currencyid=p.currencyid WHERE c.itemcode=@code AND u.currencyname=@currency", r => new { price = r.GetInt32(0), id = r.GetInt32(1) }, P("code", request.Code), P("currency", currency)) ?? throw Bad("Vật phẩm không bán bằng loại tiền này.");
        if (currency == "TOKENPAD")
        {
            if (s.Player.coins < offer.price)
                throw Bad("Không đủ TOKENPAD.");
            s.Player.coins -= offer.price;
        }
        else
        {
            if (s.Player.gems < offer.price)
                throw Bad("Không đủ ELITE TOKEN.");
            s.Player.gems -= offer.price;
        }
        await sql.Run("INSERT INTO playercosmetics(playerid,cosmeticid,equipped) VALUES(@id,@item,false)", P("id", id), P("item", offer.id));
        s.inventory.owned.Add(request.Code);
    });
    public Task<GameSnapshot> Equip(Guid id, GameCommand request) => Execute(id, request.RequestId, "equip", request, async s =>
    {
        if (!s.inventory.owned.Contains(request.Code))
            throw Bad("Bạn chưa sở hữu vật phẩm này.");
        await sql.Run("UPDATE playercosmetics SET equipped=false WHERE playerid=@id", P("id", id));
        await sql.Run("UPDATE playercosmetics SET equipped=true WHERE playerid=@id AND cosmeticid=(SELECT cosmeticid FROM cosmetics WHERE itemcode=@code)", P("id", id), P("code", request.Code));
        s.inventory.equippedPaddle = request.Code;
    });
}
