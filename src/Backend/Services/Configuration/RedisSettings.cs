using Microsoft.Extensions.Configuration;

namespace Services.Configuration;

public sealed class RedisSettings
{
    public string ConnectionString { get; }
    public string KeyPrefix { get; }
    public int SearchRetentionSeconds { get; }
    public int QueueLeaseSeconds { get; }
    public int LeaderboardCacheSeconds { get; }
    public RedisSettings(IConfiguration config)
    {
        ConnectionString=config["Redis:ConnectionString"]??throw new InvalidOperationException("Redis:ConnectionString is required.");
        KeyPrefix=config["Redis:KeyPrefix"]??"pickleverse:";
        if(string.IsNullOrWhiteSpace(KeyPrefix)||KeyPrefix.Contains('{')||KeyPrefix.Contains('}'))throw new InvalidOperationException("Invalid Redis key prefix.");
        int Number(string key,int min,int max)
        {
            if(!int.TryParse(config["Redis:"+key],out var n)||n<min||n>max)throw new InvalidOperationException("Invalid Redis:"+key);
            return n;
        }
        SearchRetentionSeconds=Number("SearchRetentionSeconds",600,86400);
        QueueLeaseSeconds=Number("QueueLeaseSeconds",10,120);
        LeaderboardCacheSeconds=Number("LeaderboardCacheSeconds",1,300);
    }
}
