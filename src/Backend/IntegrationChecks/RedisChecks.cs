using System.Net;
using Microsoft.EntityFrameworkCore;
using PickleVerse.Multiplayer;
using Repositories.Context;
using Services.Exceptions;
using Services.Game;
using StackExchange.Redis;
using static Services.Game.GameSql;

static class RedisChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, TestClock clock, Action<bool, string> check)
    {
        async Task<T> Call<T>(Func<OnlineGameService, Task<T>> action)
        {
            await using var db = new PickleVerseContext(options);
            var sql = new GameSql(db);
            return await action(new OnlineGameService(db, sql, new GameCatalogService(sql, TestSettings.Load()), clock, TestSettings.Load(), TestRedis.State, TestRedis.Leaderboard, new MatchReplayService(db, sql, TestReplayStorage.Shared, Microsoft.Extensions.Logging.Abstractions.NullLogger<MatchReplayService>.Instance)));
        }
        async Task<bool> Rejected(Func<Task> action)
        {
            try
            {
                await action();
                return false;
            }
            catch (AppException e) when (e.StatusCode == HttpStatusCode.Conflict)
            {
                return true;
            }
        }
        async Task<Guid> Player(int elo = 100)
        {
            var id = Guid.NewGuid();
            await using var db = new PickleVerseContext(options);
            var sql = new GameSql(db);
            await sql.Run("INSERT INTO users(userid,email,passwordhash,roleid) SELECT @id,@email,'disabled',roleid FROM roles WHERE rolename='Player'", P("id", id), P("email", id + "@example.invalid"));
            await sql.Run("INSERT INTO playerprofiles(userid,playername,elo) VALUES(@id,@name,@elo)", P("id", id), P("name", "Redis" + id.ToString("N")[..10]), P("elo", elo));
            await sql.Run("INSERT INTO verificationemail(userid,email,token,expiresat,isverified) VALUES(@id,@email,'fixture',CURRENT_TIMESTAMP,true)", P("id", id), P("email", id + "@example.invalid"));
            return id;
        }
        var a = await Player();
        var b = await Player();
        var c = await Player();
        var d = await Player();
        RankedSearchRequest Ticket(PlayMode mode = PlayMode.Singles, string region = "asia", string group = "", Guid[]? members = null) => new() { SearchId = Guid.NewGuid(), Mode = mode, Region = region, Protocol = 99, Group = group, Members = members ?? [] };
        var qa = Ticket();
        var qb = Ticket();
        var one = await Call(s => s.BeginRankedSearch(a, qa));
        check(one.state == "SEARCHING" && one.remainingSeconds == 360, "a single player waits in the Redis queue with a server deadline");
        var pair = await Call(s => s.BeginRankedSearch(b, qb));
        var polls = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Call(s => s.PollRankedSearch(a, qa))));
        check(pair.state == "MATCHED" && polls.All(p => p.matchId == pair.matchId && p.session == pair.session) && pair.roster!.IsValid(), "concurrent polling produces one shared match/session and consumes each queue seat once");
        check(!pair.roomReady, "guests wait until the assigned host has created the Photon room");
        var hostId = Guid.Parse(pair.hostPlayerId);
        var hostTicket = hostId == a ? qa : qb;
        var guestId = hostId == a ? b : a;
        var guestTicket = hostId == a ? qb : qa;
        check(await Rejected(() => Call(s => s.PublishRankedRoom(guestId, guestTicket))), "a guest cannot publish the host's room readiness");
        await Call(s => s.PublishRankedRoom(hostId, hostTicket));
        check((await Call(s => s.PollRankedSearch(guestId, guestTicket))).roomReady, "guests can join only after an authenticated host publishes readiness");
        check(await Rejected(() => Call(s => s.AssignRankedBot(a, new() { SearchId = qa.SearchId, MatchId = Guid.NewGuid(), Session = "forged" }))), "a human assignment cannot be replaced by a bot");
        check(await Rejected(() => Call(s => s.PollRankedSearch(c, qa))), "a player cannot read another player's queue ticket");
        OnlineMatchRequest Request(Guid player, RankedSearchRequest q) => new() { SearchId = q.SearchId.ToString("N"), Roster = pair.roster!, HostPlayerId = pair.hostPlayerId, Session = pair.session };
        var requestA = Request(a, qa);
        var requestB = Request(b, qb);
        var offered = await Call(s => s.OfferOnline(a, requestA));
        check(!offered.allAccepted && offered.readyRemainingSeconds == 120, "finding a match opens a Redis ready check without accepting either human");
        var first = await Call(s => s.PrepareOnline(a, requestA));
        check(!first.allAccepted, "only the authenticated player's consent is recorded");
        await using (var db = new PickleVerseContext(options))
            check(await new GameSql(db).One("SELECT count(*) FROM gamematchplayers WHERE matchid=@id AND accepted", r => r.GetInt64(0), P("id", Guid.Parse(pair.matchId))) == 0, "temporary consent is stored in Redis before the SQL match starts");
        check(await Rejected(() => Call(s => s.StartOnline(a, requestA))), "one confirmation cannot start a two-human match");
        var second = await Call(s => s.PrepareOnline(b, requestB));
        check(second.allAccepted, "both humans must confirm their own seats");
        await Call(s => s.StartOnline(a, requestA));
        await using (var db = new PickleVerseContext(options))
            check(await new GameSql(db).One("SELECT count(*) FROM gamematchplayers WHERE matchid=@id AND accepted", r => r.GetInt64(0), P("id", Guid.Parse(pair.matchId))) == 2, "SQL retains the durable accepted roster after Redis permits start");
        await Call(s => s.LeaveOnline(a, new() { MatchId = pair.matchId }));
        check(await TestRedis.Connection.GetDatabase().StringGetAsync(TestRedis.State.Prefix + "player:" + a.ToString("N")) == RedisValue.Null, "finished matches release Redis player reservations");

        qa = Ticket();
        qb = Ticket(region: "eu");
        await Call(s => s.BeginRankedSearch(a, qa));
        check((await Call(s => s.BeginRankedSearch(b, qb))).state == "SEARCHING", "different Photon regions never share an assignment");
        await Call(s => s.CancelRankedSearch(a, qa));
        await Call(s => s.CancelRankedSearch(b, qb));
        check(await Rejected(() => Call(s => s.PollRankedSearch(a, qa))), "cancelled queue tickets cannot be revived by late polls");

        var high = await Player(2000);
        qa = Ticket();
        qb = Ticket();
        await Call(s => s.BeginRankedSearch(a, qa));
        check((await Call(s => s.BeginRankedSearch(high, qb))).state == "SEARCHING", "Redis matchmaking preserves the existing Elo pairing window");
        await Call(s => s.CancelRankedSearch(a, qa));
        await Call(s => s.CancelRankedSearch(high, qb));

        var party = new[] { a, b };
        qa = Ticket(PlayMode.Doubles, group: "duo-proof", members: party);
        qb = Ticket(PlayMode.Doubles, group: "duo-proof", members: party);
        var qc = Ticket(PlayMode.Doubles);
        var qd = Ticket(PlayMode.Doubles);
        await Call(s => s.BeginRankedSearch(a, qa));
        await Call(s => s.BeginRankedSearch(c, qc));
        await Call(s => s.BeginRankedSearch(d, qd));
        check((await Call(s => s.PollRankedSearch(a, qa))).state == "SEARCHING", "a captain cannot enqueue a teammate who has not authenticated their own ticket");
        var doubles = await Call(s => s.BeginRankedSearch(b, qb));
        check(doubles.state == "MATCHED" && doubles.roster!.IsValid() && doubles.roster.Find(a.ToString("N")).team == doubles.roster.Find(b.ToString("N")).team, "a confirmed duo and two solos form one valid four-player assignment without splitting the party");
        await Call(s => s.CancelRankedSearch(a, qa));
        check(await Rejected(() => Call(s => s.PrepareOnline(c, new() { SearchId = qc.SearchId.ToString("N"), Roster = doubles.roster!, Session = doubles.session, HostPlayerId = doubles.hostPlayerId }))), "cancelling an assignment invalidates the ready check for its other participants");
        foreach (var t in new[] { (b, qb), (c, qc), (d, qd) })
            await Call(s => s.CancelRankedSearch(t.Item1, t.Item2));

        await using (var reset = new PickleVerseContext(options))
            await new GameSql(reset).Run("UPDATE playerprofiles SET elo=100 WHERE userid IN (@a,@b)", P("a", a), P("b", b));
        qa = Ticket();
        qb = Ticket();
        await Call(s => s.BeginRankedSearch(a, qa));
        pair = await Call(s => s.BeginRankedSearch(b, qb));
        requestA = Request(a, qa);
        requestB = Request(b, qb);
        await Call(s => s.OfferOnline(a, requestA));
        clock.Now = clock.Now.AddSeconds(121);
        check(await Rejected(() => Call(s => s.PrepareOnline(b, requestB))), "the confirmation deadline begins at offer, before the first acceptance");
        await Call(s => s.CancelRankedSearch(a, qa));
        await Call(s => s.CancelRankedSearch(b, qb));

        await using (var db = new PickleVerseContext(options))
        {
            var sql = new GameSql(db);
            await TestRedis.Leaderboard.Invalidate();
            var cache = TestRedis.Leaderboard;
            var key = await cache.Snapshot(sql);
            var dbRedis = TestRedis.Connection.GetDatabase();
            check(await dbRedis.KeyTypeAsync(key) == RedisType.SortedSet, "leaderboard cache is a real Redis Sorted Set");
            var keyAgain = await cache.Snapshot(sql);
            check(keyAgain == key, "repeated leaderboard reads reuse the cached generation");
            var ranked = await cache.Ranks(key, [c, d]);
            check(ranked[0].id == new[] { c, d }.OrderBy(x => x.ToString("N"), StringComparer.Ordinal).First(), "equal Elo sorts by UUID ascending consistently with PostgreSQL");
            await sql.Run("UPDATE playerprofiles SET elo=5000 WHERE userid=@id", P("id", d));
            await cache.Invalidate();
            var fresh = await cache.Snapshot(sql);
            check(fresh != key && await cache.Rank(fresh, d) == 1, "invalidating the cache rebuilds ranks from authoritative PostgreSQL Elo");
            await dbRedis.KeyDeleteAsync(fresh);
            check(await cache.Snapshot(sql) != fresh, "a lost Sorted Set is rebuilt automatically");
            check(await dbRedis.KeyTimeToLiveAsync(await cache.Snapshot(sql)) > TimeSpan.Zero, "leaderboard snapshots have a bounded lifetime");
            check(await sql.One("SELECT to_regclass('public.rankedsearches') IS NULL", r => r.GetBoolean(0)), "fresh SQL initialization no longer creates RankedSearches");
        }
    }
}
