using Microsoft.EntityFrameworkCore;
using PickleVerse.Progression;
using Repositories.Context;

static class TrainingSyncChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, Action<bool, string> check)
    {
        var fixture = new FunctionalFixture(options); var a = await fixture.Player(); var b = await fixture.Player();
        var initial = await fixture.Game(s => s.Read(a));
        TrainingAttempt Attempt(GameSnapshot state, string code) => new() { id = Guid.NewGuid().ToString("N"), playerId = state.player.id,
            lessonId = code, catalogVersion = state.catalog.version, progressEpoch = state.learningEpoch,
            trials = state.catalog.lessons.Single(d => d.id == code).trials, successes = state.catalog.lessons.Single(d => d.id == code).trials,
            contacts = state.catalog.lessons.Single(d => d.id == code).trials, seconds = 30, timingTotal = .1,
            rewardTokenpad = 999999, rewardXp = 999999, stars = 999 };
        Task<TrainingSyncResult> Sync(Guid id, params TrainingAttempt[] attempts) => fixture.Game(s => s.SyncTraining(id, new TrainingSyncRequest { Attempts = attempts.ToList() }));
        var serve = Attempt(initial, "serve");
        var first = await Sync(a, serve);
        check(first.Receipts.Single().Status == SyncItemStatus.Applied && first.Receipts[0].Result?.stars == 3,
            "offline sync recalculates stars and ignores client reward values");
        check(first.Snapshot.player.coins == 150 && first.Snapshot.player.levelXp == 5, "offline first pass earns canonical currency and XP");
        var repeat = await Sync(a, serve);
        check(repeat.Receipts[0].Status == SyncItemStatus.Duplicate && repeat.Snapshot.player.coins == 150 && repeat.Snapshot.player.levelXp == 5,
            "lost-response retry returns duplicate without repeating rewards");
        var tampered = GameJson.Read<TrainingAttempt>(GameJson.Write(serve)); tampered.seconds++;
        check((await Sync(a, tampered)).Receipts[0].Status == SyncItemStatus.Rejected, "same attempt id with different measurements is rejected");
        check((await Sync(b, serve)).Receipts[0].Status == SyncItemStatus.Rejected, "outbox cannot be submitted by a different account");
        var review = await Sync(a, Attempt(initial, "serve"));
        check(review.Snapshot.player.coins == 150 && review.Snapshot.player.levelXp == 5, "new replay attempt cannot farm first-pass reward");
        var bad = Attempt(initial, "drive"); bad.contacts = 0;
        var good = Attempt(initial, "drive");
        var batch = await Sync(a, bad, good);
        check(batch.Receipts[0].Status == SyncItemStatus.Rejected && batch.Receipts[1].Status == SyncItemStatus.Applied,
            "one invalid item does not discard later valid items");
        var missing = Attempt(initial, "dink"); missing.catalogVersion = "missing-version";
        check((await Sync(a, missing)).Receipts[0].Status == SyncItemStatus.Rejected, "unknown cached content version is rejected");
        var locked = await Sync(a, Attempt(initial, "modifiers"));
        check(locked.Receipts[0].Status == SyncItemStatus.Rejected && locked.Receipts[0].Retryable, "unmet prerequisites remain retryable");
        var chain = await Sync(a, Attempt(initial, "dink"), Attempt(initial, "volley"), Attempt(initial, "modifiers"));
        check(chain.Receipts.All(x => x.Status == SyncItemStatus.Applied), "offline prerequisite chain applies in order");
        var c = await fixture.Player(); var third = await fixture.Game(s => s.Read(c)); var concurrent = Attempt(third, "serve");
        var races = await Task.WhenAll(Sync(c, concurrent), Sync(c, concurrent));
        check(races.Count(r => r.Receipts[0].Status == SyncItemStatus.Applied) == 1 && races.Count(r => r.Receipts[0].Status == SyncItemStatus.Duplicate) == 1,
            "two devices submit one attempt with exactly one application");
        check((await fixture.Game(s => s.Read(c))).player.coins == 150, "parallel retry credits currency once");
        using (var db = new PickleVerseContext(options))
        {
            var profile = await db.Playerprofiles.SingleAsync(p => p.Userid == c); profile.LearningEpoch++; profile.Level = 1; profile.Exp = 0;
            await db.Playerlessondrills.Where(r => r.Playerid == c).ExecuteDeleteAsync();
            await db.Playertechniquemasteries.Where(r => r.Playerid == c).ExecuteDeleteAsync();
            await db.TrainingSessionRecords.Where(r => r.PlayerId == c).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsCurrent, false));
            await db.SaveChangesAsync();
        }
        check((await Sync(c, concurrent)).Receipts[0].Status == SyncItemStatus.Rejected, "old accepted receipt cannot revive progress after reset");
        var afterReset = await fixture.Game(s => s.Read(c));
        var relearned = await Sync(c, Attempt(afterReset, "serve"));
        check(relearned.Snapshot.player.coins == 150 && relearned.Snapshot.player.levelXp == 5,
            "new learning epoch earns XP but never repeats lifetime currency reward");
        var start = await fixture.Game(s => s.StartTraining(c, new StartTrainingRequest { RequestId = Guid.NewGuid().ToString("N"), LessonCode = "drive" }));
        var resumed = Attempt(start, "drive"); resumed.id = start.player.training.active.id;
        check((await Sync(c, resumed)).Receipts[0].Status == SyncItemStatus.Applied, "online-started attempt can finish through offline sync");
        var newcomer = await fixture.Game(s => s.Onboarding(b, new OnboardingRequest { RequestId = Guid.NewGuid().ToString("N"), Choice = "NEW" }));
        check(newcomer.experienceChoice == "NEW" && !newcomer.tutorial.completed, "newcomer onboarding persists without pretending tutorial is complete");
        await fixture.Game(s => s.Tutorial(b, new TutorialUpdateRequest { RequestId = Guid.NewGuid().ToString("N"), Key = "checkpoint", Step = 5 }));
        await fixture.Game(s => s.Tutorial(b, new TutorialUpdateRequest { RequestId = Guid.NewGuid().ToString("N"), Key = "checkpoint", Step = 2 }));
        check((await fixture.Game(s => s.Read(b))).tutorial.checkpoint == 5, "tutorial resumes its durable high-water checkpoint across sessions and devices");
        var experiencedId = await fixture.Player();
        var experienced = await fixture.Game(s => s.Onboarding(experiencedId, new OnboardingRequest { RequestId = Guid.NewGuid().ToString("N"), Choice = "EXPERIENCED" }));
        check(experienced.experienceChoice == "EXPERIENCED" && experienced.player.elo == 2000, "experienced onboarding does not change competitive Elo");
    }
}
