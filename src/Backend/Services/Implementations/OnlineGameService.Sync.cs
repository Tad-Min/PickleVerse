using System.Net;
using Entities.Exceptions;
using Entities.Models;
using PickleVerse.Progression;

namespace Services.Implementations;

public sealed partial class OnlineGameService
{
    public Task<GameSnapshot> Onboarding(Guid player, OnboardingRequest request) => Execute(player, request.RequestId, "onboarding", request, s =>
    {
        if (request.Choice is not ("NEW" or "EXPERIENCED")) throw Bad("Lựa chọn kinh nghiệm không hợp lệ.");
        if (s.profile.ExperienceChoice != null && s.profile.ExperienceChoice != request.Choice) throw Conflict("Bạn đã hoàn tất lựa chọn ban đầu.");
        s.profile.ExperienceChoice = request.Choice;
        return Task.CompletedTask;
    });

    static string TrainingFingerprint(TrainingAttempt a) => Hash(GameJson.Payload(new { a.id, a.playerId, a.lessonId, a.catalogVersion, a.progressEpoch,
        a.trials, a.successes, a.contacts, a.seconds, a.timingTotal, errors = a.errors.OrderBy(e => e.error).Select(e => new { e.error, e.count }).ToArray() }));

    static void ValidateMeasurements(TrainingAttempt input, TrainingDrill lesson)
    {
        if (input.trials != lesson.trials || input.successes < 0 || input.successes > input.trials || input.contacts < input.successes
            || input.contacts > input.trials || !double.IsFinite(input.seconds) || input.seconds < 0 || input.seconds > 86400
            || !double.IsFinite(input.timingTotal) || input.timingTotal < 0 || input.timingTotal > input.trials * 30
            || input.errors == null || input.errors.Any(e => e == null || !Enum.IsDefined(e.error) || e.count < 0 || e.count > input.trials * 8)
            || input.errors.Select(e => e.error).Distinct().Count() != input.errors.Count)
            throw Bad("Số liệu lượt luyện tập không hợp lệ hoặc chưa hoàn tất.");
    }

    public async Task<TrainingSyncResult> SyncTraining(Guid player, TrainingSyncRequest request)
    {
        if (request.Attempts == null || request.Attempts.Count is < 1 or > 50) throw Bad("Mỗi lần chỉ đồng bộ từ 1 đến 50 lượt.");
        var result = new TrainingSyncResult();
        foreach (var input in request.Attempts)
        {
            var receipt = new TrainingSyncReceipt { AttemptId = input?.id ?? "", Status = SyncItemStatus.Rejected };
            result.Receipts.Add(receipt); _unitOfWork.Game.ClearTracking();
            try
            {
                if (input == null || !Guid.TryParseExact(input.id, "N", out var attemptId) || input.playerId != player.ToString("N")
                    || string.IsNullOrEmpty(input.catalogVersion) || input.catalogVersion.Length > 64 || input.errors == null || input.errors.Any(e => e == null))
                    throw Bad("Lượt tập không thuộc tài khoản hoặc không có phiên bản danh mục.");
                // Check before Execute too: an old idempotency receipt must not revive a reset epoch.
                if (input.progressEpoch != await _unitOfWork.Game.LearningEpoch(player)) throw Conflict("Tiến trình đã được đặt lại; lượt cũ không được áp dụng.");
                string fingerprint = TrainingFingerprint(input); bool applied = false;
                var snapshot = await Execute(player, "sync:" + input.id, "training-sync", new { attempt = input.id, fingerprint }, async s =>
                {
                    if (input.progressEpoch != s.profile.LearningEpoch) throw Conflict("Tiến trình đã được đặt lại; lượt cũ không được áp dụng.");
                    await _unitOfWork.Game.Lock("training-attempt:" + input.id);
                    var existing = await _unitOfWork.Game.TrainingAttempt(player, attemptId);
                    if (existing?.Completed == true)
                    {
                        if ((existing.Fingerprint.Length == 0 ? TrainingFingerprint(GameJson.Read<TrainingAttempt>(existing.Data)) : existing.Fingerprint) != fingerprint)
                            throw Conflict("Mã lượt tập đã được dùng cho số liệu khác.");
                        receipt.Result = GameJson.Read<TrainingAttempt>(existing.Data); return;
                    }
                    if (existing == null && await _unitOfWork.Game.AttemptIdUsed(attemptId)) throw Conflict("Mã lượt tập đã được sử dụng.");
                    var saved = await _unitOfWork.Game.CatalogSnapshot(input.catalogVersion) ?? throw Bad("Không tìm thấy danh mục đã tải cho lượt tập này.");
                    var catalog = GameJson.Read<GameCatalog>(saved);
                    var lesson = catalog.lessons.SingleOrDefault(d => d.id == input.lessonId) ?? throw Bad("Bài học không thuộc danh mục đã tải.");
                    ValidateMeasurements(input, lesson);
                    if (existing != null && (existing.CatalogVersion != input.catalogVersion || existing.ProgressEpoch != input.progressEpoch))
                        throw Conflict("Lượt tập không khớp phiên bản đã bắt đầu.");
                    if (existing != null)
                    {
                        var checkpoint = GameJson.Read<TrainingAttempt>(existing.Data);
                        if (input.trials < checkpoint.trials || input.successes < checkpoint.successes || input.contacts < checkpoint.contacts
                            || input.seconds < checkpoint.seconds || input.timingTotal < checkpoint.timingTotal)
                            throw Conflict("Số liệu cũ hơn checkpoint đã lưu.");
                    }
                    var training = TrainingEngine.Ensure(s.Player);
                    if (!TrainingEngine.Unlocked(training, lesson) || s.Player.level < lesson.requiredLevel)
                    { receipt.Retryable = true; throw Bad("Cần đồng bộ bài điều kiện hoặc đạt cấp độ trước."); }
                    var original = new TrainingAttempt { id = input.id, lessonId = lesson.id, playerId = s.Player.id,
                        catalogVersion = input.catalogVersion, definition = lesson, progressEpoch = input.progressEpoch, trials = input.trials, successes = input.successes,
                        contacts = input.contacts, seconds = input.seconds, timingTotal = input.timingTotal, errors = input.errors };
                    var active = training.active;
                    if (existing == null)
                    {
                        var row = TrainingEngine.Record(training, lesson.id);
                        if (row.attempts > 0) row.retries++;
                        row.attempts++;
                    }
                    training.active = original;
                    if (!TrainingEngine.Complete(s.Player, original, catalog.lessons)) throw Bad("Không thể áp dụng kết quả luyện tập.");
                    if (original.passed && !original.review && !await _unitOfWork.Game.GrantOnce(player, "lesson:" + lesson.id,
                        GameJson.Write(new { coins = original.rewardTokenpad, lesson = lesson.id }), Now))
                    { s.Player.coins -= original.rewardTokenpad; original.rewardTokenpad = 0; }
                    training.active = active?.id == original.id ? original : active;
                    s.trainingChanged = true;
                    await _unitOfWork.Game.RecordLesson(player, lesson.id, original.stars, original.passed);
                    await _unitOfWork.Game.IncrementMastery(player, lesson.skill.ToString(), original.trials, original.trials - original.successes, Now);
                    if (existing == null)
                    {
                        var links = await _unitOfWork.Game.LessonLinks(lesson.id);
                        if (links.Count != 1) throw Bad("Bài học chưa có drill phù hợp.");
                        _unitOfWork.Game.AddSyncedTraining(new TrainingSessionRecord { AttemptId = attemptId, PlayerId = player, LessonId = links[0].lesson,
                            DrillId = links[0].drill, Data = GameJson.Write(original), Completed = true, IsCurrent = false,
                            CatalogVersion = input.catalogVersion, ProgressEpoch = input.progressEpoch, Fingerprint = fingerprint, CreatedAt = Now, UpdatedAt = Now });
                    }
                    else await _unitOfWork.Game.UpdateTraining(attemptId, GameJson.Write(original), true, Now, fingerprint);
                    receipt.Result = original; applied = true;
                });
                receipt.Status = applied ? SyncItemStatus.Applied : SyncItemStatus.Duplicate;
                receipt.Message = applied ? "Đã đồng bộ lượt tập." : "Lượt tập đã được đồng bộ trước đó.";
                result.Snapshot = snapshot;
            }
            catch (AppException error) when ((int)error.StatusCode < 500) { receipt.Message = error.Message; }
        }
        _unitOfWork.Game.ClearTracking();
        result.Snapshot = await Read(player);
        return result;
    }
}
