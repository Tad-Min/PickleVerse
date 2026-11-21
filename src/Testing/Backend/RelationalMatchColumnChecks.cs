using Entities.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using PickleVerse;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using PickleVerse.Replays;
using Repositories.Context;
using Entities.Exceptions;
using Services.Implementations;
using System.Net;

static class RelationalMatchColumnChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, TestClock clock, Action<bool,string> check)
    {
        async Task<T> Game<T>(Func<OnlineGameService,Task<T>> call)
        {
            await using var db=new PickleVerseContext(options);var sql=new PostgresCommands(db);var sqlData = new UnitOfWork(db);var settings=TestSettings.Load();
            return await call(new OnlineGameService(sqlData,new GameCatalogService(sqlData,settings),clock,settings,TestRedis.State,TestRedis.Leaderboard,
                new MatchReplayService(sqlData,TestReplayStorage.Shared,NullLogger<MatchReplayService>.Instance)));
        }
        async Task<T> Replay<T>(Func<MatchReplayService,Task<T>> call)
        {
            await using var db=new PickleVerseContext(options);
            return await call(new MatchReplayService(new UnitOfWork(db),TestReplayStorage.Shared,NullLogger<MatchReplayService>.Instance));
        }
        async Task Reject(Func<Task> call,HttpStatusCode status,string label)
        {
            try {await call();}
            catch(AppException error) when(error.StatusCode==status) {check(true,label);return;}
            throw new Exception("FAILED: "+label);
        }
        await using var context=new PickleVerseContext(options);var query=new PostgresCommands(context);var queryData = new UnitOfWork(context);
        check(!await context.Database.SqlQueryRaw<bool>("""
            SELECT EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='public'
              AND table_name IN ('gamematches','gamematchplayers') AND data_type IN ('json','jsonb')) AS "Value"
            """).SingleAsync(),"both match tables have no JSON/JSONB columns or replacement blobs");
        check(context.Model.FindEntityType(typeof(Gamematch))!.FindProperty("Data")==null
            && context.Model.FindEntityType(typeof(Gamematchplayer))!.FindProperty("Report")==null,
            "Npgsql EF Core models do not map the removed Data/Report columns");
        check(!await context.Database.SqlQueryRaw<bool>("""
            SELECT EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='public'
              AND table_name='gamematches' AND column_name IN ('ownerid','playerid','hostplayerid')) AS "Value"
            """).SingleAsync(),"the match table does not reintroduce an owner or host player foreign key");
        var ids=Enumerable.Range(0,4).Select(_=>Guid.NewGuid()).ToArray();
        var elos=new[]{400,700,600,900};
        for(int i=0;i<ids.Length;i++)
        {
            var id=ids[i];
            await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO users(userid,email,passwordhash,roleid) SELECT {id},{id+"@columns.invalid"},'disabled',roleid FROM roles WHERE rolename='Player'");
            context.Playerprofiles.Add(new Playerprofile {Userid=id,Playername="Snapshot"+i,Elo=elos[i],Ismale=i%2==0});
        }
        await context.SaveChangesAsync();
        var match=Guid.NewGuid();
        var order=new[]{2,0,3,1}; // Team B submits first; recorder is not the first roster member.
        var request=new OnlineMatchRequest {HostPlayerId=ids[1].ToString("N"),Session="columns-"+match.ToString("N"),Roster=new()
        {
            matchId=match.ToString("N"),mode=PlayMode.Doubles,region="asia",night=true,venue=CourtVenue.LowPoly,
            members=order.Select(i=>new RoomMember {id=ids[i].ToString("N"),team=i<2?0:1,name="forged",elo=99999,group="party-"+(i<2?0:1)}).ToList()
        }};
        var offered=await Game(g=>g.OfferOnline(ids[2],request));
        foreach(var id in ids)await Game(g=>g.PrepareOnline(id,request));
        var root=await MatchQueries.Query(context).SingleAsync(m=>m.Matchid==match);
        check(root.Status==MatchStatus.Preparing && root.Mode==2 && root.Kind=="MatchedDoubles" && root.Photonsession==request.Session
            && root.Region=="asia" && root.Night && root.Venue==(short)CourtVenue.LowPoly && root.Ratingpolicyversion==TestSettings.Load().HybridAi.RatingPolicyVersion,
            "match phase, mode, Photon session, region, environment and policy are stored in typed columns");
        var roster=RelationalMatch.Roster(root);
        check(roster.members.Select(m=>m.id).SequenceEqual(request.Roster.members.Select(m=>m.id)),
            "roster positions survive independent relational reads without reordering 2v2 actors");
        check(roster.members.All(m=>m.name=="Snapshot"+Array.IndexOf(ids,Guid.Parse(m.id)) && m.elo==elos[Array.IndexOf(ids,Guid.Parse(m.id))]
            && m.paddleId=="paddle.default" && m.group=="party-"+m.team),"roster joins server-owned profile/equipment data and preserves party grouping");
        check(root.Gamematchplayers.Count(p=>p.Isreplaywriter)==1 && root.Gamematchplayers.Single(p=>p.Isreplaywriter).Playerid==ids[1],
            "one participant records replay independently of roster order or account ownership");
        bool writerRejected=false;
        try {await context.Gamematchplayers.Where(p=>p.Matchid==match && p.Playerid==ids[0]).ExecuteUpdateAsync(s=>s.SetProperty(p=>p.Isreplaywriter,true));}
        catch(PostgresException e) when(e.SqlState=="23505") {writerRejected=true;}
        check(writerRejected,"database rejects a second replay writer for one match");
        await Game(g=>g.StartOnline(ids[1],request));
        root=await MatchQueries.Query(context).SingleAsync(m=>m.Matchid==match);
        check(root.Status==MatchStatus.Playing && root.Startedat!=null,"starting a match uses Status and StartedAt instead of a JSON phase");
        ReplayMetadata Header(Guid writer)=>new() {playerId=writer.ToString("N"),matchId=match.ToString("N"),night=true,
            actors=roster.members.Select(m=>new ReplayActorInfo {id=m.id,name=m.name,team=m.team,visualId=m.gender,bones=["Root"]}).ToList()};
        await Reject(()=>Replay(r=>r.Save(ids[0],match,Header(ids[0]))),HttpStatusCode.Forbidden,"a participant without the replay-writer column cannot record");
        check((await Replay(r=>r.Save(ids[1],match,Header(ids[1])))).startedUtcTicks==root.Startedat!.Value.Ticks,
            "the designated recorder can start a replay using the relational match timestamp");
        for(int i=0;i<ids.Length;i++)
        {
            var id=ids[i];var elo=1200+i;var name="Renamed"+i;
            await context.Playerprofiles.Where(p=>p.Userid==id).ExecuteUpdateAsync(s=>s.SetProperty(p=>p.Playername,name).SetProperty(p=>p.Elo,elo).SetProperty(p=>p.Ismale,i%2!=0));
        }
        var commands=new Dictionary<Guid,MatchFinishRequest>();
        for(int i=0;i<ids.Length;i++)
        {
            var pending=(await Game(g=>g.Read(ids[i]))).player.pendingMatch;
            check(pending.ownElo==elos[i] && pending.opponents.All(o=>o.name.StartsWith("Renamed",StringComparison.Ordinal)),
                "active match reads current profile names while preserving the Elo used for calculation: "+i);
            bool a=i<2;
            pending.outcome=a?MatchOutcome.Won:MatchOutcome.Lost;pending.gamesWon=a?2:1;pending.gamesLost=a?1:2;
            pending.scores=a?[new(11,5,true),new(9,11,true),new(12,10,true)]:[new(5,11,true),new(11,9,true),new(10,12,true)];
            pending.points=pending.scores.Sum(s=>s.own);pending.validReturns=10+i;pending.validServes=20+i;
            commands[ids[i]]=new() {RequestId=Guid.NewGuid().ToString("N"),Result=pending};
        }
        await Game(g=>g.ReportOnline(ids[3],commands[ids[3]]));
        root=await MatchQueries.Query(context).SingleAsync(m=>m.Matchid==match);
        check(root.Status==MatchStatus.Playing && root.Gamematchplayers.Count(p=>p.Reportedat!=null)==1 && root.Gamematchplayers.All(p=>p.Finalizedat==null)
            && root.Matchscores.OrderBy(s=>s.Gamenumber).Select(s=>(s.Teamascore,s.Teambscore)).SequenceEqual(new[]{(11,5),(9,11),(12,10)}),
            "the first team-B report stores one team-A-oriented score candidate and scalar receipt without finalizing");
        var waiting=await Game(g=>g.Read(ids[3]));
        check(waiting.player.pendingMatch!=null && waiting.player.history.All(h=>h.id!=match.ToString("N")) && waiting.player.coins==0,
            "unconfirmed score candidates are excluded from history and cannot award currency");
        await Game(g=>g.ReportOnline(ids[3],commands[ids[3]]));
        check(await context.Gamematchplayers.CountAsync(p=>p.Matchid==match && p.Reportedat!=null)==1,
            "a repeated report reads scalar receipt fields and cannot create another submission");
        var conflict=GameJson.Read<MatchFinishRequest>(GameJson.Write(commands[ids[0]]));
        conflict.Result.gamesLost=0;conflict.Result.scores=[new(11,0,true),new(11,0,true)];conflict.Result.points=22;
        await Reject(()=>Game(g=>g.ReportOnline(ids[0],conflict)),HttpStatusCode.Conflict,"a conflicting score cannot replace the first participant's candidate");
        foreach(var id in new[]{ids[2],ids[0]})await Game(g=>g.ReportOnline(id,commands[id]));
        await context.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION fail_relational_finalization() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.status='FINISHED' AND OLD.status<>'FINISHED' THEN RAISE EXCEPTION 'injected match finalization failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER fail_relational_finalization BEFORE UPDATE ON gamematches FOR EACH ROW EXECUTE FUNCTION fail_relational_finalization();
            """);
        bool rolledBack=false;
        try {await Game(g=>g.ReportOnline(ids[1],commands[ids[1]]));}
        catch(PostgresException e) when(e.SqlState=="P0001") {rolledBack=true;}
        finally {await context.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_relational_finalization ON gamematches; DROP FUNCTION fail_relational_finalization();");}
        root=await MatchQueries.Query(context).SingleAsync(m=>m.Matchid==match);
        check(rolledBack && root.Status==MatchStatus.Playing && root.Gamematchplayers.All(p=>p.Finalizedat==null) && root.Gamematchplayers.Single(p=>p.Playerid==ids[1]).Reportedat==null,
            "SQL failure rolls back the final submission and every participant finalization atomically");
        check((await Game(g=>g.Read(ids[0]))).player.coins==0,"failed finalization cannot pay a partial reward");
        await Game(g=>g.ReportOnline(ids[1],commands[ids[1]]));
        root=await MatchQueries.Query(context).SingleAsync(m=>m.Matchid==match);
        check(root.Status==MatchStatus.Finished && MatchScoreStore.GameWins(root.Matchscores)==(2,1) && root.Endedat!=null && root.Sequence>0
            && root.Gamematchplayers.All(p=>p.Finalizedat!=null && p.Reportedat!=null),"retry finalizes one root match and its four existing participant rows");
        for(int i=0;i<ids.Length;i++)
        {
            var snapshot=await Game(g=>g.Read(ids[i]));var history=snapshot.player.history.Single(h=>h.id==match.ToString("N"));
            check(history.ownElo==elos[i] && history.opponentElo==(i<2?750:550) && history.validReturns==10+i && history.validServes==20+i
                && history.scores.Count==3 && history.opponents.All(o=>o.name.StartsWith("Renamed",StringComparison.Ordinal))
                && snapshot.player.coins==(i<2?150:75),"history joins current profile names and derives statistics, scores and reward: "+i);
        }
        var paid=(await Game(g=>g.Read(ids[1]))).player.coins;
        await Game(g=>g.ReportOnline(ids[1],commands[ids[1]]));
        check((await Game(g=>g.Read(ids[1]))).player.coins==paid && await context.Matchscores.CountAsync(s=>s.Matchid==match)==3,
            "retry after commit neither duplicates rewards nor adds score rows");
        var custom=new OnlineMatchRequest {HostPlayerId=ids[0].ToString("N"),Session="typed-search-reference",SearchId=Guid.NewGuid().ToString("N"),Roster=new()
            {matchId=Guid.NewGuid().ToString("N"),mode=PlayMode.Singles,custom=true,members=[new(){id=ids[0].ToString("N"),team=0},new(){id=ids[2].ToString("N"),team=1}]}};
        await Game(g=>g.PrepareOnline(ids[0],custom));
        var customId=Guid.Parse(custom.Roster.matchId);
        check((await context.Gamematches.AsNoTracking().SingleAsync(m=>m.Matchid==customId)).Searchid==Guid.Parse(custom.SearchId),
            "the optional Redis search reference is a UUID column without a new database entity");
        await Game(g=>g.LeaveOnline(ids[0],new(){MatchId=custom.Roster.matchId}));
        check((await context.Gamematches.AsNoTracking().SingleAsync(m=>m.Matchid==customId)).Status==MatchStatus.Cancelled,
            "leaving during preparation cancels through the scalar status column");
        clock.Now=clock.Now.AddSeconds(1);
        var practice=(await Game(g=>g.StartBot(ids[1],new(){RequestId=Guid.NewGuid().ToString("N"),Kind=MatchKind.BotPractice}))).player.pendingMatch;
        practice.outcome=MatchOutcome.Lost;practice.localForfeit=true;practice.scores=[new(4,2,false)];practice.points=4;practice.validReturns=3;practice.validServes=2;
        var finish=new MatchFinishRequest {RequestId=Guid.NewGuid().ToString("N"),Result=practice};
        await Game(g=>g.FinishBot(ids[1],finish));
        var botRoot=await MatchQueries.Query(context).SingleAsync(m=>m.Matchid==Guid.Parse(practice.id));
        var human=botRoot.Gamematchplayers.Single(p=>!RankedBotAccounts.Contains(p.Playerid.ToString("N")));
        var bot=botRoot.Gamematchplayers.Single(p=>RankedBotAccounts.Contains(p.Playerid.ToString("N")));
        check(human.Isreplaywriter && !bot.Isreplaywriter && human.Localforfeit && human.Endreason.Length>0 && human.Validreturns==3 && human.Validserves==2
            && bot.Outcome=="WIN" && RelationalMatch.Result(botRoot,bot).points==2,"bot practice persists match-specific facts and derives bot identity and points");
        var restored=(await Game(g=>g.Read(ids[1]))).player.history.Single(h=>h.id==practice.id);
        check(restored.scores.Count==1 && !restored.scores[0].completed && restored.scores[0].own==4 && restored.reason==human.Endreason,
            "unfinished bot game scores and forfeit reason reload without a JSON result document");
        var losses=(await Game(g=>g.Read(ids[1]))).player.losses;
        await Game(g=>g.FinishBot(ids[1],finish));
        check((await Game(g=>g.Read(ids[1]))).player.losses==losses,"bot result comparison from canonical relations prevents duplicate finalization");

        // PvE may reuse virtual profiles. Only live online matches reserve a ranked bot;
        // the old JSON phase query excluded PvE, so scalar status must preserve that rule.
        var role=await context.Roles.Where(r=>r.Rolename=="Player").Select(r=>r.Roleid).SingleAsync();
        for(int i=0;i<RankedBotAccounts.Ids.Count;i++)
        {
            var owner=Guid.NewGuid();var practiceId=Guid.NewGuid();
            context.Users.Add(new User {Userid=owner,Email=owner+"@occupied.invalid",Passwordhash="disabled",Roleid=role,Status=UserStatus.Active});
            context.Playerprofiles.Add(new Playerprofile {Userid=owner,Playername="Occupied"+i,Elo=100});
            context.Gamematches.Add(new Gamematch {Matchid=practiceId,Kind="BotPractice",Status=MatchStatus.Playing,Createdat=clock.Now.UtcDateTime,Updatedat=clock.Now.UtcDateTime,Startedat=clock.Now.UtcDateTime});
            context.Gamematchplayers.Add(RelationalMatch.Seat(practiceId,new() {id=owner.ToString("N"),name="Occupied"+i,team=0},true,clock.Now.UtcDateTime));
            context.Gamematchplayers.Add(RelationalMatch.Seat(practiceId,new() {id=RankedBotAccounts.Ids[i],name="Virtual"+i,team=1,bot=true},false,clock.Now.UtcDateTime,1));
        }
        await context.SaveChangesAsync();
        var search=Guid.NewGuid();await Game(g=>g.BeginRankedSearch(ids[0],new(){SearchId=search}));
        clock.Now=clock.Now.AddSeconds(TestSettings.Load().Matchmaking.BotFallbackSeconds);
        var assigned=await Game(g=>g.AssignRankedBot(ids[0],new(){SearchId=search,MatchId=Guid.NewGuid(),Session="pve-profile-reuse"}));
        check(assigned.bot && RankedBotAccounts.Contains(assigned.id),"scalar match statuses preserve virtual-profile reuse between PvE practice and online fallback");
    }
}
