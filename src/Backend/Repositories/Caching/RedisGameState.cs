using System.Net;
using PickleVerse.Multiplayer;
using Entities.Exceptions;
using StackExchange.Redis;

namespace Repositories.Caching;

public sealed class QueueEntry
{
    public string searchId = "", playerId = "", group = "", region = "asia", matchId = "", session = "", host = "";
    public MatchmakingSearchStatus state = MatchmakingSearchStatus.Searching;
    public int mode = 1, protocol, elo, losses, botElo;
    public long started, lastSeen, assigned, lastRecorded;
    public string[] party = [];
    public RoomMember member = new();
    public MatchRoster? roster;
}

/// <summary>Transient state only. Every transition compares the previous value inside Redis.</summary>
public sealed class RedisGameState(IConnectionMultiplexer connection, RedisSettings settings)
{
    readonly IDatabase redis = connection.GetDatabase();
    public string Prefix => settings.KeyPrefix + "{matchmaking}:";
    RedisKey Search(string id) => Prefix + "search:" + id;
    RedisKey Owner(string id) => Prefix + "player:" + id;
    RedisKey Queue(QueueEntry e) => Prefix + "queue:" + e.mode + ":" + e.region + ":" + e.protocol;
    RedisKey Ready(string match) => Prefix + "ready:" + match;
    public int QueueLeaseSeconds => settings.QueueLeaseSeconds;
    public async Task<bool> HasActiveSearch(Guid player, long now)
    {
        var id = await redis.StringGetAsync(Owner(player.ToString("N")));
        if (id.IsNullOrEmpty) return false;
        var raw = await redis.StringGetAsync(Search(id.ToString()));
        if (raw.IsNullOrEmpty) return false;
        var entry = GameJson.Read<QueueEntry>(raw.ToString());
        return entry.state == MatchmakingSearchStatus.Searching && entry.lastSeen + settings.QueueLeaseSeconds * 1000L > now;
    }
    static AppException Conflict(string message) => new(message, HttpStatusCode.Conflict);
    RedisKey Journal => Prefix + "journal";
    public async Task<QueueEntry> Begin(QueueEntry entry)
    {
        entry.lastRecorded = entry.started;
        var raw = await redis.ScriptEvaluateAsync("""
            local old=redis.call('GET',KEYS[1])
            if old then
              local e=cjson.decode(old)
              if e.playerId~=ARGV[1] or e.state=='CANCELLED' or e.state=='TIMEOUT' then return false end
              return old
            end
            local previous=redis.call('GET',KEYS[2])
            if previous and previous~=ARGV[2] then return false end
            redis.call('SET',KEYS[1],ARGV[3],'EX',ARGV[4])
            redis.call('SET',KEYS[2],ARGV[2],'EX',ARGV[4])
            redis.call('ZADD',KEYS[3],ARGV[5],ARGV[2])
            redis.call('XADD',KEYS[4],'*','data',ARGV[3])
            return ARGV[3]
            """, [Search(entry.searchId), Owner(entry.playerId), Queue(entry), Journal],
            [entry.playerId, entry.searchId, GameJson.Write(entry), settings.SearchRetentionSeconds, entry.started]);
        if (raw.IsNull)
            throw Conflict("Bạn đang có lượt tìm trận khác hoặc lượt tìm trận đã hết hiệu lực.");
        var existing = GameJson.Read<QueueEntry>((string)raw!);
        if (existing.group != entry.group || existing.mode != entry.mode || existing.region != entry.region || existing.protocol != entry.protocol || !existing.party.SequenceEqual(entry.party))
            throw Conflict("Không thể đổi đội hình của lượt tìm trận đang chạy.");
        return existing;
    }
    public async Task<QueueEntry> Get(Guid player, Guid search, bool touch = true)
    {
        for (int retry = 0; retry < 8; retry++)
        {
            var raw = await redis.StringGetAsync(Search(search.ToString("N")));
            if (raw.IsNullOrEmpty)
                throw Conflict("Lượt tìm trận đã hết hiệu lực. Hãy tìm trận lại.");
            var entry = GameJson.Read<QueueEntry>(raw!);
            if (entry.playerId != player.ToString("N") || entry.state is MatchmakingSearchStatus.Cancelled or MatchmakingSearchStatus.TimedOut)
                throw Conflict("Lượt tìm trận không còn hiệu lực.");
            if (!touch)
                return entry;
            entry.lastSeen = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (await Compare([raw!], [entry]))
                return entry;
        }
        throw Conflict("Lượt tìm trận đang thay đổi. Vui lòng thử lại.");
    }
    // Compare-and-set all seats in one script: competing polls cannot consume a seat twice.
    public async Task<bool> Compare(string[] before, QueueEntry[] after, string? bot = null, int botLeaseSeconds = 150,
        string? cancelMatch = null)
    {
        var keys = new List<RedisKey> { Queue(after[0]), Prefix + "bot:" + (bot ?? "none") };
        var args = new List<RedisValue> { after.Length, settings.SearchRetentionSeconds, bot ?? "" };
        for (int i = 0; i < after.Length; i++)
        {
            var previous = GameJson.Read<QueueEntry>(before[i]);
            after[i].lastRecorded = after[i].state != previous.state || after[i].lastSeen - previous.lastRecorded >= 15000
                ? after[i].lastSeen : previous.lastRecorded;
            keys.Add(Search(after[i].searchId));
            keys.Add(Owner(after[i].playerId));
            args.Add(before[i]);
            args.Add(GameJson.Write(after[i]));
            args.Add(after[i].searchId);
            args.Add(after[i].started);
        }
        args.Add(botLeaseSeconds);
        args.Add(cancelMatch ?? "");
        keys.Add(Journal);
        keys.Add(Ready(cancelMatch ?? "none"));
        var result = await redis.ScriptEvaluateAsync("""
            local n=tonumber(ARGV[1])
            for i=1,n do
              local a=4*(i-1)+4
              if redis.call('GET',KEYS[2*i+1])~=ARGV[a] or redis.call('GET',KEYS[2*i+2])~=ARGV[a+2] then return 0 end
            end
            if ARGV[3]~='' and redis.call('EXISTS',KEYS[2])==1 then return 0 end
            for i=1,n do
              local a=4*(i-1)+4
              local e=cjson.decode(ARGV[a+1])
              local previous=cjson.decode(ARGV[a])
              local journal=e.state~=previous.state or e.lastRecorded~=(previous.lastRecorded or 0)
              redis.call('SET',KEYS[2*i+1],ARGV[a+1],'EX',ARGV[2])
              if journal then redis.call('XADD',KEYS[#KEYS-1],'*','data',ARGV[a+1]) end
              if e.state=='SEARCHING' then redis.call('ZADD',KEYS[1],ARGV[a+3],ARGV[a+2])
              else redis.call('ZREM',KEYS[1],ARGV[a+2]) end
              if e.state=='CANCELLED' or e.state=='TIMEOUT' then
                if redis.call('GET',KEYS[2*i+2])==ARGV[a+2] then redis.call('DEL',KEYS[2*i+2]) end
              else redis.call('SET',KEYS[2*i+2],ARGV[a+2],'EX',ARGV[2]) end
            end
            if ARGV[3]~='' then redis.call('SET',KEYS[2],ARGV[6],'EX',ARGV[#ARGV-1]) end
            if ARGV[#ARGV]~='' then
              redis.call('HSET',KEYS[#KEYS],'cancelled','1')
              redis.call('EXPIRE',KEYS[#KEYS],ARGV[2])
            end
            return 1
            """, keys.ToArray(), args.ToArray());
        return (int)result == 1;
    }
    public async Task<List<(string raw, QueueEntry entry)>> Candidates(QueueEntry partition, long? staleBefore = null)
    {
        // Rotate bounded pages per search while always including the caller and party.
        // Polls beyond the oldest 512 seats must participate, even when those seats cannot match.
        long count = await redis.SortedSetLengthAsync(Queue(partition));
        long pages = Math.Max(1, (count + 511) / 512);
        long page = (await redis.StringIncrementAsync(Prefix + "cursor:" + partition.searchId) - 1) % pages;
        await redis.KeyExpireAsync(Prefix + "cursor:" + partition.searchId, TimeSpan.FromSeconds(settings.SearchRetentionSeconds));
        var pageIds = await redis.SortedSetRangeByRankAsync(Queue(partition), page * 512, page * 512 + 511);
        var partySearches = await redis.StringGetAsync(partition.party.Select(Owner).ToArray());
        var ids = pageIds.Concat(partySearches.Where(x => !x.IsNullOrEmpty)).Append((RedisValue)partition.searchId).Distinct().ToArray();
        if (ids.Length == 0)
            return [];
        var values = await redis.StringGetAsync(ids.Select(v => Search(v.ToString())).ToArray());
        var result = new List<(string, QueueEntry)>();
        for (int i = 0; i < values.Length; i++)
            if (values[i].IsNullOrEmpty)
                await redis.SortedSetRemoveAsync(Queue(partition), ids[i]);
            else
            {
                var entry = GameJson.Read<QueueEntry>(values[i]!);
                if (staleBefore.HasValue && entry.state == MatchmakingSearchStatus.Searching && entry.lastSeen < staleBefore.Value)
                {
                    entry.state = MatchmakingSearchStatus.TimedOut;
                    await Compare([values[i]!], [entry]);
                }
                else result.Add((values[i]!, entry));
            }
        return result;
    }
    public async Task<bool> Cancel(QueueEntry expected)
    {
        var players = expected.roster?.members.Where(member => !member.bot).Select(member => member.id).ToArray() ?? expected.party;
        for (int retry = 0; retry < 8; retry++)
        {
            var active = await redis.StringGetAsync(players.Select(Owner).ToArray());
            var ids = active.Where(value => !value.IsNullOrEmpty).Append(expected.searchId).Distinct();
            var values = await redis.StringGetAsync(ids.Select(value => Search(value.ToString())).ToArray());
            var entries = values.Where(raw => !raw.IsNullOrEmpty).Select(raw => (raw: raw.ToString(), entry: GameJson.Read<QueueEntry>(raw!))).ToList();
            var own = entries.SingleOrDefault(x => x.entry.searchId == expected.searchId).entry;
            if (own == null || own.state is MatchmakingSearchStatus.Cancelled or MatchmakingSearchStatus.TimedOut)
                return true;
            if (own.matchId != expected.matchId)
                return false;
            var affected = entries.Where(x => x.entry.state is not (MatchmakingSearchStatus.Cancelled or MatchmakingSearchStatus.TimedOut) &&
                (x.entry.searchId == own.searchId || (own.matchId.Length > 0 ? x.entry.matchId == own.matchId :
                    x.entry.matchId.Length == 0 && x.entry.group == own.group && x.entry.party.SequenceEqual(own.party) && x.entry.started <= expected.lastSeen))).ToArray();
            foreach (var item in affected)
                item.entry.state = MatchmakingSearchStatus.Cancelled;
            if (await Compare(affected.Select(x => x.raw).ToArray(), affected.Select(x => x.entry).ToArray(), cancelMatch: own.matchId))
            {
                foreach (var item in affected)
                    await ReleaseBot(item.entry);
                return true;
            }
        }
        return false;
    }
    public async Task Complete(QueueEntry entry)
    {
        var raw = await redis.StringGetAsync(Search(entry.searchId));
        if (raw.IsNullOrEmpty)
            return;
        var current = GameJson.Read<QueueEntry>(raw!);
        current.state = MatchmakingSearchStatus.Cancelled;
        if (await Compare([raw!], [current]))
            await ReleaseBot(current);
    }
    async Task ReleaseBot(QueueEntry e)
    {
        var bot = e.roster?.members.FirstOrDefault(m => m.bot);
        if (bot == null)
            return;
        await redis.ScriptEvaluateAsync("if redis.call('GET',KEYS[1])==ARGV[1] then return redis.call('DEL',KEYS[1]) end return 0",
            [Prefix + "bot:" + bot.id], [e.searchId]);
    }
    public async Task<QueueEntry?> Active(Guid player)
    {
        var id = await redis.StringGetAsync(Owner(player.ToString("N")));
        if (!Guid.TryParse(id.ToString(), out var search))
            return null;
        try
        {
            return await Get(player, search, false);
        }
        catch (AppException)
        {
            return null;
        }
    }
    public async Task PublishRoom(QueueEntry entry)
    {
        if (entry.state != MatchmakingSearchStatus.Matched || entry.host != entry.playerId)
            throw Conflict("Chỉ người tạo phòng đã được ghép mới được báo phòng sẵn sàng.");
        await redis.StringSetAsync(Prefix + "room:" + entry.matchId, entry.session, TimeSpan.FromSeconds(settings.SearchRetentionSeconds));
    }
    public async Task<bool> RoomReady(QueueEntry entry) => entry.matchId.Length > 0 && await redis.StringGetAsync(Prefix + "room:" + entry.matchId) == entry.session;
    public async Task<bool> ReadyCancelled(string match) => await redis.HashGetAsync(Ready(match), "cancelled") == "1";
    public Task<bool> ReadyExists(string match) => redis.KeyExistsAsync(Ready(match));
    public async Task<HashSet<string>> ReadyAccepted(string match) => (await redis.HashKeysAsync(Ready(match)))
        .Select(key => key.ToString()).Where(key => key.StartsWith("accepted:", StringComparison.Ordinal))
        .Select(key => key[9..]).ToHashSet(StringComparer.Ordinal);
    public async Task<bool> OwnsBot(string bot, string search) => await redis.StringGetAsync(Prefix + "bot:" + bot) == search;
    public async Task<bool> BotBusy(string bot) => await redis.KeyExistsAsync(Prefix + "bot:" + bot);
    public async Task ReleaseMatch(MatchRoster roster)
    {
        foreach (var member in roster.members.Where(m => !m.bot))
        {
            var search = await redis.StringGetAsync(Owner(member.id));
            if (!Guid.TryParse(search.ToString(), out var id))
                continue;
            try
            {
                var entry = await Get(Guid.Parse(member.id), id, false);
                if (entry.matchId == roster.matchId)
                    await Complete(entry);
            }
            catch (AppException e) when (e.StatusCode == HttpStatusCode.Conflict) { }
        }
        await CancelReady(roster.matchId);
    }
    public async Task<ReadyState> Consent(string match, string fingerprint, string[] players, string[] bots, string? accept, long now, int seconds)
    {
        var value = await redis.ScriptEvaluateAsync("""
            local exists=redis.call('EXISTS',KEYS[1])==1
            if not exists then
              redis.call('HSET',KEYS[1],'fingerprint',ARGV[1],'deadline',ARGV[2]+ARGV[3]*1000,'cancelled','0')
              redis.call('EXPIRE',KEYS[1],ARGV[4])
            end
            if redis.call('HGET',KEYS[1],'fingerprint')~=ARGV[1] or redis.call('HGET',KEYS[1],'cancelled')=='1' then return false end
            if tonumber(redis.call('HGET',KEYS[1],'deadline'))<=tonumber(ARGV[2]) and ARGV[5]~=''
              and redis.call('HEXISTS',KEYS[1],'accepted:'..ARGV[5])==0 then return false end
            if ARGV[5]~='' then redis.call('HSET',KEYS[1],'accepted:'..ARGV[5],'1') end
            for _,p in ipairs(cjson.decode(ARGV[6])) do redis.call('HSET',KEYS[1],'accepted:'..p,'1') end
            return redis.call('HGETALL',KEYS[1])
            """, [Ready(match)], [fingerprint, now, seconds, settings.SearchRetentionSeconds, accept ?? "", GameJson.Write(bots)]);
        if (value.IsNull)
            throw Conflict("Lượt xác nhận đã hủy, hết hạn hoặc không khớp phòng.");
        var cells = (RedisResult[])value!;
        var accepted = new HashSet<string>();
        long deadline = 0;
        for (int i = 0; i < cells.Length; i += 2)
        {
            var key = (string)cells[i]!;
            if (key == "deadline")
                deadline = long.Parse((string)cells[i + 1]!);
            else if (key.StartsWith("accepted:"))
                accepted.Add(key[9..]);
        }
        return new ReadyState(players.All(accepted.Contains), deadline, accepted);
    }
    public async Task CancelReady(string match)
    {
        await redis.ScriptEvaluateAsync("""
            redis.call('HSET',KEYS[1],'cancelled','1')
            redis.call('EXPIRE',KEYS[1],ARGV[1])
            """, [Ready(match)], [settings.SearchRetentionSeconds]);
    }
    public async Task<List<(string id,string data)>> ReadJournal() => (await redis.StreamRangeAsync(Journal,"-","+",100))
        .Select(x=>(x.Id.ToString(),x.Values.Single(v=>v.Name=="data").Value.ToString())).ToList();
    public Task AcknowledgeJournal(string id) => redis.StreamDeleteAsync(Journal,[id]);
    public async Task<QueueEntry?> SearchSnapshot(Guid id)
    { var raw=await redis.StringGetAsync(Search(id.ToString("N")));return raw.IsNullOrEmpty?null:GameJson.Read<QueueEntry>(raw.ToString()); }
    public sealed record ReadyState(bool AllAccepted, long Deadline, HashSet<string> Accepted);
}
