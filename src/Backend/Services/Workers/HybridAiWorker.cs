using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PickleVerse.Progression;
using Services.Configuration;
using Services.Implementations;

namespace Services.Workers;

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
                    var data = scope.ServiceProvider.GetRequiredService<Repositories.UnitOfWork.IUnitOfWork>().Game;
                    if (lane == 0 && DateTime.UtcNow >= nextCleanup)
                    {
                        await data.CleanupTelemetry(DateTime.UtcNow.AddDays(-30));
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
        var data = services.GetRequiredService<Repositories.UnitOfWork.IUnitOfWork>().Game;
        var token = Guid.NewGuid();
        var job = await data.ClaimAnalysis(token);
        if (job == null) return false;
        var timer=System.Diagnostics.Stopwatch.StartNew();
        try
        {
            string request = job.request ?? await services.GetRequiredService<Services.Interfaces.IHybridCoachingService>().BuildRequest(job.id);
            if (job.request == null)
                await data.SaveAnalysisRequest(job.id, token, request);
            using var client = clients.CreateClient("hybrid-ai");
            using var message = new HttpRequestMessage(HttpMethod.Post, settings.ServiceUrl.TrimEnd('/') + "/v1/analyze-session");
            message.Headers.Add("X-AI-Key", settings.ServiceKey);
            message.Content = new StringContent(request, System.Text.Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(message, stop);
            response.EnsureSuccessStatusCode();
            var result = GameJson.Read<CoachingAnalysis>(await response.Content.ReadAsStringAsync(stop));
            using var input = System.Text.Json.JsonDocument.Parse(request);
            if (result.sessionId != input.RootElement.GetProperty("sessionId").GetString() || result.ruleVersion != "rules-v1"
                || result.findings == null || result.metrics == null || result.recommendations == null || result.recommendations.Count > 3)
                throw new InvalidDataException("AI response contract mismatch.");
            if (!await data.CompleteAnalysis(job.id, token, result, stop)) return true;
            HybridAiMetrics.AnalysisMs.Record(timer.Elapsed.TotalMilliseconds);
            HybridAiMetrics.Completed.Add(1);
            logger.LogInformation("Hybrid AI analysis completed {BatchId}", job.id);
        }
        catch (Exception error) when (!stop.IsCancellationRequested)
        {
            HybridAiMetrics.Retried.Add(1);
            // Store a bounded diagnostic without response bodies, player telemetry or service keys.
            string diagnostic = PersistenceErrors.Diagnostic(error);
            await data.RetryAnalysis(job.id, token, job.attempts, diagnostic);
            logger.LogWarning("Hybrid AI analysis {BatchId} attempt {Attempt} deferred: {Diagnostic}", job.id, job.attempts, diagnostic);
        }
        return true;
    }
}
