using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

namespace Services.Workers;

public sealed class DedicatedLeaseMonitor(IServiceScopeFactory scopes, ILogger<DedicatedLeaseMonitor> logger, Services.Configuration.ServerSettings settings) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Dedicated.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<IDedicatedServerService>().InterruptExpired(); }
            catch (Exception error) { logger.LogWarning("Dedicated lease scan failed ({ErrorType}).", error.GetType().Name); }
        }
    }
}
