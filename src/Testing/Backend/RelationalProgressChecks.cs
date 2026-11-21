using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PickleVerse.Progression;
using Repositories.Context;
using Entities.Exceptions;
using Services.Implementations;
using static Repositories.Data.PostgresCommands;

static class RelationalProgressChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, TestClock clock, Action<bool,string> check)
    {
        clock.Now=new DateTimeOffset(2026,11,1,12,0,0,TimeSpan.Zero); // Sunday: next day crosses both period boundaries.
        var player=Guid.NewGuid();var other=Guid.NewGuid();var settings=TestSettings.Load();
        async Task<T> Game<T>(Func<OnlineGameService,Task<T>> call)
        {
            await using var db=new PickleVerseContext(options);var sql=new PostgresCommands(db);var sqlData = new UnitOfWork(db);
            return await call(new OnlineGameService(sqlData,new GameCatalogService(sqlData,settings),clock,settings,TestRedis.State,TestRedis.Leaderboard,
                new MatchReplayService(sqlData,TestReplayStorage.Shared,NullLogger<MatchReplayService>.Instance)));
        }
        await using var context=new PickleVerseContext(options);var query=new PostgresCommands(context);var queryData = new UnitOfWork(context);
        foreach(var id in new[]{player,other})
        {
            await query.Run("INSERT INTO users(userid,email,passwordhash,roleid) SELECT @id,@email,'disabled',roleid FROM roles WHERE rolename='Player'",P("id",id),P("email",id+"@example.invalid"));
            await query.Run("INSERT INTO playerprofiles(userid,playername) VALUES(@id,@name)",P("id",id),P("name","State"+id.ToString("N")[..8]));
        }
        check(await query.One("SELECT to_regclass('public.gameprogress') IS NULL",r=>r.GetBoolean(0)),"initializer contains no GameProgress table");
        var initial=await Game(g=>g.Read(player));
        check(initial.player.coins==0 && initial.revision>0 && initial.player.training.lessons.Count==0,"fresh relational profile assembles a complete API state");
        await Game(g=>g.Tutorial(player,new(){RequestId=Guid.NewGuid().ToString("N"),Key="completed"}));
        var restored=await Game(g=>g.Read(player));
        check(restored.tutorial.completed && await query.One("SELECT tutorialcompleted FROM playerprofiles WHERE userid=@player",r=>r.GetBoolean(0),P("player",player)),"tutorial completion survives a new context in PlayerProfiles");
        check(restored.revision==await query.One("SELECT revision FROM playerprofiles WHERE userid=@player",r=>r.GetInt64(0),P("player",player)),"API revision comes from the locked profile row");

        var started=await Game(g=>g.StartTraining(player,new(){RequestId=Guid.NewGuid().ToString("N"),LessonCode="serve"}));
        var attempt=GameJson.Read<TrainingAttempt>(GameJson.Write(started.player.training.active));
        attempt.trials=2;attempt.successes=1;attempt.contacts=2;attempt.seconds=0;attempt.timingTotal=.2;
        await Game(g=>g.UpdateTraining(player,new(){RequestId=Guid.NewGuid().ToString("N"),Attempt=attempt},false));
        restored=await Game(g=>g.Read(player));
        check(restored.player.training.active.id==attempt.id && restored.player.training.active.trials==2 && restored.player.training.lessons.Single().attempts==1,
            "checkpoint resumes from TrainingSessions and PlayerLessonDrills after a new context");
        attempt.trials=attempt.successes=attempt.contacts=started.catalog.lessons.Single(l=>l.id=="serve").trials;
        attempt.timingTotal=attempt.contacts*.1;
        var finish=new TrainingUpdateRequest {RequestId=Guid.NewGuid().ToString("N"),Attempt=attempt};
        var finished=await Game(g=>g.UpdateTraining(player,finish,true));
        var retries=await Task.WhenAll(Game(g=>g.UpdateTraining(player,finish,true)),Game(g=>g.UpdateTraining(player,finish,true)));
        check(retries.All(s=>s.player.coins==finished.player.coins && s.revision==finished.revision),"concurrent completion retries cannot grant training rewards twice");
        var review=await Game(g=>g.StartTraining(player,new(){RequestId=Guid.NewGuid().ToString("N"),LessonCode="serve",Retry=true}));
        var weak=GameJson.Read<TrainingAttempt>(GameJson.Write(review.player.training.active));
        weak.trials=weak.contacts=attempt.trials;weak.successes=1;weak.timingTotal=weak.contacts*.1;
        weak.errors=[new(){error=TrainingError.Late,count=weak.trials-1}];
        await Game(g=>g.UpdateTraining(player,new(){RequestId=Guid.NewGuid().ToString("N"),Attempt=weak},true));
        restored=await Game(g=>g.Read(player));var lesson=restored.player.training.lessons.Single();
        check(lesson.bestAccuracy==1 && lesson.bestStars==3 && lesson.attempts==2 && lesson.retries==1 && lesson.successes==attempt.trials+1,
            "weaker repeat preserves best score and correctly accumulates per-lesson measurements");
        check(lesson.accuracyHistory.SequenceEqual(new[]{1f,1f/weak.trials}) && restored.player.coins==finished.player.coins,
            "accuracy graph reads existing ordered attempt data and review grants no additional reward");
        check(await query.One("SELECT count(*) FROM trainingsessions WHERE playerid=@player AND iscurrent",r=>r.GetInt64(0),P("player",player))==1,
            "only one training attempt is current per player");
        await query.Run("UPDATE playerlessondrills SET score=1,bestaccuracy=.75 WHERE playerid=@player",P("player",player));
        restored=await Game(g=>g.Read(player));
        check(restored.player.training.lessons.Single().bestStars==1 && restored.player.training.lessons.Single().bestAccuracy==.75,
            "lesson API reads normalized rows rather than a hidden progress snapshot");
        await using(var coachingDb=new PickleVerseContext(options))
        {
            var sql=new PostgresCommands(coachingDb);var sqlData = new UnitOfWork(coachingDb);var coaching=new HybridCoachingService(sqlData,new GameCatalogService(sqlData,settings),settings.HybridAi,clock);
            check((await coaching.Summary(player)).analysis!.recommendations[0].id=="drive","coaching recommendations use relational lesson completion");
        }
        var ledgerBefore=await query.One("SELECT count(*) FROM currencytransactions WHERE playerid=@player",r=>r.GetInt64(0),P("player",player));
        await Game(g=>g.Read(player));await Game(g=>g.Read(player));
        check(await query.One("SELECT count(*) FROM currencytransactions WHERE playerid=@player",r=>r.GetInt64(0),P("player",player))==ledgerBefore,
            "reading relational progress does not manufacture wallet transactions");
        await query.Run("UPDATE playercurrencies SET amount=777 WHERE playerid=@player AND currencyid=(SELECT currencyid FROM currencies WHERE currencyname='TOKENPAD')",P("player",player));
        check((await Game(g=>g.Read(player))).player.coins==777,"wallet row is the sole source of the displayed balance");

        string lastMatch="";
        for(int matchNumber=0;matchNumber<4;matchNumber++)
        {
            clock.Now=clock.Now.AddSeconds(1);
            var match=await Game(g=>g.StartBot(player,new(){RequestId=Guid.NewGuid().ToString("N"),Kind=MatchKind.BotPractice}));
            if(matchNumber==0)
                check((await Game(g=>g.Read(player))).player.pendingMatch.id==match.player.pendingMatch.id,"pending match is restored from match membership and game state");
            var result=GameJson.Read<MatchResult>(GameJson.Write(match.player.pendingMatch));bool won=matchNumber<3;
            result.outcome=won?MatchOutcome.Won:MatchOutcome.Lost;result.gamesWon=won?2:0;result.gamesLost=won?0:2;
            result.scores=[new(won?11:0,won?0:11,true),new(won?11:0,won?0:11,true)];result.points=won?22:0;
            await Game(g=>g.FinishBot(player,new(){RequestId=Guid.NewGuid().ToString("N"),Result=result}));lastMatch=result.id;
        }
        restored=await Game(g=>g.Read(player));
        check(restored.player.pendingMatch==null && restored.player.history[0].id==lastMatch && restored.player.lifetime.matches==4 && restored.player.wins==3 && restored.player.losses==1,
            "completed history and lifetime counters survive independent match transactions");
        check(restored.player.weekly.counters.streak==0 && restored.player.weekly.counters.bestStreak==3 && restored.player.dailyStreak.days.Contains(ProgressionEngine.DayKey(clock.Now.UtcDateTime)),
            "period win streak and active-day streak survive reload after a loss");

        await query.Run("UPDATE playermissions p SET progress=m.target FROM missions m WHERE p.missionid=m.missionid AND p.playerid=@player",P("player",player));
        var bonus=new GameCommand {RequestId=Guid.NewGuid().ToString("N"),Code="DailyBonus"};
        var rewarded=await Game(g=>g.Claim(player,bonus));
        restored=await Game(g=>g.Read(player));
        check(restored.player.daily.bonusClaimed && restored.player.coins==rewarded.player.coins,"period bonus receipt survives reload without a progress document");
        var duplicate=await Game(g=>g.Claim(player,bonus));
        check(duplicate.player.coins==rewarded.player.coins,"retrying the same bonus request returns the current state without another reward");
        check(duplicate.player.dailyStreak.current==1 && duplicate.player.dailyStreak.best==1,
            "a duplicate command response retains the reconstructed active-day streak");
        bool rejected=false;
        try{await Game(g=>g.Claim(player,new(){RequestId=Guid.NewGuid().ToString("N"),Code="DailyBonus"}));}
        catch(AppException error) when(error.StatusCode==HttpStatusCode.BadRequest){rejected=true;}
        check(rejected,"a fresh command cannot claim an already paid period bonus");
        var oldDay=restored.player.daily.key;var oldWeek=restored.player.weekly.key;
        await Game(g=>g.Claim(player,new(){RequestId=Guid.NewGuid().ToString("N"),Code="D.Play1"}));
        var beforeReset=await Game(g=>g.Read(player));
        long expected=beforeReset.catalog.definitions.Where(d=>d.scope==ProgressScope.Weekly || d.scope==ProgressScope.Daily && d.id!="D.Play1").Sum(d=>(long)d.reward.coins)+beforeReset.catalog.weeklyBonus.coins;
        clock.Now=clock.Now.AddDays(1);
        var reset=await Game(g=>g.Read(player));var resetAgain=await Game(g=>g.Read(player));
        check(reset.player.daily.key!=oldDay && reset.player.weekly.key!=oldWeek && reset.player.daily.counters.matches==0 && reset.player.weekly.counters.matches==0,
            "crossing midnight and week boundary creates clean mission periods");
        check(reset.player.coins==beforeReset.player.coins+expected && resetAgain.player.coins==reset.player.coins,
            "expired-period rewards are settled exactly once across repeated reloads");
        check(reset.player.previousDays.Single(p=>p.key==oldDay).bonusClaimed && reset.player.previousWeeks.Single(p=>p.key==oldWeek).bonusClaimed,
            "historical period claim receipts remain independent of the new period");
        check(await query.One("SELECT count(DISTINCT periodkey) FROM playermissions WHERE playerid=@player",r=>r.GetInt64(0),P("player",player))>=3,
            "mission rows retain their composite player/mission/period identity");
        var untouched=await Game(g=>g.Read(other));
        check(untouched.player.coins==0 && !untouched.tutorial.completed && untouched.player.training.lessons.Count==0,
            "another account cannot inherit progress from the assembled state");
    }
}
