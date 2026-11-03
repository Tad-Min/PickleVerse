using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

namespace Services.Workers;

// Codes exist only in bounded process memory and expire in SQL. Resend recovers a restart/lost delivery.
public sealed class PasswordRecoveryMailQueue(IServiceScopeFactory scopes, ILogger<PasswordRecoveryMailQueue> logger) : BackgroundService
{
    readonly Channel<(string email, string code)> queue = Channel.CreateBounded<(string, string)>(256);
    public bool TryEnqueue(string email, string code) => queue.Writer.TryWrite((email, code));
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IEmailSender>().SendPasswordResetCodeAsync(item.email, item.code);
            }
            catch (Exception error) { logger.LogWarning("Password recovery delivery failed ({ErrorType}); resend is available.", error.GetType().Name); }
        }
    }
}
