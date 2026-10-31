using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PickleVerse.Progression;
using Repositories.Context;
using Services.Configuration;
using Entities.Exceptions;
using Services.Implementations;
using static Repositories.Data.PostgresCommands;

static class HybridChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, TestClock clock, Action<bool, string> check)
    {
        var config = new ConfigurationBuilder().AddConfiguration(TestSettings.Configuration()).AddInMemoryCollection(new Dictionary<string, string?> {
            ["HybridAi:IngestEnabled"] = "true", ["HybridAi:CoachingEnabled"] = "true" }).Build();
        var settings = new HybridAiSettings(config);
        var player = Guid.NewGuid(); var match = Guid.NewGuid();
        await using var db = new PickleVerseContext(options);
        var sql = new PostgresCommands(db);var sqlData = new UnitOfWork(db);
        await sql.Run("INSERT INTO users(userid,email,passwordhash,roleid) SELECT @id,@email,'disabled',roleid FROM roles WHERE rolename='Player'", P("id", player), P("email", player + "@example.invalid"));
        await sql.Run("INSERT INTO playerprofiles(userid,playername) VALUES(@id,@name)", P("id", player), P("name", "Hybrid" + player.ToString("N")[..8]));
        await sql.Run("INSERT INTO gamematches(matchid,kind,status,endedat,sequence) VALUES(@match,'BotPractice','FINISHED',CURRENT_TIMESTAMP,nextval('gamematches_history_seq'))", P("match", match));
        await MatchScoreStore.Save(db,match,[new(11,5,true),new(11,8,true)]);
        await sql.Run("INSERT INTO gamematchplayers(matchid,playerid,team,outcome,finalizedat) VALUES(@match,@player,0,'WIN',CURRENT_TIMESTAMP)", P("match", match), P("player", player));
        var service = new HybridCoachingService(sqlData, new GameCatalogService(sqlData, new ServerSettings(config)), settings, clock);
        TelemetryBatch Batch() => new() { batchId = Guid.NewGuid().ToString("N"), sessionId = match.ToString("N"), sessionType = "match", playerId = player.ToString("N"), gameVersion = "integration" };
        var batch = Batch(); batch.partCount = 2; batch.partIndex = 1;
        batch.shots.Add(new() { shotId = "second", sequence = 1, kind = "Dink", technique = "Dink", source = "integration", outcome = "Contact", timingError = .1f, assisted = false });
        var accepted = await service.Ingest(player, batch);
        check(accepted.acceptedParts == 1 && accepted.status == "receiving", "out-of-order partial telemetry acknowledged durably");
        check(await sql.One("SELECT count(*) FROM aianalysisjobs", r => r.GetInt64(0)) == 0, "partial telemetry does not create a job");
        accepted = await service.Ingest(player, batch);
        check(accepted.acceptedParts == 1, "duplicate telemetry part is idempotent");
        async Task<bool> Rejected(Func<Task> action, HttpStatusCode status)
        {
            try { await action(); return false; } catch (AppException e) when (e.StatusCode == status) { return true; }
        }
        batch.shots[0].timingError = .2f;
        check(await Rejected(() => service.Ingest(player, batch), HttpStatusCode.Conflict), "conflicting part payload rejected");
        batch.shots[0].timingError = .1f;
        check(await Rejected(() => service.Ingest(Guid.NewGuid(), batch), HttpStatusCode.Forbidden), "another account cannot upload telemetry for this player");
        var outsider = Guid.NewGuid();
        var otherBatch = Batch(); otherBatch.playerId = outsider.ToString("N");
        check(await Rejected(() => service.Ingest(outsider, otherBatch), HttpStatusCode.Conflict), "matching telemetry account fields cannot bypass match participant membership");
        batch.partIndex = 0; batch.shots[0].shotId = "first"; batch.shots[0].sequence = 0;
        accepted = await service.Ingest(player, batch);
        check(accepted.status == "queued" && accepted.acceptedParts == 2, "complete batch creates one durable job");
        check(await sql.One("SELECT count(*) FROM aianalysisjobs", r => r.GetInt64(0)) == 1, "one job per complete session");
        var request = System.Text.Json.JsonDocument.Parse(await service.BuildRequest(Guid.Parse(batch.batchId)));
        check(request.RootElement.GetProperty("shots").GetArrayLength() == 2 && request.RootElement.GetProperty("shots")[0].GetProperty("shotId").GetString() == "first", "worker request reassembles parts in shot order");
        var summary = await service.Summary(player);
        check(summary.status == "pending" && summary.analysis!.recommendations.Count > 0, "pending analytics returns curriculum fallback");
        var forged = Batch(); forged.sessionId = Guid.NewGuid().ToString("N");
        check(await Rejected(() => service.Ingest(player, forged), HttpStatusCode.Conflict), "unknown session rejected");
        var invalid = Batch(); invalid.shots = [new() { shotId = "bad", sequence = 0, kind = "Drive", technique = "Drive", source = "integration", contact = [float.NaN, 0, 0] }];
        check(await Rejected(() => service.Ingest(player, invalid), HttpStatusCode.BadRequest), "non-finite telemetry rejected");
        invalid.shots[0].contact = [0, 0];
        check(await Rejected(() => service.Ingest(player, invalid), HttpStatusCode.BadRequest), "malformed vectors rejected");
        check(EloPolicy.StandardDelta(400, 400, true) == 16 && EloPolicy.StandardDelta(400, 400, false) == -16, "standard Elo equal-rating delta is sixteen");
        check(EloPolicy.StandardDelta(100, 100, false) == 0, "standard Elo keeps floor one hundred");
        check(EloPolicy.StandardDelta(400, 401, true) == 16 && EloPolicy.StandardDelta(400, 360, false) == -18, "standard Elo removes custom discontinuities");
        check(EloPolicy.VersionedDelta("legacy-v1", 400, 360, false, 0, 0) == -160, "old matches retain legacy rating policy");
        var legacy = GameJson.Read<MatchResult>("{}");
        check(legacy.ratingPolicyVersion == EloPolicy.LegacyVersion, "missing saved rating version stays legacy");
        var policy = new BotPolicy();
        check(policy.Profile(499).aggressiveness == .25f && policy.Profile(500).reactionSeconds == .15f && policy.Profile(2000).aggressiveness == .75f, "catalog bot profile preserves boundaries");
        var frozen = policy.Profile(2000); policy.tiers[2].reactionSeconds = 9;
        check(frozen.reactionSeconds == .08f, "bot match profile is a frozen copy");
        check(await sql.One("SELECT count(*) FROM pg_tables WHERE schemaname='public' AND tablename IN ('ratingledger','aitelemetrysessions','aitelemetryparts','aianalysisjobs','aicoachingsessions','aicoachingprofiles')", r => r.GetInt64(0)) == 6,
            "fresh database initializer includes every Hybrid AI table");
        await WorkerChecks(options, config, player, Guid.Parse(batch.batchId), check);
        await RatingCompletionChecks(options,clock,check,false);
        await RatingCompletionChecks(options,clock,check,true);
        await QueueBeyondFirstPage(clock, check);
    }
    static async Task RatingCompletionChecks(DbContextOptions<PickleVerseContext> options, TestClock clock, Action<bool,string> check, bool legacy)
    {
        await using var db=new PickleVerseContext(options);var sql=new PostgresCommands(db);var sqlData = new UnitOfWork(db);
        var a=Guid.NewGuid();var b=Guid.NewGuid();var match=Guid.NewGuid();
        foreach(var id in new[]{a,b})
        {
            await sql.Run("INSERT INTO users(userid,email,passwordhash,roleid) SELECT @id,@email,'disabled',roleid FROM roles WHERE rolename='Player'",P("id",id),P("email",id+"@example.invalid"));
            await sql.Run("INSERT INTO playerprofiles(userid,playername,elo) VALUES(@id,@name,400)",P("id",id),P("name","Rating"+id.ToString("N")[..8]));
        }
        var roster=new PickleVerse.Multiplayer.MatchRoster{matchId=match.ToString("N"),mode=PickleVerse.Multiplayer.PlayMode.Singles,region="asia",members=[
            new(){id=a.ToString("N"),team=0,elo=400,name="A"},new(){id=b.ToString("N"),team=1,elo=400,name="B"}]};
        if(legacy)
            await sql.Run("INSERT INTO gamematches(matchid,kind,status,photonsession,region,startedat) VALUES(@match,'RankedSingles','PLAYING','rating-fixture','asia',@start)",P("match",match),P("start",clock.Now.UtcDateTime));
        else
            await sql.Run("INSERT INTO gamematches(matchid,kind,status,photonsession,region,startedat,ratingpolicyversion) VALUES(@match,'RankedSingles','PLAYING','rating-fixture','asia',@start,@policy)",P("match",match),P("start",clock.Now.UtcDateTime),P("policy",EloPolicy.CurrentVersion));
        foreach(var id in new[]{a,b})
        {
            await sql.Run("INSERT INTO gamematchplayers(matchid,playerid,team,elobefore,isreplaywriter,rosterindex) VALUES(@match,@player,@team,400,@writer,@team)",P("match",match),P("player",id),P("team",id==a?0:1),P("writer",id==a));

        }
        MatchFinishRequest Result(Guid id,bool won)=>new(){RequestId=Guid.NewGuid().ToString("N"),Result=new MatchResult{
            id=match.ToString("N"),playerId=id.ToString("N"),kind=MatchKind.RankedSingles,outcome=won?MatchOutcome.Won:MatchOutcome.Lost,
            gamesWon=won?2:0,gamesLost=won?0:2,points=won?22:0,scores=[new(won?11:0,won?0:11,true),new(won?11:0,won?0:11,true)],
            ratingPolicyVersion="client-forged-version",ownElo=999999,opponentElo=999999}};
        async Task<GameSnapshot> Report(Guid player,MatchFinishRequest request)
        {
            await using var context=new PickleVerseContext(options);var query=new PostgresCommands(context);var queryData = new UnitOfWork(context);
            var settings=TestSettings.Load();
            var game=new OnlineGameService(queryData,new GameCatalogService(queryData,settings),clock,settings,TestRedis.State,TestRedis.Leaderboard,new MatchReplayService(queryData,TestReplayStorage.Shared,Microsoft.Extensions.Logging.Abstractions.NullLogger<MatchReplayService>.Instance));
            return await game.ReportOnline(player,request);
        }
        var requestA=Result(a,true);var requestB=Result(b,false);
        await Report(a,requestA);await Report(b,requestB);await Report(a,requestA);
        int delta=legacy?20:16;
        check(await sql.One("SELECT elo FROM playerprofiles WHERE userid=@id",r=>r.GetInt32(0),P("id",a))==400+delta,legacy?"in-flight match without version completes under legacy policy":"new rated match ignores forged client rating and uses standard Elo");
        var ledger=await sql.Query("SELECT beforeelo,afterelo,delta,policyversion FROM ratingledger WHERE matchid=@match ORDER BY delta DESC",r=>new{before=r.GetInt32(0),after=r.GetInt32(1),delta=r.GetInt32(2),policy=r.GetString(3)},P("match",match));
        check(ledger.Count==2 && ledger[0].delta==delta && ledger[1].delta==-delta && ledger.All(l=>l.after-l.before==l.delta && l.policy==(legacy?EloPolicy.LegacyVersion:EloPolicy.CurrentVersion)),"rating ledger records one atomic before/after pair per player and policy");
    }
    static async Task WorkerChecks(DbContextOptions<PickleVerseContext> options, IConfiguration config, Guid player, Guid batch, Action<bool, string> check)
    {
        var url = Environment.GetEnvironmentVariable("PKV_TEST_AI_URL");
        var workerConfig = new ConfigurationBuilder().AddConfiguration(config).AddInMemoryCollection(new Dictionary<string,string?> {
            ["HybridAi:DispatcherEnabled"]="true", ["HybridAi:ServiceUrl"]=url ?? "http://fixture.invalid", ["HybridAi:ServiceKey"]=new string('k',32) }).Build();
        var settings = new HybridAiSettings(workerConfig);
        var handler = new FlakyAiHandler(url != null);
        var services = new ServiceCollection();
        services.AddScoped(_=>new PickleVerseContext(options));
        services.AddScoped<PostgresCommands>();
        services.AddScoped<GameRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton(new ServerSettings(workerConfig));
        services.AddSingleton(settings);
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<GameCatalogService>();
        services.AddScoped<IGameCatalogService>(sp => sp.GetRequiredService<GameCatalogService>());
        services.AddScoped<HybridCoachingService>();
        services.AddScoped<IHybridCoachingService>(sp => sp.GetRequiredService<HybridCoachingService>());
        services.AddHttpClient("hybrid-ai", c=>c.Timeout=TimeSpan.FromSeconds(15)).ConfigurePrimaryHttpMessageHandler(()=>handler);
        await using var provider = services.BuildServiceProvider();
        using var worker = new Services.Workers.HybridAiWorker(provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<IHttpClientFactory>(), settings, NullLogger<Services.Workers.HybridAiWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        await using var db = new PickleVerseContext(options);
        var sql = new PostgresCommands(db);var sqlData = new UnitOfWork(db);
        async Task WaitFor(string target)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (await sql.One("SELECT state FROM aianalysisjobs WHERE batchid=@batch",r=>r.GetString(0),P("batch",batch)) != target)
                await Task.Delay(100,deadline.Token);
        }
        try
        {
            await WaitFor("completed");
            check(handler.Calls >= 2, "AI outage retries asynchronously until service recovers");
            var payloads = handler.Payloads.Select(p=>System.Text.Json.Nodes.JsonNode.Parse(p)).ToArray();
            check(payloads.All(p=>System.Text.Json.Nodes.JsonNode.DeepEquals(p,payloads[0])), "retry retains the frozen analysis request");
            check(await sql.One("SELECT count(*) FROM aicoachingsessions WHERE batchid=@batch",r=>r.GetInt64(0),P("batch",batch))==1, "worker commits one analysis session");
            long findings = await sql.One("SELECT count(*) FROM playertechniqueserrors WHERE playerid=@player",r=>r.GetInt64(0),P("player",player));
            await sql.Run("UPDATE aianalysisjobs SET state='processing',attempts=1,leaseuntil=now()-interval '1 second',leasetoken=@token WHERE batchid=@batch",P("token",Guid.NewGuid()),P("batch",batch));
            await WaitFor("completed");
            check(await sql.One("SELECT count(*) FROM playertechniqueserrors WHERE playerid=@player",r=>r.GetInt64(0),P("player",player))==findings, "expired lease recovery cannot duplicate coaching findings");
            check(await sql.One("SELECT count(*) FROM playertechniquemastery WHERE playerid=@player",r=>r.GetInt64(0),P("player",player))==0, "match analytics never overwrites training mastery");
            handler.FailAll=true;
            await sql.Run("UPDATE aianalysisjobs SET state='pending',attempts=9,availableat=now() WHERE batchid=@batch",P("batch",batch));
            await WaitFor("failed");
            check(await sql.One("SELECT attempts FROM aianalysisjobs WHERE batchid=@batch",r=>r.GetInt32(0),P("batch",batch))==10, "retry stops at ten attempts and keeps failed job");
            check(await sql.One("SELECT count(*) FROM aicoachingprofiles WHERE playerid=@player",r=>r.GetInt64(0),P("player",player))==1, "failed newer analysis preserves cached coaching");
        }
        finally { await worker.StopAsync(CancellationToken.None); }
    }
    sealed class FlakyAiHandler(bool forward) : HttpMessageHandler
    {
        public int Calls;
        public volatile bool FailAll;
        public readonly System.Collections.Concurrent.ConcurrentBag<string> Payloads = new();
        readonly HttpClient client = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            string payload=await request.Content!.ReadAsStringAsync(token);
            Payloads.Add(payload);
            if(Interlocked.Increment(ref Calls)==1 || FailAll) throw new HttpRequestException("Simulated AI outage");
            if(forward)
            {
                using var copy=new HttpRequestMessage(HttpMethod.Post,request.RequestUri);
                copy.Headers.Add("X-AI-Key",new string('k',32));
                copy.Content=new StringContent(payload,System.Text.Encoding.UTF8,"application/json");
                return await client.SendAsync(copy,token);
            }
            var input=System.Text.Json.JsonDocument.Parse(payload);
            var result=new CoachingAnalysis{sessionId=input.RootElement.GetProperty("sessionId").GetString()!};
            return new(HttpStatusCode.OK){Content=new StringContent(GameJson.Write(result))};
        }
        protected override void Dispose(bool disposing) { if(disposing)client.Dispose(); base.Dispose(disposing); }
    }
    static async Task QueueBeyondFirstPage(TestClock clock, Action<bool, string> check)
    {
        var state = TestRedis.State;
        long started = new DateTimeOffset(clock.GetUtcNow().UtcDateTime).ToUnixTimeMilliseconds();
        var entries = Enumerable.Range(0, 514).Select(i => new QueueEntry { searchId = Guid.NewGuid().ToString("N"), playerId = Guid.NewGuid().ToString("N"), group = Guid.NewGuid().ToString("N"), mode = 1, region = "hybrid-test", protocol = 91, started = started + i, lastSeen = started }).ToArray();
        foreach (var entry in entries) { entry.party = [entry.playerId]; await state.Begin(entry); }
        var caller = entries[^1];
        var first = await state.Candidates(caller);
        var second = await state.Candidates(caller);
        check(first.Any(e => e.entry.playerId == caller.playerId) && second.Any(e => e.entry.playerId == entries[512].playerId), "late queue caller and rotating page beyond 512 participate");
    }
}
