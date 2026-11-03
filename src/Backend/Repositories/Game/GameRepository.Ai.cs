using System.Text.Json;
using Entities.Models;
using Microsoft.EntityFrameworkCore;
using PickleVerse.Progression;

namespace Repositories.Game;

public sealed partial class GameRepository
{
    public sealed record CompletedSession(string mode, DateTime ended);
    public Task<CompletedSession?> CompletedSessionOwner(Guid player, Guid session, string type) => type == "match"
        ? db.Gamematches.Where(m => m.Matchid == session && m.Status == "FINISHED" && m.Gamematchplayers.Any(p => p.Playerid == player && p.Finalizedat != null))
            .Select(m => new CompletedSession(m.Kind, m.Endedat!.Value)).SingleOrDefaultAsync()
        : db.TrainingSessionRecords.Where(s => s.AttemptId == session && s.PlayerId == player && s.Completed)
            .Select(s => new CompletedSession("training", s.UpdatedAt)).SingleOrDefaultAsync();
    public Task<AiTelemetrySession?> TelemetrySession(Guid player, Guid session, string type) => db.AiTelemetrySessions.AsNoTracking()
        .SingleOrDefaultAsync(s => s.PlayerId == player && s.SessionId == session && s.SessionType == type);
    public Task<AiTelemetrySession?> TelemetryBatch(Guid batch) => db.AiTelemetrySessions.AsNoTracking().SingleOrDefaultAsync(s => s.BatchId == batch);
    public Task<AiCoachingSession?> SessionCoaching(Guid player, Guid session, string type) => db.AiCoachingSessions.AsNoTracking()
        .SingleOrDefaultAsync(s => s.PlayerId == player && s.SessionId == session && s.SessionType == type);
    public Task<bool> OwnsCoachingSession(Guid player, Guid session, string type) => type == "match"
        ? db.Gamematchplayers.AnyAsync(p => p.Playerid == player && p.Matchid == session)
        : db.TrainingSessionRecords.AnyAsync(s => s.PlayerId == player && s.AttemptId == session);
    public Task<string?> SessionAnalysisState(Guid player, Guid session, string type) =>
        (from telemetry in db.AiTelemetrySessions join job in db.AiAnalysisJobs on telemetry.BatchId equals job.BatchId
         where telemetry.PlayerId == player && telemetry.SessionId == session && telemetry.SessionType == type select job.State).SingleOrDefaultAsync();
    public async Task CreateTelemetry(AiTelemetrySession session)
    { db.AiTelemetrySessions.Add(session); await db.SaveChangesAsync(); }
    public Task<string?> TelemetryFingerprint(Guid batch, int part) => db.AiTelemetryParts.Where(p => p.BatchId == batch && p.PartIndex == part)
        .Select(p => p.Fingerprint).SingleOrDefaultAsync();
    public Task AddTelemetryPart(Guid batch, int part, string fingerprint, string shots) => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO aitelemetryparts(batchid,partindex,fingerprint,shots) VALUES({batch},{part},{fingerprint},{shots}::jsonb) ON CONFLICT DO NOTHING
        """);
    public Task<int> TelemetryPartCount(Guid batch) => db.AiTelemetryParts.CountAsync(p => p.BatchId == batch);
    public Task<List<string>> TelemetryShots(Guid batch) => db.AiTelemetryParts.Where(p => p.BatchId == batch).OrderBy(p => p.PartIndex).Select(p => p.Shots).ToListAsync();
    public Task QueueAnalysis(Guid batch) => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO aianalysisjobs(batchid) VALUES({batch}) ON CONFLICT DO NOTHING");
    public Task<AiCoachingProfile?> CoachingProfile(Guid player, DateTime cutoff) => db.AiCoachingProfiles.AsNoTracking()
        .SingleOrDefaultAsync(p => p.PlayerId == player && p.UpdatedAt >= cutoff);
    public Task<string?> LatestAnalysisState(Guid player) => (from job in db.AiAnalysisJobs join session in db.AiTelemetrySessions on job.BatchId equals session.BatchId
        where session.PlayerId == player orderby session.CreatedAt descending select job.State).FirstOrDefaultAsync();
    public Task<List<string>> OnboardingShots(Guid player, string mode, DateTime cutoff) => (from part in db.AiTelemetryParts
        join session in db.AiTelemetrySessions on part.BatchId equals session.BatchId
        where session.PlayerId == player && session.CreatedAt >= cutoff
            && EF.Functions.JsonContains(session.Metadata, GameJson.Payload(new { mode }))
            && db.AiAnalysisJobs.Any(j => j.BatchId == session.BatchId)
        orderby session.CreatedAt, part.PartIndex select part.Shots).Take(3).ToListAsync();
    public async Task<List<List<TechniqueMetric>>> CoachingHistory(Guid player, DateTime cutoff) =>
        (await db.AiCoachingSessions.Where(s => s.PlayerId == player && s.CreatedAt >= cutoff).OrderByDescending(s => s.CreatedAt)
            .Take(9).Select(s => s.Result).ToListAsync()).Select(s => GameJson.Read<CoachingAnalysis>(s).metrics).ToList();

    public async Task CleanupTelemetry(DateTime cutoff)
    {
        var expired = db.AiTelemetrySessions.Where(s => s.CreatedAt < cutoff).Select(s => s.BatchId);
        await db.AiTelemetryParts.Where(p => expired.Contains(p.BatchId)).ExecuteDeleteAsync();
        await db.AiAnalysisJobs.Where(j => expired.Contains(j.BatchId)).ExecuteUpdateAsync(s => s.SetProperty(j => j.Request, (string?)null)
            .SetProperty(j => j.State, j => j.State == "completed" ? j.State : "failed")
            .SetProperty(j => j.LastError, j => j.State == "completed" ? j.LastError : "Telemetry retention expired"));
        var sessions = await db.AiCoachingSessions.Where(s => s.CreatedAt < cutoff).ToListAsync();
        foreach (var session in sessions)
        { var analysis = GameJson.Read<CoachingAnalysis>(session.Result); analysis.findings = []; session.Result = HybridJson.Write(analysis); }
        var profiles = await db.AiCoachingProfiles.Where(s => s.UpdatedAt < cutoff).ToListAsync();
        foreach (var profile in profiles)
        { var analysis = GameJson.Read<CoachingAnalysis>(profile.Result); analysis.findings = []; profile.Result = HybridJson.Write(analysis); }
        await db.SaveChangesAsync();
    }
    public sealed record AnalysisLease(Guid id, string? request, int attempts);
    public async Task<AnalysisLease?> ClaimAnalysis(Guid token)
    {
        var now = DateTime.UtcNow;
        await db.AiAnalysisJobs.Where(j => j.State == "processing" && j.Attempts >= 10 && j.LeaseUntil < now)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.State, "failed").SetProperty(j => j.LastError, "Lease expired after final attempt")
                .SetProperty(j => j.LeaseToken, (Guid?)null).SetProperty(j => j.LeaseUntil, (DateTime?)null));
        return await new PostgresCommands(db).One("""
            UPDATE aianalysisjobs SET state='processing',attempts=attempts+1,leasetoken=@token,
                leaseuntil=now()+interval '60 seconds',updatedat=now()
            WHERE batchid=(SELECT batchid FROM aianalysisjobs
                WHERE attempts<10 AND ((state='pending' AND availableat<=now()) OR (state='processing' AND leaseuntil<now()))
                ORDER BY availableat FOR UPDATE SKIP LOCKED LIMIT 1)
            RETURNING batchid,request::text,attempts
            """, r => new AnalysisLease(r.GetGuid(0), r.IsDBNull(1) ? null : r.GetString(1), r.GetInt32(2)), PostgresCommands.P("token", token));
    }
    public Task SaveAnalysisRequest(Guid batch, Guid token, string request) => db.AiAnalysisJobs.Where(j => j.BatchId == batch && j.LeaseToken == token)
        .ExecuteUpdateAsync(s => s.SetProperty(j => j.Request, request));
    public Task RetryAnalysis(Guid batch, Guid token, int attempts, string diagnostic)
    {
        var available = DateTime.UtcNow.AddSeconds(Math.Min(300, 1 << attempts));
        return db.AiAnalysisJobs.Where(j => j.BatchId == batch && j.LeaseToken == token).ExecuteUpdateAsync(s =>
            s.SetProperty(j => j.State, attempts >= 10 ? "failed" : "pending").SetProperty(j => j.LastError, diagnostic)
                .SetProperty(j => j.AvailableAt, available).SetProperty(j => j.LeaseUntil, (DateTime?)null)
                .SetProperty(j => j.LeaseToken, (Guid?)null).SetProperty(j => j.UpdatedAt, DateTime.UtcNow));
    }
    public async Task<bool> CompleteAnalysis(Guid batch, Guid token, CoachingAnalysis result, CancellationToken cancellationToken)
    {
        await using var tx = await BeginTransactionAsync(cancellationToken: cancellationToken);
        var job = await db.AiAnalysisJobs.FromSqlInterpolated($"SELECT * FROM aianalysisjobs WHERE batchid={batch} FOR UPDATE").SingleAsync(cancellationToken);
        if (job.LeaseToken != token) return false;
        var session = await db.AiTelemetrySessions.AsNoTracking().SingleAsync(s => s.BatchId == batch, cancellationToken);
        await Lock("ai-profile:" + session.PlayerId);
        int inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO aicoachingsessions(batchid,playerid,sessionid,sessiontype,result,createdat)
            VALUES({batch},{session.PlayerId},{session.SessionId},{session.SessionType},{GameJson.Write(result)}::jsonb,{session.CreatedAt}) ON CONFLICT DO NOTHING
            """, cancellationToken);
        if (inserted == 1)
        {
            var techniques = await db.Techniques.AsNoTracking().ToDictionaryAsync(t => t.Techniquename, t => t.Techniqueid, cancellationToken);
            int? lesson = session.SessionType == "training" ? await db.TrainingSessionRecords.Where(t => t.AttemptId == session.SessionId)
                .Select(t => (int?)t.LessonId).SingleOrDefaultAsync(cancellationToken) : null;
            foreach (var finding in result.findings)
                if (techniques.TryGetValue(finding.technique, out int technique))
                    db.Playertechniqueserrors.Add(new Playertechniqueserror { Playerid = session.PlayerId, Techniqueid = technique,
                        Matchid = session.SessionType == "match" ? session.SessionId : null, Lessonid = lesson, Errortype = finding.code, Coachingtip = finding.tip });
        }
        var compact = new CoachingAnalysis { sessionId = result.sessionId, ruleVersion = result.ruleVersion, metrics = result.metrics,
            recommendations = result.recommendations, tier = result.tier, findings = result.findings.DistinctBy(f => (f.technique, f.code)).Take(20).ToList() };
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO aicoachingprofiles(playerid,batchid,result,updatedat) VALUES({session.PlayerId},{batch},{HybridJson.Write(compact)}::jsonb,{session.CreatedAt})
            ON CONFLICT(playerid) DO UPDATE SET batchid=EXCLUDED.batchid,result=EXCLUDED.result,updatedat=EXCLUDED.updatedat
            WHERE aicoachingprofiles.updatedat<=EXCLUDED.updatedat
            """, cancellationToken);
        job.State = "completed"; job.LeaseUntil = null; job.LeaseToken = null; job.LastError = null; job.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }
}
