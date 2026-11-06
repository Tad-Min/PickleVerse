using Entities.DTOs;
using Entities.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Repositories.Caching;
using Repositories.Context;
using Services.Implementations;
using PickleVerse.Progression;
using PickleVerse.Multiplayer;

static class AnalyticsChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options,Action<bool,string> check)
    {
        var fixture=new FunctionalFixture(options);var actor=await fixture.Player("Admin");var a=await fixture.Player();var b=await fixture.Player();var c=await fixture.Player();
        var clock=new TestClock();var today=DateOnly.FromDateTime(clock.Now.UtcDateTime.AddHours(7));
        static DateTime Utc(DateOnly day)=>day.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc).AddHours(-7);
        async Task<T> Call<T>(Func<AnalyticsService,Task<T>> action)
        {using var db=new PickleVerseContext(options);using var unit=new UnitOfWork(db);return await action(new AnalyticsService(unit,TestRedis.State,clock,NullLogger<AnalyticsService>.Instance));}
        async Task Record(Guid player,params DateTime[] dates)
        {using var db=new PickleVerseContext(options);using var unit=new UnitOfWork(db);foreach(var date in dates)await unit.Game.RecordLogin(player,date);}
        var baseline=await Call(s=>s.Summary());
        using(var db=new PickleVerseContext(options))
        {
            int role=await db.Roles.Where(r=>r.Rolename=="Player").Select(r=>r.Roleid).SingleAsync();
            for(int i=0;i<1010;i++)
            {
                Guid id=Guid.NewGuid();db.Users.Add(new User{Userid=id,Email=id.ToString("N")+"@example.invalid",Passwordhash="disabled-fixture",Roleid=role,Createdat=Utc(today),Status=UserStatus.Active});
                db.Playerprofiles.Add(new Playerprofile{Userid=id,Playername="an"+id.ToString("N")[..15],Avatar="initial",Level=1,Elo=2000,Timeplayedseconds=i+1});
            }
            for(int i=0;i<1005;i++)
            {
                Guid id=Guid.NewGuid();db.Gamematches.Add(new Gamematch{Matchid=id,Kind="Custom",Status=MatchStatus.Finished,Authoritymode=MatchAuthorityMode.Dedicated,
                    Sequence=100000+i,Createdat=Utc(today).AddMinutes(1),Updatedat=Utc(today).AddMinutes(3),Startedat=Utc(today).AddMinutes(1),Endedat=Utc(today).AddMinutes(3)});
                db.Gamematchplayers.Add(new Gamematchplayer{Matchid=id,Playerid=a,Team=0,Lastseenat=Utc(today).AddMinutes(3),Finalizedat=Utc(today).AddMinutes(3),Outcome="WIN"});
            }
            await db.SaveChangesAsync();
        }
        var summary=await Call(s=>s.Summary());
        check(summary.TotalUsers==baseline.TotalUsers+1010,"analytics counts all human Player accounts beyond 1000");
        check(summary.TotalMatchesPlayed==baseline.TotalMatchesPlayed+1005,"analytics counts all completed matches beyond 1000");
        check(summary.TotalPlayTimeSeconds==baseline.TotalPlayTimeSeconds+1010L*1011/2,"all-time playtime sums all profiles without leaderboard truncation");
        var cohort=today.AddDays(-30);
        await Record(a,Utc(cohort),Utc(cohort.AddDays(1)),Utc(cohort.AddDays(7)),Utc(today),Utc(today).AddSeconds(-1),Utc(today).AddHours(1));
        await Record(b,Utc(cohort).AddHours(2),Utc(cohort.AddDays(1)));
        await Record(c,Utc(today));await Record(actor,Utc(today));await Record(Guid.Parse(RankedBotAccounts.Ids.First()),Utc(today));
        summary=await Call(s=>s.Summary());check(summary.ActiveUsersToday==2,"DAU counts distinct human players and excludes admins and bots");
        var report=await Call(s=>s.Report(cohort,today));
        var old=report.Retention.Single(x=>x.Day==cohort);var fresh=report.Retention.Single(x=>x.Day==today);
        check(old.Players==2 && old.D1==1 && old.D7==.5 && old.D30==.5,"D1/D7/D30 use first-login cohorts and exact return days");
        check(fresh.Players==1 && fresh.D1==null && fresh.D7==null && fresh.D30==null,"young retention cohorts remain unavailable rather than zero");
        check(report.Daily.Single(x=>x.Day==today.AddDays(-1)).Players==1 && report.Daily.Single(x=>x.Day==today).Players==2,
            "16:59:59 and 17:00 UTC fall on different Vietnam login days");
        check(report.Daily.Count==31,"daily chart includes zero-activity days");
        var config=TestSettings.Configuration();var settings=new Services.Configuration.ServerSettings(config);
        var loginPlayer=await fixture.Player();DateOnly loginDay=DateOnly.FromDateTime(clock.Now.UtcDateTime.AddHours(7));
        async Task<AuthResponse> LoginOrRefresh(string? refresh=null)
        {
            using var db=new PickleVerseContext(options);using var unit=new UnitOfWork(db);
            var auth=new AuthService(unit,new ProfileService(unit,NullLogger<ProfileService>.Instance,settings,clock),config,new CapturedMail(),clock,settings);
            return refresh==null?await auth.LoginAsync(new(){Email=loginPlayer.ToString("N")+"@example.invalid",Password="TestPass!234"}):await auth.RefreshTokenAsync(new(){RefreshToken=refresh});
        }
        var signed=await LoginOrRefresh();clock.Now=clock.Now.AddDays(1);await LoginOrRefresh(signed.RefreshToken);
        using(var db=new PickleVerseContext(options))check(await db.PlayerLoginDays.CountAsync(x=>x.PlayerId==loginPlayer)==1
            && await db.PlayerLoginDays.AnyAsync(x=>x.PlayerId==loginPlayer&&x.Day==loginDay),"password login records activity but next-day refresh does not create a login day");
        clock.Now=DateTimeOffset.UtcNow;
        using(var db=new PickleVerseContext(options))
        {
            var link=await db.Lessondrills.OrderBy(x=>x.Lessonid).FirstAsync();
            for(int i=0;i<3;i++)db.TrainingSessionRecords.Add(new TrainingSessionRecord{AttemptId=Guid.NewGuid(),PlayerId=a,LessonId=link.Lessonid,DrillId=link.Drillid,
                Data=GameJson.Write(new TrainingAttempt{passed=i==0,completed=i<2}),Completed=i<2,IsCurrent=false,CreatedAt=Utc(today),UpdatedAt=Utc(today),CatalogVersion="fixture"});
            var technique=await db.Techniques.OrderBy(x=>x.Techniqueid).FirstAsync();
            db.Playertechniqueserrors.AddRange(new Playertechniqueserror{Playerid=a,Techniqueid=technique.Techniqueid,Errortype="Net",Coachingtip="Fixture",Createdat=Utc(today)},
                new Playertechniqueserror{Playerid=Guid.Parse(RankedBotAccounts.Ids.First()),Techniqueid=technique.Techniqueid,Errortype="Net",Coachingtip="Bot fixture",Createdat=Utc(today)});
            await db.SaveChangesAsync();
        }
        report=await Call(s=>s.Report(today,today));var lesson=report.Lessons.Single();
        check(lesson.Starts==3 && lesson.Finished==2 && lesson.Passed==1 && lesson.Players==1 && Math.Abs(lesson.CompletionRate-1d/3)<.00001,
            "popular lessons count starts, terminal sessions and passed attempts separately");
        check(report.Errors.Sum(x=>x.TotalErrorsCount)==1,"error heatmap aggregates human evidence only within the filter");
        long now=clock.Now.ToUnixTimeMilliseconds();var entry=new QueueEntry{searchId=Guid.NewGuid().ToString("N"),playerId=a.ToString("N"),group=a.ToString("N"),party=[a.ToString("N")],mode=1,region="analytics",protocol=99,started=now-10000,lastSeen=now};
        await TestRedis.State.Begin(entry);
        await Call(async s=>{await s.ProjectMatchmaking();return true;});
        var raw=GameJson.Write(entry);entry.state=MatchmakingSearchStatus.Matched;entry.assigned=now;entry.matchId=Guid.NewGuid().ToString("N");
        check(await TestRedis.State.Compare([raw],[entry]),"queue transition and analytics journal commit in the same Redis compare-and-set");
        await Call(async s=>{await s.ProjectMatchmaking();return true;});
        var current=(await TestRedis.State.SearchSnapshot(Guid.Parse(entry.searchId)))!;raw=GameJson.Write(current);current.state=MatchmakingSearchStatus.Cancelled;await TestRedis.State.Compare([raw],[current]);
        await Call(async s=>{await s.ProjectMatchmaking();await s.ProjectMatchmaking();return true;});
        report=await Call(s=>s.Report(today,today));check(report.Matchmaking.Searches==1&&report.Matchmaking.Matched==1&&report.Matchmaking.CancelledAfterAssignment==1
            && report.Matchmaking.AverageWaitSeconds==10,"durable queue health preserves the first assignment and deduplicates retries");
        var stale=new QueueEntry{searchId=Guid.NewGuid().ToString("N"),playerId=b.ToString("N"),group=b.ToString("N"),party=[b.ToString("N")],mode=1,region="analytics",protocol=99,started=now-100000,lastSeen=now-60000};
        await TestRedis.State.Begin(stale);await Call(async s=>{await s.ProjectMatchmaking();return true;});
        check((await Call(s=>s.Report(today,today))).Matchmaking.TimedOut==1,"disconnected search becomes a durable timeout before Redis retention ends");
        await TestRedis.Connection.GetDatabase().KeyDeleteAsync(TestRedis.State.Prefix+"search:"+entry.searchId);
        check((await Call(s=>s.Report(today,today))).Matchmaking.Matched==1,"queue history remains available after the Redis search key disappears");
        var active=await fixture.Player();clock.Now=new DateTimeOffset(Utc(today).AddSeconds(-10));Guid session=Guid.NewGuid(),overlap=Guid.NewGuid();
        async Task<T> Activity<T>(Func<AppActivityService, Task<T>> action)
        {
            using var db = new PickleVerseContext(options);
            using var unit = new UnitOfWork(db);
            return await action(new AppActivityService(unit, TestRedis.Activity, clock));
        }
        await Activity(s => s.OpenActivity(new() { SessionId = session }));
        await Activity(s => s.OpenActivity(new() { SessionId = overlap }));
        clock.Now = clock.Now.AddSeconds(20);
        await Activity(s => s.Activity(active, new() { SessionId = session, ElapsedSeconds = 20 }));
        await Activity(s => s.Activity(active, new() { SessionId = session, ElapsedSeconds = 20 }));
        await Activity(s => s.Activity(active, new() { SessionId = overlap, ElapsedSeconds = 20 }));
        using(var db=new PickleVerseContext(options))
        {
            var days=await db.PlayerActivityDays.Where(x=>x.PlayerId==active).ToDictionaryAsync(x=>x.Day,x=>x.Seconds);
            check(days.GetValueOrDefault(today.AddDays(-1))==10 && days.GetValueOrDefault(today)==10,"daily playtime splits at Vietnam midnight without duplicate credit on retry or overlap");
        }
        var filtered=await Call(s=>s.Summary(today,today));check(filtered.TotalPlayTimeSeconds==10&&filtered.TotalMatchesPlayed==1005,"filtered summary uses all matching rows and only that day's confirmed seconds");
    }
}
