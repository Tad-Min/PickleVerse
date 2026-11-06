using System.Net;
using Entities.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Entities.Exceptions;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using Repositories.Context;
using Services.Configuration;
using Services.Implementations;
using static Repositories.Data.PostgresCommands;

static class DedicatedChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, Action<bool, string> check)
    {
        const string key = "dedicated-integration-key-not-used-by-a-real-server-123456789";
        var config = new ConfigurationBuilder().AddConfiguration(TestSettings.Configuration()).AddInMemoryCollection(new Dictionary<string,string?> {
            ["DedicatedServer:Enabled"]="true", ["DedicatedServer:ServiceKey"]=key,
            ["HybridAi:IngestEnabled"]="true", ["HybridAi:CoachingEnabled"]="true" }).Build();
        var settings = new ServerSettings(config);
        var leases = new DedicatedLeaseRepository(TestRedis.Connection, TestRedis.Settings);
        string worker=Guid.NewGuid().ToString("N");
        bool workerRunning = false;
        async Task<T> Call<T>(Func<OnlineGameService, DedicatedServerService, Task<T>> action, ServerSettings? runtimeSettings = null)
        {
            // The fixture represents a live worker. Keep its liveness and claimed lease
            // fresh between HTTP-equivalent actions, including on a busy build machine.
            if (workerRunning)
            {
                await leases.WorkerAlive();
                var live = await leases.Read();
                if (live?.WorkerId == worker) await leases.Renew(live, settings.Dedicated.LeaseSeconds);
            }
            var active = runtimeSettings ?? settings;
            await using var db = new PickleVerseContext(options); var unit = new UnitOfWork(db);
            var catalog = new GameCatalogService(unit, active);
            var replays = new MatchReplayService(unit, TestReplayStorage.Shared, NullLogger<MatchReplayService>.Instance);
            var game = new OnlineGameService(unit, catalog, TimeProvider.System, active, TestRedis.State, TestRedis.Leaderboard, replays);
            var coaching = new HybridCoachingService(unit, catalog, active.HybridAi, TimeProvider.System);
            return await action(game, new DedicatedServerService(unit, leases, active, game, coaching, replays));
        }
        async Task Reject(Func<Task> action, HttpStatusCode expected, string name)
        {
            bool rejected=false;
            try { await action(); } catch(AppException error) { if(error.StatusCode!=expected)throw; rejected=true; }
            check(rejected,name);
        }
        async Task<OnlineMatchRequest> Match(bool custom, int size=2)
        {
            await using var db=new PickleVerseContext(options);var sql=new PostgresCommands(db);
            var roster=new MatchRoster{matchId=Guid.NewGuid().ToString("N"),mode=size==4?PlayMode.Doubles:PlayMode.Singles,
                custom=custom,region="asia"};
            for(int i=0;i<size;i++)
            {
                var id=Guid.NewGuid();string name="Ds"+id.ToString("N")[..10];
                await sql.Run("INSERT INTO users(userid,email,passwordhash,roleid) SELECT @id,@email,'disabled-fixture',roleid FROM roles WHERE rolename='Player'",P("id",id),P("email",id+"@example.invalid"));
                await sql.Run("INSERT INTO playerprofiles(userid,playername,elo) VALUES(@id,@name,400)",P("id",id),P("name",name));
                roster.members.Add(new(){id=id.ToString("N"),team=i%2,name="forged",elo=9999});
            }
            var request=new OnlineMatchRequest{Roster=roster,HostPlayerId=roster.members[0].id,Session="ds-fixture-"+roster.matchId};
            foreach(var member in roster.members) await Call((g,_)=>g.PrepareOnline(Guid.Parse(member.id),request));
            foreach(var member in roster.members) await Call((g,_)=>g.StartOnline(Guid.Parse(member.id),request));
            return request;
        }
        Task<DedicatedTicket> Ticket(OnlineMatchRequest match) => Call((_,s)=>s.Ticket(Guid.Parse(match.HostPlayerId),new(){MatchId=match.Roster.matchId}));
        async Task<List<DedicatedPlayerResult>> Results(OnlineMatchRequest match)
        {
            var results=new List<DedicatedPlayerResult>();
            foreach(var member in match.Roster.members)
            {
                var snapshot=await Call((g,_)=>g.Read(Guid.Parse(member.id)));
                var result=snapshot.player.pendingMatch;
                bool won=member.team==0;
                result.playerId=member.id;result.outcome=won?MatchOutcome.Won:MatchOutcome.Lost;
                result.gamesWon=won?2:0;result.gamesLost=won?0:2;
                result.scores=won?[new(11,5,true),new(11,8,true)]:[new(5,11,true),new(8,11,true)];
                result.points=result.scores.Sum(g=>g.own);
                result.validServes=4;result.validReturns=12;
                results.Add(new(){onlineId=member.id,result=result});
            }
            return results;
        }
        async Task Complete(DedicatedAllocation allocation,List<DedicatedPlayerResult> results) =>
            await Call(async(_,s)=>{await s.Complete(new(){LeaseId=allocation.LeaseId,Results=results});return true;});

        check(await Call((_,s)=>Task.FromResult(s.Authenticate(key)&&!s.Authenticate("wrong"))),"dedicated service credential is required");
        var first=await Match(false);Guid owner=Guid.Parse(first.HostPlayerId);
        var disabled = new ServerSettings(new ConfigurationBuilder().AddConfiguration(config).AddInMemoryCollection(new Dictionary<string,string?> { ["DedicatedServer:Enabled"]="false" }).Build());
        check((await Call((g,_)=>g.StartOnline(owner,first),disabled)).authorityMode=="dedicated","match authority survives a feature flag change after creation");
        await Reject(()=>Ticket(first),HttpStatusCode.ServiceUnavailable,"no worker cannot allocate a match");
        await Call((_,s)=>s.Poll(worker));
        workerRunning = true;
        check((await Ticket(first)).State==DedicatedAllocationStatus.Queued,"first match reserves the single server slot");
        var job=(await Call((_,s)=>s.Poll(worker)))!;
        check(await Call((_,s)=>s.Poll(Guid.NewGuid().ToString("N")))==null,"another worker cannot steal a live allocation");
        check(job.Roster.Find(first.HostPlayerId).elo==400,"dedicated roster uses database rating rather than client input");
        var second=await Match(true);
        await Reject(()=>Ticket(second),HttpStatusCode.Conflict,"custom match reports server busy");
        var waiting=await Match(false);
        check((await Ticket(waiting)).State==DedicatedAllocationStatus.Busy,"ranked match remains waiting while the slot is occupied");
        await Call(async(_,s)=>{await s.Renew(new(){LeaseId=job.LeaseId},true);return true;});
        var ticket=await Ticket(first);
        check(DedicatedAdmission.Verify(Convert.FromBase64String(ticket.ConnectionToken),key,job.MatchId,job.LeaseId,DateTimeOffset.UtcNow.ToUnixTimeSeconds())?.playerId==first.HostPlayerId,
            "database allocation issues a ticket accepted by the actual Unity verifier");
        var results=await Results(first);
        await Reject(()=>Call((g,_)=>g.ReportOnline(owner,new(){RequestId=Guid.NewGuid().ToString("N"),Result=results[0].result})),HttpStatusCode.Forbidden,"dedicated rejects a client reporting its own result");
        await Reject(()=>Call((g,_)=>g.LeaveOnline(owner,new(){MatchId=job.MatchId,RequestId=Guid.NewGuid().ToString("N")})),HttpStatusCode.Forbidden,"dedicated client leave API cannot finalize the official result");
        await Task.WhenAll(Complete(job,results),Complete(job,results));
        var winner=await Call((g,_)=>g.Read(owner));
        check(winner.player.pendingMatch==null&&winner.player.coins==150&&winner.player.wins==1,"server completion grants the singles result exactly once");
        await using(var db=new PickleVerseContext(options))
            check(await db.Ratingledgers.CountAsync(r=>r.Matchid==Guid.Parse(job.MatchId))==2,"dedicated singles commits one rating ledger row per participant");
        await leases.Release(job.LeaseId);
        await Ticket(second);var failed=(await Call((_,s)=>s.Poll(worker)))!;
        await leases.Release(failed.LeaseId);
        await Reject(()=>Ticket(second),HttpStatusCode.Gone,"lost allocation cannot restart a match with empty simulation state");
        var interrupted=await Call((g,_)=>g.Read(Guid.Parse(second.HostPlayerId)));
        check(interrupted.player.history[0].outcome==MatchOutcome.Interrupted&&interrupted.player.coins==0,"lost server ends interrupted without inferring a winner");
        await Ticket(waiting);var next=(await Call((_,s)=>s.Poll(worker)))!;
        await using(var db=new PickleVerseContext(options))
            await new PostgresCommands(db).Run("UPDATE gamematches SET serverheartbeatat=CURRENT_TIMESTAMP-INTERVAL '2 minutes',updatedat=CURRENT_TIMESTAMP WHERE matchid=@id",P("id",Guid.Parse(next.MatchId)));
        await Call((g,_)=>g.HeartbeatOnline(Guid.Parse(waiting.HostPlayerId),new(){MatchId=next.MatchId,RequestId=Guid.NewGuid().ToString("N")}));
        await Call(async(_,s)=>{await s.InterruptExpired();return true;});
        check((await Call((g,_)=>g.Read(Guid.Parse(waiting.HostPlayerId)))).player.history[0].outcome==MatchOutcome.Interrupted,"client heartbeat cannot conceal a dead server heartbeat");
        await leases.Release(next.LeaseId);
        var doubles=await Match(true,4);await Ticket(doubles);var fourth=(await Call((_,s)=>s.Poll(worker)))!;
        await Complete(fourth,await Results(doubles));
        await using(var db=new PickleVerseContext(options))
        {
            var id=Guid.Parse(fourth.MatchId);
            check(await db.Gamematchplayers.CountAsync(p=>p.Matchid==id&&p.Finalizedat!=null)==4,"server doubles finalizes all four participants atomically");
            check(await db.Matchscores.CountAsync(s=>s.Matchid==id&&s.Completed)==2,"server doubles stores the two completed game scores");
        }
        await leases.Release(fourth.LeaseId);
        var actor=await new FunctionalFixture(options).Player("Admin");
        async Task<DedicatedOperationRecord> Stop(Guid match)
        {
            using var db=new PickleVerseContext(options);using var unit=new UnitOfWork(db);
            return await new ModerationService(unit,TimeProvider.System).StopMatch(actor,match,"Quản trị dừng để kiểm tra sự cố.");
        }
        var stopped=await Match(true);await Ticket(stopped);var stoppedJob=(await Call((_,s)=>s.Poll(worker)))!;
        var late=await Results(stopped);var operation=await Stop(Guid.Parse(stoppedJob.MatchId));
        await Reject(()=>Call(async(_,s)=>{await s.AuthorizeAdmission(new(){LeaseId=stoppedJob.LeaseId,PlayerId=Guid.Parse(stopped.HostPlayerId)});return true;}),
            HttpStatusCode.Forbidden,"an already issued ticket cannot rejoin after a stop or sanction commits");
        await Complete(stoppedJob,late);
        var stoppedState=await Call((g,_)=>g.Read(Guid.Parse(stopped.HostPlayerId)));
        check(stoppedState.player.history[0].outcome==MatchOutcome.Interrupted && stoppedState.player.coins==0,
            "durable stop fences completion even before the worker receives the command");
        var heartbeat=await Call((_,s)=>s.Renew(new(){LeaseId=stoppedJob.LeaseId},false));
        check(heartbeat.StopOperationId==operation.OperationId,"heartbeat redelivers the durable stop command until acknowledged");
        await Reject(()=>Call(async(_,s)=>{await s.Acknowledge(new(){LeaseId=stoppedJob.LeaseId,OperationId=Guid.NewGuid()});return true;}),
            HttpStatusCode.Forbidden,"worker cannot acknowledge an unrelated operation");
        await Call(async(_,s)=>{await s.Acknowledge(new(){LeaseId=stoppedJob.LeaseId,OperationId=operation.OperationId});return true;});
        await Complete(stoppedJob,late);
        await using(var db=new PickleVerseContext(options))
        {
            check(!await db.Ratingledgers.AnyAsync(r=>r.Matchid==operation.MatchId),"admin interruption creates no Elo ledger");
            check((await db.DedicatedOperations.SingleAsync(o=>o.OperationId==operation.OperationId)).AcknowledgedAt!=null,
                "worker acknowledgement is durable and tied to its lease");
            check(await db.Gamematchplayers.CountAsync(p=>p.Matchid==operation.MatchId&&p.Outcome=="INTERRUPTED")==2,
                "late normal result cannot resurrect the interrupted match");
        }
        await leases.Release(stoppedJob.LeaseId);
        var racing=await Match(true);await Ticket(racing);var racingJob=(await Call((_,s)=>s.Poll(worker)))!;var raceResults=await Results(racing);
        bool stopWon=true;
        async Task RaceStop(){try{await Stop(Guid.Parse(racingJob.MatchId));}catch(AppException e) when(e.StatusCode==HttpStatusCode.Conflict){stopWon=false;}}
        await Task.WhenAll(Complete(racingJob,raceResults),RaceStop());
        await using(var db=new PickleVerseContext(options))
        {
            var outcomes=await db.Gamematchplayers.Where(p=>p.Matchid==Guid.Parse(racingJob.MatchId)).Select(p=>p.Outcome).ToListAsync();
            check(stopWon?outcomes.All(x=>x=="INTERRUPTED"):outcomes.Order().SequenceEqual(new[]{"LOSE","WIN"}),
                "simultaneous stop and completion commit exactly one terminal outcome");
        }
        await leases.Release(racingJob.LeaseId);
    }
}
