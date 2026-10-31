using System.Net;
using Entities.Exceptions;
using StackExchange.Redis;

namespace Repositories.Caching;

/// <summary>Temporary session counters and coverage; PostgreSQL keeps only the player's total.</summary>
public sealed class RedisAppActivity(IConnectionMultiplexer connection, RedisSettings settings)
{
    public const long MaxElapsedSeconds = 7 * 24 * 60 * 60;
    public const int RetentionSeconds = 8 * 24 * 60 * 60;
    readonly IDatabase redis = connection.GetDatabase();
    string Prefix => settings.KeyPrefix + "activity:";
    RedisKey SessionKey(Guid id) => Prefix + "session:" + id.ToString("N");
    RedisKey PlayerKey(Guid id) => Prefix + "player:" + id.ToString("N");

    public sealed class Opening
    {
        public long started;
        public string player = "", generation = "";
    }
    public sealed class Counter
    {
        public long started, elapsed;
        public bool closed;
    }
    public sealed class Interval
    {
        public long start, end;
    }
    public sealed class PlayerState
    {
        public string generation = Guid.NewGuid().ToString("N");
        // The projected counters below correspond to targetTotal. This journal is written
        // before SQL commits, so the next locked request can recover either commit outcome.
        public long expectedTotal, targetTotal;
        public long notBefore;
        public Dictionary<string, Counter> sessions = [];
        public List<Interval> coverage = [];
    }
    public static AppException Expired() => new("Phiên hoạt động đã hết hạn. Hãy bắt đầu phiên mới.", HttpStatusCode.Gone);

    public Task Open(Guid session, long started) => redis.StringSetAsync(SessionKey(session),
        GameJson.Write(new Opening { started = started }), TimeSpan.FromSeconds(RetentionSeconds), When.NotExists);

    // Call only while holding the corresponding PostgreSQL profile row lock.
    public async Task<(string raw, PlayerState state)> Load(Guid player, long total, long now)
    {
        var key = PlayerKey(player);
        var raw = await redis.StringGetAsync(key);
        if (raw.IsNullOrEmpty)
        {
            var initial = GameJson.Write(new PlayerState { expectedTotal = total, targetTotal = total, notBefore = total > 0 ? now : 0 });
            if (await redis.StringSetAsync(key, initial, TimeSpan.FromSeconds(RetentionSeconds), When.NotExists))
                return (initial, GameJson.Read<PlayerState>(initial));
            raw = await redis.StringGetAsync(key);
        }
        if (raw.IsNullOrEmpty)
            throw Expired();
        return (raw.ToString(), GameJson.Read<PlayerState>(raw.ToString()));
    }

    public async Task<Opening> Bind(Guid session, Guid player, string generation)
    {
        var key = SessionKey(session);
        for (int attempt = 0; attempt < 8; attempt++)
        {
            var raw = await redis.StringGetAsync(key);
            if (raw.IsNullOrEmpty)
                throw Expired();
            var opening = GameJson.Read<Opening>(raw.ToString());
            if (opening.player.Length > 0)
            {
                if (opening.player != player.ToString("N"))
                    throw new AppException("Phiên hoạt động thuộc tài khoản khác.", HttpStatusCode.Forbidden);
                // Losing the player's Redis state must not replay this session from zero.
                if (opening.generation != generation)
                    throw Expired();
                return opening;
            }
            opening.player = player.ToString("N");
            opening.generation = generation;
            if (await Compare(key, raw.ToString(), GameJson.Write(opening)))
                return opening;
        }
        throw new AppException("Phiên hoạt động đang thay đổi. Vui lòng thử lại.", HttpStatusCode.Conflict);
    }

    public async Task Save(Guid player, string before, PlayerState state)
    {
        if (!await Compare(PlayerKey(player), before, GameJson.Write(state)))
            throw Expired();
    }
    async Task<bool> Compare(RedisKey key, string before, string after) => (int)await redis.ScriptEvaluateAsync("""
        if redis.call('GET',KEYS[1])~=ARGV[1] then return 0 end
        redis.call('SET',KEYS[1],ARGV[2],'EX',ARGV[3])
        return 1
        """, [key], [before, after, RetentionSeconds]) == 1;

    public static long Credit(PlayerState state, long from, long through, long now)
    {
        long cutoff = now - MaxElapsedSeconds;
        state.coverage.RemoveAll(x => x.end < cutoff);
        foreach (var key in state.sessions.Where(x => x.Value.started < cutoff).Select(x => x.Key).ToArray())
            state.sessions.Remove(key);
        if (through <= from)
            return 0;
        long cursor = from, uncovered = 0;
        foreach (var interval in state.coverage)
        {
            if (interval.end <= from || interval.start >= through)
                continue;
            long start = Math.Max(from, interval.start), end = Math.Min(through, interval.end);
            uncovered += Math.Max(0, start - cursor);
            cursor = Math.Max(cursor, end);
        }
        uncovered += Math.Max(0, through - cursor);
        var merged = new List<Interval>();
        foreach (var interval in state.coverage.Append(new Interval { start = from, end = through }).OrderBy(x => x.start))
        {
            if (merged.Count == 0 || interval.start > merged[^1].end)
                merged.Add(new Interval { start = Math.Max(cutoff, interval.start), end = interval.end });
            else
                merged[^1].end = Math.Max(merged[^1].end, interval.end);
        }
        state.coverage = merged;
        return uncovered;
    }
}
