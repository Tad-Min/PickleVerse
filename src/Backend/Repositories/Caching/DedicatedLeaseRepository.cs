using Entities.Configuration;
using Entities.Game;
using StackExchange.Redis;

namespace Repositories.Caching;

// One slot for the first deployment. Atomic scripts keep an allocation's lease token fenced.
public sealed class DedicatedLeaseRepository(IConnectionMultiplexer connection, RedisSettings settings)
{
    IDatabase Redis => connection.GetDatabase();
    RedisKey Key(string suffix) => settings.KeyPrefix + "{dedicated}:" + suffix;
    public Task WorkerAlive() => Redis.StringSetAsync(Key("worker"), "main", TimeSpan.FromSeconds(20));
    public Task<bool> HasWorker() => Redis.KeyExistsAsync(Key("worker"));
    public async Task<DedicatedAllocation?> Read()
    {
        var value = await Redis.StringGetAsync(Key("slot"));
        return value.IsNullOrEmpty ? null : GameJson.Read<DedicatedAllocation>(value.ToString());
    }
    public async Task<DedicatedAllocation?> Claim(string worker, int seconds)
    {
        var result = await Redis.ScriptEvaluateAsync("""
            local old=redis.call('GET',KEYS[1]);if not old then return '' end
            local job=cjson.decode(old)
            if job.workerId and job.workerId~='' and job.workerId~=ARGV[1] then return '' end
            if not job.workerId or job.workerId=='' then
              job.workerId=ARGV[1];job.state='starting';old=cjson.encode(job)
              redis.call('SET',KEYS[1],old,'EX',ARGV[2])
            end
            return old
            """, [Key("slot")], [worker, seconds]);
        string json = result.ToString(); return string.IsNullOrEmpty(json) ? null : GameJson.Read<DedicatedAllocation>(json);
    }
    public async Task<DedicatedAllocation?> Allocate(DedicatedAllocation value)
    {
        var result = await Redis.ScriptEvaluateAsync("""
            local old=redis.call('GET',KEYS[1])
            if old then
              if cjson.decode(old).matchId==ARGV[1] then return old end
              return ''
            end
            redis.call('SET',KEYS[1],ARGV[2],'EX',120);return ARGV[2]
            """, [Key("slot")], [value.MatchId, GameJson.Write(value)]);
        string json = result.ToString();
        return string.IsNullOrEmpty(json) ? null : GameJson.Read<DedicatedAllocation>(json);
    }
    public async Task<bool> Renew(DedicatedAllocation value, int seconds)
    {
        var result = await Redis.ScriptEvaluateAsync("""
            local old=redis.call('GET',KEYS[1]);if not old then return 0 end
            if cjson.decode(old).leaseId~=ARGV[1] then return 0 end
            redis.call('SET',KEYS[1],ARGV[2],'EX',ARGV[3]);return 1
            """, [Key("slot")], [value.LeaseId, GameJson.Write(value), seconds]);
        return (int)result == 1;
    }
    public async Task Release(string lease)
    {
        await Redis.ScriptEvaluateAsync("""
            local old=redis.call('GET',KEYS[1]);if old and cjson.decode(old).leaseId==ARGV[1] then return redis.call('DEL',KEYS[1]) end;return 0
            """, [Key("slot")], [lease]);
    }
    public Task SaveLive(string match, DedicatedLiveState live) => Redis.StringSetAsync(Key("live:" + match), GameJson.Write(live), TimeSpan.FromMinutes(5));
    public async Task<DedicatedLiveState?> Live(string match)
    {
        var value = await Redis.StringGetAsync(Key("live:" + match));
        return value.IsNullOrEmpty ? null : GameJson.Read<DedicatedLiveState>(value.ToString());
    }
}
