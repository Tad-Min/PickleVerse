using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Entities.DTOs;
using Entities.Exceptions;
using Entities.Models;
using PickleVerse.Progression;
using Repositories.UnitOfWork;
using Services.Interfaces;

namespace Services.Implementations;

public sealed class ExpertReviewService(IUnitOfWork unit, TimeProvider clock) : IExpertReviewService
{
    static readonly HashSet<string> Codes = new(StringComparer.Ordinal)
    {
        "Early", "Late", "LateServe", "Kitchen", "Momentum", "Net", "Out", "Target", "Movement",
        "DinkTooHigh", "ServeFeet", "WrongInput", "Spacing", "Miss", "DoubleBounce"
    };

    public Task<List<ReviewableTrainingSession>> Sessions(Guid? player, int page)
    {
        if (page is < 1 or > 100000) throw new BadRequestException("Trang phiên đánh giá không hợp lệ.");
        return unit.Game.ReviewableSessions(player, (page - 1) * 30, 30);
    }

    public Task<List<CoachingDemoSession>> DemoSessions() => unit.Game.DemoCoachingSessions();

    public async Task<CoachingDemoEvidence> DemoEvidence(string sessionId)
    {
        var row = await unit.Game.DemoCoachingSession(sessionId)
            ?? throw new NotFoundException("Không tìm thấy phiên mô phỏng.");
        return new CoachingDemoEvidence(new CoachingDemoSession(row.SessionId, row.PlayerCode, row.Tier, row.Split),
            row.GameVersion, GameJson.Read<List<ShotTelemetry>>(row.Shots));
    }

    static ExpertReviewReceipt Receipt(ExpertReviewRecord row) => new()
    {
        ReviewId = row.ReviewId, ReviewerId = row.ReviewerId, Version = row.Version, CreatedAt = row.CreatedAt,
        Review = GameJson.Read<ExpertReviewRequest>(row.Data)
    };

    public async Task<ExpertSessionEvidence> Evidence(Guid batch)
    {
        var session = await unit.Game.ReviewableSession(batch)
            ?? throw new NotFoundException("Phiên luyện tập chưa có đủ bằng chứng hoặc không thuộc dữ liệu người chơi thật.");
        var telemetry = await unit.Game.TelemetryBatch(batch) ?? throw new NotFoundException("Không tìm thấy telemetry.");
        var latest = await unit.Game.LatestExpertReview(batch);
        var parts = await unit.Game.TelemetryShots(batch);
        if (parts.Count != telemetry.PartCount) throw new NotFoundException("Dữ liệu cú đánh không còn đầy đủ.");
        using var metadata = JsonDocument.Parse(telemetry.Metadata);
        string gameVersion = "";
        if (metadata.RootElement.TryGetProperty("header", out var header) && header.ValueKind == JsonValueKind.String)
        {
            using var details = JsonDocument.Parse(header.GetString()!);
            if (details.RootElement.TryGetProperty("gameVersion", out var version)) gameVersion = version.GetString() ?? "";
        }
        var attempt = await unit.Game.TrainingAttempt(session.PlayerId, session.SessionId);
        if (attempt != null)
            session.LessonTitle = GameJson.Read<TrainingAttempt>(attempt.Data).definition?.title ?? session.LessonTitle;
        return new ExpertSessionEvidence
        {
            Session = session, GameVersion = gameVersion,
            Shots = parts.SelectMany(p => GameJson.Read<List<ShotTelemetry>>(p)).OrderBy(s => s.sequence).ToList(),
            LatestReview = latest == null ? null : Receipt(latest)
        };
    }

    static bool ValidText(string? value, int max) => value != null && value.Trim().Length is >= 3 && value.Length <= max;

    public async Task<ExpertReviewReceipt> Review(Guid actor, Guid batch, ExpertReviewRequest request)
    {
        if (!await unit.Game.IsAdmin(actor)) throw new ForbiddenException("Bạn không có quyền xác nhận nhãn chuyên gia.");
        if (!request.ConfirmedHumanSession || request.Split == null || !Enum.IsDefined(request.Split.Value)
            || request.Tier.HasValue && !Enum.IsDefined(request.Tier.Value)
            || !ValidText(request.ExpertName, 120) || !ValidText(request.Observation, 2000)
            || request.ShotLabels == null || request.ShotLabels.Count > 1000 || request.Tier == null && request.ShotLabels.Count == 0)
            throw new BadRequestException("Hãy xác nhận phiên người thật, người đánh giá, bằng chứng và ít nhất một nhãn.");

        await using var tx = await unit.Game.BeginTransactionAsync();
        await unit.Game.Lock("expert-review:" + batch.ToString("N"));
        var evidence = await Evidence(batch);
        long version = evidence.LatestReview?.Version ?? 0;
        if (version != request.ExpectedVersion)
            throw new AppException("Phiên đã có bản đánh giá mới. Hãy tải lại bằng chứng.", HttpStatusCode.Conflict);
        var shots = evidence.Shots.ToDictionary(s => s.shotId);
        var distinctLabels = new HashSet<(string, string)>();
        foreach (var label in request.ShotLabels)
        {
            if (label == null || !Codes.Contains(label.Code) || label.Present == null || !ValidText(label.Evidence, 1000)
                || string.IsNullOrEmpty(label.ShotId) || !shots.TryGetValue(label.ShotId, out var shot)
                || !distinctLabels.Add((label.ShotId, label.Code)))
                throw new BadRequestException("Nhãn cú đánh bị trùng, thiếu bằng chứng hoặc không thuộc phiên.");
            if (label.Code == "DinkTooHigh" && (shot.kind != "Dink" || shot.netClearance == null))
                throw new BadRequestException("Nhãn quỹ đạo Dink cần cú Dink có số đo qua lưới.");
        }
        var row = new ExpertReviewRecord
        {
            ReviewId = Guid.NewGuid(), BatchId = batch, ReviewerId = actor, Version = version + 1,
            Data = GameJson.Write(request), CreatedAt = clock.GetUtcNow().UtcDateTime
        };
        unit.Game.AddExpertReview(row);
        await unit.SaveChangesAsync();
        await tx.CommitAsync();
        return Receipt(row);
    }

    static string Digest(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    public async Task<ExpertDatasetFile> Export(bool tierOnly)
    {
        await using var tx = await unit.Game.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var output = new List<object>();
        foreach (var player in (await unit.Game.ReviewedSessions()).GroupBy(s => s.PlayerId))
        {
            var initial = tierOnly ? (await unit.Game.InitialTrainingBatches(player.Key)).ToHashSet() : null;
            var partitions = new HashSet<DatasetSplit>();
            var tiers = new HashSet<ExpertTier>();
            foreach (var item in player)
            {
                var evidence = await Evidence(item.BatchId);
                var latest = evidence.LatestReview!;
                var review = latest.Review;
                if (review.Split == null || !review.ConfirmedHumanSession) continue;
                partitions.Add(review.Split.Value);
                if (review.Tier.HasValue && initial?.Contains(item.BatchId) == true) tiers.Add(review.Tier.Value);
                if (tierOnly && (!review.Tier.HasValue || initial?.Contains(item.BatchId) != true)) continue;
                if (!tierOnly && review.ShotLabels.Count == 0) continue;
                output.Add(new
                {
                    playerId = Digest(Encoding.UTF8.GetBytes("pickleverse-expert:" + player.Key.ToString("N"))),
                    tier = review.Tier, split = review.Split, labelSource = "expert", synthetic = false,
                    labelVersion = latest.Version, expert = review.ExpertName, reviewerId = latest.ReviewerId,
                    reviewedAt = latest.CreatedAt, observation = review.Observation, shotLabels = review.ShotLabels,
                    completedAt = item.CompletedAt,
                    session = new { sessionId = item.SessionId.ToString("N"), mode = "training", gameVersion = evidence.GameVersion, shots = evidence.Shots }
                });
            }
            if (partitions.Count > 1 || tiers.Count > 1)
                throw new AppException($"Người chơi {player.Key} có nhãn trình độ hoặc nhóm dữ liệu không nhất quán.", HttpStatusCode.Conflict);
        }
        var bytes = Encoding.UTF8.GetBytes(HybridJson.Write(output));
        string kind = tierOnly ? "tier" : "coaching";
        await tx.CommitAsync();
        return new ExpertDatasetFile(bytes, Digest(bytes), $"pickleverse-{kind}-{clock.GetUtcNow():yyyyMMdd-HHmmss}.json");
    }
}
