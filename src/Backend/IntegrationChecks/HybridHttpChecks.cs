using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using PickleVerse.Progression;
using Repositories.Context;
using Repositories.UnitOfWork;
using Services.Configuration;
using Services.Game;
using Services.Implementations;
using Services.Interfaces;
using static Services.Game.GameSql;

static class HybridHttpChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, Action<bool,string> check)
    {
        // Functional fixtures disable pooling to simplify database cleanup. Production does
        // pool connections; benchmark the production behavior, not a fresh login per request.
        using(var source=new PickleVerseContext(options))
        {
            var connection=new Npgsql.NpgsqlConnectionStringBuilder(source.Database.GetConnectionString()) {Pooling=true};
            if(connection.Host=="localhost")connection.Host="127.0.0.1";
            connection["GSS Encryption Mode"]="Disable";
            options=new DbContextOptionsBuilder<PickleVerseContext>().UseNpgsql(connection.ConnectionString).Options;
        }
        int measurementPhase=0;
        var stages=new System.Collections.Concurrent.ConcurrentDictionary<string,System.Collections.Concurrent.ConcurrentBag<double>>();
        using var listener=new System.Diagnostics.Metrics.MeterListener();
        listener.InstrumentPublished=(instrument,l)=>{if(instrument.Meter.Name=="PickleVerse.HybridAi")l.EnableMeasurementEvents(instrument);};
        listener.SetMeasurementEventCallback<double>((instrument,value,tags,state)=>{
            if(instrument.Name!="hybrid.ingest.stage.ms" || measurementPhase!=1)return;
            foreach(var tag in tags)if(tag.Key=="stage")stages.GetOrAdd(tag.Value!.ToString()!,_=>new()).Add(value);
        });
        listener.Start();
        string aiUrl=Environment.GetEnvironmentVariable("PKV_TEST_AI_URL") ?? throw new InvalidOperationException("PKV_TEST_AI_URL is required for the HTTP benchmark.");
        var config=new ConfigurationBuilder().AddConfiguration(TestSettings.Configuration()).AddInMemoryCollection(new Dictionary<string,string?> {
            ["HybridAi:IngestEnabled"]="true",["HybridAi:DispatcherEnabled"]="true",["HybridAi:CoachingEnabled"]="true",
            ["HybridAi:ServiceUrl"]=aiUrl,["HybridAi:ServiceKey"]=new string('k',32) }).Build();
        var settings=new ServerSettings(config);
        var builder=WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddScoped(_=>new PickleVerseContext(options));
        builder.Services.AddScoped<GameSql>(); builder.Services.AddScoped<GameCatalogService>();
        builder.Services.AddSingleton(settings); builder.Services.AddSingleton(settings.HybridAi);
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddScoped<HybridCoachingService>();
        builder.Services.AddScoped<IUnitOfWork,UnitOfWork>();
        builder.Services.AddScoped<IAiCoachingService,AiCoachingService>();
        builder.Services.AddHttpClient("hybrid-ai",c=>c.Timeout=TimeSpan.FromSeconds(15));
        builder.Services.AddHostedService<API.Workers.HybridAiWorker>();
        builder.Services.AddControllers().AddJsonOptions(o=>{
            o.JsonSerializerOptions.IncludeFields=true;
            o.JsonSerializerOptions.IgnoreReadOnlyProperties=true;
            o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        }).AddApplicationPart(typeof(API.Controllers.AiCoachingController).Assembly);
        builder.Services.AddExceptionHandler<API.Middlewares.GlobalExceptionHandler>(); builder.Services.AddProblemDetails();
        var key=new SymmetricSecurityKey(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o=>o.TokenValidationParameters=new TokenValidationParameters {
            ValidateIssuerSigningKey=true,IssuerSigningKey=key,ValidateIssuer=true,ValidIssuer="hybrid-check",
            ValidateAudience=true,ValidAudience="hybrid-check",ValidateLifetime=true,ClockSkew=TimeSpan.Zero });
        builder.Services.AddAuthorization();
        builder.Services.AddRequestDecompression();
        await using var app=builder.Build();
        app.UseExceptionHandler();app.UseRequestDecompression();app.UseAuthentication();app.UseAuthorization();app.MapControllers();
        await app.StartAsync();
        await using var db=new PickleVerseContext(options);
        var sql=new GameSql(db);
        var player=Guid.NewGuid();
        await sql.Run("INSERT INTO users(userid,email,passwordhash,roleid) SELECT @id,@email,'disabled',roleid FROM roles WHERE rolename='Player'",P("id",player),P("email",player+"@example.invalid"));
        await sql.Run("INSERT INTO playerprofiles(userid,playername) VALUES(@id,@name)",P("id",player),P("name","Http"+player.ToString("N")[..8]));
        var token=new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("hybrid-check","hybrid-check",
            [new Claim(ClaimTypes.NameIdentifier,player.ToString())],expires:DateTime.UtcNow.AddMinutes(10),signingCredentials:new(key,SecurityAlgorithms.HmacSha256)));
        using var client=new HttpClient {BaseAddress=new Uri(app.Urls.Single()),Timeout=TimeSpan.FromSeconds(30)};
        var unauthorized=await client.GetAsync("api/AiCoaching/summary");
        check(unauthorized.StatusCode==HttpStatusCode.Unauthorized,"coaching HTTP requires a valid JWT");
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
        using (var initial=await client.GetAsync("api/AiCoaching/summary"))
            check(initial.IsSuccessStatusCode,"authenticated coaching HTTP supplies curriculum fallback");
        using (var legacy=await client.GetAsync("api/AiCoaching/recommendation"))
            check(legacy.IsSuccessStatusCode,"legacy recommendation reads the shared Hybrid policy");
        var payloads=new List<TelemetryBatch>();
        for(int index=0;index<112;index++)
        {
            var match=Guid.NewGuid();
            await sql.Run("INSERT INTO gamematches(matchid,kind,data,status) VALUES(@match,'BotPractice','{}','FINISHED')",P("match",match));
            await sql.Run("INSERT INTO gamematchplayers(matchid,playerid,team,accepted) VALUES(@match,@player,0,true)",P("match",match),P("player",player));
            await sql.Run("INSERT INTO matchhistory(matchid,matchtype,teamascore,teambscore) VALUES(@match,'BotPractice',2,0)",P("match",match));
            var batch=new TelemetryBatch{batchId=Guid.NewGuid().ToString("N"),sessionId=match.ToString("N"),sessionType="match",playerId=player.ToString("N"),gameVersion="benchmark"};
            int count=index<12?1000:200;
            batch.shots=Enumerable.Range(0,count).Select(i=>new ShotTelemetry{shotId=$"{index}-{i}",sequence=i,kind="Drive",technique="Drive",source="benchmark",outcome="Contact",assisted=false,timingError=i%5==0?.1f:0}).ToList();
            payloads.Add(batch);
        }
        async Task<double> Send(TelemetryBatch batch)
        {
            var sw=Stopwatch.StartNew();
            byte[] bytes=Encoding.UTF8.GetBytes(HybridJson.Write(batch));
            using var buffer=new MemoryStream();
            using(var gzip=new System.IO.Compression.GZipStream(buffer,System.IO.Compression.CompressionLevel.Fastest,true))gzip.Write(bytes);
            using var body=new ByteArrayContent(buffer.ToArray());
            body.Headers.ContentType=new MediaTypeHeaderValue("application/json");body.Headers.ContentEncoding.Add("gzip");
            using var response=await client.PostAsync("api/AiCoaching/telemetry",body);
            response.EnsureSuccessStatusCode();
            return sw.Elapsed.TotalMilliseconds;
        }
        // Warm request/JIT/connection pools before measuring the stated steady workload.
        await Send(payloads[0]);
        using(var warmDeadline=new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            while(true)
            {
                var warm=await sql.One("SELECT state,lasterror FROM aianalysisjobs WHERE batchid=@batch",r=>new {state=r.GetString(0),error=r.IsDBNull(1)?null:r.GetString(1)},P("batch",Guid.Parse(payloads[0].batchId)));
                if(warm?.error!=null)throw new InvalidOperationException("Benchmark worker warmup failed: "+warm.error);
                if(warm?.state=="completed")break;
                await Task.Delay(100,warmDeadline.Token);
            }
        }
        await Send(payloads[1]);
        var steady=new List<double>();
        var accepted=new List<(Guid id,DateTime time)>();
        measurementPhase=1;
        foreach(var batch in payloads.Skip(2).Take(10))
        {
            steady.Add(await Send(batch));accepted.Add((Guid.Parse(batch.batchId),DateTime.UtcNow));
            await Task.Delay(1000);
        }
        var ready=new List<double>();
        foreach(var item in accepted)
        {
            var completed=await sql.One("SELECT updatedat FROM aianalysisjobs WHERE batchid=@id AND state='completed'",r=>(DateTime?)r.GetDateTime(0),P("id",item.id));
            while(completed==null && DateTime.UtcNow-item.time<TimeSpan.FromSeconds(5))
            {
                await Task.Delay(100);
                completed=await sql.One("SELECT updatedat FROM aianalysisjobs WHERE batchid=@id AND state='completed'",r=>(DateTime?)r.GetDateTime(0),P("id",item.id));
            }
            if(completed!=null) ready.Add(Math.Max(0,(completed.Value-item.time).TotalMilliseconds));
        }
        var burstClock=Stopwatch.StartNew();
        measurementPhase=2;
        var burst=await Task.WhenAll(payloads.Skip(12).Select(Send));
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(120));
        while(await sql.One("SELECT count(*) FROM aianalysisjobs j JOIN aitelemetrysessions s USING(batchid) WHERE s.playerid=@player AND j.state<>'completed'",r=>r.GetInt64(0),P("player",player))>0)
        {
            var diagnostic=await sql.One("SELECT j.lasterror FROM aianalysisjobs j JOIN aitelemetrysessions s USING(batchid) WHERE s.playerid=@player AND j.lasterror IS NOT NULL ORDER BY j.updatedat DESC LIMIT 1",r=>r.GetString(0),P("player",player));
            if(diagnostic!=null)throw new InvalidOperationException("Burst worker deferred: "+diagnostic);
            await Task.Delay(100,deadline.Token);
        }
        int jobs=(int)await sql.One("SELECT count(*) FROM aicoachingsessions WHERE playerid=@player",r=>r.GetInt64(0),P("player",player));
        check(jobs==112,"HTTP steady/burst workload loses no acknowledged session");
        static double P95(IEnumerable<double> values){var sorted=values.Order().ToArray();return sorted[(int)Math.Ceiling(sorted.Length*.95)-1];}
        var report=new {steadySessions=10,steadyShotsPerSession=1000,steadyIngestP95Ms=P95(steady),
            steadyReadySamples=ready.Count,steadyReadyP95Ms=ready.Count>0?P95(ready):double.NaN,
            burstSessions=100,burstShotsPerSession=200,burstIngestP95Ms=P95(burst),burstDrainMs=burstClock.Elapsed.TotalMilliseconds,
            acknowledged=112,completed=jobs,connectionPooling=true,runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            steadyStageP95Ms=stages.ToDictionary(s=>s.Key,s=>P95(s.Value))};
        string reportFile=Path.Combine(TestSettings.Root,"artifacts/verification/hybrid-http-benchmark.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportFile)!);
        await File.WriteAllTextAsync(reportFile,GameJson.Payload(report));
        check(P95(steady)<250,"steady HTTP ingest p95 below 250ms");
        check(ready.Count==10 && P95(ready)<5000,"steady coaching p95 ready within five seconds");
        await app.StopAsync();
        Npgsql.NpgsqlConnection.ClearAllPools();
    }
}
