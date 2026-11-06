using Entities.DTOs;
using Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Game;

public sealed partial class GameRepository
{
    IQueryable<ReviewableTrainingSession> ReviewableTraining() =>
        from telemetry in db.AiTelemetrySessions.AsNoTracking()
        join attempt in db.TrainingSessionRecords on telemetry.SessionId equals attempt.AttemptId
        join lesson in db.Lessons on attempt.LessonId equals lesson.Lessonid
        join player in db.Playerprofiles on telemetry.PlayerId equals player.Userid
        where telemetry.SessionType == "training" && attempt.Completed && attempt.PlayerId == telemetry.PlayerId
            && HumanPlayers.Any(u => u.Userid == telemetry.PlayerId && !u.Email.EndsWith("@example.invalid"))
            && !EF.Functions.JsonContains(telemetry.Metadata, "{\"partial\":true}")
            && db.AiTelemetryParts.Count(p => p.BatchId == telemetry.BatchId) == telemetry.PartCount
        select new ReviewableTrainingSession
        {
            BatchId = telemetry.BatchId, SessionId = telemetry.SessionId, PlayerId = telemetry.PlayerId,
            PlayerName = player.Playername, LessonTitle = lesson.Lessonname, CatalogVersion = attempt.CatalogVersion, CompletedAt = telemetry.CreatedAt,
            ReviewedVersion = db.ExpertReviews.Where(r => r.BatchId == telemetry.BatchId).Max(r => (long?)r.Version) ?? 0
        };

    public Task<List<ReviewableTrainingSession>> ReviewableSessions(Guid? player, int skip, int take) => ReviewableTraining()
        .Where(x => player == null || x.PlayerId == player).OrderByDescending(x => x.CompletedAt).ThenBy(x => x.BatchId)
        .Skip(skip).Take(take).ToListAsync();

    public Task<ReviewableTrainingSession?> ReviewableSession(Guid batch) => ReviewableTraining().SingleOrDefaultAsync(s => s.BatchId == batch);

    public Task<List<ReviewableTrainingSession>> ReviewedSessions() => ReviewableTraining()
        .Where(s => s.ReviewedVersion > 0).OrderBy(s => s.CompletedAt).ThenBy(s => s.BatchId).ToListAsync();

    public Task<List<Guid>> InitialTrainingBatches(Guid player) => ReviewableTraining().Where(s => s.PlayerId == player)
        .OrderBy(s => s.CompletedAt).ThenBy(s => s.BatchId).Take(3).Select(s => s.BatchId).ToListAsync();

    public Task<ExpertReviewRecord?> LatestExpertReview(Guid batch) => db.ExpertReviews.AsNoTracking()
        .Where(r => r.BatchId == batch).OrderByDescending(r => r.Version).FirstOrDefaultAsync();

    public void AddExpertReview(ExpertReviewRecord review) => db.ExpertReviews.Add(review);

    public Task<List<CoachingDemoSession>> DemoCoachingSessions() => db.CoachingDemoSessions.AsNoTracking()
        .OrderBy(row => row.PlayerCode)
        .Select(row => new CoachingDemoSession(row.SessionId, row.PlayerCode, row.Tier, row.Split)).ToListAsync();

    public Task<CoachingDemoSessionRecord?> DemoCoachingSession(string sessionId) => db.CoachingDemoSessions.AsNoTracking()
        .SingleOrDefaultAsync(row => row.SessionId == sessionId);
}
