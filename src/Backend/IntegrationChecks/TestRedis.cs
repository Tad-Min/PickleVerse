using Microsoft.Extensions.Configuration;
using Services.Configuration;
using Services.Game;
using StackExchange.Redis;

static class TestRedis
{
    public static readonly string Prefix = "pkv-check-" + Guid.NewGuid().ToString("N") + ":";
    static readonly Lazy<ConnectionMultiplexer> connection = new(() => ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("PKV_TEST_REDIS") ?? "localhost:16379,abortConnect=false"));
    public static ConnectionMultiplexer Connection => connection.Value;
    public static RedisSettings Settings => new(new ConfigurationBuilder().AddConfiguration(TestSettings.Configuration())
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Redis:KeyPrefix"] = Prefix }).Build());
    public static RedisGameState State => new(Connection, Settings);
    public static LeaderboardCache Leaderboard => new(Connection, Settings);
    public static async Task Cleanup()
    {
        if (!connection.IsValueCreated)
            return;
        foreach (var endpoint in Connection.GetEndPoints())
        {
            var server = Connection.GetServer(endpoint);
            await foreach (var key in server.KeysAsync(pattern: Prefix + "*"))
                await Connection.GetDatabase().KeyDeleteAsync(key);
        }
        await Connection.DisposeAsync();
    }
}
