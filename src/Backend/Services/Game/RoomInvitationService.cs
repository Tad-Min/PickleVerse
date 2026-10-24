using System.Net;
using System.Text.RegularExpressions;
using PickleVerse.Multiplayer;
using Services.Configuration;
using Services.Exceptions;
using StackExchange.Redis;
using static Services.Game.GameSql;

namespace Services.Game;

public sealed class RoomInvitationService(GameSql sql,IConnectionMultiplexer connection,RedisSettings settings,TimeProvider clock)
{
    public const int PresenceSeconds=20, InvitationSeconds=60;
    IDatabase Redis=>connection.GetDatabase();
    long Now=>clock.GetUtcNow().ToUnixTimeSeconds();
    RedisKey Key(string value)=>settings.KeyPrefix+"{lobby}:"+value;
    static AppException Bad(string message,HttpStatusCode status=HttpStatusCode.Conflict)=>new(message,status);
    public sealed class Presence
    {
        public string PlayerId="";
        public LobbyPresenceRequest Value=new();
        public long ExpiresAt;
    }
    public sealed class StoredInvitation
    {
        public RoomInvitation Value=new();
        public string SenderSession="", OwnerSession="", OwnerId="", ReceiverId="";
    }
    async Task<T?> Read<T>(string key) where T:class
    {var value=await Redis.StringGetAsync(Key(key));return value.IsNullOrEmpty?null:GameJson.Read<T>(value.ToString());}
    async Task<Presence?> Online(string player)
    {var p=await Read<Presence>("presence:"+player);return p!=null&&p.ExpiresAt>Now?p:null;}
    async Task<bool> Friends(Guid a,Guid b)=>await sql.One("""
        SELECT EXISTS(SELECT 1 FROM friendships WHERE userid1=LEAST(@a,@b) AND userid2=GREATEST(@a,@b))
          AND NOT EXISTS(SELECT 1 FROM blockedusers WHERE (userid=@a AND blockeduserid=@b) OR (userid=@b AND blockeduserid=@a))
          AND EXISTS(SELECT 1 FROM users WHERE userid=@b AND status='ACTIVE')
        """,r=>r.GetBoolean(0),P("a",a),P("b",b));
    async Task<bool> InMatch(Guid player)=>await sql.One("""
        SELECT EXISTS(SELECT 1 FROM gamematches m WHERE m.status='STARTED'
          AND (m.ownerid=@player OR EXISTS(SELECT 1 FROM gamematchplayers p WHERE p.matchid=m.matchid AND p.playerid=@player)))
        """,r=>r.GetBoolean(0),P("player",player));
    static int Capacity(Presence p)=>p.Value.Custom?2*p.Value.Mode:2;
    async Task<Presence?> Room(string code)
    {
        var room=await Read<Presence>("room:"+code);
        if(room==null||room.ExpiresAt<=Now)return null;
        var owner=await Online(room.PlayerId);
        return owner!=null&&owner.Value.State=="lobby"&&owner.Value.IsHost&&owner.Value.RoomCode==code
            &&owner.Value.SessionId==room.Value.SessionId?room:null;
    }
    public async Task<LobbyPresenceReply> Publish(Guid self,LobbyPresenceRequest input)
    {
        if(input.RoomCode==null||input.Region==null||!Guid.TryParse(input.SessionId,out _)||input.State is not("lobby" or "busy")||input.Mode is not(1 or 2)
            ||input.RoomCode.Length>64||input.Region.Length>30||input.Members==null||input.Members.Length>4
            ||input.Members.Any(x=>!Guid.TryParse(x,out _)))throw Bad("Trạng thái lobby không hợp lệ.",HttpStatusCode.BadRequest);
        input.RoomCode=input.RoomCode.Trim().ToUpperInvariant();input.Region=input.Region.Trim().ToLowerInvariant();
        if(input.RoomCode.Length>0&&(!Regex.IsMatch(input.RoomCode,"^[A-Z0-9-]{6,64}$")||!Regex.IsMatch(input.Region,"^[a-z0-9-]{1,30}$")))
            throw Bad("Thông tin phòng không hợp lệ.",HttpStatusCode.BadRequest);
        input.Members=input.Members.Select(x=>Guid.Parse(x).ToString("N")).Distinct().ToArray();
        if(input.RoomCode.Length>0&&(!input.Members.Contains(self.ToString("N"))||input.Members.Length>(input.Custom?2*input.Mode:2)))
            throw Bad("Đội hình phòng không hợp lệ.",HttpStatusCode.BadRequest);
        if(await InMatch(self))input.State="busy";
        var presence=new Presence{PlayerId=self.ToString("N"),Value=input,ExpiresAt=Now+PresenceSeconds};
        await Redis.StringSetAsync(Key("presence:"+presence.PlayerId),GameJson.Write(presence),TimeSpan.FromSeconds(PresenceSeconds));
        if(input.IsHost&&input.RoomCode.Length>0)
        {
            var registered=await Redis.ScriptEvaluateAsync("""
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
                """,[Key("room:"+input.RoomCode)],[settings.KeyPrefix+"{lobby}:presence:",presence.PlayerId,GameJson.Write(presence),PresenceSeconds,Now]);
            if((int)registered!=1)throw Bad("Mã phòng đang được một chủ phòng khác sử dụng.",HttpStatusCode.Forbidden);
        }
        return new(){Invitations=input.State=="lobby"?await Inbox(self):[]};
    }
    async Task<RoomInvitation[]> Inbox(Guid self)
    {
        var key=Key("inbox:"+self.ToString("N"));
        await Redis.SortedSetRemoveRangeByScoreAsync(key,double.NegativeInfinity,Now);
        var ids=await Redis.SortedSetRangeByRankAsync(key,0,19);
        var result=new List<RoomInvitation>();
        foreach(var id in ids)
        {
            var stored=await Read<StoredInvitation>("invite:"+id);
            if(stored==null||stored.Value.State!="pending"||stored.Value.ExpiresAt<=Now)continue;
            if(await ValidRoom(stored)!=null&&await Friends(Guid.Parse(stored.Value.SenderId),self))result.Add(stored.Value);
        }
        return result.ToArray();
    }
    async Task<Presence?> ValidRoom(StoredInvitation invite)
    {
        var sender=await Online(invite.Value.SenderId);var room=await Room(invite.Value.RoomCode);
        return sender!=null&&sender.Value.State=="lobby"&&sender.Value.SessionId==invite.SenderSession
            &&sender.Value.RoomCode==invite.Value.RoomCode&&room!=null&&room.PlayerId==invite.OwnerId
            &&room.Value.SessionId==invite.OwnerSession&&room.Value.Members.Contains(invite.Value.SenderId)
            &&room.Value.Members.Length<Capacity(room)?room:null;
    }
    public async Task<InvitableFriendsPage> List(Guid self,string? query,int offset)
    {
        query=(query??"").Trim();if(query.Length>64||offset<0||offset>100000)throw Bad("Bộ lọc không hợp lệ.",HttpStatusCode.BadRequest);
        var friends=await sql.Query("""
            SELECT p.userid,p.playername,p.elo FROM friendships f
            JOIN playerprofiles p ON p.userid=CASE WHEN f.userid1=@self THEN f.userid2 ELSE f.userid1 END
            JOIN users u ON u.userid=p.userid
            WHERE (f.userid1=@self OR f.userid2=@self) AND u.status='ACTIVE'
              AND NOT EXISTS(SELECT 1 FROM blockedusers b WHERE (b.userid=@self AND b.blockeduserid=p.userid) OR (b.userid=p.userid AND b.blockeduserid=@self))
              AND (strpos(lower(p.playername),lower(@q))>0 OR strpos(replace(p.userid::text,'-',''),replace(lower(@q),'-',''))>0)
            ORDER BY lower(p.playername),p.userid LIMIT 51 OFFSET @offset
            """,r=>new InvitableFriend{Id=r.GetGuid(0).ToString("N"),Name=r.GetString(1),Elo=r.GetInt32(2)},P("self",self),P("q",query),P("offset",offset));
        var sender=await Online(self.ToString("N"));var room=sender==null?null:await Room(sender.Value.RoomCode);
        foreach(var friend in friends.Take(50))
        {
            var presence=await Online(friend.Id);var pendingId=await Redis.StringGetAsync(Key("pair:"+self.ToString("N")+":"+friend.Id));
            var pending=pendingId.IsNullOrEmpty?null:await Read<StoredInvitation>("invite:"+pendingId);
            friend.Pending=pending?.Value.State=="pending"&&pending.Value.ExpiresAt>Now;
            bool inRoom=room?.Value.Members.Contains(friend.Id)==true;
            friend.Status=presence==null?"offline":inRoom?"in_room":presence.Value.State=="lobby"?"lobby":"busy";
            friend.CanInvite=sender?.Value.State=="lobby"&&room!=null&&room.Value.Members.Contains(self.ToString("N"))
                &&room.Value.Members.Length<Capacity(room)&&presence?.Value.State=="lobby"&&!inRoom&&!friend.Pending;
        }
        return new(){Players=friends.Take(50).ToArray(),HasMore=friends.Count>50,Offset=offset};
    }
    public async Task<RoomInvitation> Send(Guid self,SendRoomInvitationRequest input)
    {
        if(!Guid.TryParse(input.PlayerId,out var target)||target==self)throw Bad("Chọn một người bạn khác.",HttpStatusCode.BadRequest);
        if(!await Friends(self,target))throw Bad("Chỉ có thể mời người trong danh sách bạn bè.",HttpStatusCode.Forbidden);
        var sender=await Online(self.ToString("N"));var receiver=await Online(target.ToString("N"));
        var room=sender==null?null:await Room(sender.Value.RoomCode);
        if(sender?.Value.State!="lobby"||receiver?.Value.State!="lobby"||await InMatch(target))throw Bad("Người chơi hiện không ở lobby hoặc đang bận.");
        if(room==null||!room.Value.Members.Contains(self.ToString("N")))throw Bad("Phòng đã đóng hoặc chưa sẵn sàng.");
        if(room.Value.Members.Length>=Capacity(room)||room.Value.Members.Contains(target.ToString("N")))throw Bad("Phòng đã đầy hoặc người chơi đã ở trong phòng.");
        string name=await sql.One("SELECT playername FROM playerprofiles WHERE userid=@self",r=>r.GetString(0),P("self",self))??"Người chơi";
        var invitation=new RoomInvitation{Id=Guid.NewGuid().ToString("N"),SenderId=self.ToString("N"),SenderName=name,
            RoomCode=room.Value.RoomCode,Region=room.Value.Region,Mode=room.Value.Mode,Custom=room.Value.Custom,ExpiresAt=Now+InvitationSeconds};
        var stored=new StoredInvitation{Value=invitation,SenderSession=sender.Value.SessionId,OwnerId=room.PlayerId,OwnerSession=room.Value.SessionId,ReceiverId=target.ToString("N")};
        var result=await Redis.ScriptEvaluateAsync("""
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
            """,[Key("pair:"+self.ToString("N")+":"+target.ToString("N")),Key("invite:"+invitation.Id),Key("inbox:"+target.ToString("N")),Key("cooldown:"+self.ToString("N")+":"+target.ToString("N"))],
            [settings.KeyPrefix+"{lobby}:invite:",GameJson.Write(stored),invitation.Id,Now,InvitationSeconds,invitation.ExpiresAt]);
        string id=result.ToString();
        if(id=="cooldown")throw Bad("Vui lòng chờ vài giây trước khi mời lại.",HttpStatusCode.TooManyRequests);
        if(id=="full")throw Bad("Người chơi đang có quá nhiều lời mời. Hãy thử lại sau.");
        return (await Read<StoredInvitation>("invite:"+id))!.Value;
    }
    public async Task<RoomInvitation> Respond(Guid self,string id,bool accept)
    {
        if(!Guid.TryParseExact(id,"N",out _))throw Bad("Lời mời không hợp lệ.",HttpStatusCode.BadRequest);
        var stored=await Read<StoredInvitation>("invite:"+id)??throw Bad("Lời mời đã hết hạn.",HttpStatusCode.Gone);
        var inbox=Key("inbox:"+self.ToString("N"));
        if(stored.ReceiverId!=self.ToString("N"))throw Bad("Lời mời không thuộc tài khoản này.",HttpStatusCode.Forbidden);
        if(stored.Value.ExpiresAt<=Now)throw Bad("Lời mời đã hết hạn.",HttpStatusCode.Gone);
        if(stored.Value.State==(accept?"accepted":"declined"))return stored.Value;
        if(stored.Value.State!="pending")throw Bad("Lời mời đã được xử lý.",HttpStatusCode.Gone);
        if(accept)
        {
            var presence=await Online(self.ToString("N"));
            if(presence?.Value.State!="lobby"||await InMatch(self))throw Bad("Chỉ có thể nhận lời mời khi đang ở lobby.");
            if(!await Friends(Guid.Parse(stored.Value.SenderId),self)||await ValidRoom(stored)==null)throw Bad("Phòng đã đóng, đã đầy hoặc lời mời không còn hiệu lực.");
        }
        string state=accept?"accepted":"declined";
        var changed=await Redis.ScriptEvaluateAsync("""
            local raw=redis.call('GET',KEYS[1]);if not raw then return false end
            local data=cjson.decode(raw)
            if data.value.expiresAt<=tonumber(ARGV[2]) then return false end
            if data.value.state~= 'pending' and data.value.state~=ARGV[1] then return false end
            data.value.state=ARGV[1]
            redis.call('SET',KEYS[1],cjson.encode(data),'KEEPTTL');redis.call('ZREM',KEYS[2],ARGV[3]);return cjson.encode(data)
            """,[Key("invite:"+id),inbox],[state,Now,id]);
        if(changed.IsNull)throw Bad("Lời mời đã được xử lý hoặc đã hết hạn.",HttpStatusCode.Gone);
        return GameJson.Read<StoredInvitation>(changed.ToString()).Value;
    }
}
