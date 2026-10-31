using StackExchange.Redis;
using Entities.Game;
using Presence = Entities.Game.LobbyPresence;
using StoredInvitation = Entities.Game.StoredRoomInvitation;
namespace Repositories.Caching;

public sealed class LobbyRepository(IConnectionMultiplexer connection, RedisSettings settings)
{
    IDatabase Redis => connection.GetDatabase();
    RedisKey Key(string value) => settings.KeyPrefix + "{lobby}:" + value;
    public async Task<T?> Read<T>(string key) where T : class
    {
        var value = await Redis.StringGetAsync(Key(key));
        return value.IsNullOrEmpty ? null : GameJson.Read<T>(value.ToString());
    }
    public Task StorePresence(Presence presence, int ttl) => Redis.StringSetAsync(Key("presence:" + presence.PlayerId), GameJson.Write(presence), TimeSpan.FromSeconds(ttl));
    public async Task<bool> RegisterRoom(Presence presence, long now, int ttl)
    {
            var registered = await Redis.ScriptEvaluateAsync("""
                local old=redis.call('GET',KEYS[1])
                if old then
                  local room=cjson.decode(old)
                  local owner=redis.call('GET',ARGV[1]..room.playerId)
                  if owner and room.playerId~=ARGV[2] then
                    local p=cjson.decode(owner)
                    if p.expiresAt>tonumber(ARGV[5]) and p.value.isHost and p.value.roomCode==room.value.roomCode and p.value.sessionId==room.value.sessionId then return 0 end
                  end
                end
                redis.call('SET',KEYS[1],ARGV[3],'EX',ARGV[4]);return 1
                """, [Key("room:" + presence.Value.RoomCode)], [settings.KeyPrefix + "{lobby}:presence:", presence.PlayerId, GameJson.Write(presence), ttl, now]);
        return (int)registered == 1;
    }
    public async Task<string[]> InvitationIds(Guid player, long now)
    {
        var key = Key("inbox:" + player.ToString("N"));
        await Redis.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity, now);
        return (await Redis.SortedSetRangeByRankAsync(key, 0, 19)).Select(v => v.ToString()).ToArray();
    }
    public async Task<string?> PendingPair(Guid self, string target)
    {
        var value = await Redis.StringGetAsync(Key("pair:" + self.ToString("N") + ":" + target));
        return value.IsNullOrEmpty ? null : value.ToString();
    }
    public async Task<string> StoreInvitation(Guid self, Guid target, StoredInvitation stored, long now, int ttl)
    {
        var invitation = stored.Value;
        var result = await Redis.ScriptEvaluateAsync("""
            local old=redis.call('GET',KEYS[1])
            if old then
              local data=redis.call('GET',ARGV[1]..old)
              if data then local v=cjson.decode(data).value; if v.state=='pending' and v.expiresAt>tonumber(ARGV[4]) then return old end end
            end
            redis.call('ZREMRANGEBYSCORE',KEYS[3],'-inf',ARGV[4])
            if redis.call('ZCARD',KEYS[3])>=20 then return 'full' end
            if not redis.call('SET',KEYS[4],'1','NX','EX',3) then return 'cooldown' end
            redis.call('SET',KEYS[2],ARGV[2],'EX',ARGV[5])
            redis.call('SET',KEYS[1],ARGV[3],'EX',ARGV[5])
            redis.call('ZADD',KEYS[3],ARGV[6],ARGV[3]);redis.call('EXPIRE',KEYS[3],120)
            return ARGV[3]
            """, [Key("pair:" + self.ToString("N") + ":" + target.ToString("N")), Key("invite:" + invitation.Id), Key("inbox:" + target.ToString("N")), Key("cooldown:" + self.ToString("N") + ":" + target.ToString("N"))],
            [settings.KeyPrefix + "{lobby}:invite:", GameJson.Write(stored), invitation.Id, now, ttl, invitation.ExpiresAt]);
        return result.ToString();
    }
    public async Task<StoredInvitation?> Respond(Guid self, string id, string state, long now)
    {
        var inbox = Key("inbox:" + self.ToString("N"));
        var changed = await Redis.ScriptEvaluateAsync("""
            local raw=redis.call('GET',KEYS[1]);if not raw then return false end
            local data=cjson.decode(raw)
            if data.value.expiresAt<=tonumber(ARGV[2]) then return false end
            if data.value.state~= 'pending' and data.value.state~=ARGV[1] then return false end
            data.value.state=ARGV[1]
            redis.call('SET',KEYS[1],cjson.encode(data),'KEEPTTL');redis.call('ZREM',KEYS[2],ARGV[3]);return cjson.encode(data)
            """, [Key("invite:" + id), inbox], [state, now, id]);
        return changed.IsNull ? null : GameJson.Read<StoredInvitation>(changed.ToString());
    }
}
