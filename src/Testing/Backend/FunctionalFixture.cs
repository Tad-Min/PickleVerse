using Entities.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Repositories.Context;
using Services.Implementations;

sealed class FunctionalFixture(DbContextOptions<PickleVerseContext> options)
{
    public DbContextOptions<PickleVerseContext> Options => options;
    public async Task<Guid> Player(string role = "Player")
    {
        using var db = new PickleVerseContext(options); Guid id = Guid.NewGuid(); string email = id.ToString("N") + "@example.invalid";
        db.Users.Add(new User { Userid = id, Email = email, Passwordhash = BCrypt.Net.BCrypt.HashPassword("TestPass!234", 4),
            Roleid = await db.Roles.Where(r => r.Rolename == role).Select(r => r.Roleid).SingleAsync(), Status = UserStatus.Active, Createdat = DateTime.UtcNow });
        db.Playerprofiles.Add(new Playerprofile { Userid = id, Playername = "test" + id.ToString("N")[..12], Avatar = "initial", Level = 1, Elo = 2000 });
        db.Verificationemails.Add(new Verificationemail { Userid = id, Email = email, Token = "test-only", Isverified = true,
            Createdat = DateTime.UtcNow, Expiresat = DateTime.UtcNow.AddHours(1) });
        db.Playercosmetics.Add(new Playercosmetic { Playerid = id, Cosmeticid = await db.Cosmetics.Where(c => c.Itemcode == "paddle.default").Select(c => c.Cosmeticid).SingleAsync(), Equipped = true });
        await db.SaveChangesAsync(); return id;
    }
    public async Task<T> Game<T>(Func<OnlineGameService, Task<T>> action)
    {
        using var db = new PickleVerseContext(options); using var unit = new UnitOfWork(db); var settings = TestSettings.Load();
        return await action(new OnlineGameService(unit, new GameCatalogService(unit, settings), TimeProvider.System, settings,
            TestRedis.State, TestRedis.Leaderboard, new MatchReplayService(unit, TestReplayStorage.Shared, NullLogger<MatchReplayService>.Instance)));
    }
}
