using System.Security.Cryptography;
using System.Text;
using Entities.DTOs;
using Entities.Game;
using StackExchange.Redis;

namespace Repositories.Caching;

public sealed class ReplayLaunchRepository(IConnectionMultiplexer connection, RedisSettings settings)
{
    IDatabase Redis => connection.GetDatabase();
    RedisKey Key(string kind, string token) => settings.KeyPrefix + "{replay-launch}:" + kind + ":" +
        Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token)));
    public Task<bool> Issue(string code, ReplayLaunchAccess access, TimeSpan lifetime) =>
        Redis.StringSetAsync(Key("code", code), GameJson.Write(access), lifetime, When.NotExists);
    async Task<ReplayLaunchAccess?> Read(string kind, string token)
    {
        var data = await Redis.StringGetAsync(Key(kind, token));
        return data.IsNullOrEmpty ? null : GameJson.Read<ReplayLaunchAccess>(data!);
    }
    public Task<ReplayLaunchAccess?> ReadCode(string code) => Read("code", code);
    public Task<ReplayLaunchAccess?> ReadView(string token) => Read("view", token);
    public async Task<bool> Exchange(string code, ReplayLaunchAccess expected, string token, ReplayLaunchAccess view, TimeSpan lifetime)
        => (int)await Redis.ScriptEvaluateAsync("""
            if redis.call('GET',KEYS[1])~=ARGV[1] or redis.call('EXISTS',KEYS[2])~=0 then return 0 end
            redis.call('SET',KEYS[2],ARGV[2],'PX',ARGV[3])
            redis.call('DEL',KEYS[1])
            return 1
            """, [Key("code", code), Key("view", token)],
            [GameJson.Write(expected), GameJson.Write(view), (long)lifetime.TotalMilliseconds]) == 1;
}
