using System.Data;
using Entities.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PickleVerse.Progression;
using Repositories.Context;
using Repositories.Interfaces;

namespace Repositories.Game;

public interface IRepositoryTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}

// All repositories in a request share the scoped context and its transaction.
public sealed partial class GameRepository(PickleVerseContext db) : IAppActivityRepository
{
    sealed class Transaction(IDbContextTransaction transaction) : IRepositoryTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }

    public async Task<IRepositoryTransaction> BeginTransactionAsync(IsolationLevel isolation = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default) => new Transaction(await db.Database.BeginTransactionAsync(isolation, cancellationToken));
    public Task<int> SaveChangesAsync() => db.SaveChangesAsync();
    public void ClearTracking() => db.ChangeTracker.Clear();
    public Task Lock(string key) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key},0))");
    public Task<bool> CanConnect(CancellationToken cancellationToken) => db.Database.CanConnectAsync(cancellationToken);

    public Task<Playerprofile?> LockProfile(Guid player, bool tracking = true)
    {
        var query = db.Playerprofiles.FromSqlInterpolated($"SELECT * FROM playerprofiles WHERE userid={player} FOR UPDATE");
        return (tracking ? query : query.AsNoTracking()).SingleOrDefaultAsync();
    }

    public Task<DateTime> AccountCreated(Guid player) => db.Users.Where(u => u.Userid == player).Select(u => u.Createdat).SingleAsync();
    public Task<UserStatus?> AccountStatus(Guid player) => db.Users.Where(u => u.Userid == player).Select(u => (UserStatus?)u.Status).SingleOrDefaultAsync();
    public Task<string?> PlayerName(Guid player) => db.Playerprofiles.Where(p => p.Userid == player).Select(p => p.Playername).SingleOrDefaultAsync();
    public async Task<long?> LockActivityTotal(Guid player) => (await LockProfile(player, tracking: false))?.Timeplayedseconds;
    public Task SetActivityTotal(Guid player, long total) => db.Playerprofiles.Where(p => p.Userid == player)
        .ExecuteUpdateAsync(s => s.SetProperty(p => p.Timeplayedseconds, total));
    public Task<List<Playerprofile>> Profiles(Guid[] ids) => db.Playerprofiles.AsNoTracking().Include(p => p.User)
        .Where(p => ids.Contains(p.Userid)).ToListAsync();

    public async Task<OnlineInventory> Inventory(Guid player)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO playercurrencies(playerid,currencyid,amount)
            SELECT {player},currencyid,0 FROM currencies WHERE currencyname IN ('TOKENPAD','ELITE TOKEN') ON CONFLICT DO NOTHING
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO playercosmetics(playerid,cosmeticid,equipped)
            SELECT {player},cosmeticid,NOT EXISTS(SELECT 1 FROM playercosmetics WHERE playerid={player} AND equipped)
            FROM cosmetics WHERE itemcode='paddle.default' ON CONFLICT DO NOTHING
            """);
        var balances = await db.Playercurrencies.AsNoTracking().Where(p => p.Playerid == player)
            .Select(p => new { Name = p.Currency.Currencyname, p.Amount }).ToListAsync();
        var items = await db.Playercosmetics.AsNoTracking().Where(p => p.Playerid == player).OrderBy(p => p.Cosmeticid)
            .Select(p => new { Code = p.Cosmetic.Itemcode, p.Equipped }).ToListAsync();
        return new OnlineInventory
        {
            tokenpad = balances.SingleOrDefault(b => b.Name == "TOKENPAD")?.Amount ?? 0,
            elite = balances.SingleOrDefault(b => b.Name == "ELITE TOKEN")?.Amount ?? 0,
            owned = items.Select(i => i.Code).ToList(),
            equippedPaddle = items.FirstOrDefault(i => i.Equipped)?.Code ?? "paddle.default"
        };
    }

    public async Task SaveBalance(Guid player, string currency, int amount, long before, string command, string reason)
    {
        var currencyId = await db.Currencies.Where(c => c.Currencyname == currency).Select(c => c.Currencyid).SingleAsync();
        await db.Playercurrencies.Where(p => p.Playerid == player && p.Currencyid == currencyId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Amount, amount));
        if (amount != before)
            db.CurrencyTransactions.Add(new CurrencyTransaction { PlayerId = player, CurrencyId = currencyId, CommandId = command,
                Delta = amount - before, BalanceAfter = amount, Reason = reason });
    }

    public sealed record ShopPrice(int price, int id);
    public Task<ShopPrice?> Price(string code, string currency) =>
        (from price in db.CosmeticPrices
         join cosmetic in db.Cosmetics on price.CosmeticId equals cosmetic.Cosmeticid
         join unit in db.Currencies on price.CurrencyId equals unit.Currencyid
         where cosmetic.Itemcode == code && unit.Currencyname == currency
         select new ShopPrice(price.Price, cosmetic.Cosmeticid)).SingleOrDefaultAsync();
    public void AddCosmetic(Guid player, int cosmetic) => db.Playercosmetics.Add(new Playercosmetic { Playerid = player, Cosmeticid = cosmetic, Equipped = false });
    public async Task Equip(Guid player, string code)
    {
        await db.Playercosmetics.Where(p => p.Playerid == player).ExecuteUpdateAsync(s => s.SetProperty(p => p.Equipped, false));
        await db.Playercosmetics.Where(p => p.Playerid == player && p.Cosmetic.Itemcode == code)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Equipped, true));
    }

    public Task<GameCommandRecord?> Command(Guid player, string command) => db.GameCommandRecords.AsNoTracking()
        .SingleOrDefaultAsync(c => c.PlayerId == player && c.CommandId == command);
    public void AddCommand(Guid player, string command, string kind, string fingerprint, string result) =>
        db.GameCommandRecords.Add(new GameCommandRecord { PlayerId = player, CommandId = command, Kind = kind, Fingerprint = fingerprint, Result = result });
    public Task<List<string>> BonusCommands(Guid player) => db.GameCommandRecords.Where(c => c.PlayerId == player && c.Kind == "period-bonus")
        .Select(c => c.CommandId).ToListAsync();
    public Task<List<string>> AchievementClaims(Guid player) => db.Playerachievements.Where(p => p.Playerid == player && p.Isclaimed)
        .Select(p => p.Achievement.Achievementcode).ToListAsync();
    public Task SaveAchievement(Guid player, string code, int progress, bool claimed) => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO playerachievements(playerid,achievementid,progress,isclaimed,claimedat)
        SELECT {player},achievementid,{progress},{claimed},CASE WHEN {claimed} THEN CURRENT_TIMESTAMP ELSE NULL END
        FROM achievements WHERE achievementcode={code}
        ON CONFLICT(playerid,achievementid) DO UPDATE SET progress=EXCLUDED.progress,isclaimed=EXCLUDED.isclaimed,
            claimedat=COALESCE(playerachievements.claimedat,EXCLUDED.claimedat)
        """);

    public sealed record MissionProgress(string code, string period, int progress, bool claimed, int streak);
    public Task<List<MissionProgress>> MissionPeriods(Guid player) => db.Playermissions.AsNoTracking()
        .Where(p => p.Playerid == player && p.Periodkey != "").OrderBy(p => p.Periodkey)
        .Select(p => new MissionProgress(p.Mission.Code!, p.Periodkey, p.Progress, p.Isclaimed, p.CurrentStreak)).ToListAsync();
    public Task SaveMission(Guid player, string code, string period, int progress, bool claimed, int streak) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO playermissions(playerid,missionid,periodkey,progress,isclaimed,claimedat,currentstreak)
        SELECT {player},missionid,{period},{progress},{claimed},CASE WHEN {claimed} THEN CURRENT_TIMESTAMP ELSE NULL END,{streak}
        FROM missions WHERE code={code}
        ON CONFLICT(playerid,missionid,periodkey) DO UPDATE SET progress=EXCLUDED.progress,isclaimed=EXCLUDED.isclaimed,
            claimedat=COALESCE(playermissions.claimedat,EXCLUDED.claimedat),currentstreak=EXCLUDED.currentstreak
        """);

    public async Task<GameCatalog> Catalog()
    {
        if (db.Database.GetDbConnection().State != ConnectionState.Open)
            await db.Database.OpenConnectionAsync();
        var achievements = await db.Achievements.AsNoTracking().OrderBy(a => a.Achievementid).ToListAsync();
        var missions = await db.Missions.AsNoTracking().Where(m => m.Code != null).OrderBy(m => m.Missionid).Select(m => m.Config).ToListAsync();
        var lessons = await db.Lessons.AsNoTracking().Where(l => l.Code != null && l.PublishStatus == ContentPublishStatus.Published).OrderBy(l => l.SortOrder).ThenBy(l => l.Lessonid).Select(l => l.Config).ToListAsync();
        var cosmetics = await db.Cosmetics.AsNoTracking().Where(c => c.Itemcode.StartsWith("paddle.")).OrderBy(c => c.Cosmeticid).Select(c => c.Config).ToListAsync();
        return new GameCatalog {
            definitions = achievements.Select(a => new ProgressDefinition(a.Achievementcode, a.Achievementname, ProgressScope.Achievement,
                Enum.Parse<ProgressMetric>(a.Type), a.Target, GameJson.Read<RewardBundle>(a.Reward)))
                .Concat(missions.Select(GameJson.Read<ProgressDefinition>)).ToList(),
            lessons = lessons.Select(GameJson.Read<TrainingDrill>).ToArray(), cosmetics = cosmetics.Select(GameJson.Read<CosmeticOffer>).ToArray()
        };
    }
    public Task<string?> DefinitionCode(int id, bool achievement) => achievement
        ? db.Achievements.Where(a => a.Achievementid == id).Select(a => a.Achievementcode).SingleOrDefaultAsync()
        : db.Missions.Where(m => m.Missionid == id).Select(m => m.Code).SingleOrDefaultAsync();
    public sealed record CosmeticInfo(string code, string currency);
    public Task<CosmeticInfo?> Cosmetic(int id) => db.Cosmetics.Where(c => c.Cosmeticid == id)
        .Select(c => new CosmeticInfo(c.Itemcode, c.Currency.Currencyname)).SingleOrDefaultAsync();
}
