using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Repositories.Context;
using Entities.Exceptions;
using Services.Implementations;
using static Repositories.Data.PostgresCommands;

static class ActivityRedisChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, TestClock clock, Action<bool, string> check)
    {
        async Task<T> Call<T>(Func<OnlineGameService, Task<T>> action)
        {
            await using var db = new PickleVerseContext(options);
            var sql = new PostgresCommands(db);var sqlData = new UnitOfWork(db);
            return await action(new OnlineGameService(sqlData, new GameCatalogService(sqlData, TestSettings.Load()), clock, TestSettings.Load(),
                TestRedis.State, TestRedis.Leaderboard, new MatchReplayService(sqlData, TestReplayStorage.Shared, NullLogger<MatchReplayService>.Instance)));
        }
        async Task<Guid> Player()
        {
            var id = Guid.NewGuid();
            await using var db = new PickleVerseContext(options);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO users(userid,email,passwordhash,roleid) SELECT {id},{id + "@activity.invalid"},'disabled',roleid FROM roles WHERE rolename='Player'");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO playerprofiles(userid,playername) VALUES({id},{"Activity" + id.ToString("N")[..8]})");
            return id;
        }
        async Task<long> Total(Guid id)
        {
            await using var db = new PickleVerseContext(options);
            return await db.Playerprofiles.Where(x => x.Userid == id).Select(x => x.Timeplayedseconds).SingleAsync();
        }
        async Task Reject(Func<Task> call, HttpStatusCode status, string name)
        {
            try { await call(); }
            catch (AppException error) when (error.StatusCode == status) { check(true, name); return; }
            throw new Exception("FAILED: " + name);
        }
        var redis = TestRedis.Connection.GetDatabase();
        string SessionKey(Guid id) => TestRedis.Prefix + "activity:session:" + id.ToString("N");
        string PlayerKey(Guid id) => TestRedis.Prefix + "activity:player:" + id.ToString("N");
        Task Open(Guid session, long elapsed = 0) => Call(s => s.OpenActivity(new() { SessionId = session, ElapsedSeconds = elapsed }));
        Task<AppActivityReceipt> Tick(Guid player, Guid session, long elapsed, bool close = false) =>
            Call(s => s.Activity(player, new() { SessionId = session, ElapsedSeconds = elapsed, Close = close }));

        await using (var db = new PickleVerseContext(options))
            check(await new PostgresCommands(db).One("SELECT to_regclass('public.appactivitysessions') IS NULL", r => r.GetBoolean(0)), "activity session table is absent from the current schema");
        var id = await Player();
        var sid = Guid.NewGuid();
        await Open(sid);
        var opening = await redis.StringGetAsync(SessionKey(sid));
        check(!opening.IsNullOrEmpty && await redis.KeyTimeToLiveAsync(SessionKey(sid)) is { TotalDays: > 7 and <= 8 }, "anonymous opening lives in Redis with bounded retention");
        clock.Now = clock.Now.AddSeconds(30);
        await Open(sid, 0);
        check(await redis.StringGetAsync(SessionKey(sid)) == opening, "retrying open does not reset the server's opening time");
        var receipt = await Tick(id, sid, 30);
        check(receipt.totalSeconds == 30 && await Total(id) == 30, "pre-login app time is credited to the PostgreSQL profile total");
        var duplicates = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Tick(id, sid, 30)));
        check(duplicates.All(x => x.totalSeconds == 30) && await Total(id) == 30, "concurrent checkpoint retries credit an interval once");
        check((await Tick(id, sid, 9999)).totalSeconds == 30, "client elapsed time cannot exceed the server clock");
        clock.Now = clock.Now.AddSeconds(30);
        check((await Tick(id, sid, 60)).totalSeconds == 60 && (await Tick(id, sid, 15)).elapsedSeconds == 60, "later checkpoints add a delta and older checkpoints cannot move it backwards");
        var overlap = Guid.NewGuid();
        await Open(overlap, 60);
        check((await Tick(id, overlap, 60)).totalSeconds == 60, "two simultaneous app windows do not double the persisted time");
        await Tick(id, sid, 60, true);
        clock.Now = clock.Now.AddSeconds(10);
        check((await Tick(id, sid, 70)).totalSeconds == 60, "a closed session cannot add time or reopen on retries");
        await Tick(id, overlap, 70, true);
        var delayed = Guid.NewGuid();
        await Open(delayed, 80);
        check((await Tick(id, delayed, 80, true)).totalSeconds == 80, "late login backfills only uncovered earlier time");
        var another = await Player();
        await Reject(() => Tick(another, sid, 70), HttpStatusCode.Forbidden, "a bound Redis session cannot be credited to another account");
        await Reject(() => Tick(id, Guid.NewGuid(), 0), HttpStatusCode.Gone, "missing Redis session returns 410 so Unity can rotate its session");
        check(await Total(id) == 80, "a missing session leaves the saved total unchanged");
        await redis.KeyDeleteAsync(PlayerKey(id));
        await Reject(() => Tick(id, delayed, 80), HttpStatusCode.Gone, "loss of per-player Redis state cannot replay previously credited sessions");
        check(await Total(id) == 80, "Redis state loss does not erase the PostgreSQL total");
        var fresh = Guid.NewGuid();
        await Open(fresh);
        clock.Now = clock.Now.AddSeconds(5);
        check((await Tick(id, fresh, 5)).totalSeconds == 85, "a fresh session resumes from the persisted total after Redis state loss");
        await redis.KeyDeleteAsync(SessionKey(fresh));
        await Reject(() => Tick(id, fresh, 5), HttpStatusCode.Gone, "expiry of the individual opening also returns 410");
        var beforeRestore = Guid.NewGuid();
        await Open(beforeRestore);
        await Tick(id, beforeRestore, 0);
        var staleState = GameJson.Read<RedisAppActivity.PlayerState>((await redis.StringGetAsync(PlayerKey(id))).ToString());
        staleState.expectedTotal = staleState.targetTotal = 84;
        await redis.StringSetAsync(PlayerKey(id), GameJson.Write(staleState), TimeSpan.FromDays(8));
        await Reject(() => Tick(id, beforeRestore, 0), HttpStatusCode.Gone, "a stale Redis AOF snapshot rotates the generation instead of getting stuck on a total mismatch");
        check(await Total(id) == 85, "stale Redis recovery cannot overwrite the newer PostgreSQL total");
        var afterRestore = Guid.NewGuid();
        await Open(afterRestore);
        check((await Tick(id, afterRestore, 0)).totalSeconds == 85, "a fresh opening works after stale Redis snapshot recovery");
        var anonymousBeforeLoss = Guid.NewGuid();
        await Open(anonymousBeforeLoss, 10);
        await redis.KeyDeleteAsync(PlayerKey(id));
        await Reject(() => Tick(id, anonymousBeforeLoss, 10), HttpStatusCode.Gone, "an unbound opening cannot backfill unknown older coverage after Redis player state loss");
        check(await Total(id) == 85, "discarding unknown earlier coverage preserves the saved total");
        await Reject(() => Open(Guid.NewGuid(), RedisAppActivity.MaxElapsedSeconds + 1), HttpStatusCode.Gone, "sessions beyond the supported lifetime request a new Unity session");
        await Reject(() => Open(Guid.Empty), HttpStatusCode.BadRequest, "empty session identifiers are rejected");
        await Reject(() => Open(Guid.NewGuid(), -1), HttpStatusCode.BadRequest, "negative elapsed time is rejected");

        // Force SQL to fail after Redis prepared the checkpoint, then let a different
        // session recover it. This exercises the actual cross-store failure boundary.
        var recoveryPlayer = await Player();
        var pendingSession = Guid.NewGuid();
        await Open(pendingSession);
        clock.Now = clock.Now.AddSeconds(10);
        await using (var db = new PickleVerseContext(options))
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION fail_activity_checkpoint() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN IF NEW.timeplayedseconds<>OLD.timeplayedseconds
                THEN RAISE EXCEPTION 'activity checkpoint injected failure'; END IF; RETURN NEW; END $$;
                CREATE TRIGGER fail_activity_checkpoint BEFORE UPDATE ON playerprofiles FOR EACH ROW EXECUTE FUNCTION fail_activity_checkpoint();
                """);
        bool failed = false;
        try { await Tick(recoveryPlayer, pendingSession, 10); }
        catch (PostgresException error) when (error.SqlState == "P0001") { failed = true; }
        finally
        {
            await using var db = new PickleVerseContext(options);
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_activity_checkpoint ON playerprofiles; DROP FUNCTION fail_activity_checkpoint();");
        }
        var projected = GameJson.Read<RedisAppActivity.PlayerState>((await redis.StringGetAsync(PlayerKey(recoveryPlayer))).ToString());
        check(failed && await Total(recoveryPlayer) == 0 && projected.expectedTotal == 0 && projected.targetTotal == 10, "SQL rollback retains a recoverable checkpoint journal only in Redis");
        var nextSession = Guid.NewGuid();
        await Open(nextSession);
        clock.Now = clock.Now.AddSeconds(5);
        check((await Tick(recoveryPlayer, nextSession, 5)).totalSeconds == 15, "a different session recovers pending credit and adds only its uncovered time");
        check((await Tick(recoveryPlayer, pendingSession, 10)).totalSeconds == 15 && await Total(recoveryPlayer) == 15, "retrying after SQL commit or response loss does not credit the recovered interval twice");
        var snapshot = await Call(s => s.Read(recoveryPlayer));
        check(snapshot.player.activeSeconds == 15, "the existing Unity state DTO reads the total directly from PlayerProfiles");

        var raceSession = Guid.NewGuid();
        await Open(raceSession);
        clock.Now = clock.Now.AddSeconds(10);
        async Task<bool> Claim(Guid player)
        {
            try { await Tick(player, raceSession, 10); return true; }
            catch (AppException error) when (error.StatusCode == HttpStatusCode.Forbidden) { return false; }
        }
        var claims = await Task.WhenAll(Claim(id), Claim(another));
        check(claims.Count(x => x) == 1, "concurrent claims by different profiles bind an anonymous session to exactly one account");
        var old = Guid.NewGuid();
        await Open(old);
        clock.Now = clock.Now.AddSeconds(RedisAppActivity.MaxElapsedSeconds + 1);
        await Reject(() => Tick(id, old, 1), HttpStatusCode.Gone, "an aged session expires even if the client reports a smaller elapsed time");
    }
}
