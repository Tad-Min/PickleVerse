using System.Net;
using Microsoft.EntityFrameworkCore;
using PickleVerse.Progression;
using Repositories.Context;
using Entities.Exceptions;
using Services.Implementations;
using PickleVerse.Replays;
using System.IO.Compression;
using Services.Interfaces;

static class GameChecks
{
    public static async Task RunAsync(DbContextOptions<PickleVerseContext> options, TimeProvider clock,
        Action<bool, string> check, string root)
    {
        async Task<T> Call<T>(Func<OnlineGameService, Task<T>> action)
        {
            await using var context = new PickleVerseContext(options);
            var sql = new PostgresCommands(context);var sqlData = new UnitOfWork(context);
            return await action(new OnlineGameService(sqlData, new GameCatalogService(sqlData, TestSettings.Load()), clock, TestSettings.Load(), TestRedis.State, TestRedis.Leaderboard, new MatchReplayService(sqlData, TestReplayStorage.Shared, Microsoft.Extensions.Logging.Abstractions.NullLogger<MatchReplayService>.Instance)));
        }
        async Task Rejected(Func<Task> action, string name, HttpStatusCode status = HttpStatusCode.BadRequest)
        {
            bool rejected = false;
            try
            {
                await action();
            }
            catch (AppException ex)
            {
                rejected = ex.StatusCode == status;
            }
            check(rejected, name);
        }
        Guid id;
        await using (var context = new PickleVerseContext(options))
        {
            id = await context.Users.Where(u => u.Email == "race-check@example.invalid").Select(u => u.Userid).SingleAsync();
            string script = await File.ReadAllTextAsync(Path.Combine(root, "src", "Database.sql"));
            var sql = new PostgresCommands(context);var sqlData = new UnitOfWork(context);
            const string seedStart = "-- BEGIN INITIAL DATA", seedEnd = "-- END INITIAL DATA";
            int start = script.IndexOf(seedStart, StringComparison.Ordinal), end = script.IndexOf(seedEnd, StringComparison.Ordinal);
            if (start < 0 || end <= start)
                throw new InvalidOperationException("Database.sql must contain one complete initial data section.");
            await sql.Run("BEGIN;\n" + script[start..end] + "\nCOMMIT;");
            var catalog = await new GameCatalogService(sqlData, TestSettings.Load()).Load();
            check(catalog.definitions.Count(d => d.scope == ProgressScope.Achievement) == 10 && catalog.definitions.Count(d => d.scope != ProgressScope.Achievement) == 17,
                "Unity seed is repeatable: 10 achievements and 17 missions");
            check(catalog.lessons.Length == 12 && catalog.lessons.Count(l => l.prerequisites.Length == 0) == 4,
                "online catalog has every Unity lesson and the original four entry lessons");
            check(catalog.lessons.Single(l => l.id == "serve").title == "Giao bóng đúng ô"
                && catalog.lessons.Single(l => l.id == "drive").title == "Đánh bạt về cuối sân"
                && catalog.definitions.Single(d => d.id == "D.Serve10").title == "Giao bóng hợp lệ 10 lần"
                && catalog.definitions.Single(d => d.id == "A.Win5").title == "Thắng 5 trận",
                "UTF-8 catalog text survives SQL initialization and PostgreSQL reads");
            check(catalog.cosmetics.Length == 3 && catalog.cosmetics.Single(c => c.id == "paddle.thot").tokenpadPrice == 900
                && catalog.cosmetics.Single(c => c.id == "paddle.thot").elitePrice == 250 && catalog.cosmetics.Single(c => c.id == "paddle.gemstone").elitePrice == 650,
                "online cosmetics retain both original currency prices");
            check(catalog.lessons.Single(l => l.id == "doubles-transition").prerequisites.SequenceEqual(new[] { "approach", "doubles-cover", "reset" }),
                "advanced training prerequisites match the Unity prototype");
        }
        var state = await Call(s => s.Read(id));
        check(state.player.id == id.ToString("N") && state.player.online && state.revision > 0,
            "online game state initializes and persists for the authenticated player");
        check(state.inventory.owned.SequenceEqual(new[] { "paddle.default" }) && state.player.coins == 0 && state.player.gems == 0,
            "new account owns only its default paddle with no imported local money");
        check(GameJson.Read<GameSnapshot>(GameJson.Write(state)).catalog.lessons.Length == 12,
            "snapshot JSON preserves nested Unity fields and enum names");
        check(GameJson.Read<GameSnapshot>(GameJson.Write(state)).catalog.lessons.Single(l => l.id == "serve").title == "Giao bóng đúng ô",
            "snapshot JSON preserves Vietnamese diacritics end to end");
        check(GameJson.Payload(new { team = 0, winner = true }) != GameJson.Payload(new { team = 1, winner = false }),
            "fingerprints include get-only values rather than collapsing distinct payloads");
        await Rejected(() => Call(s => s.Claim(id, new GameCommand { RequestId = Guid.NewGuid().ToString("N"), Code = "A.Win5" })),
            "unearned achievement cannot be claimed");
        await Rejected(() => Call(s => s.Purchase(id, new GameCommand { RequestId = Guid.NewGuid().ToString("N"), Code = "paddle.thot" })),
            "purchase rejects insufficient server balance");
        await Rejected(() => Call(s => s.StartTraining(id, new StartTrainingRequest { RequestId = Guid.NewGuid().ToString("N"), LessonCode = "modifiers" })),
            "server rejects a locked lesson even when the client requests it directly");

        async Task<GameSnapshot> Complete(string code, bool retry = false)
        {
            var started = await Call(s => s.StartTraining(id, new StartTrainingRequest { RequestId = Guid.NewGuid().ToString("N"), LessonCode = code, Retry = retry }));
            var attempt = started.player.training.active;
            var lesson = started.catalog.lessons.Single(l => l.id == code);
            attempt.trials = attempt.successes = attempt.contacts = lesson.trials;
            attempt.timingTotal = lesson.trials * .1;
            attempt.seconds = 1;
            attempt.rewardTokenpad = 999999;
            attempt.rewardXp = 999999;
            attempt.stars = 999;
            var command = new TrainingUpdateRequest { RequestId = Guid.NewGuid().ToString("N"), Attempt = attempt };
            var finished = await Call(s => s.UpdateTraining(id, command, true));
            long balance = finished.player.coins;
            var duplicate = await Call(s => s.UpdateTraining(id, command, true));
            check(duplicate.player.coins == balance && duplicate.revision == finished.revision,
                "repeated completion does not award twice: " + code);
            if (!retry)
                check(finished.player.training.active.rewardTokenpad == lesson.reward && finished.player.training.active.stars == 3,
                                "server calculates original rewards and stars, ignoring client values: " + code);
            return finished;
        }
        var passed = await Complete("serve");
        check(passed.player.coins == 150 && passed.player.levelXp == 5, "first successful serve lesson grants 150 TOKENPAD and 5 XP");
        var reviewed = await Complete("serve", true);
        check(reviewed.player.coins == 150 && reviewed.player.levelXp == 5 && reviewed.player.training.active.rewardTokenpad == 0,
            "reviewing a passed lesson preserves progress without repeat rewards");
        foreach (var code in new[] { "drive", "dink", "volley" })
            state = await Complete(code);
        var advanced = await Call(s => s.StartTraining(id, new StartTrainingRequest { RequestId = Guid.NewGuid().ToString("N"), LessonCode = "modifiers" }));
        check(advanced.player.training.active.lessonId == "modifiers", "completing prerequisites opens the next lesson online");
        var invalid = GameJson.Read<TrainingAttempt>(GameJson.Write(advanced.player.training.active));
        invalid.trials = 10;
        invalid.successes = 11;
        await Rejected(() => Call(s => s.UpdateTraining(id, new TrainingUpdateRequest { RequestId = Guid.NewGuid().ToString("N"), Attempt = invalid }, true)),
            "server rejects impossible training measurements");
        var expiredKey = new StartTrainingRequest { RequestId = Guid.NewGuid().ToString("N"), LessonCode = "serve" };
        await Call(s => s.StartTraining(id, expiredKey));
        expiredKey.LessonCode = "drive";
        await Rejected(() => Call(s => s.StartTraining(id, expiredKey)), "idempotency key cannot be reused for another lesson", HttpStatusCode.Conflict);

        // Test funding is confined to the disposable database; no public credit endpoint exists.
        await using (var context = new PickleVerseContext(options))
            await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE playercurrencies SET amount=1100 WHERE playerid={id} AND currencyid=(SELECT currencyid FROM currencies WHERE currencyname='TOKENPAD')");
        var purchase = new GameCommand { RequestId = Guid.NewGuid().ToString("N"), Code = "paddle.thot" };
        var racing = await Task.WhenAll(Call(s => s.Purchase(id, purchase)), Call(s => s.Purchase(id, purchase)));
        check(racing.All(s => s.player.coins == 200 && s.inventory.owned.Contains("paddle.thot")),
            "simultaneous purchase retries charge once and return the online inventory");
        await Call(s => s.Equip(id, new GameCommand { RequestId = Guid.NewGuid().ToString("N"), Code = "paddle.thot" }));
        state = await Call(s => s.Read(id));
        check(state.inventory.equippedPaddle == "paddle.thot" && state.player.coins == 200,
            "equipped paddle and remaining wallet survive a fresh database context");
        await using (var context = new PickleVerseContext(options))
        {
            var sql = new PostgresCommands(context);var sqlData = new UnitOfWork(context);
            var count = await sql.One("SELECT count(*) FROM currencytransactions WHERE playerid=@id AND commandid=@command", r => r.GetInt64(0), PostgresCommands.P("id", id), PostgresCommands.P("command", purchase.RequestId));
            check(count == 1, "currency ledger records one transaction for the concurrent purchase");
            check(await context.Playerlessondrills.CountAsync(x => x.Playerid == id && x.Score > 0) == 4,
                "training results also populate normalized lesson progress");
        }
        var beforeMatch = await Call(s => s.Read(id));
        var matchState = await Call(s => s.StartBot(id, new MatchStartRequest { RequestId = Guid.NewGuid().ToString("N"), Kind = MatchKind.BotPractice }));
        var result = GameJson.Read<MatchResult>(GameJson.Write(matchState.player.pendingMatch));
        check(Guid.TryParse(result.id, out _) && result.ownElo == beforeMatch.player.elo,
            "backend allocates bot match identity and rating snapshot");
        var opponent = result.opponents.Single();
        check(PickleVerse.Multiplayer.RankedBotAccounts.Contains(opponent.id), "practice opponent uses an existing seeded bot profile");
        var outsider = Guid.NewGuid();
        await using (var context = new PickleVerseContext(options))
        {
            var sql = new PostgresCommands(context);var sqlData = new UnitOfWork(context);
            var seats = await sql.Query("SELECT p.playerid,p.team,u.playername FROM gamematchplayers p JOIN playerprofiles u ON u.userid=p.playerid WHERE p.matchid=@match",
                r => (id: r.GetGuid(0), team: r.GetInt16(1), name: r.GetString(2)), PostgresCommands.P("match", Guid.Parse(result.id)));
            check(seats.Count == 2 && seats.Any(p => p.id == id && p.team == 0)
                && seats.Any(p => p.id == Guid.Parse(opponent.id) && p.team == 1 && p.name == opponent.name),
                "practice stores the human and named bot in GameMatchPlayers with opposite teams");
            await sql.Run("INSERT INTO users(userid,email,passwordhash,roleid) SELECT @id,@email,'disabled',roleid FROM roles WHERE rolename='Player'",
                PostgresCommands.P("id", outsider), PostgresCommands.P("email", outsider+"@example.invalid"));
            await sql.Run("INSERT INTO playerprofiles(userid,playername) VALUES(@id,@name)", PostgresCommands.P("id", outsider), PostgresCommands.P("name", "Outside"+outsider.ToString("N")[..8]));
        }
        result.outcome = MatchOutcome.Won;
        result.gamesWon = 2;
        result.scores = [new GameScore(11, 5, true), new GameScore(11, 8, true)];
        result.points = 22;
        result.validServes = 12;
        result.validReturns = 20;
        result.ownElo = 999999;
        result.opponentElo = 999999;
        result.eloDelta = 999999;
        var finish = new MatchFinishRequest { RequestId = Guid.NewGuid().ToString("N"), Result = result };
        await Rejected(() => Call(s => s.FinishBot(outsider, finish)), "outsider cannot submit a known practice match without participant membership");
        await Rejected(() => Call(s => s.FinishBot(Guid.Parse(opponent.id), finish)), "virtual bot cannot submit the human's practice result");
        var finishedMatch = await Call(s => s.FinishBot(id, finish));
        var replayedMatch = await Call(s => s.FinishBot(id, finish));
        check(finishedMatch.player.coins == beforeMatch.player.coins + 100 && finishedMatch.player.wins == beforeMatch.player.wins + 1
            && finishedMatch.player.elo == beforeMatch.player.elo && finishedMatch.player.levelXp == beforeMatch.player.levelXp + 10,
            "finished bot match grants original coins and XP using server rating data");
        check(replayedMatch.player.coins == finishedMatch.player.coins && replayedMatch.player.wins == finishedMatch.player.wins,
            "repeated match result never grants duplicate counters or rewards");
        finish.RequestId = Guid.NewGuid().ToString("N");
        var anotherRequest = await Call(s => s.FinishBot(id, finish));
        check(anotherRequest.player.coins == finishedMatch.player.coins, "a new command ID cannot replay an already finished match reward");
        result.scores[0].own = 99;
        finish.RequestId = Guid.NewGuid().ToString("N");
        await Rejected(() => Call(s => s.FinishBot(id, finish)), "finished match cannot be overwritten by a conflicting result", HttpStatusCode.Conflict);
        var legacy = await Call(s => s.LegacyLessons(id));
        check(legacy.Count == 12 && legacy.Count(l => l.Status == "COMPLETED") == 4, "legacy lesson API uses the complete online prerequisite graph");
        await using (var context = new PickleVerseContext(options))
        {
            var sql = new PostgresCommands(context);var sqlData = new UnitOfWork(context);
            check(await sql.One("SELECT count(*) FROM playermissions WHERE playerid=@id AND periodkey<>''", r => r.GetInt64(0), PostgresCommands.P("id", id)) == 17, "daily and weekly mission state is normalized with its reset period");
        }
        var storage = TestReplayStorage.Shared;
        async Task<T> Replay<T>(Func<MatchReplayService, Task<T>> action)
        {
            await using var context = new PickleVerseContext(options);
            return await action(new MatchReplayService(new UnitOfWork(context), storage, Microsoft.Extensions.Logging.Abstractions.NullLogger<MatchReplayService>.Instance));
        }
        var match = Guid.Parse(result.id);
        var metadata = new ReplayMetadata
        {
            playerId = id.ToString("N"),
            matchId = match.ToString("N"),
            actors = [
            new ReplayActorInfo{id = id.ToString("N"), name = "Check player", team = 0, visualId = "Male", bones = ["Root"]},
            new ReplayActorInfo{id = opponent.id, name = opponent.name, team = 1, visualId = "Female", bones = ["Root"]}]
        };
        await Rejected(() => Replay(s => s.Save(outsider, match, metadata)), "outsider cannot upload a replay for another player's practice match", HttpStatusCode.NotFound);
        await Replay(s => s.Save(id, match, metadata));
        var chunk = new ReplayChunk { frames = [new ReplayFrame { time = 0, ball = [0, 1, 0], actors = [new ReplayActorPose { root = new float[10], bones = new float[10] }, new ReplayActorPose { root = new float[10], bones = new float[10] }] }] };
        ReplayChunkUpload Compressed()
        {
            using var bytes = new MemoryStream();
            using (var gzip = new GZipStream(bytes, CompressionLevel.Fastest, true))
            {
                var raw = System.Text.Encoding.UTF8.GetBytes(GameJson.Write(chunk));
                gzip.Write(raw);
            }
            return new ReplayChunkUpload { Data = Convert.ToBase64String(bytes.ToArray()) };
        }
        var upload = Compressed();
        await Replay(s => s.Upload(id, match, 0, upload));
        await Replay(s => s.Upload(id, match, 0, upload));
        var fetched = await Replay(s => s.Chunk(id, match, 0));
        check(fetched.Data == upload.Data && storage.Objects.Count == 2, "replay header and compressed chunks use storage keys; retry writes once");
        metadata.complete = true;
        metadata.chunks = [];
        var saved = await Replay(s => s.Save(id, match, metadata));
        check(saved.complete && saved.chunks.Count == 1, "replay completion retains the server chunk manifest");
        chunk.frames[0].ball[0] = 10;
        await Rejected(() => Replay(s => s.Upload(id, match, 0, Compressed())), "replay chunk cannot be overwritten by different content");
        await Rejected(() => Replay(s => s.Upload(id, match, 3, upload)), "replay upload rejects gaps in chunk sequence");
        await Rejected(() => Replay(s => s.Read(Guid.NewGuid(), match)), "a different account cannot read replay metadata", HttpStatusCode.NotFound);
        await Rejected(() => Replay(s => s.Chunk(Guid.NewGuid(), match, 0)), "a different account cannot read replay chunks", HttpStatusCode.NotFound);
        check((await Replay(s => s.List(id))).Count == 1, "replay metadata can be restored after a new service session");
        await OnlineMatchChecks.Run(options, clock, check, id);
        await SocialChecks.Run(options, check);
    }

}
