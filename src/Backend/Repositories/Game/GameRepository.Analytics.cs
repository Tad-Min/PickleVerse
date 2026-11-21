using Entities.DTOs;
using Entities.Models;
using Microsoft.EntityFrameworkCore;
using PickleVerse.Multiplayer;

namespace Repositories.Game;
public sealed partial class GameRepository
{
    static readonly Guid[] SystemBots = RankedBotAccounts.Ids.Select(Guid.Parse).ToArray();
    IQueryable<User> HumanPlayers => db.Users.AsNoTracking().Where(u=>u.Role.Rolename=="Player" && !SystemBots.Contains(u.Userid));
    public Task RecordLogin(Guid player, DateTime at) => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO playerlogindays(playerid,day,firstat,lastat) VALUES({player},{DateOnly.FromDateTime(at.AddHours(7))},{at},{at})
        ON CONFLICT(playerid,day) DO UPDATE SET firstat=LEAST(playerlogindays.firstat,EXCLUDED.firstat),lastat=GREATEST(playerlogindays.lastat,EXCLUDED.lastat)
        """);
    public Task<Dictionary<DateOnly,long>> ActivityDays(Guid player,DateOnly since) => db.PlayerActivityDays.AsNoTracking()
        .Where(x=>x.PlayerId==player && x.Day>=since).ToDictionaryAsync(x=>x.Day,x=>x.Seconds);
    public Task SaveActivityDay(Guid player,DateOnly day,long seconds) => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO playeractivitydays(playerid,day,seconds) VALUES({player},{day},{seconds})
        ON CONFLICT(playerid,day) DO UPDATE SET seconds=GREATEST(playeractivitydays.seconds,EXCLUDED.seconds)
        """);
    public async Task<AdminAnalyticsResponse> AnalyticsSummary(DateTime? from,DateTime? through,DateOnly today)
    {
        var ids=HumanPlayers.Select(u=>u.Userid);
        var matches=db.Gamematches.AsNoTracking().Where(m=>m.Status==MatchStatus.Finished && m.Endedat!=null
            && (from==null || m.Endedat>=from) && (through==null || m.Endedat<through) && m.Gamematchplayers.Any(p=>ids.Contains(p.Playerid)));
        long seconds;
        if(from==null && through==null)
            seconds=await db.Playerprofiles.Where(p=>ids.Contains(p.Userid)).SumAsync(p=>p.Timeplayedseconds);
        else
            seconds=await db.PlayerActivityDays.Where(a=>ids.Contains(a.PlayerId)
                && (from==null || a.Day>=DateOnly.FromDateTime(from.Value.AddHours(7)))
                && (through==null || a.Day<DateOnly.FromDateTime(through.Value.AddHours(7)))).SumAsync(x=>x.Seconds);
        return new AdminAnalyticsResponse { TotalUsers=await HumanPlayers.CountAsync(u=>(from==null || u.Createdat>=from) && (through==null || u.Createdat<through)),
            ActiveUsersToday=await db.PlayerLoginDays.CountAsync(x=>x.Day==today && ids.Contains(x.PlayerId)),
            TotalMatchesPlayed=await matches.CountAsync(),TotalPlayTimeSeconds=seconds,
            TotalReportsPending=await db.Reportplayerinmatches.CountAsync(r=>r.Status==ReportStatus.Pending || r.Status==ReportStatus.Checking) };
    }
    public async Task<List<DailyActivePlayers>> DailyActive(DateOnly from,DateOnly to) => await db.PlayerLoginDays
        .Where(a=>a.Day>=from && a.Day<=to && HumanPlayers.Any(u=>u.Userid==a.PlayerId)).GroupBy(a=>a.Day)
        .Select(g=>new DailyActivePlayers{Day=g.Key,Players=g.Count()}).OrderBy(x=>x.Day).ToListAsync();
    public async Task<List<RetentionCohort>> Retention(DateOnly from,DateOnly to,DateOnly today)
    {
        var activity=db.PlayerLoginDays.Where(a=>HumanPlayers.Any(u=>u.Userid==a.PlayerId));
        var first=activity.GroupBy(x=>x.PlayerId).Select(g=>new{Player=g.Key,Day=g.Min(x=>x.Day)}).Where(x=>x.Day>=from && x.Day<=to);
        var cohorts=await first.GroupBy(x=>x.Day).Select(g=>new RetentionCohort{Day=g.Key,Players=g.Count()}).OrderBy(x=>x.Day).ToListAsync();
        foreach(int days in new[]{1,7,30})
        {
            var returned=await (from f in first join a in activity on new{Player=f.Player,Day=f.Day.AddDays(days)} equals new{Player=a.PlayerId,a.Day}
                select new{f.Day,f.Player}).GroupBy(x=>x.Day).Select(g=>new{Day=g.Key,Count=g.Count()}).ToDictionaryAsync(x=>x.Day,x=>x.Count);
            foreach(var cohort in cohorts)
            {
                double? rate=cohort.Day.AddDays(days)<=today?(double)returned.GetValueOrDefault(cohort.Day)/cohort.Players:null;
                if(days==1)cohort.D1=rate;else if(days==7)cohort.D7=rate;else cohort.D30=rate;
            }
        }
        return cohorts;
    }
    public async Task<List<PopularLesson>> PopularLessons(DateTime from,DateTime through)
    {
        var data=await db.TrainingSessionRecords.AsNoTracking().Where(s=>s.CreatedAt>=from && s.CreatedAt<through && HumanPlayers.Any(u=>u.Userid==s.PlayerId))
            .GroupBy(x=>x.LessonId).Select(g=>new{Lesson=g.Key,Starts=g.Count(),Finished=g.Count(x=>x.Completed),
                Passed=g.Count(x=>x.Completed && EF.Functions.JsonContains(x.Data,"{\"passed\":true}")),Players=g.Select(x=>x.PlayerId).Distinct().Count()}).ToListAsync();
        var ids=data.Select(x=>x.Lesson).ToArray();var lessons=await db.Lessons.AsNoTracking().Where(l=>ids.Contains(l.Lessonid)).ToDictionaryAsync(l=>l.Lessonid);
        return data.Select(x=>new PopularLesson{LessonId=x.Lesson,Code=lessons[x.Lesson].Code??"",Title=lessons[x.Lesson].Lessonname,
            Starts=x.Starts,Finished=x.Finished,Passed=x.Passed,Players=x.Players,CompletionRate=x.Starts==0?0:(double)x.Passed/x.Starts})
            .OrderByDescending(x=>x.Starts).ThenBy(x=>x.Code).ToList();
    }
    public async Task<List<AiTechniqueErrorHeatmapDto>> ErrorHeatmap(DateTime? from,DateTime? through)
    {
        var rows=await db.Playertechniqueserrors.AsNoTracking().Where(e=>(from==null || e.Createdat>=from) && (through==null || e.Createdat<through)
            && HumanPlayers.Any(u=>u.Userid==e.Playerid)).GroupBy(e=>new{e.Techniqueid,e.Errortype})
            .Select(g=>new{g.Key.Techniqueid,g.Key.Errortype,Count=g.Count()}).ToListAsync();
        var techniques=await db.Techniques.AsNoTracking().ToListAsync();
        return techniques.Select(t=>new AiTechniqueErrorHeatmapDto{TechniqueId=t.Techniqueid,TechniqueName=t.Techniquename,
            TotalErrorsCount=rows.Where(r=>r.Techniqueid==t.Techniqueid).Sum(r=>r.Count),
            ErrorBreakdown=rows.Where(r=>r.Techniqueid==t.Techniqueid).Select(r=>new ErrorTypeBreakdownDto{ErrorType=r.Errortype,Count=r.Count}).ToList()}).ToList();
    }
    public async Task<QueueHealth> QueueMetrics(DateTime from,DateTime through)
    {
        var query=db.MatchmakingSearches.Where(s=>s.StartedAt>=from && s.StartedAt<through && HumanPlayers.Any(u=>u.Userid==s.PlayerId));
        var counts=await query.GroupBy(x=>x.State).Select(g=>new{State=g.Key,Count=g.Count()}).ToDictionaryAsync(x=>x.State,x=>x.Count);
        var waits=await query.Where(s=>s.WaitMilliseconds!=null).OrderBy(s=>s.WaitMilliseconds).Select(s=>s.WaitMilliseconds!.Value).ToListAsync();
        return new QueueHealth{Searches=counts.Values.Sum(),Searching=counts.GetValueOrDefault(MatchmakingSearchStatus.Searching),
            Matched=counts.GetValueOrDefault(MatchmakingSearchStatus.Matched),Cancelled=counts.GetValueOrDefault(MatchmakingSearchStatus.Cancelled),
            TimedOut=counts.GetValueOrDefault(MatchmakingSearchStatus.TimedOut),BotFallback=counts.GetValueOrDefault(MatchmakingSearchStatus.Bot),
            CancelledAfterAssignment=await query.CountAsync(s=>s.CancelledAfterAssignment),AverageWaitSeconds=waits.Count==0?null:waits.Average()/1000d,
            P95WaitSeconds=waits.Count==0?null:waits[Math.Max(0,(int)Math.Ceiling(waits.Count*.95)-1)]/1000d};
    }
}
