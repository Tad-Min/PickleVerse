using Services.Configuration;
using StackExchange.Redis;
using static Services.Game.GameSql;

namespace Services.Game;

/// <summary>Elo stays in PostgreSQL. A versioned Sorted Set is disposable and expires automatically.</summary>
public sealed class LeaderboardCache(IConnectionMultiplexer connection,RedisSettings settings)
{
    readonly IDatabase redis=connection.GetDatabase();
    string Prefix=>settings.KeyPrefix+"{leaderboard}:";
    public async Task<string> Snapshot(GameSql sql)
    {
        var cached=await redis.StringGetAsync(Prefix+"current");
        if(!cached.IsNullOrEmpty&&await redis.KeyExistsAsync(cached.ToString()))return cached!;
        string key=Prefix+"scores:"+Guid.NewGuid().ToString("N");
        var epoch=await redis.StringGetAsync(Prefix+"epoch");
        var rows=await sql.Query("""
            SELECT p.userid,p.elo FROM playerprofiles p JOIN users u ON u.userid=p.userid
            WHERE u.status='ACTIVE' AND COALESCE((SELECT v.isverified FROM verificationemail v WHERE v.userid=u.userid
            ORDER BY v.createdat DESC,v.verificationemailid DESC LIMIT 1),false)
            """,r=>new SortedSetEntry(r.GetGuid(0).ToString("N"),-r.GetInt32(1)));
        // A sentinel permits caching an empty leaderboard; it sorts after every valid Elo.
        rows.Add(new SortedSetEntry("~empty",double.MaxValue));
        var tx=redis.CreateTransaction();
        _=tx.SortedSetAddAsync(key,rows.ToArray());
        _=tx.KeyExpireAsync(key,TimeSpan.FromSeconds(settings.LeaderboardCacheSeconds*2));
        await tx.ExecuteAsync();
        var published=await redis.ScriptEvaluateAsync("""
            if (redis.call('GET',KEYS[1]) or '')~=ARGV[1] then return 0 end
            redis.call('SET',KEYS[2],ARGV[2],'EX',ARGV[3]); return 1
            """,[Prefix+"epoch",Prefix+"current"],[epoch.IsNull?"":epoch.ToString(),key,settings.LeaderboardCacheSeconds]);
        if((int)published==0){await redis.KeyDeleteAsync(key);return await Snapshot(sql);}
        return key;
    }
    public async Task<List<(Guid id,int position)>> Ranks(string key,Guid[] candidates)
    {
        var ranks=await Task.WhenAll(candidates.Select(id=>redis.SortedSetRankAsync(key,id.ToString("N"))));
        return candidates.Select((id,i)=>(id,rank:ranks[i])).Where(x=>x.rank.HasValue)
            .Select(x=>(x.id,position:checked((int)x.rank!.Value+1))).OrderBy(x=>x.position).ToList();
    }
    public async Task<Guid[]> Top(string key,int count)=>(await redis.SortedSetRangeByRankAsync(key,0,count-1))
        .Where(x=>x!="~empty").Select(x=>Guid.Parse(x.ToString())).ToArray();
    public async Task<int> Rank(string key,Guid id)=>(int)((await redis.SortedSetRankAsync(key,id.ToString("N")))+1??0);
    public async Task<int> Count(string key)=>(int)Math.Max(0,await redis.SortedSetLengthAsync(key)-1);
    public async Task Invalidate()=>await redis.ScriptEvaluateAsync("redis.call('INCR',KEYS[1]);redis.call('DEL',KEYS[2]);return 1",[Prefix+"epoch",Prefix+"current"]);
}
