using System.Net;
using Entities.DTOs;
using Entities.Exceptions;
using Entities.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PickleVerse.Progression;
using Repositories.Context;
using Services.Implementations;

static class ContentChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, Action<bool, string> check)
    {
        async Task<T> Content<T>(Func<ContentService, Task<T>> action)
        { using var db = new PickleVerseContext(options); using var unit = new UnitOfWork(db); return await action(new ContentService(unit)); }
        async Task<T> Game<T>(Func<OnlineGameService, Task<T>> action)
        {
            using var db = new PickleVerseContext(options); using var unit = new UnitOfWork(db); var settings = TestSettings.Load();
            return await action(new OnlineGameService(unit, new GameCatalogService(unit, settings), TimeProvider.System, settings,
                TestRedis.State, TestRedis.Leaderboard, new MatchReplayService(unit, TestReplayStorage.Shared, NullLogger<MatchReplayService>.Instance)));
        }
        async Task Reject(Func<Task> action, HttpStatusCode expected, string label)
        {
            bool rejected = false; try { await action(); } catch (AppException e) { rejected = e.StatusCode == expected; }
            check(rejected, label);
        }
        var player = Guid.NewGuid();
        using (var db = new PickleVerseContext(options))
        {
            db.Users.Add(new User { Userid = player, Email = player.ToString("N") + "@example.invalid", Passwordhash = "test-only",
                Roleid = await db.Roles.Where(r => r.Rolename == "Player").Select(r => r.Roleid).SingleAsync(), Status = UserStatus.Active, Createdat = DateTime.UtcNow });
            db.Playerprofiles.Add(new Playerprofile { Userid = player, Playername = "cms" + player.ToString("N")[..12], Avatar = "initial", Level = 1, Elo = 2000 });
            await db.SaveChangesAsync();
        }
        var all = await Content(s => s.Lessons());
        check(all.Count == 15, "catalog contains 15 seeded playable lessons");
        var serve = all.Single(l => l.Code == "serve");
        var config = GameJson.Read<TrainingDrill>(GameJson.Write(serve.Draft));
        config.id = "cms-serve"; config.prerequisites = [];
        int created = await Content(s => s.Create(new CreateLessonRequest { Code = config.id, LessonName = "Bài giao CMS", Description = "Luyện giao bóng qua CMS.", Config = config }));
        var draft = await Content(s => s.Get(created));
        check(draft.Status == ContentPublishStatus.Draft && draft.Published == null, "creation persists a draft with valid JSON reward and linked drill");
        var state = await Game(s => s.Read(player));
        check(!state.catalog.lessons.Any(d => d.id == config.id), "draft is absent from player catalog");
        var published = await Content(s => s.Publish(created, new ContentVersionRequest { ExpectedVersion = draft.Version }, true));
        state = await Game(s => s.Read(player));
        check(state.catalog.lessons.Any(d => d.id == config.id), "published CMS lesson enters player catalog");
        var started = await Game(s => s.StartTraining(player, new StartTrainingRequest { RequestId = Guid.NewGuid().ToString("N"), LessonCode = config.id }));
        var attempt = started.player.training.active;
        check(attempt.catalogVersion == started.catalog.version, "attempt captures immutable catalog version");
        string capturedVersion = attempt.catalogVersion;
        var references = await Content(s=>s.References());
        var originalReference = references.Single(r=>r.ReferenceId.ToString("D")==serve.Draft.referenceId);
        var nextReference = await Content(s=>s.CreateReference(originalReference.TechniqueId,new CreateTechniqueReference {
            ExpectedLatestVersion=originalReference.Version,Parameters=new TrainingReferenceData { contactWindowSeconds=.18f } }));
        check(nextReference.Version==2 && (await Content(s=>s.References())).Single(r=>r.ReferenceId==originalReference.ReferenceId).Parameters.contactWindowSeconds==.17f,
            "technical reference creates an immutable new version without overwriting the old parameters");
        await Reject(()=>Content(s=>s.CreateReference(originalReference.TechniqueId,new CreateTechniqueReference {ExpectedLatestVersion=1,Parameters=new()})),
            HttpStatusCode.Conflict,"stale reference edit cannot overwrite a concurrent version");
        check(attempt.definition.referenceId==originalReference.ReferenceId.ToString("D") && attempt.definition.reference.contactWindowSeconds==.17f,
            "active attempt retains its original technique reference parameters");
        config.referenceId=nextReference.ReferenceId.ToString("D");
        config.trials = 3; config.required = 2;
        var edited = await Content(s => s.Save(created, new SaveLessonContent { ExpectedVersion = published.Version, Config = config }));
        await Reject(() => Content(s => s.Save(created, new SaveLessonContent { ExpectedVersion = published.Version, Config = config })), HttpStatusCode.Conflict, "stale CMS edit is rejected");
        var republished = await Content(s => s.Publish(created, new ContentVersionRequest { ExpectedVersion = edited.Version }, true));
        check(republished.Published!.reference.contactWindowSeconds==.18f,"republished lesson resolves the selected version rather than client-supplied parameters");
        var hidden = await Content(s => s.Publish(created, new ContentVersionRequest { ExpectedVersion = republished.Version }, false));
        check(hidden.Status == ContentPublishStatus.Unpublished, "unpublish records typed status");
        config.referenceId=references.First(r=>r.TechniqueId!=originalReference.TechniqueId).ReferenceId.ToString("D");
        var mismatched=await Content(s=>s.Save(created,new SaveLessonContent {ExpectedVersion=hidden.Version,Config=config}));
        await Reject(()=>Content(s=>s.Publish(created,new ContentVersionRequest {ExpectedVersion=mismatched.Version},true)),HttpStatusCode.BadRequest,
            "publish rejects a reference belonging to another technique");
        await Reject(() => Game(s => s.StartTraining(player, new StartTrainingRequest { RequestId = Guid.NewGuid().ToString("N"), LessonCode = config.id, Retry = true })), HttpStatusCode.BadRequest, "unpublished lesson cannot start through current catalog");
        attempt.trials = attempt.successes = attempt.contacts = serve.Draft.trials; attempt.seconds = 1; attempt.timingTotal = .1 * attempt.contacts;
        var complete = new TrainingUpdateRequest { RequestId = Guid.NewGuid().ToString("N"), Attempt = attempt };
        var finished = await Game(s => s.UpdateTraining(player, complete, true));
        check(finished.player.training.active.passed && finished.player.training.active.catalogVersion == capturedVersion,
            "in-flight lesson completes against original snapshot after edit and unpublish");
        long money = finished.player.coins;
        var duplicate = await Game(s => s.UpdateTraining(player, complete, true));
        check(duplicate.player.coins == money, "completion retry cannot duplicate currency");
        var parent = await Content(s => s.Get(serve.LessonId));
        parent.Draft.prerequisites = ["serve"];
        var cyclic = await Content(s => s.Save(parent.LessonId, new SaveLessonContent { ExpectedVersion = parent.Version, Config = parent.Draft }));
        await Reject(() => Content(s => s.Publish(parent.LessonId, new ContentVersionRequest { ExpectedVersion = cyclic.Version }, true)), HttpStatusCode.BadRequest, "publish rejects cyclic prerequisite graph");
        using (var db = new PickleVerseContext(options))
        {
            var unit = new UnitOfWork(db);
            foreach (var lesson in all.Take(10)) await unit.Game.RecordLesson(player, lesson.Code, 1, true);
            await db.SaveChangesAsync();
        }
        var milestone = await Game(s => s.Read(player));
        check(milestone.inventory.owned.Contains("paddle.thot"), "ten completed lessons award THOT");
        check((await Game(s => s.Read(player))).inventory.owned.Count(x => x == "paddle.thot") == 1, "milestone grants exactly one cosmetic");
        using (var db = new PickleVerseContext(options))
        {
            var profile = await db.Playerprofiles.SingleAsync(p => p.Userid == player); profile.Level = 24; await db.SaveChangesAsync();
        }
        check(!(await Game(s => s.Read(player))).inventory.owned.Contains("paddle.gemstone"), "level 24 does not grant GEMSTONE");
        using (var db = new PickleVerseContext(options))
        {
            var profile = await db.Playerprofiles.SingleAsync(p => p.Userid == player); profile.Level = 25; await db.SaveChangesAsync();
        }
        check((await Game(s => s.Read(player))).inventory.owned.Contains("paddle.gemstone"), "level 25 grants GEMSTONE");
        using (var db = new PickleVerseContext(options))
            check(await db.ProgressRewardGrants.CountAsync(r => r.PlayerId == player && r.RewardKey.StartsWith("milestone:")) == 2,
                "milestone receipts are durable and independent of repeated reads");
    }
}
