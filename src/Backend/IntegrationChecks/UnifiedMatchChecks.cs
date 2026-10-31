using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using PickleVerse.Progression;
using PickleVerse.Multiplayer;
using Repositories.Context;
using Repositories.UnitOfWork;
using Services.Implementations;
using static Repositories.Data.PostgresCommands;

static class UnifiedMatchChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options,TestClock clock,Action<bool,string> check)
    {
        async Task<T> Game<T>(Func<OnlineGameService,Task<T>> call)
        {
            await using var db=new PickleVerseContext(options);var sql=new PostgresCommands(db);var sqlData = new UnitOfWork(db);var settings=TestSettings.Load();
            return await call(new OnlineGameService(sqlData,new GameCatalogService(sqlData,settings),clock,settings,TestRedis.State,TestRedis.Leaderboard,
                new MatchReplayService(sqlData,TestReplayStorage.Shared,NullLogger<MatchReplayService>.Instance)));
        }
        await using var context=new PickleVerseContext(options);var q=new PostgresCommands(context);
        var a=Guid.NewGuid();var b=Guid.NewGuid();
        foreach(var id in new[]{a,b})
        {
            await q.Run("INSERT INTO users(userid,email,passwordhash,roleid) SELECT @id,@email,'disabled',roleid FROM roles WHERE rolename='Player'",P("id",id),P("email",id+"@example.invalid"));
            await q.Run("INSERT INTO playerprofiles(userid,playername) VALUES(@id,@name)",P("id",id),P("name","Unified"+id.ToString("N")[..8]));
        }
        check(context.Database.ProviderName=="Npgsql.EntityFrameworkCore.PostgreSQL","backend uses Npgsql Entity Framework Core");
        check(await q.One("SELECT to_regclass('public.matchhistory') IS NULL AND to_regclass('public.playermatchhistory') IS NULL",r=>r.GetBoolean(0)),
            "old match and participant history tables are absent");
        check(await q.One("SELECT count(*) FROM pg_constraint WHERE contype='f' AND confrelid='gamematches'::regclass AND conrelid IN ('matchhistoryreplay'::regclass,'reportplayerinmatch'::regclass,'playertechniqueserrors'::regclass,'ratingledger'::regclass)",r=>r.GetInt64(0))==4,
            "replay, moderation, technique errors and rating all reference GameMatches");
        var start=await Game(g=>g.StartBot(a,new(){RequestId=Guid.NewGuid().ToString("N"),Kind=MatchKind.BotPractice}));
        var result=GameJson.Read<MatchResult>(GameJson.Write(start.player.pendingMatch));var match=Guid.Parse(result.id);
        await using(var readDb=new PickleVerseContext(options))
        {
            using var uow=new UnitOfWork(readDb);var service=new MatchService(uow);
            check((await service.GetMyMatchHistoryAsync(a)).Count==0,"legacy history API excludes an active match");
        }
        result.outcome=MatchOutcome.Won;result.gamesWon=2;result.gamesLost=0;result.points=22;result.scores=[new(11,5,true),new(11,8,true)];
        var finish=new MatchFinishRequest {RequestId=Guid.NewGuid().ToString("N"),Result=result};
        var completed=await Game(g=>g.FinishBot(a,finish));
        var root=await context.Gamematches.AsNoTracking().Include(m=>m.Gamematchplayers).Include(m=>m.Matchscores).AsSplitQuery().SingleAsync(m=>m.Matchid==match);
        check(root.Status=="FINISHED" && root.Startedat!=null && root.Endedat!=null && root.Sequence>0 && MatchScoreStore.GameWins(root.Matchscores)==(2,0),
            "GameMatches stores lifecycle while final team scores derive from MatchScore");
        check(root.Gamematchplayers.Count==2 && root.Gamematchplayers.All(p=>p.Finalizedat!=null && p.Reportedat!=null)
            && !await context.Ratingledgers.AnyAsync(r=>r.Matchid==match)
            && root.Gamematchplayers.Single(p=>p.Playerid==a).Outcome=="WIN" && root.Gamematchplayers.Single(p=>p.Playerid!=a).Outcome=="LOSE",
            "the existing human and bot participant rows hold canonical final outcomes");
        var retry=await Game(g=>g.FinishBot(a,finish));
        check(retry.player.coins==completed.player.coins && await context.Gamematchplayers.CountAsync(p=>p.Matchid==match)==2,
            "a result retry cannot create extra participant records or rewards");
        await using(var readDb=new PickleVerseContext(options))
        {
            using var uow=new UnitOfWork(readDb);var service=new MatchService(uow);
            var history=await service.GetMyMatchHistoryAsync(a);var recent=await uow.Matches.GetRecentMatchesAsync();
            check(history.Count==1 && history[0].MatchType=="BotPractice" && history[0].IsWinner==true && history[0].TeamAScore==2,
                "legacy history DTO reads the merged EF Core entities");
            check(recent.Single().Gamematchplayers.Count==2,"admin match repository includes both human and bot participants");
            check(!await service.ReportPlayerAsync(b,new(){MatchId=match,ReportedPlayerId=a,Reason="outsider"}),
                "a nonparticipant cannot report a player in a known match");
            check(await service.ReportPlayerAsync(a,new(){MatchId=match,ReportedPlayerId=Guid.Parse(result.opponents[0].id),Reason="fixture"}),
                "a participant report targets the merged completed match");
        }
        var request=new OnlineMatchRequest {HostPlayerId=a.ToString("N"),Session="unified-cancel",Roster=new(){matchId=Guid.NewGuid().ToString("N"),custom=true,mode=PlayMode.Singles,region="asia",members=[new(){id=a.ToString("N"),team=0},new(){id=b.ToString("N"),team=1}]}};
        await Game(g=>g.PrepareOnline(a,request));await Game(g=>g.LeaveOnline(a,new(){MatchId=request.Roster.matchId}));
        var cancelled=await context.Gamematches.AsNoTracking().Include(m=>m.Gamematchplayers).SingleAsync(m=>m.Matchid==Guid.Parse(request.Roster.matchId));
        check(cancelled.Status=="CANCELLED" && cancelled.Endedat==null && cancelled.Sequence==null && cancelled.Gamematchplayers.All(p=>p.Finalizedat==null && p.Outcome==null),
            "cancelled preparation creates no final outcomes or history receipt");
        check((await Game(g=>g.Read(a))).player.history.Count==1,"cancelled preparation stays out of the player's history");

        async Task RejectSql(string command,string state,string label)
        {
            bool rejected=false;try{await q.Run(command,P("player",a),P("match",cancelled.Matchid));}catch(PostgresException error) when(error.SqlState==state){rejected=true;}
            check(rejected,label);
        }
        await RejectSql("UPDATE gamematches SET status='FINISHED' WHERE matchid=@match","23514","database rejects finished status without final timestamps");
        await RejectSql("UPDATE gamematchplayers SET outcome='WIN' WHERE matchid=@match AND playerid=@player","23514","participant outcome and finalization fields must be written atomically");
        var pair=await q.One("SELECT l.lessonid,d.drillid FROM lessons l JOIN lessondrills ld USING(lessonid) JOIN drills d USING(drillid) WHERE l.code='serve'",r=>(lesson:r.GetInt32(0),drill:r.GetInt32(1)));
        var wrongDrill=await q.One("SELECT drillid FROM drills WHERE code='drive'",r=>r.GetInt32(0));
        bool invalidPair=false;
        await using(var invalidDb=new PickleVerseContext(options))
        {
            invalidDb.Playerlessondrills.Add(new(){Playerid=a,Lessonid=pair.lesson,Drillid=wrongDrill,Score=1});
            try{await invalidDb.SaveChangesAsync();}catch(DbUpdateException error) when(error.InnerException is PostgresException {SqlState:"23503"}){invalidPair=true;}
        }
        check(invalidPair,"Npgsql EF Core rejects individually existing lesson/drill IDs that are not a valid pair");
        await using(var validDb=new PickleVerseContext(options))
        {
            validDb.Playerlessondrills.Add(new(){Playerid=a,Lessonid=pair.lesson,Drillid=pair.drill,Score=1,Isclaimedreward=true});
            await validDb.SaveChangesAsync();validDb.ChangeTracker.Clear();
            var progress=await validDb.Playerlessondrills.Include(p=>p.LessonDrill).ThenInclude(l=>l.Lesson).SingleAsync(p=>p.Playerid==a);
            check(progress.LessonDrill.Lessonid==pair.lesson && progress.LessonDrill.Drillid==pair.drill && progress.LessonDrill.Lesson.Lessonid==pair.lesson,
                "valid progress follows the composite LessonDrills navigation in EF Core");
            var metadata=validDb.Model.FindEntityType(typeof(Entities.Models.Playerlessondrill))!;
            check(metadata.GetForeignKeys().Count()==2 && metadata.GetForeignKeys().Any(f=>f.Properties.Select(p=>p.Name).SequenceEqual(new[]{"Lessonid","Drillid"})),
                "EF Core maps exactly the player FK and composite lesson-drill FK");
        }
        check(await q.One("SELECT count(*) FROM pg_constraint WHERE contype='f' AND conrelid='playerlessondrills'::regclass AND confrelid IN ('lessons'::regclass,'drills'::regclass)",r=>r.GetInt64(0))==0,
            "old independent lesson and drill foreign keys are removed");
    }
}
