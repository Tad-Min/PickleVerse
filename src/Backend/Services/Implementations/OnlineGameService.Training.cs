using PickleVerse.Progression;

namespace Services.Implementations;

public sealed partial class OnlineGameService
{
    public Task<GameSnapshot> StartTraining(Guid id, StartTrainingRequest request) => Execute(id, request.RequestId, "training-start", request, async s =>
    {
        if (s.Player.pendingMatch != null)
            throw Conflict("Bạn cần hoàn tất hoặc rời trận hiện tại trước khi luyện tập.");
        var training = TrainingEngine.Ensure(s.Player);
        if (training.active is { completed: false } active && active.lessonId == request.LessonCode && !request.Retry) return;
        var lesson = s.catalog.lessons.SingleOrDefault(d => d.id == request.LessonCode) ?? throw Bad("Bài học không tồn tại.");
        if (!TrainingEngine.Unlocked(training, lesson))
            throw Bad("Hãy hoàn thành các bài học điều kiện trước.");
        if (s.Player.level < lesson.requiredLevel) throw Bad("Bạn chưa đủ cấp độ để luyện bài này.");
        var attempt = TrainingEngine.Start(s.Player, lesson.id, request.Retry, s.catalog.lessons);
        attempt.catalogVersion = s.catalog.version;
        attempt.definition = lesson;
        attempt.progressEpoch = s.profile.LearningEpoch;
        s.trainingChanged=true;
        var rows = await _unitOfWork.Game.StartTraining(id, attempt, Now);
        if (rows != 1)
            throw Bad("Danh mục bài học chưa được liên kết đủ.");
    });

    public Task<GameSnapshot> UpdateTraining(Guid id, TrainingUpdateRequest request, bool complete) => Execute(id, request.RequestId, complete ? "training-complete" : "training-checkpoint", request, async s =>
    {
        var input = request.Attempt;
        if (!Guid.TryParse(input.id, out var attemptId))
            throw Bad("Mã lượt tập không hợp lệ.");
        var row = await _unitOfWork.Game.TrainingAttempt(id, attemptId) ?? throw Bad("Không tìm thấy lượt tập của bạn.");
        if (row.Completed)
            throw Conflict("Lượt tập đã hoàn tất. Hãy tải lại tiến trình.");
        var original = GameJson.Read<TrainingAttempt>(row.Data);
        if (row.ProgressEpoch != s.profile.LearningEpoch) throw Conflict("Tiến trình đã được đặt lại. Hãy tải hồ sơ mới.");
        var training = TrainingEngine.Ensure(s.Player);
        if (training.active?.id != original.id)
            throw Conflict("Lượt tập hiện tại đã thay đổi trên thiết bị khác.");
        var snapshot = string.IsNullOrEmpty(row.CatalogVersion) ? s.catalog : GameJson.Read<GameCatalog>(
            await _unitOfWork.Game.CatalogSnapshot(row.CatalogVersion) ?? throw Bad("Không tìm thấy phiên bản bài học."));
        var lesson = snapshot.lessons.Single(d => d.id == original.lessonId);
        if (input.lessonId != original.lessonId || input.playerId != s.Player.id || input.trials < original.trials || input.trials > lesson.trials || input.successes < original.successes || input.successes > input.trials || input.contacts < original.contacts || input.contacts > lesson.trials || input.seconds < original.seconds || !double.IsFinite(input.seconds) || input.seconds > Math.Max(0, (Now - row.CreatedAt).TotalSeconds) + 5 || !double.IsFinite(input.timingTotal) || input.timingTotal < original.timingTotal || input.timingTotal > lesson.trials * 30 || input.errors == null || input.errors.Any(e => !Enum.IsDefined(e.error) || e.count < 0 || e.count > lesson.trials * 8) || input.errors.Select(e => e.error).Distinct().Count() != input.errors.Count)
            throw Bad("Số liệu lượt tập không hợp lệ hoặc đã cũ.");
        if (complete && input.trials != lesson.trials)
            throw Bad("Chưa đủ số lượt đánh của bài học.");
        // Only measurements are accepted. Player, lesson, review and rewards remain server-owned.
        original.trials = input.trials;
        original.successes = input.successes;
        original.contacts = input.contacts;
        original.seconds = input.seconds;
        original.timingTotal = input.timingTotal;
        original.errors = input.errors;
        training.active = original;
        if (complete)
        {
            s.trainingChanged=true;
            if (!TrainingEngine.Complete(s.Player, original, snapshot.lessons))
                throw Conflict("Không thể hoàn tất lượt tập.");
            if (original.passed && !original.review && !await _unitOfWork.Game.GrantOnce(id, "lesson:" + lesson.id,
                GameJson.Write(new { coins = original.rewardTokenpad, lesson = lesson.id }), Now))
            {
                s.Player.coins -= original.rewardTokenpad;
                original.rewardTokenpad = 0;
            }
            await _unitOfWork.Game.RecordLesson(id, lesson.id, original.stars, original.passed);
            await _unitOfWork.Game.IncrementMastery(id, lesson.skill.ToString(), original.trials, original.trials - original.successes, Now);
        }
        await _unitOfWork.Game.UpdateTraining(attemptId, GameJson.Write(original), complete, Now, complete ? TrainingFingerprint(original) : null);
    });
}
