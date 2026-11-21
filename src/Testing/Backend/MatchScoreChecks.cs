using Entities.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using Repositories.Context;
using Repositories.UnitOfWork;
using Services.Implementations;
using static Repositories.Data.PostgresCommands;

static class MatchScoreChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options,TestClock clock,Action<bool,string> check)
    {
        async Task<T> Game<T>(Func<OnlineGameService,Task<T>> call)
        {
            await using var db=new PickleVerseContext(options);var sql=new PostgresCommands(db);var sqlData = new UnitOfWork(db);var settings=TestSettings.Load();
            return await call(new OnlineGameService(sqlData,new GameCatalogService(sqlData,settings),clock,settings,TestRedis.State,TestRedis.Leaderboard,
                new MatchReplayService(sqlData,TestReplayStorage.Shared,NullLogger<MatchReplayService>.Instance)));
        }
        await using var context=new PickleVerseContext(options);var query=new PostgresCommands(context);var queryData = new UnitOfWork(context);
        var players=Enumerable.Range(0,4).Select(_=>Guid.NewGuid()).ToArray();
        foreach(var id in players)
        {
            await query.Run("INSERT INTO users(userid,email,passwordhash,roleid) SELECT @id,@email,'disabled',roleid FROM roles WHERE rolename='Player'",P("id",id),P("email",id+"@example.invalid"));
            await query.Run("INSERT INTO playerprofiles(userid,playername) VALUES(@id,@name)",P("id",id),P("name","Score"+id.ToString("N")[..8]));
        }
        check(await query.One("SELECT to_regclass('public.matchscore') IS NOT NULL AND to_regclass('public.gamescores') IS NULL",r=>r.GetBoolean(0)),
            "schema uses the approved MatchScore name without a GameScores entity");
        var model=context.Model.FindEntityType(typeof(Gamematch))!;
        check(model.GetProperties().Count()==15 && model.FindProperty("Teamascore")==null && model.FindProperty("Teambscore")==null,
            "GameMatches EF mapping has 15 scalar columns without cached team win counts");
        check(await query.One("SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='gamematches'",r=>r.GetInt64(0))==15
            && await query.One("SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='gamematches' AND column_name IN ('teamascore','teambscore')",r=>r.GetInt64(0))==0,
            "canonical SQL removes both redundant GameMatches score columns");
        var match=Guid.NewGuid();
        var request=new OnlineMatchRequest {HostPlayerId=players[0].ToString("N"),Session="score-"+match.ToString("N"),Roster=new MatchRoster {
            matchId=match.ToString("N"),custom=true,mode=PlayMode.Doubles,region="asia",members=players.Select((id,i)=>new RoomMember{id=id.ToString("N"),team=i<2?0:1}).ToList()}};
        foreach(var id in players)await Game(g=>g.PrepareOnline(id,request));
        await Game(g=>g.StartOnline(players[0],request));
        check(await query.One("SELECT count(*) FROM matchscore WHERE matchid=@match",r=>r.GetInt64(0),P("match",match))==0,
            "starting a match does not invent game scores");
        var commands=new List<MatchFinishRequest>();
        for(int i=0;i<4;i++)
        {
            var state=await Game(g=>g.Read(players[i]));var result=GameJson.Read<MatchResult>(GameJson.Write(state.player.pendingMatch));bool a=i<2;
            result.outcome=a?MatchOutcome.Won:MatchOutcome.Lost;result.gamesWon=a?2:1;result.gamesLost=a?1:2;
            result.scores=a?[new(11,5,true),new(9,11,true),new(12,10,true)]:[new(5,11,true),new(11,9,true),new(10,12,true)];
            result.points=result.scores.Sum(s=>s.own);result.validReturns=i+1;result.validServes=i+2;
            var command=new MatchFinishRequest {RequestId=Guid.NewGuid().ToString("N"),Result=result};commands.Add(command);
            await Game(g=>g.ReportOnline(players[i],command));
            if(i==0)check(await query.One("SELECT count(*) FROM matchscore s JOIN gamematches m USING(matchid) WHERE s.matchid=@match AND m.status='FINISHED'",r=>r.GetInt64(0),P("match",match))==0,
                "one unconfirmed client report is not published as canonical scores");
        }
        var scores=await query.Query("SELECT gamenumber,teamascore,teambscore,completed FROM matchscore WHERE matchid=@match ORDER BY gamenumber",
            r=>(game:(int)r.GetInt16(0),a:r.GetInt32(1),b:r.GetInt32(2),complete:r.GetBoolean(3)),P("match",match));
        check(scores.SequenceEqual(new[]{(1,11,5,true),(2,9,11,true),(3,12,10,true)}),
            "a doubles match stores three team score rows rather than per-player copies");
        foreach(var (id,index) in players.Select((id,index)=>(id,index)))
        {
            var history=(await Game(g=>g.Read(id))).player.history.Single(h=>h.id==match.ToString("N"));
            check(GameJson.Write(history.scores)==GameJson.Write(commands[index].Result.scores)
                && history.gamesWon==(index<2?2:1) && history.gamesLost==(index<2?1:2),"history reconstructs scores and game wins for doubles seat "+index);
        }
        await using(var legacyDb=new PickleVerseContext(options))
        {
            using var uow=new UnitOfWork(legacyDb);
            var playerHistory=await new MatchService(uow).GetMyMatchHistoryAsync(players[3]);
            check(playerHistory.Single().TeamAScore==2 && playerHistory.Single().TeamBScore==1 && playerHistory.Single().IsWinner==false,
                "legacy player history counts wins in team-A orientation even for a team-B caller");
            var adminHistory=await new AdminService(uow,NullLogger<AdminService>.Instance, new ContentService(uow)).GetRecentMatchesAsync();
            check(adminHistory.Single().TeamAScore==2 && adminHistory.Single().TeamBScore==1,
                "admin history counts completed games through the same MatchScore relation");
        }
        check(await query.One("SELECT NOT EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='gamematches' AND data_type IN ('json','jsonb'))",r=>r.GetBoolean(0)),
            "GameMatches contains no JSON documents or duplicated score arrays");
        check(await query.One("SELECT NOT EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='gamematchplayers' AND data_type IN ('json','jsonb'))",r=>r.GetBoolean(0)),
            "GameMatchPlayers contains only scalar participant result columns");
        var before=await Game(g=>g.Read(players[0]));await Game(g=>g.ReportOnline(players[0],commands[0]));
        var after=await Game(g=>g.Read(players[0]));
        check(after.player.coins==before.player.coins && after.player.wins==before.player.wins && await query.One("SELECT count(*) FROM matchscore WHERE matchid=@match",r=>r.GetInt64(0),P("match",match))==3,
            "a result retry cannot duplicate score rows or rewards");

        async Task RejectSql(string text,string code,string label)
        {
            bool rejected=false;try{await query.Run(text,P("match",match));}catch(PostgresException error) when(error.SqlState==code){rejected=true;}
            check(rejected,label);
        }
        await RejectSql("INSERT INTO matchscore VALUES(@match,1,11,0,true)","23505","database rejects a duplicate game number within the same match");
        await RejectSql("INSERT INTO matchscore VALUES(@match,4,11,0,true)","23514","database enforces the current three-game match format");
        await RejectSql("UPDATE matchscore SET teamascore=-1 WHERE matchid=@match AND gamenumber=1","23514","database rejects negative scores");
        await RejectSql("INSERT INTO matchscore VALUES(gen_random_uuid(),1,11,0,true)","23503","score rows must belong to an existing GameMatches record");

        var practice=await Game(g=>g.StartBot(players[0],new(){RequestId=Guid.NewGuid().ToString("N"),Kind=MatchKind.BotPractice}));
        var partial=GameJson.Read<MatchResult>(GameJson.Write(practice.player.pendingMatch));
        partial.outcome=MatchOutcome.Lost;partial.localForfeit=true;partial.scores=[new(11,5,true),new(4,2,false)];partial.points=15;
        partial.gamesWon=2;partial.gamesLost=2; // Redundant client totals cannot override the actual games.
        await Game(g=>g.FinishBot(players[0],new(){RequestId=Guid.NewGuid().ToString("N"),Result=partial}));
        var restored=(await Game(g=>g.Read(players[0]))).player.history.Single(h=>h.id==partial.id);
        check(restored.scores.Count==2 && restored.scores[1].own==4 && restored.scores[1].opponent==2 && !restored.scores[1].completed,
            "forfeit history retains the unfinished game's score and completion flag");
        check(restored.gamesWon==1 && restored.gamesLost==0 && restored.points==15 && restored.outcome==MatchOutcome.Lost && restored.localForfeit,
            "forfeit keeps its loss outcome while only completed games count toward the game-win tally");
        var retry=new MatchFinishRequest {RequestId=Guid.NewGuid().ToString("N"),Result=GameJson.Read<MatchResult>(GameJson.Write(partial))};
        retry.Result.gamesWon=1;retry.Result.gamesLost=0;
        var paidForfeit=await Game(g=>g.Read(players[0]));
        await Game(g=>g.FinishBot(players[0],retry));
        check((await Game(g=>g.Read(players[0]))).player.losses==paidForfeit.player.losses,
            "a retry with recomputed game totals cannot reapply a bot forfeit result");
        check(await query.One("SELECT count(*) FROM matchscore WHERE matchid=@match",r=>r.GetInt64(0),P("match",Guid.Parse(partial.id)))==2,
            "practice match scores also live exclusively in MatchScore");
        check(await query.One("SELECT NOT EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='gamematchplayers' AND column_name='fingerprint')",r=>r.GetBoolean(0)),
            "practice idempotency derives its comparison from canonical data without storing a duplicate hash");
        var botView=new MatchResult {id=partial.id};
        await MatchScoreStore.Hydrate(context,Guid.Parse(partial.opponents[0].id),[botView]);
        check(botView.scores[1].own==2 && botView.scores[1].opponent==4,"the existing bot participant reads the reverse score orientation");
        var interrupted=(await Game(g=>g.StartBot(players[0],new(){RequestId=Guid.NewGuid().ToString("N"),Kind=MatchKind.BotPractice}))).player.pendingMatch;
        interrupted.outcome=MatchOutcome.Interrupted;interrupted.gamesWon=2;interrupted.gamesLost=2;
        interrupted.scores=[new(11,5,true),new(7,9,false)];interrupted.points=18;
        await Game(g=>g.FinishBot(players[0],new(){RequestId=Guid.NewGuid().ToString("N"),Result=interrupted}));
        var interruptedHistory=(await Game(g=>g.Read(players[0]))).player.history.Single(h=>h.id==interrupted.id);
        check(interruptedHistory.gamesWon==1 && interruptedHistory.gamesLost==0 && interruptedHistory.outcome==MatchOutcome.Interrupted,
            "interrupted matches also derive completed game counts independently of their final outcome");
        var underfilled=new OnlineMatchRequest {HostPlayerId=players[0].ToString("N"),Session="underfilled-doubles",Roster=new()
            {matchId=Guid.NewGuid().ToString("N"),custom=true,mode=PlayMode.Doubles,
             members=[new(){id=players[0].ToString("N"),team=0},new(){id=players[2].ToString("N"),team=1}]}};
        foreach(var id in new[]{players[0],players[2]})await Game(g=>g.PrepareOnline(id,underfilled));
        var underfilledStart=await Game(g=>g.StartOnline(players[0],underfilled));
        check(underfilledStart.roster.mode==PlayMode.Doubles && underfilledStart.roster.members.Count==2,
            "Mode remains explicit because custom Doubles currently allows fewer than four participants");
        await Game(g=>g.LeaveOnline(players[0],new(){MatchId=underfilled.Roster.matchId}));
        var cascade=Guid.NewGuid();
        await query.Run("INSERT INTO gamematches(matchid,kind) VALUES(@match,'Custom')",P("match",cascade));
        await MatchScoreStore.Save(context,cascade,[new(11,0,true)]);
        await query.Run("DELETE FROM gamematches WHERE matchid=@match",P("match",cascade));
        check(await query.One("SELECT count(*) FROM matchscore WHERE matchid=@match",r=>r.GetInt64(0),P("match",cascade))==0,
            "deleting a match cascades only its score rows");
    }
}
