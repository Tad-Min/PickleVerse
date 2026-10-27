using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using PickleVerse.Progression;
using Repositories.Context;
using Services.Configuration;
using Services.Game;
using static Services.Game.GameSql;

namespace API.Workers;

public sealed class HybridAiWorker(IServiceScopeFactory scopes, IHttpClientFactory clients, HybridAiSettings settings,
    ILogger<HybridAiWorker> logger) : BackgroundService
{
    DateTime nextCleanup = DateTime.MinValue;
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.WhenAll(Loop(0, stoppingToken), Loop(1, stoppingToken));
    async Task Loop(int lane, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                if (settings.DispatcherEnabled || settings.IngestEnabled)
                {
                    using var scope = scopes.CreateScope();
                    var sql = scope.ServiceProvider.GetRequiredService<GameSql>();
                    if (lane == 0 && DateTime.UtcNow >= nextCleanup)
                    {
                        await sql.Run("DELETE FROM aitelemetryparts p USING aitelemetrysessions s WHERE p.batchid=s.batchid AND s.createdat < @cutoff", P("cutoff", DateTime.UtcNow.AddDays(-30)));
                        await sql.Run("UPDATE aianalysisjobs j SET request=NULL,state=CASE WHEN state='completed' THEN state ELSE 'failed' END,lasterror=CASE WHEN state='completed' THEN lasterror ELSE 'Telemetry retention expired' END FROM aitelemetrysessions s WHERE j.batchid=s.batchid AND s.createdat < @cutoff", P("cutoff", DateTime.UtcNow.AddDays(-30)));
                        await sql.Run("UPDATE aicoachingsessions SET result=jsonb_set(result,'{findings}','[]'::jsonb) WHERE createdat < @cutoff",P("cutoff",DateTime.UtcNow.AddDays(-30)));
                        await sql.Run("UPDATE aicoachingprofiles SET result=jsonb_set(result,'{findings}','[]'::jsonb) WHERE updatedat < @cutoff",P("cutoff",DateTime.UtcNow.AddDays(-30)));
                        nextCleanup = DateTime.UtcNow.AddHours(24);
                    }
                    if (settings.DispatcherEnabled && await Process(scope.ServiceProvider, stop)) continue;
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Hybrid AI worker lane {Lane} failed; durable jobs remain retryable", lane); }
            try { await Task.Delay(TimeSpan.FromSeconds(1), stop); }
            catch (OperationCanceledException) { break; }
        }
    }
    async Task<bool> Process(IServiceProvider services, CancellationToken stop)
    {
        var sql = services.GetRequiredService<GameSql>();
        await sql.Run("UPDATE aianalysisjobs SET state='failed',lasterror='Lease expired after final attempt',leasetoken=NULL,leaseuntil=NULL WHERE state='processing' AND attempts>=10 AND leaseuntil<now()");
        var token = Guid.NewGuid();
        var job = await sql.One("""
            UPDATE aianalysisjobs SET state='processing',attempts=attempts+1,leasetoken=@token,
                leaseuntil=now()+interval '60 seconds',updatedat=now()
            WHERE batchid=(SELECT batchid FROM aianalysisjobs
                WHERE attempts<10 AND ((state='pending' AND availableat<=now()) OR (state='processing' AND leaseuntil<now()))
                ORDER BY availableat FOR UPDATE SKIP LOCKED LIMIT 1)
            RETURNING batchid,request::text,attempts
            """, r => new { id = r.GetGuid(0), request = r.IsDBNull(1) ? null : r.GetString(1), attempts = r.GetInt32(2) }, P("token", token));
        if (job == null) return false;
        var timer=System.Diagnostics.Stopwatch.StartNew();
        try
        {
            string request = job.request ?? await services.GetRequiredService<HybridCoachingService>().BuildRequest(job.id);
            if (job.request == null)
                await sql.Run("UPDATE aianalysisjobs SET request=@request::jsonb WHERE batchid=@batch AND leasetoken=@token", P("request", request), P("batch", job.id), P("token", token));
            using var client = clients.CreateClient("hybrid-ai");
            using var message = new HttpRequestMessage(HttpMethod.Post, settings.ServiceUrl.TrimEnd('/') + "/v1/analyze-session");
            message.Headers.Add("X-AI-Key", settings.ServiceKey);
            message.Content = new StringContent(request, System.Text.Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(message, stop);
            response.EnsureSuccessStatusCode();
            var result = GameJson.Read<CoachingAnalysis>(await response.Content.ReadAsStringAsync(stop));
            var input = System.Text.Json.JsonDocument.Parse(request);
            if (result.sessionId != input.RootElement.GetProperty("sessionId").GetString() || result.ruleVersion != "rules-v1"
                || result.findings == null || result.metrics == null || result.recommendations == null || result.recommendations.Count > 3)
                throw new InvalidDataException("AI response contract mismatch.");
            var db = services.GetRequiredService<PickleVerseContext>();
            await using var tx = await db.Database.BeginTransactionAsync(stop);
            var lease = await sql.One("SELECT leasetoken FROM aianalysisjobs WHERE batchid=@batch FOR UPDATE", r => r.IsDBNull(0) ? Guid.Empty : r.GetGuid(0), P("batch", job.id));
            if (lease != token) return true;
            var player = await sql.One("SELECT playerid FROM aitelemetrysessions WHERE batchid=@batch", r => r.GetGuid(0), P("batch", job.id));
            await sql.Run("SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", P("key", "ai-profile:" + player));
            int inserted = await sql.Run("INSERT INTO aicoachingsessions(batchid,playerid,sessionid,sessiontype,result,createdat) SELECT batchid,playerid,sessionid,sessiontype,@result::jsonb,createdat FROM aitelemetrysessions WHERE batchid=@batch ON CONFLICT DO NOTHING", P("result", GameJson.Write(result)), P("batch", job.id));
            if (inserted == 1)
                await sql.Run("""
                    INSERT INTO playertechniqueserrors(playerid,techniqueid,matchid,lessonid,errortype,coachingtip)
                    SELECT s.playerid,t.techniqueid,CASE WHEN s.sessiontype='match' THEN s.sessionid ELSE NULL END,
                        tr.lessonid,f->>'code',f->>'tip'
                    FROM aitelemetrysessions s CROSS JOIN jsonb_array_elements(@result::jsonb->'findings') f
                    JOIN techniques t ON t.techniquename=f->>'technique'
                    LEFT JOIN trainingsessions tr ON s.sessiontype='training' AND tr.attemptid=s.sessionid
                    WHERE s.batchid=@batch
                    """, P("result", GameJson.Write(result)), P("batch", job.id));
            var compact = new CoachingAnalysis {sessionId=result.sessionId,ruleVersion=result.ruleVersion,metrics=result.metrics,
                recommendations=result.recommendations,tier=result.tier,
                findings=result.findings.DistinctBy(f=>(f.technique,f.code)).Take(20).ToList()};
            await sql.Run("""
                INSERT INTO aicoachingprofiles(playerid,batchid,result,updatedat)
                SELECT playerid,batchid,@result::jsonb,createdat FROM aitelemetrysessions WHERE batchid=@batch
                ON CONFLICT(playerid) DO UPDATE SET batchid=EXCLUDED.batchid,result=EXCLUDED.result,updatedat=EXCLUDED.updatedat
                WHERE aicoachingprofiles.updatedat<=EXCLUDED.updatedat
                """, P("batch", job.id),P("result",HybridJson.Write(compact)));
            await sql.Run("UPDATE aianalysisjobs SET state='completed',leaseuntil=NULL,leasetoken=NULL,lasterror=NULL,updatedat=now() WHERE batchid=@batch AND leasetoken=@token", P("batch", job.id), P("token", token));
            await tx.CommitAsync(stop);
            HybridAiMetrics.AnalysisMs.Record(timer.Elapsed.TotalMilliseconds);
            HybridAiMetrics.Completed.Add(1);
            logger.LogInformation("Hybrid AI analysis completed {BatchId}", job.id);
        }
        catch (Exception error) when (!stop.IsCancellationRequested)
        {
            HybridAiMetrics.Retried.Add(1);
            // Store a bounded diagnostic without response bodies, player telemetry or service keys.
            string diagnostic = error is Npgsql.PostgresException postgres
                ? $"PostgresException:{postgres.SqlState}:{postgres.ConstraintName}" : error.GetType().Name;
            await sql.Run("UPDATE aianalysisjobs SET state=@state,lasterror=@error,availableat=now()+@delay*interval '1 second',leaseuntil=NULL,leasetoken=NULL,updatedat=now() WHERE batchid=@batch AND leasetoken=@token",
                P("state", job.attempts >= 10 ? "failed" : "pending"), P("error", diagnostic), P("delay", Math.Min(300, 1 << job.attempts)), P("batch", job.id), P("token", token));
            logger.LogWarning("Hybrid AI analysis {BatchId} attempt {Attempt} deferred: {Diagnostic}", job.id, job.attempts, diagnostic);
        }
        return true;
    }
}
