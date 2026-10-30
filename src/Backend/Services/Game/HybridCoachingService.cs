using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PickleVerse.Progression;
using Repositories.Context;
using Services.Configuration;
using Services.Exceptions;
using static Services.Game.GameSql;

namespace Services.Game;

public sealed class HybridCoachingService(PickleVerseContext db, GameSql sql, GameCatalogService catalogs,
    HybridAiSettings settings, TimeProvider clock)
{
    static AppException Invalid(string message) => new(message, HttpStatusCode.BadRequest);
    static AppException Conflict(string message) => new(message, HttpStatusCode.Conflict);
    static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static void Validate(Guid player, TelemetryBatch batch)
    {
        if (!Guid.TryParse(batch.playerId, out var owner) || owner != player)
            throw new AppException("Telemetry không thuộc tài khoản hiện tại.", HttpStatusCode.Forbidden);
        if (!Guid.TryParse(batch.batchId, out var batchId) || batchId == Guid.Empty || !Guid.TryParse(batch.sessionId, out var sessionId) || sessionId == Guid.Empty
            || batch.sessionType is not ("match" or "training") || batch.schemaVersion != "1"
            || batch.gameVersion?.Length is not (> 0 and <= 80) || batch.partCount is < 1 or > 100 || batch.partIndex < 0 || batch.partIndex >= batch.partCount
            || batch.shots == null || batch.shots.Count > 1000)
            throw Invalid("Gói telemetry không hợp lệ.");
        foreach (var shot in batch.shots)
        {
            bool Vector(float[]? v) => v == null || v.Length == 3 && v.All(float.IsFinite);
            if (shot == null || string.IsNullOrWhiteSpace(shot.shotId) || shot.shotId.Length > 100 || shot.sequence < 0 || shot.rally < 0
                || !double.IsFinite(shot.time) || shot.time < 0 || shot.kind?.Length is not (> 0 and <= 40) || shot.technique?.Length is not (> 0 and <= 40)
                || shot.source?.Length is not (> 0 and <= 40) || shot.outcome?.Length is not (> 0 and <= 40)
                || shot.timingError.HasValue && !float.IsFinite(shot.timingError.Value) || shot.netClearance.HasValue && !float.IsFinite(shot.netClearance.Value)
                || !Vector(shot.contact) || !Vector(shot.leftFoot) || !Vector(shot.rightFoot) || !Vector(shot.intendedTarget) || !Vector(shot.landing) || shot.bounces < 0)
                throw Invalid("Số liệu cú đánh không hợp lệ.");
        }
        if (batch.shots.Select(s => s.shotId).Distinct().Count() != batch.shots.Count || batch.shots.Select(s => s.sequence).Distinct().Count() != batch.shots.Count)
            throw Invalid("Mã hoặc thứ tự cú đánh bị trùng.");
        if (Encoding.UTF8.GetByteCount(GameJson.Write(batch)) > 1024 * 1024)
            throw new AppException("Gói telemetry vượt 1 MiB.", HttpStatusCode.RequestEntityTooLarge);
    }
    public async Task<TelemetryReceipt> Ingest(Guid player, TelemetryBatch batch)
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();double stagePrevious=0;
        void Stage(string name) {double now=timer.Elapsed.TotalMilliseconds;HybridAiMetrics.IngestStageMs.Record(now-stagePrevious,new KeyValuePair<string,object?>("stage",name));stagePrevious=now;}
        if (!settings.IngestEnabled) throw new AppException("Telemetry đang tắt.", HttpStatusCode.ServiceUnavailable);
        Validate(player, batch);
        Stage("validate");
        var session = Guid.Parse(batch.sessionId);
        var batchId = Guid.Parse(batch.batchId);
        // Authenticated ownership and server-accepted completion precede analytics.
        var owner = batch.sessionType == "match"
            ? await sql.One("SELECT m.kind,m.updatedat FROM gamematches m WHERE m.matchid=@session AND m.status='FINISHED' AND EXISTS(SELECT 1 FROM gamematchplayers p WHERE p.matchid=m.matchid AND p.playerid=@player)",
                r => new { mode = r.GetString(0), ended = r.GetDateTime(1) }, P("session", session), P("player", player))
            : await sql.One("SELECT 'training',updatedat FROM trainingsessions WHERE attemptid=@session AND playerid=@player AND completed",
                r => new { mode = r.GetString(0), ended = r.GetDateTime(1) }, P("session", session), P("player", player));
        if (owner == null) throw Conflict("Phiên chưa được máy chủ xác nhận hoàn tất hoặc không thuộc tài khoản này.");
        Stage("ownership");
        var header = GameJson.Payload(new { playerId = player.ToString("N"), sessionId = session.ToString("N"), batch.sessionType, batch.partCount, batch.partial, batch.schemaVersion, batch.gameVersion });
        string fingerprint = Digest(HybridJson.Write(batch.shots));
        Stage("fingerprint");
        await using var tx = await db.Database.BeginTransactionAsync();
        await sql.Run("SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", P("key", "ai-session:" + player + ":" + session + ":" + batch.sessionType));
        Stage("lock");
        var existing = await sql.One("SELECT batchid,metadata->>'header' FROM aitelemetrysessions WHERE playerid=@player AND sessionid=@session AND sessiontype=@type",
            r => new { id = r.GetGuid(0), header = r.GetString(1) }, P("player", player), P("session", session), P("type", batch.sessionType));
        if (existing != null && (existing.id != batchId || existing.header != header)) throw Conflict("Phiên đã có gói telemetry khác.");
        Stage("read_session");
        if (existing == null)
        {
            var occupied = await sql.One("SELECT playerid FROM aitelemetrysessions WHERE batchid=@batch", r => (Guid?)r.GetGuid(0), P("batch", batchId));
            if (occupied != null) throw Conflict("Mã gói telemetry đã được sử dụng.");
            await sql.Run("INSERT INTO aitelemetrysessions(batchid,playerid,sessionid,sessiontype,partcount,metadata,createdat) VALUES(@batch,@player,@session,@type,@count,@metadata::jsonb,@ended)",
                P("batch", batchId), P("player", player), P("session", session), P("type", batch.sessionType), P("count", batch.partCount),
                P("metadata", GameJson.Payload(new { header, owner.mode, batch.partial })), P("ended", owner.ended));
        }
        var previous = await sql.One("SELECT fingerprint FROM aitelemetryparts WHERE batchid=@batch AND partindex=@part", r => r.GetString(0), P("batch", batchId), P("part", batch.partIndex));
        Stage("session_and_part_lookup");
        if (previous != null && previous != fingerprint) throw Conflict("Nội dung phần telemetry đã thay đổi.");
        await sql.Run("INSERT INTO aitelemetryparts(batchid,partindex,fingerprint,shots) VALUES(@batch,@part,@fingerprint,@shots::jsonb) ON CONFLICT DO NOTHING",
            P("batch", batchId), P("part", batch.partIndex), P("fingerprint", fingerprint), P("shots", HybridJson.Write(batch.shots)));
        Stage("write_part");
        int received = (int)await sql.One("SELECT count(*) FROM aitelemetryparts WHERE batchid=@batch", r => r.GetInt64(0), P("batch", batchId));
        Stage("count_parts");
        if (received == batch.partCount)
        {
            var rows = await sql.Query("SELECT shots::text FROM aitelemetryparts WHERE batchid=@batch ORDER BY partindex", r => r.GetString(0), P("batch", batchId));
            var shots = rows.SelectMany(row => GameJson.Read<List<ShotTelemetry>>(row)).ToList();
            if (shots.Select(s => s.shotId).Distinct().Count() != shots.Count || shots.Select(s => s.sequence).Distinct().Count() != shots.Count)
                throw Conflict("Cú đánh bị trùng giữa các phần telemetry.");
            Stage("validate_complete_batch");
            await sql.Run("INSERT INTO aianalysisjobs(batchid) VALUES(@batch) ON CONFLICT DO NOTHING", P("batch", batchId));
        }
        Stage("queue");
        await tx.CommitAsync();
        Stage("commit");
        return new() { batchId = batch.batchId, acceptedParts = received, status = received == batch.partCount ? "queued" : "receiving" };
    }
    public async Task<CoachingSummary> Summary(Guid player)
    {
        var catalog = await catalogs.Load();
        var training = await RelationalTraining.Load(sql,player);
        var available = catalog.lessons.Where(d => TrainingEngine.Unlocked(training, d)).ToDictionary(d => d.id);
        string fallback = TrainingEngine.Recommended(training);
        var summary = settings.CoachingEnabled ? await sql.One("SELECT result::text,updatedat FROM aicoachingprofiles WHERE playerid=@player AND updatedat>=@cutoff",
            r => new CoachingSummary { status = "ready", analysis = GameJson.Read<CoachingAnalysis>(r.GetString(0)), updatedAt = r.GetDateTime(1).ToString("O") }, P("player", player),P("cutoff",clock.GetUtcNow().UtcDateTime.AddDays(-30))) : null;
        summary ??= new() { status = settings.CoachingEnabled ? "empty" : "disabled", analysis = new() };
        summary.analysis!.recommendations = summary.analysis.recommendations.Where(d => available.ContainsKey(d.id)).DistinctBy(d => d.id).Take(3).ToList();
        if (summary.analysis.recommendations.Count == 0 && available.TryGetValue(fallback, out var lesson))
            summary.analysis.recommendations.Add(new() { id = lesson.id, title = lesson.title, reason = "Tiếp tục lộ trình luyện tập hiện tại." });
        if (settings.CoachingEnabled)
        {
            var state = await sql.One("SELECT j.state FROM aianalysisjobs j JOIN aitelemetrysessions s USING(batchid) WHERE s.playerid=@player ORDER BY s.createdat DESC LIMIT 1", r => r.GetString(0), P("player", player));
            if (state != null && state != "completed") summary.status = state;
        }
        return summary;
    }
    public async Task<Services.DTOs.RecommendationResponse> LegacyRecommendation(Guid player)
    {
        var summary = await Summary(player);
        var result = new Services.DTOs.RecommendationResponse { CoachMessage = "Bài tập dựa trên buổi chơi gần đây và lộ trình đã mở khóa." };
        foreach (var recommendation in summary.analysis!.recommendations)
        {
            var drill = await sql.One("SELECT d.drillid,d.techniqueid,d.drillname,d.description,ld.target,t.techniquename FROM drills d JOIN lessondrills ld USING(drillid) JOIN techniques t USING(techniqueid) WHERE d.code=@code ORDER BY ld.lessonid LIMIT 1",
                r => new Services.DTOs.DrillDto { DrillId = r.GetInt32(0), TechniqueId = r.GetInt32(1), DrillName = r.GetString(2), Description = r.GetString(3), Target = Convert.ToInt32(r.GetValue(4)), TechniqueName = r.GetString(5) }, P("code", recommendation.id));
            if (drill != null) result.RecommendedDrills.Add(drill);
        }
        var first=result.RecommendedDrills.FirstOrDefault();
        if(first!=null)
        {
            result.WeakestTechniqueName=first.TechniqueName;
            result.CurrentMasteryScore=await sql.One("SELECT masteryscore FROM playertechniquemastery WHERE playerid=@player AND techniqueid=@technique",r=>Convert.ToInt32(r.GetValue(0)),P("player",player),P("technique",first.TechniqueId));
            result.CoachMessage=summary.analysis.recommendations.First().reason;
        }
        return result;
    }
    public async Task<string> BuildRequest(Guid batch)
    {
        var session = await sql.One("SELECT playerid,sessionid,metadata->>'mode' FROM aitelemetrysessions WHERE batchid=@batch", r => new { player = r.GetGuid(0), session = r.GetGuid(1), mode = r.GetString(2) }, P("batch", batch)) ?? throw Invalid("Missing telemetry session.");
        var rows = await sql.Query("SELECT shots::text FROM aitelemetryparts WHERE batchid=@batch ORDER BY partindex", r => r.GetString(0), P("batch", batch));
        var onboarding = settings.ModelEnabled ? await sql.Query("""
            SELECT p.shots::text FROM aitelemetryparts p JOIN aitelemetrysessions s USING(batchid)
            WHERE s.playerid=@player AND s.metadata->>'mode'=@mode
              AND s.createdat>=@cutoff AND EXISTS(SELECT 1 FROM aianalysisjobs j WHERE j.batchid=s.batchid)
            ORDER BY s.createdat,p.partindex LIMIT 3
            """, r => r.GetString(0), P("player", session.player), P("mode", session.mode), P("cutoff", clock.GetUtcNow().UtcDateTime.AddDays(-30))) : new List<string>();
        var history = await sql.Query("SELECT (result->'metrics')::text FROM aicoachingsessions WHERE playerid=@player AND createdat>=@cutoff ORDER BY createdat DESC LIMIT 9", r => GameJson.Read<List<TechniqueMetric>>(r.GetString(0)), P("player", session.player), P("cutoff", clock.GetUtcNow().UtcDateTime.AddDays(-30)));
        var training = await RelationalTraining.Load(sql,session.player);
        var catalog = await catalogs.Load();
        return HybridJson.Write(new { sessionId = session.session.ToString("N"), ruleVersion = "rules-v1", mode = session.mode,
            shots = rows.SelectMany(row => GameJson.Read<List<ShotTelemetry>>(row)).OrderBy(s => s.sequence), history,
            onboardingShots = onboarding.SelectMany(row => GameJson.Read<List<ShotTelemetry>>(row)).Take(3000),
            drills = catalog.lessons.Select(d => new { d.id, d.title, d.tier, skill = d.skill.ToString(), prerequisites = d.prerequisites ?? [] }),
            completedLessons = training.lessons.Where(l => l.bestStars > 0).Select(l => l.id), fallbackLesson = TrainingEngine.Recommended(training),
            resumeActive = training.active is {completed:false} });
    }
}
