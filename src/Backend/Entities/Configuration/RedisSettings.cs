namespace Entities.Configuration;

public sealed class RedisSettings
{
    public string ConnectionString { get; init; } = "";
    public string KeyPrefix { get; init; } = "pickleverse:";
    public int SearchRetentionSeconds { get; init; }
    public int QueueLeaseSeconds { get; init; }
    public int LeaderboardCacheSeconds { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new InvalidOperationException("Redis:ConnectionString is required.");
        if (string.IsNullOrWhiteSpace(KeyPrefix) || KeyPrefix.Contains('{') || KeyPrefix.Contains('}'))
            throw new InvalidOperationException("Invalid Redis key prefix.");
        static void Number(int value, string key, int min, int max)
        {
            if (value < min || value > max)
                throw new InvalidOperationException("Invalid Redis:" + key);
        }
        Number(SearchRetentionSeconds, nameof(SearchRetentionSeconds), 600, 86400);
        Number(QueueLeaseSeconds, nameof(QueueLeaseSeconds), 10, 120);
        Number(LeaderboardCacheSeconds, nameof(LeaderboardCacheSeconds), 1, 300);
    }
}
