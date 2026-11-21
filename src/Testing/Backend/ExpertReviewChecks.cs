using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Entities.DTOs;
using Entities.Exceptions;
using Entities.Models;
using Microsoft.EntityFrameworkCore;
using PickleVerse.Progression;
using Repositories.Context;
using Services.Implementations;

static class ExpertReviewChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, Action<bool, string> check)
    {
        var fixture = new FunctionalFixture(options);
        var reviewer = await fixture.Player("Admin");
        var player = await fixture.Player();
        var syntheticPlayer = await fixture.Player();
        async Task<T> Call<T>(Func<ExpertReviewService, Task<T>> action)
        {
            using var db = new PickleVerseContext(options);
            using var unit = new UnitOfWork(db);
            return await action(new ExpertReviewService(unit, TimeProvider.System));
        }
        async Task Reject(Func<Task> action, HttpStatusCode code, string name)
        {
            bool rejected = false;
            try { await action(); }
            catch (AppException error) { rejected = error.StatusCode == code; }
            check(rejected, name);
        }
        // These observations live only in the disposable test database and never leave as a training artifact.
        async Task<Guid> Evidence(Guid user, bool eligibleAccount)
        {
            using var db = new PickleVerseContext(options);
            using var unit = new UnitOfWork(db);
            if (eligibleAccount)
                (await db.Users.SingleAsync(u => u.Userid == user)).Email = user.ToString("N") + "@fixture.example.test";
            var lesson = await db.Lessondrills.OrderBy(x => x.Lessonid).FirstAsync();
            var session = Guid.NewGuid(); var batch = Guid.NewGuid();
            var now = DateTime.UtcNow;
            db.TrainingSessionRecords.Add(new TrainingSessionRecord
            {
                AttemptId = session, PlayerId = user, LessonId = lesson.Lessonid, DrillId = lesson.Drillid,
                Completed = true, CreatedAt = now, UpdatedAt = now, CatalogVersion = "software-fixture",
                Data = GameJson.Write(new TrainingAttempt { completed = true, passed = true })
            });
            await db.SaveChangesAsync();
            await unit.Game.CreateTelemetry(new AiTelemetrySession
            {
                BatchId = batch, PlayerId = user, SessionId = session, SessionType = "training", PartCount = 1, CreatedAt = now,
                Metadata = GameJson.Payload(new { mode = "training", partial = false, header = GameJson.Payload(new { gameVersion = "software-fixture" }) })
            });
            var shot = new ShotTelemetry { shotId = "fixture-shot", sequence = 0, kind = "Dink", technique = "Dink", source = "training-trial",
                rawPressedAt = .1, rawReleasedAt = .2, rawPosition = [1, 0, -3], rawTimingError = -.1f, netClearance = .8f };
            await unit.Game.AddTelemetryPart(batch, 0, "software-only", GameJson.Write(new[] { shot }));
            return batch;
        }
        var batch = await Evidence(player, true);
        await Evidence(syntheticPlayer, false);
        var demoSessions = await Call(s => s.DemoSessions());
        check(demoSessions.Count == 30 && demoSessions.GroupBy(s => s.Tier).Count() == 3
            && demoSessions.GroupBy(s => s.Tier).All(g => g.Count() == 10), "development seed includes ten separate simulated sessions per tier");
        var demo = await Call(s => s.DemoEvidence("sim-beginner-01-training"));
        check(demo.Session.Tier == ExpertTier.Beginner && demo.Session.Split == DatasetSplit.Train
            && demo.GameVersion == "synthetic-demo-v1" && demo.Shots.Select(shot => shot.sequence).SequenceEqual(Enumerable.Range(0, 24))
            && demo.Shots.All(shot => shot.assisted && shot.engineAimed && shot.rawReleasedAt.HasValue),
            "simulated evidence preserves seed labels and the 24 ordered raw inputs");
        await Reject(() => Call(s => s.DemoEvidence("missing")), HttpStatusCode.NotFound, "unknown simulated session returns 404");
        var sessions = await Call(s => s.Sessions(null, 1));
        check(sessions.Count == 1 && sessions[0].BatchId == batch, "expert selection excludes known synthetic fixture accounts");
        var detail = await Call(s => s.Evidence(batch));
        check(detail.GameVersion == "software-fixture" && detail.Shots[0].rawReleasedAt == .2 && detail.LatestReview == null,
            "reviewer can inspect source version and raw evidence before labelling");
        var request = new ExpertReviewRequest
        {
            Split = DatasetSplit.Calibration, Tier = ExpertTier.Beginner, ExpertName = "Software fixture reviewer", Observation = "Software fixture evidence",
            ConfirmedHumanSession = true,
            ShotLabels = [new ExpertShotLabel { ShotId = "fixture-shot", Code = "DinkTooHigh", Present = true, Evidence = "Software fixture trajectory" }]
        };
        await Reject(() => Call(s => s.Review(player, batch, request)), HttpStatusCode.Forbidden, "regular player cannot confirm expert labels");
        request.ConfirmedHumanSession = false;
        await Reject(() => Call(s => s.Review(reviewer, batch, request)), HttpStatusCode.BadRequest, "review requires explicit human evidence confirmation");
        request.ConfirmedHumanSession = true;
        var saved = await Call(s => s.Review(reviewer, batch, request));
        check(saved.ReviewerId == reviewer && saved.Version == 1 && saved.Review.ShotLabels.Count == 1, "review records authenticated reviewer and immutable version");
        await Reject(() => Call(s => s.Review(reviewer, batch, request)), HttpStatusCode.Conflict, "stale review cannot replace an existing version");
        var export = await Call(s => s.Export(true));
        using var data = JsonDocument.Parse(export.Content);
        var entry = data.RootElement[0];
        check(data.RootElement.GetArrayLength() == 1 && entry.GetProperty("tier").GetString() == "beginner"
            && entry.GetProperty("split").GetString() == "calibration" && entry.GetProperty("playerId").GetString() != player.ToString("N"),
            "dataset preserves the reviewer partition and canonical tier with a pseudonymous player key");
        check(export.Sha256 == Convert.ToHexString(SHA256.HashData(export.Content)).ToLowerInvariant(), "dataset checksum identifies the exact downloaded bytes");
    }
}
