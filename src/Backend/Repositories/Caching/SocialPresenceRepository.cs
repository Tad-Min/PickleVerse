using StackExchange.Redis;

namespace Repositories.Caching;

public sealed class SocialPresenceRepository(IConnectionMultiplexer connection, RedisSettings settings)
{
    public sealed record VideoGrant(Guid PlayerId, int AuthVersion, long PostId);
    IDatabase Redis => connection.GetDatabase();
    RedisKey Key(Guid player) => settings.KeyPrefix + "{social-presence}:" + player.ToString("N");
    public async Task Touch(Guid player, string connectionId)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await Redis.ScriptEvaluateAsync("""
            redis.call('ZREMRANGEBYSCORE',KEYS[1],'-inf',ARGV[1])
            if redis.call('ZCARD',KEYS[1])>=8 and not redis.call('ZSCORE',KEYS[1],ARGV[2]) then
              redis.call('ZPOPMIN',KEYS[1])
            end
            redis.call('ZADD',KEYS[1],ARGV[3],ARGV[2])
            redis.call('PEXPIRE',KEYS[1],45000)
            """, [Key(player)], [now, connectionId, now + 45000]);
    }
    public Task Clear(Guid player, string connectionId) => Redis.SortedSetRemoveAsync(Key(player), connectionId);
    public async Task<string[]> Connections(Guid player) =>
        (await Redis.SortedSetRangeByScoreAsync(Key(player), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            double.PositiveInfinity, Exclude.Start)).Select(value => value.ToString()).ToArray();

    public Task GrantVideo(string token, VideoGrant grant) => Redis.StringSetAsync(
        settings.KeyPrefix + "social-video:" + token, Entities.Game.GameJson.Write(grant), TimeSpan.FromMinutes(20));
    public async Task<VideoGrant?> ReadVideoGrant(string token)
    {
        var raw = await Redis.StringGetAsync(settings.KeyPrefix + "social-video:" + token);
        return raw.IsNullOrEmpty ? null : Entities.Game.GameJson.Read<VideoGrant>(raw!);
    }
}
