using System.Net;
using Entities.DTOs;
using Entities.Exceptions;
using Entities.Models;
using Microsoft.EntityFrameworkCore;
using PickleVerse.Progression;
using Repositories.Context;
using Services.Implementations;

static class ProgressResetChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, Action<bool, string> check)
    {
        var fixture = new FunctionalFixture(options); var actor = await fixture.Player("Admin"); var player = await fixture.Player();
        async Task<T> Reset<T>(Func<ProgressResetService, Task<T>> action)
        {
            using var db = new PickleVerseContext(options); using var unit = new UnitOfWork(db);
            return await action(new ProgressResetService(unit, TestRedis.State, TestRedis.Leaderboard, TestSettings.Load(), TimeProvider.System));
        }
        async Task Reject(Func<Task> action, HttpStatusCode expected, string label)
        { bool rejected = false; try { await action(); } catch (AppException e) { rejected = e.StatusCode == expected; } check(rejected, label); }
        var initial = await fixture.Game(s => s.Read(player)); var lesson = initial.catalog.lessons.Single(d => d.id == "serve");
        var attempt = new TrainingAttempt { id = Guid.NewGuid().ToString("N"), lessonId = lesson.id, playerId = initial.player.id,
            catalogVersion = initial.catalog.version, progressEpoch = initial.learningEpoch, trials = lesson.trials, contacts = lesson.trials,
            successes = lesson.trials, seconds = 30, timingTotal = .1 };
        await fixture.Game(s => s.SyncTraining(player, new TrainingSyncRequest { Attempts = [attempt] }));
        Guid batch = Guid.NewGuid(), token = Guid.NewGuid();
        using (var db = new PickleVerseContext(options))
        {
            var profile = await db.Playerprofiles.SingleAsync(p => p.Userid == player); profile.Level = 25; profile.Elo = 2300;
            db.AiTelemetrySessions.Add(new AiTelemetrySession { BatchId = batch, PlayerId = player, SessionId = Guid.Parse(attempt.id), SessionType = "training",
                PartCount = 1, Metadata = "{}", CreatedAt = DateTime.UtcNow.AddHours(-1) });
            db.AiAnalysisJobs.Add(new AiAnalysisJob { BatchId = batch, State = "processing", LeaseToken = token, LeaseUntil = DateTime.UtcNow.AddMinutes(1),
                RuleVersion = "rules-v1", AvailableAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            db.AiCoachingProfiles.Add(new AiCoachingProfile { PlayerId = player, BatchId = batch, Result = "{}", UpdatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        await fixture.Game(s => s.Read(player)); // Grants the real level milestone before reset.
        var selection = new ProgressResetSelection { Groups = [ProgressResetGroup.Learning] };
        var preview = await Reset(s => s.Preview(player, selection));
        check(preview.LessonRecords == 1 && preview.MasteryRecords == 1 && preview.Elo == 2300 && preview.OwnedCosmetics.Contains("paddle.gemstone"),
            "reset preview reports actual learning records, ranking and inventory");
        var request = new ExecuteProgressReset { RequestId = Guid.NewGuid().ToString("N"), Groups = selection.Groups, ExpectedRevision = preview.Revision };
        await Reject(() => Reset(s => s.Execute(player, player, request)), HttpStatusCode.Forbidden, "ordinary player cannot execute admin reset");
        var receipt = await Reset(s => s.Execute(actor, player, request));
        var duplicate = await Reset(s => s.Execute(actor, player, request));
        check(receipt.AuditId == duplicate.AuditId, "lost-response retry returns one reset receipt");
        var state = await fixture.Game(s => s.Read(player));
        check(state.player.level == 1 && state.player.levelXp == 0 && state.learningEpoch == 1 && state.player.training.lessons.Count == 0,
            "learning reset clears XP and curriculum and advances epoch once");
        check(state.player.elo == 2300 && state.player.coins == 150 && state.inventory.owned.Contains("paddle.gemstone"), "learning reset preserves ranking, currency and cosmetics");
        using (var db = new PickleVerseContext(options))
        {
            check(await db.AdminAudits.CountAsync(x => x.TargetId == player) == 1, "reset audit is durable and not duplicated");
            check(!await db.AiCoachingProfiles.AnyAsync(p => p.PlayerId == player) && (await db.AiAnalysisJobs.SingleAsync(j => j.BatchId == batch)).State == "failed",
                "learning reset clears coaching profile and fences pending AI jobs");
            check(await db.TrainingSessionRecords.AnyAsync(s => s.AttemptId == Guid.Parse(attempt.id) && s.Completed), "reset preserves historic session evidence");
            using var unit = new UnitOfWork(db);
            check(!await unit.Game.CompleteAnalysis(batch, token, new CoachingAnalysis(), CancellationToken.None), "old AI lease cannot repopulate the reset profile");
            check(await unit.Game.CompletedSessionOwner(player, Guid.Parse(attempt.id), "training") == null, "old training epoch cannot be reingested as current coaching");
        }
        check((await fixture.Game(s => s.SyncTraining(player, new TrainingSyncRequest { Attempts = [attempt] }))).Receipts[0].Status == SyncItemStatus.Rejected,
            "actual admin reset rejects old offline attempts");
        var ranking = await Reset(s => s.Preview(player, new ProgressResetSelection { Groups = [ProgressResetGroup.Ranking] }));
        using (var db = new PickleVerseContext(options)) { var p = await db.Playerprofiles.SingleAsync(p => p.Userid == player); p.Revision++; await db.SaveChangesAsync(); }
        await Reject(() => Reset(s => s.Execute(actor, player, new ExecuteProgressReset { RequestId = Guid.NewGuid().ToString("N"), ExpectedRevision = ranking.Revision,
            Groups = [ProgressResetGroup.Ranking] })), HttpStatusCode.Conflict, "stale preview cannot execute reset");
        ranking = await Reset(s => s.Preview(player, new ProgressResetSelection { Groups = [ProgressResetGroup.Ranking] }));
        await Reset(s => s.Execute(actor, player, new ExecuteProgressReset { RequestId = Guid.NewGuid().ToString("N"), ExpectedRevision = ranking.Revision, Groups = ranking.Groups }));
        check((await fixture.Game(s => s.Read(player))).player.elo == 2000, "ranking reset sets agreed baseline Elo");
        using (var db = new PickleVerseContext(options)) { var p = await db.Playerprofiles.SingleAsync(p => p.Userid == player); p.Level = 7; p.Exp = 30; await db.SaveChangesAsync(); }
        var economy = await Reset(s => s.Preview(player, new ProgressResetSelection { Groups = [ProgressResetGroup.Economy] }));
        await Reset(s => s.Execute(actor, player, new ExecuteProgressReset { RequestId = Guid.NewGuid().ToString("N"), ExpectedRevision = economy.Revision, Groups = economy.Groups }));
        state = await fixture.Game(s => s.Read(player));
        check(state.player.coins == 0 && state.inventory.owned.SequenceEqual(new[] { "paddle.default" }) && state.inventory.equippedPaddle == "paddle.default", "economy reset restores initial inventory and balance");
        check(state.player.level == 7 && state.player.levelXp == 30 && state.learningEpoch == 1, "economy reset does not reset learning");
        using (var db = new PickleVerseContext(options))
        {
            check(await db.ProgressRewardGrants.AnyAsync(g => g.PlayerId == player && g.RewardKey == "milestone:level:25"), "economy reset retains lifetime cosmetic award receipt");
            check(await db.CurrencyTransactions.AnyAsync(t => t.PlayerId == player && t.Reason == "admin-reset" && t.Delta == -150), "economy reset creates a balance adjustment transaction");
            var p = await db.Playerprofiles.SingleAsync(p => p.Userid == player); p.Wincount = 12; p.LifetimeMatches = 20;
            var claim = await db.Playerachievements.Where(a => a.Playerid == player).FirstAsync();
            claim.Progress = 10; claim.Isclaimed = true; claim.Claimedat = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        var missions = await Reset(s => s.Preview(player, new ProgressResetSelection { Groups = [ProgressResetGroup.Missions] }));
        await Reset(s => s.Execute(actor, player, new ExecuteProgressReset { RequestId = Guid.NewGuid().ToString("N"), ExpectedRevision = missions.Revision, Groups = missions.Groups }));
        using (var db = new PickleVerseContext(options))
        {
            var claim = await db.Playerachievements.SingleAsync(p => p.Playerid == player && p.Isclaimed); var p = await db.Playerprofiles.SingleAsync(p => p.Userid == player);
            check(claim.Isclaimed && claim.Progress == 0 && p.Wincount == 0 && p.LifetimeMatches == 0 && p.MissionEpoch == 1,
                "mission reset clears counters while preserving claimed reward evidence");
        }
        Guid busyMatch = Guid.NewGuid();
        using (var db = new PickleVerseContext(options))
        {
            db.Gamematches.Add(new Gamematch { Matchid = busyMatch, Kind = "Custom", Status = MatchStatus.Playing,
                Authoritymode = MatchAuthorityMode.Dedicated, Createdat = DateTime.UtcNow, Updatedat = DateTime.UtcNow });
            db.Gamematchplayers.Add(new Gamematchplayer { Matchid = busyMatch, Playerid = player, Lastseenat = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var busyPreview = await Reset(s => s.Preview(player, selection));
        check(busyPreview.Busy, "preview detects an active match");
        await Reject(() => Reset(s => s.Execute(actor, player, new ExecuteProgressReset { RequestId = Guid.NewGuid().ToString("N"),
            ExpectedRevision = busyPreview.Revision, Groups = selection.Groups })), HttpStatusCode.Conflict, "active match blocks progress reset");
        using (var db = new PickleVerseContext(options))
        {
            await db.Gamematchplayers.Where(p => p.Matchid == busyMatch).ExecuteDeleteAsync();
            await db.Gamematches.Where(m => m.Matchid == busyMatch).ExecuteDeleteAsync();
        }
        var rollback = await Reset(s => s.Preview(player, selection));
        using (var db = new PickleVerseContext(options)) await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION fail_reset_test() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'injected audit failure'; END; $$;
            CREATE TRIGGER fail_reset_test BEFORE INSERT ON adminaudits FOR EACH ROW EXECUTE FUNCTION fail_reset_test();
            """);
        bool failed = false;
        try { await Reset(s => s.Execute(actor, player, new ExecuteProgressReset { RequestId = Guid.NewGuid().ToString("N"), ExpectedRevision = rollback.Revision, Groups = selection.Groups })); }
        catch (DbUpdateException) { failed = true; }
        using (var db = new PickleVerseContext(options))
        {
            var p = await db.Playerprofiles.AsNoTracking().SingleAsync(p => p.Userid == player);
            check(failed && p.Level == 7 && p.Exp == 30 && p.LearningEpoch == 1, "late persistence failure rolls back the whole reset");
        }
    }
}
