using Entities.Configuration;
using Entities.Game;
using StackExchange.Redis;

namespace Repositories.Caching;

// All indexes share one hash tag. Lua owns both lease identity and the worker's single slot.
public sealed class DedicatedLeaseRepository(IConnectionMultiplexer connection, RedisSettings settings)
{
    IDatabase Redis => connection.GetDatabase();
    RedisKey Key(string suffix) => settings.KeyPrefix + "{dedicated}:v2:" + suffix;
    RedisKey[] Keys() => [Key("alive"), Key("idle"), Key("worker-leases"), Key("leases"),
        Key("matches"), Key("expires"), Key("waiting"), Key("waiting-expires")];

    const string Indexes = """
        local alive,idle,workerLeases,leases,matches,expires,waiting,waitingExpires=unpack(KEYS)
        local t=redis.call('TIME')
        local now=tonumber(t[1])*1000+math.floor(tonumber(t[2])/1000)
        local function read(match)
          local value=redis.call('HGET',matches,match)
          return value and cjson.decode(value)
        end
        local function forget(match)
          redis.call('ZREM',waiting,match)
          redis.call('ZREM',waitingExpires,match)
        end
        local function release(match)
          local job=read(match)
          if not job then return end
          redis.call('HDEL',matches,match)
          redis.call('HDEL',leases,job.leaseId)
          redis.call('HDEL',workerLeases,job.workerId)
          redis.call('ZREM',expires,match)
          local registered=tonumber(redis.call('ZSCORE',alive,job.workerId) or '0')
          if registered>now and (job.state=='reserved' or job.state=='queued') then
            redis.call('ZADD',idle,registered,job.workerId)
          end
        end
        local function prune()
          redis.call('ZREMRANGEBYSCORE',alive,'-inf',now)
          redis.call('ZREMRANGEBYSCORE',idle,'-inf',now)
          for _,match in ipairs(redis.call('ZRANGEBYSCORE',expires,'-inf',now)) do release(match) end
          for _,match in ipairs(redis.call('ZRANGEBYSCORE',waitingExpires,'-inf',now)) do forget(match) end
        end
        local function save(job,seconds)
          local json=cjson.encode(job)
          redis.call('HSET',matches,job.matchId,json)
          redis.call('ZADD',expires,now+tonumber(seconds)*1000,job.matchId)
          return json
        end
        local function current(lease,worker)
          local match=redis.call('HGET',leases,lease)
          if not match or tonumber(redis.call('ZSCORE',expires,match) or '0')<=now then return end
          local job=read(match)
          if job and job.leaseId==lease and (worker=='' or job.workerId==worker) then return job end
        end
        """;

    async Task<DedicatedAllocation?> Evaluate(string operation, params RedisValue[] args)
    {
        var result = await Redis.ScriptEvaluateAsync(Indexes + "\n" + operation, Keys(), args);
        return result.IsNull ? null : GameJson.Read<DedicatedAllocation>((string)result!);
    }

    public Task<DedicatedAllocation?> ReadMatch(string match) => Evaluate("""
        local job=read(ARGV[1])
        if job and current(job.leaseId,'') then return cjson.encode(job) end
        """, match);

    public async Task<bool> IsWaiting(string match)
        => (int)await Redis.ScriptEvaluateAsync(Indexes + """

            if tonumber(redis.call('ZSCORE',waitingExpires,ARGV[1]) or '0')>now then return 1 end
            return 0
            """, Keys(), [match]) == 1;

    public Task<DedicatedAllocation?> ReadLease(string lease, string worker) => Evaluate("""
        local job=current(ARGV[1],ARGV[2])
        if job and job.state~='reserved' and job.state~='queued' then return cjson.encode(job) end
        """, lease, worker);

    public Task<DedicatedAllocation?> Reserve(DedicatedAllocation job, int limit, int seconds) => Evaluate("""
        prune()
        local match=ARGV[1]
        local existing=read(match)
        if existing then
          if existing.state~='reserved' or tonumber(redis.call('ZSCORE',alive,existing.workerId) or '0')>now then
            return cjson.encode(existing)
          end
          release(match)
        end
        if redis.call('ZCARD',waiting)>=4096 and not redis.call('ZSCORE',waiting,match) then return end
        redis.call('ZADD',waiting,'NX',now,match)
        redis.call('ZADD',waitingExpires,now+15000,match)
        if redis.call('ZCARD',expires)>=tonumber(ARGV[3]) then return end
        local first=redis.call('ZRANGE',waiting,0,0)[1]
        if first~=match then return end
        local worker=redis.call('ZRANGE',idle,0,0)[1]
        if not worker then return end
        local job=cjson.decode(ARGV[2])
        job.workerId=worker
        job.state='reserved'
        forget(match)
        redis.call('ZREM',idle,worker)
        redis.call('HSET',workerLeases,worker,match)
        redis.call('HSET',leases,job.leaseId,match)
        return save(job,ARGV[4])
        """, job.MatchId, GameJson.Write(job), Math.Clamp(limit, 1, DedicatedServerSettings.MaximumConcurrentMatches), seconds);

    public Task<DedicatedAllocation?> Activate(string match, int seconds) => Evaluate("""
        local job=read(ARGV[1])
        if not job or not current(job.leaseId,'') then return end
        if job.state~='reserved' then return cjson.encode(job) end
        if tonumber(redis.call('ZSCORE',alive,job.workerId) or '0')<=now then
          release(job.matchId)
          return
        end
        job.state='queued'
        return save(job,ARGV[2])
        """, match, seconds);

    public Task<DedicatedAllocation?> Poll(string worker, int seconds) => Evaluate("""
        prune()
        local worker=ARGV[1]
        redis.call('ZADD',alive,now+20000,worker)
        local match=redis.call('HGET',workerLeases,worker)
        if not match then
          redis.call('ZADD',idle,now+20000,worker)
          return
        end
        local job=read(match)
        if job.state=='queued' then
          job.state='starting'
          return save(job,ARGV[2])
        end
        if job.state=='starting' then return cjson.encode(job) end
        """, worker, seconds);

    public async Task<bool> Renew(string lease, string worker, DedicatedAllocationStatus? state, int seconds)
        => await Evaluate("""
            local job=current(ARGV[1],ARGV[2])
            if not job or job.state=='reserved' or job.state=='queued' then return end
            if ARGV[3]=='finishing' or (ARGV[3]=='ready' and job.state=='starting') then job.state=ARGV[3] end
            redis.call('ZADD',alive,now+20000,job.workerId)
            return save(job,ARGV[4])
            """, lease, worker, state.HasValue ? Entities.Enums.StatusWire.Format(state.Value) : "", seconds) != null;

    public Task<DedicatedAllocation?> Release(string lease, string worker) => Evaluate("""
        local job=current(ARGV[1],ARGV[2])
        if job then
          release(job.matchId)
          return cjson.encode(job)
        end
        """, lease, worker);

    public Task CancelReservation(string match) => Redis.ScriptEvaluateAsync(Indexes + """

        forget(ARGV[1])
        local job=read(ARGV[1])
        if job and job.state=='reserved' then release(job.matchId) end
        """, Keys(), [match]);

    public Task SaveLive(string match, DedicatedLiveState live) => Redis.StringSetAsync(Key("live:" + match), GameJson.Write(live), TimeSpan.FromMinutes(5));
    public async Task<DedicatedLiveState?> Live(string match)
    {
        var value = await Redis.StringGetAsync(Key("live:" + match));
        return value.IsNullOrEmpty ? null : GameJson.Read<DedicatedLiveState>(value.ToString());
    }
}
