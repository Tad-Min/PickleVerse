using Entities.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using Repositories.Context;
using Repositories.UnitOfWork;
using Entities.Exceptions;
using Services.Implementations;
using System.Net;

static class NormalizedParticipantChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, TestClock clock, Action<bool,string> check)
    {
        async Task<T> Game<T>(Func<OnlineGameService,Task<T>> call)
        {
            await using var db=new PickleVerseContext(options);var sql=new PostgresCommands(db);var sqlData = new UnitOfWork(db);var settings=TestSettings.Load();
            return await call(new OnlineGameService(sqlData,new GameCatalogService(sqlData,settings),clock,settings,TestRedis.State,TestRedis.Leaderboard,
                new MatchReplayService(sqlData,TestReplayStorage.Shared,NullLogger<MatchReplayService>.Instance)));
        }
        async Task Conflict(Func<Task> call,string label)
        {
            try {await call();}
            catch(AppException error) when(error.StatusCode==HttpStatusCode.Conflict) {check(true,label);return;}
            throw new Exception("FAILED: "+label);
        }
        await using var context=new PickleVerseContext(options);
        var removed=new[]{"Playername","Ismale","Paddleid","Isbot","Points","Accepted","Rosterready","Receivedelo","Fingerprint"};
        var model=context.Model.FindEntityType(typeof(Gamematchplayer))!;
        check(model.GetProperties().Count()==15 && removed.All(name=>model.FindProperty(name)==null),"participant EF mapping has 15 scalar fields and none of the nine duplicate fields");
        var columns=await context.Database.SqlQueryRaw<string>("""SELECT column_name AS "Value" FROM information_schema.columns WHERE table_schema='public' AND table_name='gamematchplayers'""").ToListAsync();
        check(columns.Count==15 && removed.All(name=>!columns.Contains(name.ToLowerInvariant())),"canonical SQL has the same 15 participant columns and no hidden duplicates");
        check(context.Model.FindEntityType(typeof(Ratingledger))!.GetTableName()=="ratingledger",
            "EF maps the existing ratingledger table as the sole persisted source of Elo deltas");
        var role=await context.Roles.Where(r=>r.Rolename=="Player").Select(r=>r.Roleid).SingleAsync();
        var a=Guid.NewGuid();var b=Guid.NewGuid();
        foreach(var id in new[]{a,b})
        {
            context.Users.Add(new User {Userid=id,Email=id+"@normalized.invalid",Passwordhash="disabled",Roleid=role,Status="ACTIVE"});
            context.Playerprofiles.Add(new Playerprofile {Userid=id,Playername=id==a?"OriginalA":"OriginalB",Elo=id==a?400:700,Ismale=id==a});
        }
        await context.SaveChangesAsync();
        var request=new OnlineMatchRequest {HostPlayerId=a.ToString("N"),Session="normalized-participants",Roster=new()
        {
            matchId=Guid.NewGuid().ToString("N"),mode=PlayMode.Singles,region="asia",
            members=[new(){id=a.ToString("N"),team=0},new(){id=b.ToString("N"),team=1}]
        }};
        var first=await Game(g=>g.PrepareOnline(a,request));
        check(first.roster.Find(a.ToString("N")).ready && !first.roster.Find(b.ToString("N")).ready && !first.allAccepted,
            "individual ready flags come from Redis consent without SQL Accepted/RosterReady copies");
        await Game(g=>g.PrepareOnline(b,request));await Game(g=>g.StartOnline(a,request));
        var match=Guid.Parse(request.Roster.matchId);
        var record=await MatchQueries.Query(context).SingleAsync(m=>m.Matchid==match);
        check(RelationalMatch.Roster(record).members.All(p=>p.ready),"started roster readiness derives from the durable match start");
        await context.Playerprofiles.Where(p=>p.Userid==b).ExecuteUpdateAsync(s=>s.SetProperty(p=>p.Playername,"CurrentB").SetProperty(p=>p.Ismale,true));
        await context.Playercosmetics.Where(p=>p.Playerid==b).ExecuteUpdateAsync(s=>s.SetProperty(p=>p.Equipped,false));
        var cosmetic=await context.Cosmetics.AsNoTracking().Where(c=>c.Itemcode!="paddle.default").OrderBy(c=>c.Cosmeticid).FirstAsync();
        context.Playercosmetics.Add(new Playercosmetic {Playerid=b,Cosmeticid=cosmetic.Cosmeticid,Equipped=true});await context.SaveChangesAsync();
        record=await MatchQueries.Query(context).SingleAsync(m=>m.Matchid==match);
        var member=RelationalMatch.Roster(record).Find(b.ToString("N"));
        check(member.name=="CurrentB" && member.gender=="Male","name and gender resolve from the current PlayerProfiles row");
        check(member.paddleId==cosmetic.Itemcode,"equipped paddle resolves through PlayerCosmetics and Cosmetics");
        check(record.Gamematchplayers.All(p=>!RelationalMatch.Member(record,p).bot),"human/bot classification derives from the existing profile identities");
        MatchFinishRequest Finish(Guid id,bool won)
        {
            var result=RelationalMatch.Result(record,record.Gamematchplayers.Single(p=>p.Playerid==id),false);
            result.outcome=won?MatchOutcome.Won:MatchOutcome.Lost;result.gamesWon=won?2:0;result.gamesLost=won?0:2;
            result.scores=won?[new(11,5,true),new(11,8,true)]:[new(5,11,true),new(8,11,true)];
            result.points=result.scores.Sum(s=>s.own);result.validReturns=won?9:2;result.validServes=won?3:1;
            return new(){RequestId=Guid.NewGuid().ToString("N"),Result=result};
        }
        var win=Finish(a,true);var lose=Finish(b,false);
        await Game(g=>g.ReportOnline(a,win));
        await context.Playerprofiles.Where(p=>p.Userid==b).ExecuteUpdateAsync(s=>s.SetProperty(p=>p.Playername,"LatestB"));
        await Game(g=>g.ReportOnline(a,win));
        check((await Game(g=>g.Read(a))).player.history.Count==0,"profile changes do not break an identical pending score retry or publish unconfirmed history");
        var changed=GameJson.Read<MatchFinishRequest>(GameJson.Write(win));changed.Result.validReturns++;
        await Conflict(()=>Game(g=>g.ReportOnline(a,changed)),"changed per-player shot statistics are still rejected without a stored fingerprint");
        await Game(g=>g.ReportOnline(b,lose));
        record=await MatchQueries.Query(context).SingleAsync(m=>m.Matchid==match);
        var delta=record.Ratingledgers.Single(r=>r.Playerid==a).Delta;
        check(record.Ratingledgers.Count==2 && delta==EloPolicy.Delta(400,700,true,0,0),"ranked finalization writes one Elo delta per player to ratingledger");
        var state=await Game(g=>g.Read(a));var history=state.player.history.Single(h=>h.id==request.Roster.matchId);
        check(history.eloDelta==delta && history.ownElo==400 && history.opponentElo==700,"history reads Elo delta from the ledger while retaining the match's calculation inputs");
        check(history.points==22 && history.opponents.Single().name=="LatestB","history derives total points from team scores and joins the current opponent name");
        await using(var legacyDb=new PickleVerseContext(options))
        {
            using var uow=new UnitOfWork(legacyDb);
            var legacy=await new MatchService(uow).GetMyMatchHistoryAsync(a);
            check(legacy.Single().ReceivedElo==delta,"legacy history API uses the same ratingledger delta without a participant copy");
        }
        var paid=state.player.coins;
        await Game(g=>g.ReportOnline(a,win));
        check((await Game(g=>g.Read(a))).player.coins==paid && await context.Ratingledgers.CountAsync(r=>r.Matchid==match)==2,
            "post-commit result retries cannot duplicate rewards or ledger rows after removing fingerprint/delta copies");
        clock.Now=clock.Now.AddSeconds(1);
        var before=await Game(g=>g.Read(a));
        var practice=(await Game(g=>g.StartBot(a,new(){RequestId=Guid.NewGuid().ToString("N"),Kind=MatchKind.BotPractice}))).player.pendingMatch;
        practice.outcome=MatchOutcome.Lost;practice.localForfeit=true;practice.scores=[new(4,2,false)];practice.points=999;practice.validReturns=3;practice.validServes=2;
        var submit=new MatchFinishRequest {RequestId=Guid.NewGuid().ToString("N"),Result=practice};
        await Game(g=>g.FinishBot(a,submit));
        var after=await Game(g=>g.Read(a));var botHistory=after.player.history.Single(h=>h.id==practice.id);
        check(botHistory.points==4 && botHistory.scores.Single().own==4,"bot points are derived from actual scores instead of trusting a redundant client total");
        check(after.player.lifetime.points==before.player.lifetime.points,"forfeit still grants no progression points even when the client supplies a forged total");
        var botRecord=await MatchQueries.Query(context).SingleAsync(m=>m.Matchid==Guid.Parse(practice.id));
        var bot=botRecord.Gamematchplayers.Single(p=>RankedBotAccounts.Contains(p.Playerid.ToString("N")));
        check(RelationalMatch.Member(botRecord,bot).bot && RelationalMatch.Result(botRecord,bot).points==2 && botRecord.Ratingledgers.Count==0,
            "bot identity, opposite-side points and zero practice Elo require no duplicated participant fields");
        var retry=GameJson.Read<MatchFinishRequest>(GameJson.Write(submit));retry.RequestId=Guid.NewGuid().ToString("N");retry.Result.points=0;
        await Game(g=>g.FinishBot(a,retry));
        check((await Game(g=>g.Read(a))).player.losses==after.player.losses,"derived-point differences cannot cause a second bot result or duplicate loss");
        retry.RequestId=Guid.NewGuid().ToString("N");retry.Result.validServes++;
        await Conflict(()=>Game(g=>g.FinishBot(a,retry)),"a changed canonical bot statistic still conflicts without a persisted result hash");
        clock.Now=clock.Now.AddSeconds(1);
        var completed=(await Game(g=>g.StartBot(a,new(){RequestId=Guid.NewGuid().ToString("N"),Kind=MatchKind.BotPractice}))).player.pendingMatch;
        completed.outcome=MatchOutcome.Won;completed.gamesWon=2;completed.gamesLost=0;
        completed.scores=[new(11,5,true),new(11,7,true)];completed.points=999;
        await Game(g=>g.FinishBot(a,new(){RequestId=Guid.NewGuid().ToString("N"),Result=completed}));
        var winner=await Game(g=>g.Read(a));
        check(winner.player.history.Single(h=>h.id==completed.id).points==22 && winner.player.lifetime.points==before.player.lifetime.points+22,
            "normal match history and progression both use the score sum rather than a forged redundant client total");
    }
}
