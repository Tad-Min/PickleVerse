using Repositories.UnitOfWork;
using Services.Interfaces;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using PickleVerse.Progression;
using Services.Configuration;
using Entities.Exceptions;

namespace Services.Implementations;

public sealed class HybridCoachingService : IHybridCoachingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGameCatalogService catalogs;
    private readonly HybridAiSettings settings;
    private readonly TimeProvider clock;

    public HybridCoachingService(IUnitOfWork unitOfWork, IGameCatalogService catalogs, HybridAiSettings settings, TimeProvider clock)
    {
        _unitOfWork = unitOfWork;
        this.catalogs = catalogs;
        this.settings = settings;
        this.clock = clock;
    }

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
            bool Timestamp(double? v) => v == null || double.IsFinite(v.Value) && v.Value >= 0;
            if (shot == null || string.IsNullOrWhiteSpace(shot.shotId) || shot.shotId.Length > 100 || shot.sequence < 0 || shot.rally < 0
                || !double.IsFinite(shot.time) || shot.time < 0 || shot.kind?.Length is not (> 0 and <= 40) || shot.technique?.Length is not (> 0 and <= 40)
                || shot.source?.Length is not (> 0 and <= 40) || shot.outcome?.Length is not (> 0 and <= 40)
                || shot.timingError.HasValue && !float.IsFinite(shot.timingError.Value) || shot.netClearance.HasValue && !float.IsFinite(shot.netClearance.Value)
                || !Timestamp(shot.rawPressedAt) || !Timestamp(shot.rawReleasedAt) || shot.rawReleasedAt < shot.rawPressedAt
                || shot.rawTimingError.HasValue && !float.IsFinite(shot.rawTimingError.Value) || shot.rawInputMask is < 0 or > 31
                || shot.rawTechnique?.Length > 40 || (shot.referenceId != null && !Guid.TryParse(shot.referenceId, out _)) || !Vector(shot.rawPosition)
                || !Vector(shot.contact) || !Vector(shot.leftFoot) || !Vector(shot.rightFoot) || !Vector(shot.intendedTarget) || !Vector(shot.landing) || shot.bounces < 0)
                throw Invalid("Số liệu cú đánh không hợp lệ.");
        }
        if (batch.shots.Select(s => s.shotId).Distinct().Count() != batch.shots.Count || batch.shots.Select(s => s.sequence).Distinct().Count() != batch.shots.Count)
            throw Invalid("Mã hoặc thứ tự cú đánh bị trùng.");
        if (Encoding.UTF8.GetByteCount(HybridJson.Write(batch)) > 1024 * 1024)
            throw new AppException("Gói telemetry vượt 1 MiB.", HttpStatusCode.RequestEntityTooLarge);
    }
    public async Task<TelemetryReceipt> Ingest(Guid player, TelemetryBatch batch, bool trustedServer = false)
    {
        if (!trustedServer && batch.sessionType == "match" && Guid.TryParse(batch.sessionId, out var matchId) && await _unitOfWork.Game.IsDedicated(matchId))
            throw new AppException("Telemetry trận dedicated do máy chủ gửi.", HttpStatusCode.Forbidden);
        var timer=System.Diagnostics.Stopwatch.StartNew();double stagePrevious=0;
        void Stage(string name) {double now=timer.Elapsed.TotalMilliseconds;HybridAiMetrics.IngestStageMs.Record(now-stagePrevious,new KeyValuePair<string,object?>("stage",name));stagePrevious=now;}
        if (!settings.IngestEnabled) throw new AppException("Telemetry đang tắt.", HttpStatusCode.ServiceUnavailable);
        Validate(player, batch);
        Stage("validate");
        var session = Guid.Parse(batch.sessionId);
        var batchId = Guid.Parse(batch.batchId);
        // Authenticated ownership and server-accepted completion precede analytics.
        var owner = await _unitOfWork.Game.CompletedSessionOwner(player, session, batch.sessionType);
        if (owner == null) throw Conflict("Phiên chưa được máy chủ xác nhận hoàn tất hoặc không thuộc tài khoản này.");
        Stage("ownership");
        var header = GameJson.Payload(new { playerId = player.ToString("N"), sessionId = session.ToString("N"), batch.sessionType, batch.partCount, batch.partial, batch.schemaVersion, batch.gameVersion });
        string fingerprint = Digest(HybridJson.Write(batch.shots));
        Stage("fingerprint");
        await using var tx = await _unitOfWork.Game.BeginTransactionAsync();
        await _unitOfWork.Game.Lock("ai-session:" + player + ":" + session + ":" + batch.sessionType);
        Stage("lock");
        var existing = await _unitOfWork.Game.TelemetrySession(player, session, batch.sessionType);
        if (existing != null)
        {
            using var existingMetadata = System.Text.Json.JsonDocument.Parse(existing.Metadata);
            if (existing.BatchId != batchId || existingMetadata.RootElement.GetProperty("header").GetString() != header)
                throw Conflict("Phiên đã có gói telemetry khác.");
        }
        Stage("read_session");
        if (existing == null)
        {
            var occupied = await _unitOfWork.Game.TelemetryBatch(batchId);
            if (occupied != null) throw Conflict("Mã gói telemetry đã được sử dụng.");
            await _unitOfWork.Game.CreateTelemetry(new Entities.Models.AiTelemetrySession { BatchId = batchId, PlayerId = player,
                SessionId = session, SessionType = batch.sessionType, PartCount = batch.partCount,
                Metadata = GameJson.Payload(new { header, owner.mode, batch.partial }), CreatedAt = owner.ended });
        }
        var previous = await _unitOfWork.Game.TelemetryFingerprint(batchId, batch.partIndex);
        Stage("session_and_part_lookup");
        if (previous != null && previous != fingerprint) throw Conflict("Nội dung phần telemetry đã thay đổi.");
        await _unitOfWork.Game.AddTelemetryPart(batchId, batch.partIndex, fingerprint, HybridJson.Write(batch.shots));
        Stage("write_part");
        int received = await _unitOfWork.Game.TelemetryPartCount(batchId);
        Stage("count_parts");
        if (received == batch.partCount)
        {
            var rows = await _unitOfWork.Game.TelemetryShots(batchId);
            var shots = rows.SelectMany(row => GameJson.Read<List<ShotTelemetry>>(row)).ToList();
            if (shots.Select(s => s.shotId).Distinct().Count() != shots.Count || shots.Select(s => s.sequence).Distinct().Count() != shots.Count)
                throw Conflict("Cú đánh bị trùng giữa các phần telemetry.");
            Stage("validate_complete_batch");
            await _unitOfWork.Game.QueueAnalysis(batchId);
        }
        Stage("queue");
        await tx.CommitAsync();
        Stage("commit");
        return new() { batchId = batch.batchId, acceptedParts = received, status = received == batch.partCount ? "queued" : "receiving" };
    }
    public async Task<CoachingSummary> Summary(Guid player, Guid? sessionId = null, string sessionType = "match")
    {
        if (sessionId.HasValue)
        {
            if (sessionType is not ("match" or "training") || sessionId.Value == Guid.Empty) throw Invalid("Phiên phân tích không hợp lệ.");
            if (!settings.CoachingEnabled) return new() { status = "disabled" };
            var result = await _unitOfWork.Game.SessionCoaching(player, sessionId.Value, sessionType);
            if (result != null) return new() { status = "ready", analysis = GameJson.Read<CoachingAnalysis>(result.Result), updatedAt = result.CreatedAt.ToString("O") };
            if (!await _unitOfWork.Game.OwnsCoachingSession(player, sessionId.Value, sessionType))
                throw new AppException("Không tìm thấy phiên phân tích của bạn.", HttpStatusCode.NotFound);
            return new() { status = await _unitOfWork.Game.SessionAnalysisState(player, sessionId.Value, sessionType) ?? "pending" };
        }
        var catalog = await catalogs.Load();
        var training = await _unitOfWork.Game.Training(player);
        var available = catalog.lessons.Where(d => TrainingEngine.Unlocked(training, d)).ToDictionary(d => d.id);
        string fallback = TrainingEngine.Recommended(training, catalog.lessons);
        var profile = settings.CoachingEnabled ? await _unitOfWork.Game.CoachingProfile(player, clock.GetUtcNow().UtcDateTime.AddDays(-30)) : null;
        CoachingSummary? summary = profile == null ? null : new() { status = "ready", analysis = GameJson.Read<CoachingAnalysis>(profile.Result), updatedAt = profile.UpdatedAt.ToString("O") };
        summary ??= new() { status = settings.CoachingEnabled ? "empty" : "disabled", analysis = new() };
        summary.analysis!.recommendations = summary.analysis.recommendations.Where(d => available.ContainsKey(d.id)).DistinctBy(d => d.id).Take(3).ToList();
        if (summary.analysis.recommendations.Count == 0 && available.TryGetValue(fallback, out var lesson))
            summary.analysis.recommendations.Add(new() { id = lesson.id, title = lesson.title, reason = "Tiếp tục lộ trình luyện tập hiện tại." });
        if (settings.CoachingEnabled)
        {
            var state = await _unitOfWork.Game.LatestAnalysisState(player);
            if (state != null && state != "completed") summary.status = state;
        }
        return summary;
    }
    public async Task<Entities.DTOs.RecommendationResponse> LegacyRecommendation(Guid player)
    {
        var summary = await Summary(player);
        var result = new Entities.DTOs.RecommendationResponse { CoachMessage = "Bài tập dựa trên buổi chơi gần đây và lộ trình đã mở khóa." };
        foreach (var recommendation in summary.analysis!.recommendations)
        {
            var drill = await _unitOfWork.Game.RecommendedDrill(recommendation.id);
            if (drill != null) result.RecommendedDrills.Add(drill);
        }
        var first=result.RecommendedDrills.FirstOrDefault();
        if(first!=null)
        {
            result.WeakestTechniqueName=first.TechniqueName;
            result.CurrentMasteryScore=await _unitOfWork.Game.Mastery(player, first.TechniqueId);
            result.CoachMessage=summary.analysis.recommendations.First().reason;
        }
        return result;
    }
    public async Task<string> BuildRequest(Guid batch)
    {
        var session = await _unitOfWork.Game.TelemetryBatch(batch) ?? throw Invalid("Missing telemetry session.");
        using var metadata = System.Text.Json.JsonDocument.Parse(session.Metadata);
        string mode = metadata.RootElement.GetProperty("mode").GetString()!;
        string gameVersion = "";
        if (metadata.RootElement.TryGetProperty("header", out var header) && header.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            using var details = System.Text.Json.JsonDocument.Parse(header.GetString()!);
            if (details.RootElement.TryGetProperty("gameVersion", out var version)) gameVersion = version.GetString() ?? "";
        }
        var rows = await _unitOfWork.Game.TelemetryShots(batch);
        var cutoff = clock.GetUtcNow().UtcDateTime.AddDays(-30);
        var onboarding = settings.ModelEnabled ? await _unitOfWork.Game.OnboardingShots(session.PlayerId, mode, cutoff) : new List<string>();
        var history = await _unitOfWork.Game.CoachingHistory(session.PlayerId, cutoff);
        var training = await _unitOfWork.Game.Training(session.PlayerId);
        var catalog = await catalogs.Load();
        return HybridJson.Write(new { sessionId = session.SessionId.ToString("N"), ruleVersion = "rules-v1", mode, gameVersion,
            shots = rows.SelectMany(row => GameJson.Read<List<ShotTelemetry>>(row)).OrderBy(s => s.sequence), history,
            useInitialTrainingEvidence = settings.ModelEnabled && mode == "training",
            onboardingShots = onboarding.SelectMany(row => GameJson.Read<List<ShotTelemetry>>(row)).Take(3000),
            drills = catalog.lessons.Select(d => new { d.id, d.title, d.tier, skill = d.skill.ToString(), prerequisites = d.prerequisites ?? [] }),
            completedLessons = training.lessons.Where(l => l.bestStars > 0).Select(l => l.id), fallbackLesson = TrainingEngine.Recommended(training, catalog.lessons),
            resumeActive = training.active is {completed:false} });
    }
}
