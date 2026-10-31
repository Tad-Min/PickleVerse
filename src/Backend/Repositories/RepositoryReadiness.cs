using Microsoft.Extensions.Logging;
using Repositories.Game;
using StackExchange.Redis;

namespace Repositories;

public sealed class RepositoryReadiness(GameRepository data, IConnectionMultiplexer redis, ILogger<RepositoryReadiness> logger)
{
    public async Task<bool> Check(CancellationToken cancellationToken)
    {
        try
        {
            if (!await data.CanConnect(cancellationToken)) return false;
            await redis.GetDatabase().PingAsync().WaitAsync(cancellationToken);
            return true;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning(error, "A required backend dependency is unavailable.");
            return false;
        }
    }
}
