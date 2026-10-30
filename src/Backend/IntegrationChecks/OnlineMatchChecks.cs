using System.Net;
using Microsoft.EntityFrameworkCore;
using PickleVerse;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using Repositories.Context;
using Services.Exceptions;
using Services.Game;
using static Services.Game.GameSql;

static class OnlineMatchChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, TimeProvider clock, Action<bool, string> check, Guid a)
    {
        async Task<T> Call<T>(Func<OnlineGameService, Task<T>> action)
        {
            await using var context = new PickleVerseContext(options);
            var sql = new GameSql(context);
            return await action(new OnlineGameService(context, sql, new GameCatalogService(sql, TestSettings.Load()), clock, TestSettings.Load(), TestRedis.State, TestRedis.Leaderboard, new MatchReplayService(context, sql, TestReplayStorage.Shared, Microsoft.Extensions.Logging.Abstractions.NullLogger<MatchReplayService>.Instance)));
        }
        async Task Reject(Func<Task> call, string name, HttpStatusCode status = HttpStatusCode.Conflict)
        {
            bool rejected = false;
            try
            {
                await call();
            }
            catch (AppException ex)
            {
                if (ex.StatusCode != status)
                    throw new Exception(name + ": expected " + status + ", received " + ex.StatusCode + ": " + ex.Message, ex);
                rejected = true;
            }
            check(rejected, name);
        }
        async Task<Guid> Player(string name)
        {
            await using var context = new PickleVerseContext(options);
            var sql = new GameSql(context);
            var id = Guid.NewGuid();
            await sql.Run("INSERT INTO users(userid,email,passwordhash,roleid) SELECT @id,@email,'disabled-test-account',roleid FROM roles WHERE rolename='Player'", P("id", id), P("email", name + "@example.invalid"));
            await sql.Run("INSERT INTO playerprofiles(userid,playername) VALUES(@id,@name)", P("id", id), P("name", name));
            return id;
        }
        var b = await Player("OnlineOpponent");
        var outsider = await Player("OnlineOutsider");
        OnlineMatchRequest Request(bool custom = false) => new()
        {
            HostPlayerId = a.ToString("N"),
            Session = "PKV-" + Guid.NewGuid().ToString("N"),
            Roster = new MatchRoster
            {
                matchId = Guid.NewGuid().ToString("N"),
                mode = PlayMode.Singles,
                custom = custom,
                region = "asia",
                venue = CourtVenue.Professional,
                members = [
                new RoomMember{id = a.ToString("N"), name = "FORGED", team = 0, elo = 99999, paddleId = "unowned"},
                new RoomMember{id = b.ToString("N"), name = "FORGED", team = 1, elo = 99999}]
            }
        };
        var beforeA = await Call(s => s.Read(a));
        var beforeB = await Call(s => s.Read(b));
        var request = Request();
        var first = await Call(s => s.PrepareOnline(a, request));
        check(!first.allAccepted && first.roster.Find(a.ToString("N")).name == beforeA.player.playerName && first.roster.Find(a.ToString("N")).elo == beforeA.player.elo
            && first.roster.Find(a.ToString("N")).paddleId == beforeA.inventory.equippedPaddle, "online roster uses database names, Elo and equipped paddles");
        await Reject(() => Call(s => s.StartOnline(a, request)), "court cannot start before every account accepts");
        var accepted = await Call(s => s.PrepareOnline(b, request));
        check(accepted.allAccepted, "each authenticated participant accepts their own seat");
        await Reject(() => Call(s => s.StartOnline(outsider, request)), "outsider cannot start a known match", HttpStatusCode.BadRequest);
        var changed = GameJson.Read<OnlineMatchRequest>(GameJson.Write(request));
        changed.Session = "other-room";
        await Reject(() => Call(s => s.PrepareOnline(a, changed)), "consent cannot be reused in another Photon room");
        var started = await Task.WhenAll(Call(s => s.StartOnline(a, request)), Call(s => s.StartOnline(b, request)));
        check(started.All(t => t.phase == "PLAYING"), "concurrent start requests create one playing match");
        var pendingA = await Call(s => s.Read(a));
        var pendingB = await Call(s => s.Read(b));
        check(pendingA.player.pendingMatch.id == pendingB.player.pendingMatch.id && pendingA.player.pendingMatch.startedUtcTicks == clock.GetUtcNow().Ticks, "both profiles share server match identity and start time");
        await Reject(() => Call(s => s.StartBot(a, new MatchStartRequest { RequestId = Guid.NewGuid().ToString("N") })), "bot start cannot overwrite an online match");
        await Reject(() => Call(s => s.StartTraining(a, new StartTrainingRequest { RequestId = Guid.NewGuid().ToString("N"), LessonCode = "serve" })), "training cannot run while an online match is active");
        MatchFinishRequest Result(Guid player, bool won)
        {
            var r = GameJson.Read<MatchResult>(GameJson.Write(player == a ? pendingA.player.pendingMatch : pendingB.player.pendingMatch));
            r.outcome = won ? MatchOutcome.Won : MatchOutcome.Lost;
            r.gamesWon = won ? 2 : 0;
            r.gamesLost = won ? 0 : 2;
            r.scores = won ? [new(11, 5, true), new(11, 8, true)] : [new(5, 11, true), new(8, 11, true)];
            r.points = won ? 22 : 13;
            r.validReturns = 12;
            r.validServes = 4;
            r.ownElo = 999999;
            r.eloDelta = 999999;
            return new MatchFinishRequest { RequestId = Guid.NewGuid().ToString("N"), Result = r };
        }
        var win = Result(a, true);
        var loss = Result(b, false);
        var waiting = await Call(s => s.ReportOnline(a, win));
        check(waiting.player.pendingMatch != null && waiting.player.coins == beforeA.player.coins, "one player's report cannot grant online rewards");
        var conflict = Result(b, true);
        await Reject(() => Call(s => s.ReportOnline(b, conflict)), "conflicting winners do not grant rewards");
        await Reject(() => Call(s => s.ReportOnline(outsider, loss)), "report must belong to authenticated participant", HttpStatusCode.Forbidden);
        await Task.WhenAll(Call(s => s.ReportOnline(b, loss)), Call(s => s.ReportOnline(b, loss)));
        var afterA = await Call(s => s.Read(a));
        var afterB = await Call(s => s.Read(b));
        check(afterA.player.pendingMatch == null && afterB.player.pendingMatch == null && afterA.player.coins == beforeA.player.coins + 150 && afterB.player.coins == beforeB.player.coins + 75,
            "matching reports atomically pay original win/loss rewards to both accounts once");
        check(afterA.player.elo == beforeA.player.elo + EloPolicy.Delta(beforeA.player.elo, beforeB.player.elo, true, beforeA.player.rankWinStreak, beforeA.player.rankLossStreak)
            && afterB.player.elo == beforeB.player.elo + EloPolicy.Delta(beforeB.player.elo, beforeA.player.elo, false, beforeB.player.rankWinStreak, beforeB.player.rankLossStreak), "online Elo is computed from server rating snapshots, including the minimum rating");
        var retry = await Call(s => s.ReportOnline(a, win));
        check(retry.player.coins == afterA.player.coins && retry.player.wins == afterA.player.wins, "completed online result retry cannot grant rewards twice");
        win.Result.validServes++;
        await Reject(() => Call(s => s.ReportOnline(a, win)), "completed online statistics cannot be changed on retry");
        await using (var context = new PickleVerseContext(options))
        {
            var sql = new GameSql(context);
            var mid = Guid.Parse(request.Roster.matchId);
            check(await sql.One("SELECT count(*) FROM playermatchhistory WHERE matchid=@match", r => r.GetInt64(0), P("match", mid)) == 2
                && await sql.One("SELECT isteama FROM playermatchhistory WHERE matchid=@match AND playerid=@player", r => r.GetBoolean(0), P("match", mid), P("player", a))
                && !await sql.One("SELECT isteama FROM playermatchhistory WHERE matchid=@match AND playerid=@player", r => r.GetBoolean(0), P("match", mid), P("player", b)), "normalized online history retains both team identities");
        }
        var forfeit = Request();
        await Call(s => s.PrepareOnline(a, forfeit));
        await Call(s => s.PrepareOnline(b, forfeit));
        await Call(s => s.StartOnline(a, forfeit));
        await Reject(() => Call(s => s.LeaveOnline(outsider, new OnlineMatchCommand { MatchId = forfeit.Roster.matchId })), "outsider cannot forfeit someone else's match", HttpStatusCode.Forbidden);
        var leave = new OnlineMatchCommand { MatchId = forfeit.Roster.matchId, RequestId = Guid.NewGuid().ToString("N") };
        var forfeited = await Call(s => s.LeaveOnline(a, leave));
        var forfeitedRetry = await Call(s => s.LeaveOnline(a, leave));
        check(forfeited.player.history[0].localForfeit && forfeited.player.coins == afterA.player.coins && forfeitedRetry.player.losses == forfeited.player.losses, "authenticated leave forfeits only once and gives the leaver no coins");
        var custom = Request(true);
        await Call(s => s.PrepareOnline(a, custom));
        await Call(s => s.PrepareOnline(b, custom));
        await Call(s => s.StartOnline(a, custom));
        await using (var context = new PickleVerseContext(options))
            await new GameSql(context).Run("UPDATE gamematchplayers SET lastseenat=@stale WHERE matchid=@match", P("stale", clock.GetUtcNow().UtcDateTime.AddMinutes(-1)), P("match", Guid.Parse(custom.Roster.matchId)));
        var recovered = await Call(s => s.Read(a));
        check(recovered.player.pendingMatch == null && recovered.player.history[0].outcome == MatchOutcome.Interrupted && recovered.player.coins == forfeited.player.coins, "both disconnected accounts reconcile as interrupted without reward on next read");
    }
}
