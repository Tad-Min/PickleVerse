using System.Net;
using Microsoft.EntityFrameworkCore;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using Repositories.Context;
using Services.Exceptions;
using Services.Game;
using static Services.Game.GameSql;

static class ActivityRankedChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, TestClock clock, Action<bool, string> check, Guid id)
    {
        async Task<T> Call<T>(Func<OnlineGameService, Task<T>> action)
        {
            await using var db = new PickleVerseContext(options);
            var sql = new GameSql(db);
            return await action(new OnlineGameService(db, sql, new GameCatalogService(sql, TestSettings.Load()), clock, TestSettings.Load(), TestRedis.State, TestRedis.Leaderboard, new MatchReplayService(db, sql, TestReplayStorage.Shared, Microsoft.Extensions.Logging.Abstractions.NullLogger<MatchReplayService>.Instance)));
        }
        async Task Reject(Func<Task> action, string name, HttpStatusCode code = HttpStatusCode.Conflict)
        {
            bool rejected = false;
            try
            {
                await action();
            }
            catch (AppException error)
            {
                if (error.StatusCode != code)
                    throw;
                rejected = true;
            }
            check(rejected, name);
        }
        await using (var db = new PickleVerseContext(options))
        {
            var bots = await db.Users.Include(u => u.Role).Include(u => u.Playerprofile).ToListAsync();
            check(RankedBotAccounts.Ids.Count == 10 && RankedBotAccounts.Ids.Distinct().Count() == 10 &&
                RankedBotAccounts.Ids.All(key => bots.Any(u => u.Userid == Guid.Parse(key) && u.Role.Rolename == "Player" && u.Playerprofile != null)), "all ten Unity virtual UUIDs exist with Player roles and profiles");
        }
        var sid = Guid.NewGuid();
        await Call(s => s.OpenActivity(new() { SessionId = sid }));
        clock.Now = clock.Now.AddSeconds(30);
        var receipt = await Call(s => s.Activity(id, new() { SessionId = sid, ElapsedSeconds = 30 }));
        check(receipt.totalSeconds == 30, "activity includes app time before login and without any match");
        var duplicates = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Call(s => s.Activity(id, new() { SessionId = sid, ElapsedSeconds = 30 }))));
        check(duplicates.All(r => r.totalSeconds == 30), "concurrent activity retries do not double count");
        receipt = await Call(s => s.Activity(id, new() { SessionId = sid, ElapsedSeconds = 9999 }));
        check(receipt.totalSeconds == 30, "activity checkpoints cannot jump ahead of server wall time");
        clock.Now = clock.Now.AddSeconds(40);
        receipt = await Call(s => s.Activity(id, new() { SessionId = sid, ElapsedSeconds = 70 }));
        check(receipt.totalSeconds == 70, "menu, training and waiting time share one continuous app clock");
        var other = Guid.NewGuid();
        await Call(s => s.OpenActivity(new() { SessionId = other, ElapsedSeconds = 70 }));
        receipt = await Call(s => s.Activity(id, new() { SessionId = other, ElapsedSeconds = 70 }));
        check(receipt.totalSeconds == 70, "overlapping app instances credit time only once");
        await Call(s => s.Activity(id, new() { SessionId = sid, ElapsedSeconds = 70, Close = true }));
        clock.Now = clock.Now.AddSeconds(10);
        receipt = await Call(s => s.Activity(id, new() { SessionId = sid, ElapsedSeconds = 80, Close = true }));
        check(receipt.totalSeconds == 70, "closed app sessions stop counting and final checkpoints are idempotent");
        var delayed = Guid.NewGuid();
        var foreground = Guid.NewGuid();
        await Call(s => s.OpenActivity(new() { SessionId = delayed, ElapsedSeconds = 10 }));
        await Call(s => s.OpenActivity(new() { SessionId = foreground }));
        clock.Now = clock.Now.AddSeconds(10);
        await Call(s => s.Activity(id, new() { SessionId = foreground, ElapsedSeconds = 10 }));
        clock.Now = clock.Now.AddSeconds(10);
        receipt = await Call(s => s.Activity(id, new() { SessionId = delayed, ElapsedSeconds = 30, Close = true }));
        check(receipt.totalSeconds == 100, "late login credits earlier uncovered app time without duplicating overlapping sessions");
        await Reject(() => Call(s => s.Activity(Guid.Parse(RankedBotAccounts.Ids[0]), new() { SessionId = sid, ElapsedSeconds = 80 })), "an app activity session cannot switch accounts", HttpStatusCode.Forbidden);
        await Reject(() => Call(s => s.StartBot(id, new() { RequestId = Guid.NewGuid().ToString("N"), Kind = MatchKind.RankedBot })), "ranked bot cannot start through the PvE endpoint", HttpStatusCode.BadRequest);

        var search = Guid.NewGuid();
        var ticket = await Call(s => s.BeginRankedSearch(id, new() { SearchId = search }));
        check(ticket.remainingSeconds == 360, "rank search starts a server-owned six-minute deadline");
        var assignment = new RankedBotRequest { SearchId = search, MatchId = Guid.NewGuid(), Session = "PKV-fallback-check" };
        clock.Now = clock.Now.AddSeconds(359);
        await Reject(() => Call(s => s.AssignRankedBot(id, assignment)), "bot assignment is rejected at 359 seconds");
        clock.Now = clock.Now.AddSeconds(1);
        var bot = await Call(s => s.AssignRankedBot(id, assignment));
        var repeated = await Call(s => s.AssignRankedBot(id, assignment));
        check(bot.bot && RankedBotAccounts.Contains(bot.id) && bot.id == repeated.id, "at 360 seconds one valid virtual account is assigned idempotently");
        OnlineMatchRequest Request(RankedBotRequest a, RoomMember member) => new()
        {
            SearchId = a.SearchId.ToString("N"),
            Session = a.Session,
            HostPlayerId = id.ToString("N"),
            Roster = new() { matchId = a.MatchId.ToString("N"), mode = PlayMode.Singles, region = "asia", members = [new() { id = id.ToString("N"), name = "forged", team = 0 }, member] }
        };
        var cancelled = Request(assignment, bot);
        await Call(s => s.CancelRankedSearch(id, new() { SearchId = search }));
        await Reject(() => Call(s => s.PrepareOnline(id, cancelled)), "cancelling at the deadline prevents a late bot match");
        search = Guid.NewGuid();
        await Call(s => s.BeginRankedSearch(id, new() { SearchId = search }));
        clock.Now = clock.Now.AddSeconds(360);
        assignment = new() { SearchId = search, MatchId = Guid.NewGuid(), Session = "PKV-real-online-fallback" };
        bot = await Call(s => s.AssignRankedBot(id, assignment));
        var request = Request(assignment, bot);
        var forged = GameJson.Read<OnlineMatchRequest>(GameJson.Write(request));
        forged.Roster.members[1].bot = false;
        await Reject(() => Call(s => s.PrepareOnline(id, forged)), "virtual IDs cannot masquerade as human seats", HttpStatusCode.BadRequest);
        var prepared = await Call(s => s.PrepareOnline(id, request));
        check(prepared.allAccepted && prepared.roster.RankedBot, "server accepts only the assigned virtual seat while the human confirms normally");
        await Call(s => s.StartOnline(id, request));
        var pending = await Call(s => s.Read(id));
        check(pending.player.pendingMatch.kind == MatchKind.RankedSingles && pending.player.pendingMatch.opponents[0].id == bot.id, "fallback uses Rank Online with a database opponent UUID");
        await using (var db = new PickleVerseContext(options))
            await new GameSql(db).Run("UPDATE gamematchplayers SET lastseenat=@stale WHERE matchid=@match AND playerid=@bot", P("stale", clock.Now.UtcDateTime.AddMinutes(-1)), P("match", assignment.MatchId), P("bot", Guid.Parse(bot.id)));
        var heartbeat = await Call(s => s.HeartbeatOnline(id, new() { MatchId = request.Roster.matchId }));
        check(heartbeat.player.pendingMatch != null, "virtual opponents do not require a fabricated JWT or client heartbeat");
        var result = GameJson.Read<MatchResult>(GameJson.Write(pending.player.pendingMatch));
        result.outcome = MatchOutcome.Won;
        result.gamesWon = 2;
        result.gamesLost = 0;
        result.scores = [new(11, 5, true), new(11, 8, true)];
        result.points = 22;
        result.validReturns = 12;
        result.validServes = 4;
        var finish = new MatchFinishRequest { RequestId = Guid.NewGuid().ToString("N"), Result = result };
        var saved = await Call(s => s.ReportOnline(id, finish));
        check(saved.player.pendingMatch == null && saved.player.history[0].eloDelta == EloPolicy.Delta(result.ownElo, result.opponentElo, true, 0, 0) && saved.player.coins == 150, "ranked fallback finishes with online rewards and server Elo");
        var retry = await Call(s => s.ReportOnline(id, finish));
        check(retry.player.elo == saved.player.elo && retry.player.coins == saved.player.coins && retry.player.wins == saved.player.wins, "retrying fallback results cannot duplicate Elo or rewards");
        check(saved.player.activeSeconds == 100, "finishing a match no longer adds match duration to app activity");
        await using (var db = new PickleVerseContext(options))
        {
            var sql = new GameSql(db);
            check(await sql.One("SELECT count(*) FROM playermatchhistory WHERE matchid=@match", r => r.GetInt64(0), P("match", assignment.MatchId)) == 2 &&
                await sql.One("SELECT count(*) FROM gamematchplayers WHERE matchid=@match AND accepted AND report IS NOT NULL", r => r.GetInt64(0), P("match", assignment.MatchId)) == 2, "fallback history and online result retain both valid player identities");
        }
    }
}
