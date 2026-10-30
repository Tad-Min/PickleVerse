using PickleVerse.Progression;
using static Services.Game.GameSql;

namespace Services.Game;

// Lesson progress is authoritative in PlayerLessonDrills; a live attempt lives in TrainingSessions.
public static class RelationalTraining
{
    public static async Task<TrainingProfile> Load(GameSql sql, Guid player)
    {
        var training = new TrainingProfile();
        training.lessons = await sql.Query("""
            SELECT l.code,p.attempts,p.retries,p.completions,p.score,p.trials,p.successes,p.seconds,p.bestaccuracy,p.errors::text
            FROM playerlessondrills p JOIN lessons l USING(lessonid)
            WHERE p.playerid=@player AND l.code IS NOT NULL ORDER BY l.sortorder,l.lessonid
            """, r => new TrainingLessonRecord { id=r.GetString(0),attempts=r.GetInt32(1),retries=r.GetInt32(2),completions=r.GetInt32(3),
                bestStars=r.GetInt32(4),trials=r.GetInt32(5),successes=r.GetInt32(6),seconds=r.GetDouble(7),bestAccuracy=r.GetDouble(8),
                errors=GameJson.Read<List<TrainingErrorCount>>(r.GetString(9)) }, P("player",player));
        var active = await sql.One("SELECT data::text FROM trainingsessions WHERE playerid=@player AND iscurrent", r=>r.GetString(0),P("player",player));
        if (active != null)
        {
            training.active=GameJson.Read<TrainingAttempt>(active);
            training.lastLesson=training.active.lessonId;
        }
        training.completedAttempts=await sql.Query("SELECT replace(attemptid::text,'-','') FROM trainingsessions WHERE playerid=@player AND completed",r=>r.GetString(0),P("player",player));
        // Reuse existing attempt measurements for the graph; do not store a second history in progress JSON.
        var accuracy=await sql.Query("""
            SELECT l.code,s.data::text FROM (
                SELECT lessonid,data,sequence,row_number() OVER(PARTITION BY lessonid ORDER BY sequence DESC) AS position
                FROM trainingsessions WHERE playerid=@player AND completed
            ) s JOIN lessons l USING(lessonid) WHERE s.position<=8 ORDER BY s.sequence
            """,r=>(lesson:r.GetString(0),attempt:GameJson.Read<TrainingAttempt>(r.GetString(1))),P("player",player));
        foreach(var row in accuracy)
            training.lessons.FirstOrDefault(l=>l.id==row.lesson)?.accuracyHistory.Add((float)row.attempt.Accuracy);
        return training;
    }

    public static async Task Save(GameSql sql, Guid player, TrainingProfile training)
    {
        foreach(var row in training.lessons)
            await sql.Run("""
                INSERT INTO playerlessondrills(playerid,lessonid,drillid,score,isclaimedreward,attempts,retries,completions,trials,successes,seconds,bestaccuracy,errors)
                SELECT @player,l.lessonid,ld.drillid,@score,@claimed,@attempts,@retries,@completions,@trials,@successes,@seconds,@accuracy,@errors::jsonb
                FROM lessons l JOIN lessondrills ld USING(lessonid) JOIN drills d USING(drillid) WHERE l.code=@code AND d.code=l.code
                ON CONFLICT(playerid,lessonid,drillid) DO UPDATE SET score=EXCLUDED.score,isclaimedreward=EXCLUDED.isclaimedreward,
                    attempts=EXCLUDED.attempts,retries=EXCLUDED.retries,completions=EXCLUDED.completions,trials=EXCLUDED.trials,
                    successes=EXCLUDED.successes,seconds=EXCLUDED.seconds,bestaccuracy=EXCLUDED.bestaccuracy,errors=EXCLUDED.errors
                """,P("player",player),P("code",row.id),P("score",row.bestStars),P("claimed",row.bestStars>0),
                P("attempts",row.attempts),P("retries",row.retries),P("completions",row.completions),P("trials",row.trials),
                P("successes",row.successes),P("seconds",row.seconds),P("accuracy",row.bestAccuracy),P("errors",GameJson.Write(row.errors)));
    }
}
