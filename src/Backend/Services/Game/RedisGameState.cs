using System.Net;
using PickleVerse.Multiplayer;
using Services.Configuration;
using Services.Exceptions;
using StackExchange.Redis;

namespace Services.Game;

public sealed class QueueEntry
{
    public string searchId="",playerId="",group="",region="asia",state="SEARCHING",matchId="",session="",host="";
    public int mode=1,protocol,elo,losses,botElo;
    public long started,lastSeen,assigned;
    public string[] party=[];
    public RoomMember member=new();
    public MatchRoster? roster;
}

/// <summary>Transient state only. Every transition compares the previous value inside Redis.</summary>
public sealed class RedisGameState(IConnectionMultiplexer connection,RedisSettings settings)
{
    readonly IDatabase redis=connection.GetDatabase();
    public string Prefix=>settings.KeyPrefix+"{matchmaking}:";
    RedisKey Search(string id)=>Prefix+"search:"+id;
    RedisKey Owner(string id)=>Prefix+"player:"+id;
    RedisKey Queue(QueueEntry e)=>Prefix+"queue:"+e.mode+":"+e.region+":"+e.protocol;
    RedisKey Ready(string match)=>Prefix+"ready:"+match;
    public int QueueLeaseSeconds=>settings.QueueLeaseSeconds;
    static AppException Conflict(string message)=>new(message,HttpStatusCode.Conflict);
    public async Task<QueueEntry> Begin(QueueEntry entry)
    {
        var raw=await redis.ScriptEvaluateAsync("""
            local old=redis.call('GET',KEYS[1])
            if old then
              local e=cjson.decode(old)
              if e.playerId~=ARGV[1] or e.state=='CANCELLED' then return false end
              return old
            end
            local previous=redis.call('GET',KEYS[2])
            if previous and previous~=ARGV[2] then return false end
            redis.call('SET',KEYS[1],ARGV[3],'EX',ARGV[4])
            redis.call('SET',KEYS[2],ARGV[2],'EX',ARGV[4])
            redis.call('ZADD',KEYS[3],ARGV[5],ARGV[2])
            return ARGV[3]
            """,[Search(entry.searchId),Owner(entry.playerId),Queue(entry)],
            [entry.playerId,entry.searchId,GameJson.Write(entry),settings.SearchRetentionSeconds,entry.started]);
        if(raw.IsNull)throw Conflict("Bạn đang có lượt tìm trận khác hoặc lượt tìm trận đã hết hiệu lực.");
        var existing=GameJson.Read<QueueEntry>((string)raw!);
        if(existing.group!=entry.group||existing.mode!=entry.mode||existing.region!=entry.region||existing.protocol!=entry.protocol||!existing.party.SequenceEqual(entry.party))
            throw Conflict("Không thể đổi đội hình của lượt tìm trận đang chạy.");
        return existing;
    }
    public async Task<QueueEntry> Get(Guid player,Guid search,bool touch=true)
    {
        for(int retry=0;retry<8;retry++)
        {
            var raw=await redis.StringGetAsync(Search(search.ToString("N")));
            if(raw.IsNullOrEmpty)throw Conflict("Lượt tìm trận đã hết hiệu lực. Hãy tìm trận lại.");
            var entry=GameJson.Read<QueueEntry>(raw!);
            if(entry.playerId!=player.ToString("N")||entry.state=="CANCELLED")throw Conflict("Lượt tìm trận không còn hiệu lực.");
            if(!touch)return entry;
            entry.lastSeen=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if(await Compare([raw!],[entry]))return entry;
        }
        throw Conflict("Lượt tìm trận đang thay đổi. Vui lòng thử lại.");
    }
    // Compare-and-set all seats in one script: competing polls cannot consume a seat twice.
    public async Task<bool> Compare(string[] before,QueueEntry[] after,string? bot=null,int botLeaseSeconds=150)
    {
        var keys=new List<RedisKey>{Queue(after[0]),Prefix+"bot:"+(bot??"none")};
        var args=new List<RedisValue>{after.Length,settings.SearchRetentionSeconds,bot??""};
        for(int i=0;i<after.Length;i++){keys.Add(Search(after[i].searchId));keys.Add(Owner(after[i].playerId));args.Add(before[i]);args.Add(GameJson.Write(after[i]));args.Add(after[i].searchId);args.Add(after[i].started);}
        args.Add(botLeaseSeconds);
        var result=await redis.ScriptEvaluateAsync("""
            local n=tonumber(ARGV[1])
            for i=1,n do if redis.call('GET',KEYS[2*i+1])~=ARGV[4*(i-1)+4] then return 0 end end
            if ARGV[3]~='' and redis.call('EXISTS',KEYS[2])==1 then return 0 end
            for i=1,n do
              local a=4*(i-1)+4
              redis.call('SET',KEYS[2*i+1],ARGV[a+1],'EX',ARGV[2])
              local e=cjson.decode(ARGV[a+1])
              if e.state=='SEARCHING' then redis.call('ZADD',KEYS[1],ARGV[a+3],ARGV[a+2])
              else redis.call('ZREM',KEYS[1],ARGV[a+2]) end
              if e.state=='CANCELLED' then
                if redis.call('GET',KEYS[2*i+2])==ARGV[a+2] then redis.call('DEL',KEYS[2*i+2]) end
              else redis.call('SET',KEYS[2*i+2],ARGV[a+2],'EX',ARGV[2]) end
            end
            if ARGV[3]~='' then redis.call('SET',KEYS[2],ARGV[6],'EX',ARGV[#ARGV]) end
            return 1
            """,keys.ToArray(),args.ToArray());
        return (int)result==1;
    }
    public async Task<List<(string raw,QueueEntry entry)>> Candidates(QueueEntry partition,long? staleBefore=null)
    {
        var ids=await redis.SortedSetRangeByRankAsync(Queue(partition),0,511);
        if(ids.Length==0)return [];
        var values=await redis.StringGetAsync(ids.Select(v=>Search(v.ToString())).ToArray());
        var result=new List<(string,QueueEntry)>();
        for(int i=0;i<values.Length;i++)
            if(values[i].IsNullOrEmpty)await redis.SortedSetRemoveAsync(Queue(partition),ids[i]);
            else
            {
                var entry=GameJson.Read<QueueEntry>(values[i]!);
                if(staleBefore.HasValue&&entry.state=="SEARCHING"&&entry.lastSeen<staleBefore.Value)
                {entry.state="CANCELLED";await Compare([values[i]!],[entry]);}
                else result.Add((values[i]!,entry));
            }
        return result;
    }
    public async Task Cancel(Guid player,Guid search)
    {
        var entry=await Get(player,search,false);
        var affected=(await Candidates(entry)).Where(x=>x.entry.group==entry.group&&x.entry.party.SequenceEqual(entry.party)).ToList();
        if(!affected.Any(x=>x.entry.searchId==entry.searchId))affected.Add((GameJson.Write(entry),entry));
        foreach(var item in affected)
        {
            for(int n=0;n<8;n++)
            {
                var raw=await redis.StringGetAsync(Search(item.entry.searchId));if(raw.IsNullOrEmpty)break;
                var e=GameJson.Read<QueueEntry>(raw!);e.state="CANCELLED";
                if(await Compare([raw!],[e])){await ReleaseBot(e);break;}
            }
        }
        if(entry.matchId.Length>0)
        {
            await CancelReady(entry.matchId);
            if(entry.roster!=null)await ReleaseMatch(entry.roster);
        }
    }
    public async Task Complete(QueueEntry entry)
    {
        var raw=await redis.StringGetAsync(Search(entry.searchId));if(raw.IsNullOrEmpty)return;
        var current=GameJson.Read<QueueEntry>(raw!);current.state="CANCELLED";
        if(await Compare([raw!],[current]))await ReleaseBot(current);
    }
    async Task ReleaseBot(QueueEntry e)
    {
        var bot=e.roster?.members.FirstOrDefault(m=>m.bot);if(bot==null)return;
        await redis.ScriptEvaluateAsync("if redis.call('GET',KEYS[1])==ARGV[1] then return redis.call('DEL',KEYS[1]) end return 0",
            [Prefix+"bot:"+bot.id],[e.searchId]);
    }
    public async Task<QueueEntry?> Active(Guid player)
    {
        var id=await redis.StringGetAsync(Owner(player.ToString("N")));
        if(!Guid.TryParse(id.ToString(),out var search))return null;
        try{return await Get(player,search,false);}catch(AppException){return null;}
    }
    public async Task PublishRoom(QueueEntry entry)
    {
        if(entry.state!="MATCHED"||entry.host!=entry.playerId)throw Conflict("Chỉ người tạo phòng đã được ghép mới được báo phòng sẵn sàng.");
        await redis.StringSetAsync(Prefix+"room:"+entry.matchId,entry.session,TimeSpan.FromSeconds(settings.SearchRetentionSeconds));
    }
    public async Task<bool> RoomReady(QueueEntry entry)=>entry.matchId.Length>0&&await redis.StringGetAsync(Prefix+"room:"+entry.matchId)==entry.session;
    public async Task<bool> ReadyCancelled(string match)=>await redis.HashGetAsync(Ready(match),"cancelled")=="1";
    public async Task<bool> OwnsBot(string bot,string search)=>await redis.StringGetAsync(Prefix+"bot:"+bot)==search;
    public async Task<bool> BotBusy(string bot)=>await redis.KeyExistsAsync(Prefix+"bot:"+bot);
    public async Task ReleaseMatch(MatchRoster roster)
    {
        foreach(var member in roster.members.Where(m=>!m.bot))
        {
            var search=await redis.StringGetAsync(Owner(member.id));if(!Guid.TryParse(search.ToString(),out var id))continue;
            try
            {
                var entry=await Get(Guid.Parse(member.id),id,false);
                if(entry.matchId==roster.matchId)await Complete(entry);
            }
            catch(AppException e) when(e.StatusCode==HttpStatusCode.Conflict){}
        }
        await CancelReady(roster.matchId);
    }
    public async Task<ReadyState> Consent(string match,string fingerprint,string[] players,string[] bots,string? accept,long now,int seconds)
    {
        var value=await redis.ScriptEvaluateAsync("""
            local exists=redis.call('EXISTS',KEYS[1])==1
            if not exists then
              redis.call('HSET',KEYS[1],'fingerprint',ARGV[1],'deadline',ARGV[2]+ARGV[3]*1000,'cancelled','0')
              redis.call('EXPIRE',KEYS[1],ARGV[4])
            end
            if redis.call('HGET',KEYS[1],'fingerprint')~=ARGV[1] or redis.call('HGET',KEYS[1],'cancelled')=='1'
              or tonumber(redis.call('HGET',KEYS[1],'deadline'))<=tonumber(ARGV[2]) then return false end
            if ARGV[5]~='' then redis.call('HSET',KEYS[1],'accepted:'..ARGV[5],'1') end
            for _,p in ipairs(cjson.decode(ARGV[6])) do redis.call('HSET',KEYS[1],'accepted:'..p,'1') end
            return redis.call('HGETALL',KEYS[1])
            """,[Ready(match)],[fingerprint,now,seconds,settings.SearchRetentionSeconds,accept??"",GameJson.Write(bots)]);
        if(value.IsNull)throw Conflict("Lượt xác nhận đã hủy, hết hạn hoặc không khớp phòng.");
        var cells=(RedisResult[])value!;var accepted=new HashSet<string>();long deadline=0;
        for(int i=0;i<cells.Length;i+=2){var key=(string)cells[i]!;if(key=="deadline")deadline=long.Parse((string)cells[i+1]!);else if(key.StartsWith("accepted:"))accepted.Add(key[9..]);}
        return new ReadyState(players.All(accepted.Contains),deadline,accepted);
    }
    public async Task CancelReady(string match)
    {
        await redis.HashSetAsync(Ready(match),"cancelled","1");
        await redis.KeyExpireAsync(Ready(match),TimeSpan.FromSeconds(settings.SearchRetentionSeconds));
    }
    public sealed record ReadyState(bool AllAccepted,long Deadline,HashSet<string> Accepted);
}
