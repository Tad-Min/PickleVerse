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
        async Task<T> Call<T>(Func<OnlineGameService, DedicatedServerService, Task<T>> action, ServerSettings? runtimeSettings = null)
        {
            var active = runtimeSettings ?? settings;
            await using var db = new PickleVerseContext(options); var unit = new UnitOfWork(db);
            var catalog = new GameCatalogService(unit, active);
            var replays = new MatchReplayService(unit, TestReplayStorage.Shared, NullLogger<MatchReplayService>.Instance);
            var game = new OnlineGameService(unit, catalog, TimeProvider.System, active, TestRedis.State, TestRedis.Leaderboard, replays, leases);
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
            return request;
        }
        async Task Enter(OnlineMatchRequest request)
        {
            await leases.Poll(worker, settings.Dedicated.LeaseSeconds);
            foreach (var member in request.Roster.members)
            {
                var ready = await Call((g,_) => g.PrepareOnline(Guid.Parse(member.id), request));
                if (ready.phase != "PREPARING") throw new InvalidOperationException("Fixture did not get a reservation.");
            }
            await Call((g,_) => g.StartOnline(Guid.Parse(request.HostPlayerId), request));
        }
        Task<DedicatedAllocation?> Poll(string id) => Call((_,s)=>s.Poll(id, settings.Dedicated.Protocol));
        Task<DedicatedTicket> Ticket(OnlineMatchRequest match) => Call((_,s)=>s.Ticket(Guid.Parse(match.HostPlayerId),new(){MatchId=match.Roster.matchId}, 0));
        async Task<DedicatedAllocation> Take(OnlineMatchRequest match)
        {
            var reserved = await leases.ReadMatch(match.Roster.matchId);
            if (reserved == null) throw new InvalidOperationException("Fixture has no worker reservation.");
            return await Poll(reserved.WorkerId) ?? throw new InvalidOperationException("Reserved worker did not receive the assignment.");
        }
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
                result.scores=won?[new(11,5,true,-1),new(11,8,true,1)]:[new(5,11,true,-1),new(8,11,true,1)];
                result.points=result.scores.Sum(g=>g.own);
                result.validServes=4;result.validReturns=12;
                results.Add(new(){onlineId=member.id,result=result});
            }
            return results;
        }
        async Task Complete(DedicatedAllocation allocation,List<DedicatedPlayerResult> results) =>
            await Call(async(_,s)=>{await s.Complete(new(){LeaseId=allocation.LeaseId,Results=results},allocation.WorkerId);return true;});

        check(await Call((_,s)=>Task.FromResult(s.Authenticate(key)&&!s.Authenticate("wrong"))),"dedicated service credential is required");
        var first=await Match(false);Guid owner=Guid.Parse(first.HostPlayerId);
        var noWorker = await Call((g,_) => g.OfferOnline(owner, first));
        check(noWorker.phase == "WAITING_CAPACITY" && noWorker.readyRemainingSeconds == 0, "wait for capacity before showing confirmation");
        await Enter(first);
        var disabled = new ServerSettings(new ConfigurationBuilder().AddConfiguration(config).AddInMemoryCollection(new Dictionary<string,string?> { ["DedicatedServer:Enabled"]="false" }).Build());
        check((await Call((g,_)=>g.StartOnline(owner,first),disabled)).authorityMode=="dedicated","match authority survives a feature flag change after creation");
        check((await Ticket(first)).State==DedicatedAllocationStatus.Queued,"confirmed match activates its reserved worker");
        var job=(await Poll(worker))!;
        check(await Poll(Guid.NewGuid().ToString("N"))==null,"another worker cannot steal a live allocation");
        await Reject(() => Call((_,s) => s.Renew(new() { LeaseId = job.LeaseId }, true, Guid.NewGuid().ToString("N"))),
            HttpStatusCode.Gone, "a different worker cannot renew a claimed lease");
        check(job.Roster.Find(first.HostPlayerId).elo==400,"dedicated roster uses database rating rather than client input");
        var second=await Match(true);
        check((await Call((g,_) => g.OfferOnline(Guid.Parse(second.HostPlayerId), second))).phase == "WAITING_CAPACITY",
            "second match waits before confirmation while the configured capacity is full");
        var waiting=await Match(false);
        await Call(async(_,s)=>{await s.Renew(new(){LeaseId=job.LeaseId},true,worker);return true;});
        var ticket=await Ticket(first);
        check(DedicatedAdmission.Verify(Convert.FromBase64String(ticket.ConnectionToken),key,job.MatchId,job.LeaseId,DateTimeOffset.UtcNow.ToUnixTimeSeconds())?.playerId==first.HostPlayerId,
            "database allocation issues a ticket accepted by the actual Unity verifier");
        await Call(async (_,s) => { await s.AuthorizeAdmission(new() { LeaseId = job.LeaseId, PlayerId = owner, AuthVersion = 0 }, worker); return true; });
        var connectedSessions = new DedicatedPlayerSessions();
        connectedSessions.TryAdmit(owner.ToString("N"), 0, 2, out _);
        string nativeHeartbeat = Newtonsoft.Json.JsonConvert.SerializeObject(connectedSessions.CreateHeartbeat(job.LeaseId,
            new { scoreA = 0, scoreB = 0, gamesA = 0, gamesB = 0, players = job.Roster.members.Select(m => new { id = m.id, connected = true }).ToArray() }));
        foreach (bool compact in new[] { false, true })
        {
            for (int cycle = 0; cycle < 3; cycle++)
            {
                string wire = compact ? nativeHeartbeat.Replace(owner.ToString("D"), owner.ToString("N")) : nativeHeartbeat;
                var bound = System.Text.Json.JsonSerializer.Deserialize<Entities.Game.DedicatedCommand>(wire,
                    new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
                var renewed = await Call((_,s) => s.Renew(bound, false, worker));
                check(renewed.ExpiredSessions.Count == 0 && (await leases.ReadMatch(job.MatchId)) != null,
                    (compact ? "deployed compact" : "current Unity") + " heartbeat renews after admission and court spawn, cycle " + cycle);
            }
        }
        await using (var db = new PickleVerseContext(options))
            await db.Users.Where(u => u.Userid == owner).ExecuteUpdateAsync(update => update.SetProperty(u => u.Authversion, 1));
        await Reject(() => Ticket(first), HttpStatusCode.Unauthorized, "an in-flight ticket request cannot borrow the replacement login epoch");
        await Reject(() => Call(async (_,s) => { await s.AuthorizeAdmission(new() { LeaseId = job.LeaseId, PlayerId = owner, AuthVersion = 0 }, worker); return true; }),
            HttpStatusCode.Forbidden, "an already signed ticket cannot enter after its login is replaced");
        var replacement = await Call((_,s) => s.Ticket(owner, new() { MatchId = job.MatchId }, 1));
        check(replacement.AuthVersion == 1 && DedicatedAdmission.Verify(Convert.FromBase64String(replacement.ConnectionToken), key, job.MatchId, job.LeaseId,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds())?.authVersion == 1, "replacement device gets a signed ticket for its login epoch");
        await Call(async (_,s) => { await s.AuthorizeAdmission(new() { LeaseId = job.LeaseId, PlayerId = owner, AuthVersion = 1 }, worker); return true; });
        var expired = await Call((_,s) => s.Renew(new() { LeaseId = job.LeaseId, Sessions = [new() { PlayerId = owner, AuthVersion = 0 }] }, false, worker));
        check(expired.ExpiredSessions.Count == 1 && expired.ExpiredSessions[0].PlayerId == owner && expired.ExpiredSessions[0].AuthVersion == 0,
            "heartbeat fences a replaced login while preserving its reconnect reservation");
        var active = await Call((_,s) => s.Renew(new() { LeaseId = job.LeaseId, Sessions = [new() { PlayerId = owner, AuthVersion = 1 }] }, false, worker));
        check(active.ExpiredSessions.Count == 0, "heartbeat preserves the replacement session");
        await Reject(() => Call((_,s) => s.Renew(new() { LeaseId = job.LeaseId, Sessions = [new() { PlayerId = Guid.NewGuid(), AuthVersion = 0 }] }, false, worker)),
            HttpStatusCode.BadRequest, "heartbeat cannot check sessions outside its assigned roster");
        check((await Call((g,_) => g.Read(owner))).player.pendingMatch?.id == job.MatchId,
            "login takeover preserves the pending match for the replacement device");
        var results=await Results(first);
        await Reject(()=>Call((g,_)=>g.ReportOnline(owner,new(){RequestId=Guid.NewGuid().ToString("N"),Result=results[0].result})),HttpStatusCode.Forbidden,"dedicated rejects a client reporting its own result");
        await Reject(()=>Call((g,_)=>g.LeaveOnline(owner,new(){MatchId=job.MatchId,RequestId=Guid.NewGuid().ToString("N")})),HttpStatusCode.Forbidden,"dedicated client leave API cannot finalize the official result");
        await Task.WhenAll(Complete(job,results),Complete(job,results));
        var winner=await Call((g,_)=>g.Read(owner));
        check(winner.player.pendingMatch==null&&winner.player.coins==150&&winner.player.wins==1,"server completion grants the singles result exactly once");
        check(winner.player.level == 2 && winner.player.levelXp == 0 && winner.player.totalXp == 10 && winner.player.NextLevelXp == 20,
            "ten XP from the first ranked win persists level two once even when completion is retried");
        var loser = await Call((g, _) => g.Read(Guid.Parse(first.Roster.members[1].id)));
        check(loser.player.level == 1 && loser.player.levelXp == 5 && loser.player.totalXp == 5,
            "a ranked loss at level one grants the configured loss XP");
        await using(var db=new PickleVerseContext(options))
            check(await db.Ratingledgers.CountAsync(r=>r.Matchid==Guid.Parse(job.MatchId))==2,"dedicated singles commits one rating ledger row per participant");
        await leases.Release(job.LeaseId, worker);
        await Enter(second);var failed=await Take(second);
        await leases.Release(failed.LeaseId, failed.WorkerId);
        await Reject(()=>Ticket(second),HttpStatusCode.Gone,"lost allocation cannot restart a match with empty simulation state");
        var interrupted=await Call((g,_)=>g.Read(Guid.Parse(second.HostPlayerId)));
        check(interrupted.player.history[0].outcome==MatchOutcome.Interrupted&&interrupted.player.coins==0,"lost server ends interrupted without inferring a winner");
        await Enter(waiting);var next=await Take(waiting);
        await using(var db=new PickleVerseContext(options))
            await new PostgresCommands(db).Run("UPDATE gamematches SET serverheartbeatat=CURRENT_TIMESTAMP-INTERVAL '2 minutes',updatedat=CURRENT_TIMESTAMP WHERE matchid=@id",P("id",Guid.Parse(next.MatchId)));
        await leases.Release(next.LeaseId, next.WorkerId);
        await Call((g,_)=>g.HeartbeatOnline(Guid.Parse(waiting.HostPlayerId),new(){MatchId=next.MatchId,RequestId=Guid.NewGuid().ToString("N")}));
        await Call(async(_,s)=>{await s.InterruptExpired();return true;});
        var recovered = await Call((g,_) => g.HeartbeatOnline(Guid.Parse(waiting.HostPlayerId),
            new() { MatchId = next.MatchId, RequestId = Guid.NewGuid().ToString("N") }));
        check(recovered.player.pendingMatch == null && recovered.player.history.Any(result => result.id == next.MatchId &&
            result.outcome == MatchOutcome.Interrupted && !string.IsNullOrWhiteSpace(result.reason)),
            "client heartbeat exposes the interrupted result and clears the pending match after lease expiry");
        var doubles=await Match(true,4);await Enter(doubles);var fourth=await Take(doubles);
        await Complete(fourth,await Results(doubles));
        await using(var db=new PickleVerseContext(options))
        {
            var id=Guid.Parse(fourth.MatchId);
            check(await db.Gamematchplayers.CountAsync(p=>p.Matchid==id&&p.Finalizedat!=null)==4,"server doubles finalizes all four participants atomically");
            check(await db.Matchscores.CountAsync(s=>s.Matchid==id&&s.Completed)==2,"server doubles stores the two completed game scores");
            var analyzed = Guid.Parse(doubles.Roster.members[1].id);
            Guid batch = Guid.NewGuid();
            var sql = new PostgresCommands(db);
            await sql.Run("""
                INSERT INTO aitelemetrysessions(batchid,playerid,sessionid,sessiontype,partcount,metadata)
                VALUES(@batch,@player,@match,'match',1,'{}'::jsonb)
                """, P("batch",batch), P("player",analyzed), P("match",id));
            await sql.Run("""
                INSERT INTO aicoachingsessions(batchid,playerid,sessionid,sessiontype,result)
                VALUES(@batch,@player,@match,'match',CAST(@result AS jsonb))
                """, P("batch",batch), P("player",analyzed), P("match",id),
                P("result","""{"gameMetrics":[{"gameNumber":2,"metrics":[{"technique":"Drive","code":"TIMING","errors":2,"opportunities":5}]}]}"""));
            using var unit = new UnitOfWork(db);
            var detail = await new MatchService(unit).Detail(Guid.Parse(doubles.HostPlayerId), id);
            check(detail.Mode == 2 && detail.TeamAWins == 2 && detail.TeamBWins == 0
                && detail.Games.Select(game => game.TeamAStartSide).SequenceEqual(new int?[] { -1, 1 }),
                "match detail exposes the original court side and score of each game");
            var tag = detail.Players.Single(player => player.PlayerId == analyzed).Games.Single();
            check(detail.Players.Count(player => player.CanReport) == 3 && tag.GameNumber == 2
                && tag.Metrics.Single().Errors == 2 && tag.Metrics.Single().Opportunities == 5,
                "a participant can read another player's per-game metrics and exact report targets");
        }
        await leases.Release(fourth.LeaseId, fourth.WorkerId);
        var actor=await new FunctionalFixture(options).Player("Admin");
        async Task<DedicatedOperationRecord> Stop(Guid match)
        {
            using var db=new PickleVerseContext(options);using var unit=new UnitOfWork(db);
            return await new ModerationService(unit,TimeProvider.System).StopMatch(actor,match,"Quản trị dừng để kiểm tra sự cố.");
        }
        var stopped=await Match(true);await Enter(stopped);var stoppedJob=await Take(stopped);
        var late=await Results(stopped);var operation=await Stop(Guid.Parse(stoppedJob.MatchId));
        await Reject(()=>Call(async(_,s)=>{await s.AuthorizeAdmission(new(){LeaseId=stoppedJob.LeaseId,PlayerId=Guid.Parse(stopped.HostPlayerId)},stoppedJob.WorkerId);return true;}),
            HttpStatusCode.Forbidden,"an already issued ticket cannot rejoin after a stop or sanction commits");
        await Complete(stoppedJob,late);
        var stoppedState=await Call((g,_)=>g.Read(Guid.Parse(stopped.HostPlayerId)));
        check(stoppedState.player.history[0].outcome==MatchOutcome.Interrupted && stoppedState.player.coins==0,
            "durable stop fences completion even before the worker receives the command");
        var heartbeat=await Call((_,s)=>s.Renew(new(){LeaseId=stoppedJob.LeaseId},false,stoppedJob.WorkerId));
        check(heartbeat.StopOperationId==operation.OperationId,"heartbeat redelivers the durable stop command until acknowledged");
        await Reject(()=>Call(async(_,s)=>{await s.Acknowledge(new(){LeaseId=stoppedJob.LeaseId,OperationId=Guid.NewGuid()},stoppedJob.WorkerId);return true;}),
            HttpStatusCode.Forbidden,"worker cannot acknowledge an unrelated operation");
        await Call(async(_,s)=>{await s.Acknowledge(new(){LeaseId=stoppedJob.LeaseId,OperationId=operation.OperationId},stoppedJob.WorkerId);return true;});
        await Complete(stoppedJob,late);
        await using(var db=new PickleVerseContext(options))
        {
            check(!await db.Ratingledgers.AnyAsync(r=>r.Matchid==operation.MatchId),"admin interruption creates no Elo ledger");
            check((await db.DedicatedOperations.SingleAsync(o=>o.OperationId==operation.OperationId)).AcknowledgedAt!=null,
                "worker acknowledgement is durable and tied to its lease");
            check(await db.Gamematchplayers.CountAsync(p=>p.Matchid==operation.MatchId&&p.Outcome=="INTERRUPTED")==2,
                "late normal result cannot resurrect the interrupted match");
        }
        await leases.Release(stoppedJob.LeaseId, stoppedJob.WorkerId);
        var racing=await Match(true);await Enter(racing);var racingJob=await Take(racing);var raceResults=await Results(racing);
        bool stopWon=true;
        async Task RaceStop(){try{await Stop(Guid.Parse(racingJob.MatchId));}catch(AppException e) when(e.StatusCode==HttpStatusCode.Conflict){stopWon=false;}}
        await Task.WhenAll(Complete(racingJob,raceResults),RaceStop());
        await using(var db=new PickleVerseContext(options))
        {
            var outcomes=await db.Gamematchplayers.Where(p=>p.Matchid==Guid.Parse(racingJob.MatchId)).Select(p=>p.Outcome).ToListAsync();
            check(stopWon?outcomes.All(x=>x=="INTERRUPTED"):outcomes.Order().SequenceEqual(new[]{"LOSE","WIN"}),
                "simultaneous stop and completion commit exactly one terminal outcome");
        }
        await leases.Release(racingJob.LeaseId, racingJob.WorkerId);

        var singlesGroup = await Match(false);
        var doublesGroup = await Match(false, 4);
        var twoWorkers = new[] { worker, Guid.NewGuid().ToString("N") };
        foreach (var node in twoWorkers) await Poll(node);
        var parallelConfig = new ConfigurationBuilder().AddConfiguration(config)
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DedicatedServer:MaxConcurrentMatches"] = "2" }).Build();
        var parallelSettings = new ServerSettings(parallelConfig);
        async Task<(Dictionary<string, RankedSearchRequest> Searches, RankedSearchTicket Assignment)> Queue(MatchRoster roster)
        {
            var searches = roster.members.ToDictionary(seat => seat.id, seat => new RankedSearchRequest
            {
                SearchId = Guid.NewGuid(), Mode = roster.mode, Protocol = settings.Dedicated.Protocol,
                Region = "asia", Group = seat.id, Members = [Guid.Parse(seat.id)]
            });
            foreach (var (player, search) in searches)
                await Call((game, _) => game.BeginRankedSearch(Guid.Parse(player), search));
            var tickets = await Task.WhenAll(searches.Select(item =>
                Call((game, _) => game.PollRankedSearch(Guid.Parse(item.Key), item.Value))));
            check(tickets.All(ticket => ticket.state == MatchmakingSearchStatus.Matched && ticket.roster?.HumanCount == roster.HumanCount) &&
                tickets.Select(ticket => ticket.matchId).Distinct().Count() == 1, $"one canonical {roster.mode} roster for solo searches");
            var assignment = tickets[0];
            await Call((game, _) => game.PublishRankedRoom(Guid.Parse(assignment.hostPlayerId), searches[assignment.hostPlayerId]));
            return (searches, assignment);
        }
        static OnlineMatchRequest Request(RankedSearchTicket assignment, RankedSearchRequest search) => new()
        {
            SearchId = search.SearchId.ToString("N"), Roster = assignment.roster!, HostPlayerId = assignment.hostPlayerId, Session = assignment.session
        };
        var singlesQueue = await Queue(singlesGroup.Roster);
        var doublesQueue = await Queue(doublesGroup.Roster);
        var singlesRequests = singlesQueue.Searches.ToDictionary(item => item.Key, item => Request(singlesQueue.Assignment, item.Value));
        var doublesRequests = doublesQueue.Searches.ToDictionary(item => item.Key, item => Request(doublesQueue.Assignment, item.Value));
        foreach (var item in singlesRequests.Concat(doublesRequests))
        {
            var consent = await Call((game, _) => game.OfferOnline(Guid.Parse(item.Key), item.Value), parallelSettings);
            check(consent.phase == "PREPARING" && consent.readyRemainingSeconds > 0,
                "capacity is reserved before consent and each seat can see its offer");
        }
        var acceptedSeats = await Task.WhenAll(doublesRequests.Skip(1).Select(item =>
            Call((game, _) => game.PrepareOnline(Guid.Parse(item.Key), item.Value), parallelSettings)));
        check(acceptedSeats.All(offer => offer.accepted && !offer.allAccepted), "three confirmed doubles players wait for the remaining player");
        var decliner = doublesQueue.Searches.First();
        await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            Call((game, _) => game.CancelRankedSearch(Guid.Parse(decliner.Key), decliner.Value), parallelSettings)));
        check(await leases.ReadMatch(doublesQueue.Assignment.matchId) == null, "repeated decline releases only the doubles lease");
        var retrying = doublesRequests.Skip(1).First();
        var cancelled = await Call((game, _) => game.OfferOnline(Guid.Parse(retrying.Key), retrying.Value), parallelSettings);
        check(cancelled.phase == "CANCELLED" && cancelled.partyAccepted, "the confirming solo can still read their consent after a decline");
        var retry = doublesQueue.Searches[retrying.Key];
        retry.SearchId = Guid.NewGuid();
        await Call((game, _) => game.BeginRankedSearch(Guid.Parse(retrying.Key), retry), parallelSettings);
        await Call((game, _) => game.CancelRankedSearch(Guid.Parse(decliner.Key), decliner.Value), parallelSettings);
        check((await Call((game, _) => game.PollRankedSearch(Guid.Parse(retrying.Key), retry), parallelSettings)).state == MatchmakingSearchStatus.Searching,
            "late cancellation cannot remove the replacement search");
        foreach (var item in singlesRequests)
            await Call((game, _) => game.PrepareOnline(Guid.Parse(item.Key), item.Value), parallelSettings);
        string captain = singlesQueue.Assignment.hostPlayerId;
        await Call((game, _) => game.StartOnline(Guid.Parse(captain), singlesRequests[captain]), parallelSettings);
        var surviving = await Take(singlesRequests[captain]);
        check(surviving.Roster.HumanCount == 2, "declining the doubles offer leaves the unrelated singles reservation usable");
        await Call(async (_, service) =>
        {
            await service.Release(new() { LeaseId = surviving.LeaseId }, surviving.WorkerId);
            return true;
        }, parallelSettings);
        var afterRelease = await Call((game, _) => game.Read(Guid.Parse(captain)), parallelSettings);
        check(afterRelease.player.pendingMatch == null && afterRelease.player.history[0].outcome == MatchOutcome.Interrupted,
            "worker release immediately clears a match that never finished loading");
        await Capacity(check);
    }

    static async Task Capacity(Action<bool, string> check)
    {
        var settings = TestRedis.Settings;
        var pool = new DedicatedLeaseRepository(TestRedis.Connection, new RedisSettings
        {
            KeyPrefix = settings.KeyPrefix + "capacity:",
            ConnectionString = settings.ConnectionString
        });
        const int capacity = 500, leaseSeconds = 45;
        var workers = Enumerable.Range(0, capacity + 1).Select(_ => Guid.NewGuid().ToString("N")).ToArray();
        DedicatedAllocation NewMatch() => new()
        {
            MatchId = Guid.NewGuid().ToString("N"), LeaseId = Guid.NewGuid().ToString("N"),
            Roster = new MatchRoster
            {
                members = [
                    new RoomMember { id = Guid.NewGuid().ToString("N"), team = 0 },
                    new RoomMember { id = Guid.NewGuid().ToString("N"), team = 1 }
                ]
            }
        };
        var matches = workers.Select(_ => NewMatch()).ToArray();
        await Task.WhenAll(workers.Select(worker => pool.Poll(worker, leaseSeconds)));
        var reserved = await Task.WhenAll(matches.Select(match => pool.Reserve(match, capacity, leaseSeconds)));
        check(reserved.Count(job => job != null) == capacity, "500 concurrent reservations with one worker per match and overflow queued");
        check(reserved.Where(job => job != null).Select(job => job!.WorkerId).Distinct().Count() == capacity,
            "each reserved match has a distinct worker");
        var first = reserved.First(job => job != null)!;
        await pool.Activate(first.MatchId, leaseSeconds);
        var running = await pool.Poll(first.WorkerId, leaseSeconds);
        check(running?.LeaseId == first.LeaseId, "claimed match retains the reserved lease");
        var overflow = matches[Array.FindIndex(reserved, job => job == null)];
        await pool.Release(first.LeaseId, first.WorkerId);
        check(await pool.Reserve(overflow, capacity, leaseSeconds) != null, "queued overflow fills the released slot");
        check(!await pool.Renew(first.LeaseId, first.WorkerId, DedicatedAllocationStatus.Ready, leaseSeconds),
            "stale lease cannot mutate the next allocation");
    }
}
