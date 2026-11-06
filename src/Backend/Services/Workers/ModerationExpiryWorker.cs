using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

namespace Services.Workers;

public sealed class ModerationExpiryWorker(IServiceScopeFactory scopes, ILogger<ModerationExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(token))
        {
            try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<IModerationService>().Expire(); }
            catch (Exception error) when (!token.IsCancellationRequested) { logger.LogWarning("Sanction expiry scan failed ({ErrorType}).", error.GetType().Name); }
        }
    }
}
