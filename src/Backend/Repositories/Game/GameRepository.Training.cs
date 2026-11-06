using Entities.DTOs;
using Entities.Models;
using Microsoft.EntityFrameworkCore;
using PickleVerse.Progression;

namespace Repositories.Game;

public sealed partial class GameRepository
{
    public async Task<TrainingProfile> Training(Guid player)
    {
        long epoch = await LearningEpoch(player);
        var records = await db.Playerlessondrills.AsNoTracking().Where(p => p.Playerid == player && p.LessonDrill.Lesson.Code != null)
            .OrderBy(p => p.LessonDrill.Lesson.SortOrder).ThenBy(p => p.Lessonid)
            .Select(p => new { Code = p.LessonDrill.Lesson.Code!, Row = p }).ToListAsync();
        var training = new TrainingProfile { lessons = records.Select(p => new TrainingLessonRecord { id = p.Code,
            attempts = p.Row.Attempts, retries = p.Row.Retries, completions = p.Row.Completions, bestStars = p.Row.Score,
            trials = p.Row.Trials, successes = p.Row.Successes, seconds = p.Row.Seconds, bestAccuracy = p.Row.BestAccuracy,
            errors = GameJson.Read<List<TrainingErrorCount>>(p.Row.Errors) }).ToList() };
        var active = await db.TrainingSessionRecords.AsNoTracking().Where(s => s.PlayerId == player && s.IsCurrent && s.ProgressEpoch == epoch).Select(s => s.Data).SingleOrDefaultAsync();
        if (active != null) { training.active = GameJson.Read<TrainingAttempt>(active); training.lastLesson = training.active.lessonId; }
        var completed = await (from session in db.TrainingSessionRecords.AsNoTracking()
            join lesson in db.Lessons on session.LessonId equals lesson.Lessonid
            where session.PlayerId == player && session.Completed && session.ProgressEpoch == epoch
            orderby session.Sequence
            select new { session.AttemptId, session.LessonId, session.Data, lesson.Code }).ToListAsync();
        training.completedAttempts = completed.Select(s => s.AttemptId.ToString("N")).ToList();
        foreach (var group in completed.GroupBy(s => s.LessonId))
            foreach (var row in group.TakeLast(8))
                training.lessons.FirstOrDefault(l => l.id == row.Code)?.accuracyHistory.Add((float)GameJson.Read<TrainingAttempt>(row.Data).Accuracy);
        return training;
    }
    public async Task SaveTraining(Guid player, TrainingProfile training)
    {
        foreach (var row in training.lessons)
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO playerlessondrills(playerid,lessonid,drillid,score,isclaimedreward,attempts,retries,completions,trials,successes,seconds,bestaccuracy,errors)
                SELECT {player},l.lessonid,ld.drillid,{row.bestStars},{row.bestStars > 0},{row.attempts},{row.retries},{row.completions},
                    {row.trials},{row.successes},{row.seconds},{row.bestAccuracy},{GameJson.Write(row.errors)}::jsonb
                FROM lessons l JOIN lessondrills ld USING(lessonid) JOIN drills d USING(drillid) WHERE l.code={row.id} AND d.code=l.code
                ON CONFLICT(playerid,lessonid,drillid) DO UPDATE SET score=EXCLUDED.score,isclaimedreward=EXCLUDED.isclaimedreward,
                    attempts=EXCLUDED.attempts,retries=EXCLUDED.retries,completions=EXCLUDED.completions,trials=EXCLUDED.trials,
                    successes=EXCLUDED.successes,seconds=EXCLUDED.seconds,bestaccuracy=EXCLUDED.bestaccuracy,errors=EXCLUDED.errors
                """);
    }
    public async Task<int> StartTraining(Guid player, TrainingAttempt attempt, DateTime now)
    {
        var links = await LessonLinks(attempt.lessonId);
        if (links.Count != 1) return 0;
        await db.TrainingSessionRecords.Where(s => s.PlayerId == player && s.IsCurrent).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsCurrent, false));
        var row = links[0];
        db.TrainingSessionRecords.Add(new TrainingSessionRecord { AttemptId = Guid.Parse(attempt.id), PlayerId = player,
            LessonId = row.lesson, DrillId = row.drill, Data = GameJson.Write(attempt), CatalogVersion = attempt.catalogVersion ?? "",
            ProgressEpoch = attempt.progressEpoch, IsCurrent = true, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();
        return 1;
    }
    public Task<TrainingSessionRecord?> TrainingAttempt(Guid player, Guid attempt) => db.TrainingSessionRecords.AsNoTracking()
        .SingleOrDefaultAsync(s => s.PlayerId == player && s.AttemptId == attempt);
    public Task UpdateTraining(Guid attempt, string data, bool completed, DateTime now, string? fingerprint = null) => db.TrainingSessionRecords.Where(s => s.AttemptId == attempt)
        .ExecuteUpdateAsync(s => s.SetProperty(x => x.Data, data).SetProperty(x => x.Completed, completed).SetProperty(x => x.UpdatedAt, now)
            .SetProperty(x => x.Fingerprint, x => fingerprint ?? x.Fingerprint));
    public Task RecordLesson(Guid player, string code, int stars, bool claimed) => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO playerlessondrills(playerid,lessonid,drillid,score,isclaimedreward)
        SELECT {player},l.lessonid,d.drillid,{stars},{claimed} FROM lessons l JOIN drills d ON d.code=l.code WHERE l.code={code}
        ON CONFLICT(playerid,lessonid,drillid) DO UPDATE SET score=GREATEST(playerlessondrills.score,EXCLUDED.score),
            isclaimedreward=playerlessondrills.isclaimedreward OR EXCLUDED.isclaimedreward
        """);
    public Task RecordMastery(Guid player, string skill, int attempts, int errors, DateTime now) => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO playertechniquemastery(playerid,techniqueid,totalattempts,totalerrors,lastupdatedat)
        SELECT {player},techniqueid,{attempts},{errors},{now} FROM techniques WHERE techniquename={skill}
        ON CONFLICT(playerid,techniqueid) DO UPDATE SET totalattempts=EXCLUDED.totalattempts,totalerrors=EXCLUDED.totalerrors,lastupdatedat=EXCLUDED.lastupdatedat
        """);
    public sealed record LessonLink(int lesson, string code, int drill, int technique);
    public Task<List<LessonLink>> LessonLinks(string? code = null) => (from l in db.Lessons join d in db.Drills on l.Code equals d.Code
        where l.Code != null && (code == null || l.Code == code)
        select new LessonLink(l.Lessonid, l.Code!, d.Drillid, d.Techniqueid)).ToListAsync();
    public Task<DrillDto?> RecommendedDrill(string code) => (from drill in db.Drills
        join ld in db.Lessondrills on drill.Drillid equals ld.Drillid
        where drill.Code == code orderby ld.Lessonid
        select new DrillDto { DrillId = drill.Drillid, TechniqueId = drill.Techniqueid, DrillName = drill.Drillname,
            Description = drill.Description, Target = (int)ld.Target, TechniqueName = drill.Technique.Techniquename }).FirstOrDefaultAsync();
    public async Task<int> Mastery(Guid player, int technique) => Convert.ToInt32(await db.Playertechniquemasteries
        .Where(p => p.Playerid == player && p.Techniqueid == technique).Select(p => p.Masteryscore).SingleOrDefaultAsync());
}
