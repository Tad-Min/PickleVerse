using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

namespace Services.Workers;
public sealed class AnalyticsProjectionWorker(IServiceScopeFactory scopes,ILogger<AnalyticsProjectionWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(5));
        while(await timer.WaitForNextTickAsync(token))
        {
            try{using var scope=scopes.CreateScope();await scope.ServiceProvider.GetRequiredService<IAnalyticsService>().ProjectMatchmaking();}
            catch(Exception error)when(!token.IsCancellationRequested){logger.LogWarning("Analytics projection failed ({ErrorType}).",error.GetType().Name);}
        }
    }
}
