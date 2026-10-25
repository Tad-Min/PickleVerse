using PickleVerse.Progression;
using static Services.Game.GameSql;

namespace Services.Game;

public sealed partial class OnlineGameService
{
    public Task<GameSnapshot> StartTraining(Guid id, StartTrainingRequest request) => Execute(id, request.RequestId, "training-start", request, async s =>
    {
        if (s.Player.pendingMatch != null)
            throw Conflict("Bạn cần hoàn tất hoặc rời trận hiện tại trước khi luyện tập.");
        var lesson = s.catalog.lessons.SingleOrDefault(d => d.id == request.LessonCode) ?? throw Bad("Bài học không tồn tại.");
        var data = TrainingEngine.Ensure(s.Player);
        if (!TrainingEngine.Unlocked(data, lesson))
            throw Bad("Hãy hoàn thành các bài học điều kiện trước.");
        if (data.active is { completed: false } active && active.lessonId == lesson.id && !request.Retry)
            return;
        var attempt = TrainingEngine.Start(s.Player, lesson.id, request.Retry);
        var rows = await sql.Run("INSERT INTO trainingsessions(attemptid,playerid,lessonid,drillid,data,createdat,updatedat) SELECT @attempt,@player,l.lessonid,d.drillid,@data::jsonb,@now,@now FROM lessons l JOIN drills d ON d.code=l.code WHERE l.code=@code", P("attempt", Guid.Parse(attempt.id)), P("player", id), P("data", GameJson.Write(attempt)), P("now", Now), P("code", lesson.id));
        if (rows != 1)
            throw Bad("Danh mục bài học chưa được liên kết đủ.");
    });

    public Task<GameSnapshot> UpdateTraining(Guid id, TrainingUpdateRequest request, bool complete) => Execute(id, request.RequestId, complete ? "training-complete" : "training-checkpoint", request, async s =>
    {
        var input = request.Attempt;
        if (!Guid.TryParse(input.id, out var attemptId))
            throw Bad("Mã lượt tập không hợp lệ.");
        var row = await sql.One("SELECT data::text,completed,createdat FROM trainingsessions WHERE attemptid=@attempt AND playerid=@player", r => new { data = r.GetString(0), completed = r.GetBoolean(1), started = r.GetDateTime(2) }, P("attempt", attemptId), P("player", id)) ?? throw Bad("Không tìm thấy lượt tập của bạn.");
        if (row.completed)
            throw Conflict("Lượt tập đã hoàn tất. Hãy tải lại tiến trình.");
        var original = GameJson.Read<TrainingAttempt>(row.data);
        var data = TrainingEngine.Ensure(s.Player);
        if (data.active?.id != original.id)
            throw Conflict("Lượt tập hiện tại đã thay đổi trên thiết bị khác.");
        var lesson = s.catalog.lessons.Single(d => d.id == original.lessonId);
        if (input.lessonId != original.lessonId || input.playerId != s.Player.id || input.trials < original.trials || input.trials > lesson.trials || input.successes < original.successes || input.successes > input.trials || input.contacts < original.contacts || input.contacts > lesson.trials || input.seconds < original.seconds || !double.IsFinite(input.seconds) || input.seconds > Math.Max(0, (Now - row.started).TotalSeconds) + 5 || !double.IsFinite(input.timingTotal) || input.timingTotal < original.timingTotal || input.timingTotal > lesson.trials * 30 || input.errors == null || input.errors.Any(e => !Enum.IsDefined(e.error) || e.count < 0 || e.count > lesson.trials * 8) || input.errors.Select(e => e.error).Distinct().Count() != input.errors.Count)
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
        data.active = original;
        if (complete)
        {
            if (!TrainingEngine.Complete(s.Player, original))
                throw Conflict("Không thể hoàn tất lượt tập.");
            await sql.Run("INSERT INTO playerlessondrills(playerid,lessonid,drillid,score,isclaimedreward) SELECT @player,l.lessonid,d.drillid,@stars,@claimed FROM lessons l JOIN drills d ON d.code=l.code WHERE l.code=@code ON CONFLICT(playerid,lessonid,drillid) DO UPDATE SET score=GREATEST(playerlessondrills.score,EXCLUDED.score),isclaimedreward=playerlessondrills.isclaimedreward OR EXCLUDED.isclaimedreward", P("player", id), P("stars", original.stars), P("claimed", original.passed), P("code", lesson.id));
            var aggregate = data.lessons.Single(x => x.id == lesson.id);
            await sql.Run("INSERT INTO playertechniquemastery(playerid,techniqueid,totalattempts,totalerrors,lastupdatedat) SELECT @player,techniqueid,@attempts,@errors,@now FROM techniques WHERE techniquename=@skill ON CONFLICT(playerid,techniqueid) DO UPDATE SET totalattempts=EXCLUDED.totalattempts,totalerrors=EXCLUDED.totalerrors,lastupdatedat=EXCLUDED.lastupdatedat", P("player", id), P("attempts", aggregate.trials), P("errors", aggregate.trials - aggregate.successes), P("now", Now), P("skill", lesson.skill.ToString()));
        }
        await sql.Run("UPDATE trainingsessions SET data=@data::jsonb,completed=@complete,updatedat=@now WHERE attemptid=@attempt", P("data", GameJson.Write(original)), P("complete", complete), P("now", Now), P("attempt", attemptId));
    });
}
