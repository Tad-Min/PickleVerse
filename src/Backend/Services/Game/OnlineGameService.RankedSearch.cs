using PickleVerse;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using static Services.Game.GameSql;

namespace Services.Game;

public sealed partial class OnlineGameService
{
    long Milliseconds=>new DateTimeOffset(Now).ToUnixTimeMilliseconds();
    public async Task<RankedSearchTicket> BeginRankedSearch(Guid id,RankedSearchRequest request)
    {
        if(request.SearchId==Guid.Empty||request.Mode is not(PlayMode.Singles or PlayMode.Doubles)||request.Protocol<0
            ||string.IsNullOrWhiteSpace(request.Region)||request.Region.Length>30||request.Group.Length>100)throw Bad("Lượt tìm trận không hợp lệ.");
        var party=(request.Members.Length==0?[id]:request.Members).Distinct().Order().ToArray();
        if(!party.Contains(id)||party.Length>(int)request.Mode||party.Any(p=>RankedBotAccounts.Contains(p.ToString("N"))))throw Bad("Nhóm tìm trận không hợp lệ.");
        await using var tx=await db.Database.BeginTransactionAsync();var s=await Load(id,await catalogs.Load());
        if(s.Player.pendingMatch!=null)throw Conflict("Bạn vẫn còn trận chưa hoàn tất.");
        var entry=new QueueEntry{searchId=request.SearchId.ToString("N"),playerId=id.ToString("N"),mode=(int)request.Mode,
            group=party.Length==1?id.ToString("N"):request.Group,party=party.Select(p=>p.ToString("N")).ToArray(),
            region=request.Region.Trim().ToLowerInvariant(),protocol=request.Protocol,elo=s.Player.elo,losses=s.Player.rankLossStreak,started=Milliseconds,lastSeen=Milliseconds,
            member=new(){id=id.ToString("N"),name=s.Player.playerName,gender=s.profile.Ismale?"Male":"Female",elo=s.Player.elo,paddleId=s.inventory.equippedPaddle}};
        if(string.IsNullOrWhiteSpace(entry.group))throw Bad("Thiếu mã nhóm tìm trận.");
        var previous=await matchmaking.Active(id);
        if(previous!=null&&previous.searchId!=entry.searchId)
        {
            bool expired=previous.state=="SEARCHING"?previous.lastSeen<Milliseconds-matchmaking.QueueLeaseSeconds*1000L:
                previous.assigned<Milliseconds-(settings.Matchmaking.ReadyConfirmationSeconds+35)*1000L;
            bool ended=previous.matchId.Length>0&&await sql.One("SELECT status IN ('FINISHED','CANCELLED') FROM gamematches WHERE matchid=@match",r=>r.GetBoolean(0),P("match",Guid.Parse(previous.matchId)));
            if(expired||ended)await matchmaking.Cancel(id,Guid.Parse(previous.searchId));
        }
        await matchmaking.Begin(entry);await tx.CommitAsync();
        return await PollRankedSearch(id,new(){SearchId=request.SearchId});
    }
    async Task<RankedSearchTicket> QueueTicket(QueueEntry e)=>new(){roomReady=await matchmaking.RoomReady(e),remainingSeconds=Math.Max(0,settings.Matchmaking.BotFallbackSeconds-(Milliseconds-e.started)/1000d),
        state=e.state,matchId=e.matchId,session=e.session,hostPlayerId=e.host,region=e.region,roster=e.roster};
    public async Task<RankedSearchTicket> PollRankedSearch(Guid id,RankedSearchRequest request)
    {
        var entry=await matchmaking.Get(id,request.SearchId,false);
        for(int retry=0;retry<8;retry++)
        {
            var before=GameJson.Write(entry);entry.lastSeen=Milliseconds;
            if(await matchmaking.Compare([before],[entry]))break;
            entry=await matchmaking.Get(id,request.SearchId,false);
        }
        if(entry.state!="SEARCHING")
        {
            if(await matchmaking.ReadyCancelled(entry.matchId)||entry.assigned<Milliseconds-(settings.Matchmaking.ReadyConfirmationSeconds+35)*1000L)
                throw Conflict("Lượt ghép trận đã bị hủy hoặc hết hạn.");
            return await QueueTicket(entry);
        }
        var candidates=(await matchmaking.Candidates(entry,Milliseconds-matchmaking.QueueLeaseSeconds*1000L)).Where(x=>x.entry.state=="SEARCHING"&&x.entry.mode==entry.mode&&x.entry.region==entry.region
            &&x.entry.protocol==entry.protocol&&x.entry.lastSeen>=Milliseconds-matchmaking.QueueLeaseSeconds*1000L).ToList();
        var groups=candidates.GroupBy(x=>x.entry.group).Select(g=>g.OrderBy(x=>x.entry.playerId,StringComparer.Ordinal).ToList())
            .Where(g=>g.Count==g[0].entry.party.Length&&g.Select(x=>x.entry.playerId).SequenceEqual(g[0].entry.party)
                &&g.All(x=>x.entry.party.SequenceEqual(g[0].entry.party))).OrderBy(g=>g.Min(x=>x.entry.started)).ToList();
        var own=groups.FirstOrDefault(g=>g.Any(x=>x.entry.playerId==entry.playerId));
        if(own==null)return await QueueTicket(entry);
        var selected=new List<List<(string raw,QueueEntry entry)>>{own};int cap=entry.mode;
        bool Fill(int at,int a,int b)
        {
            if(a==cap&&b==cap)return true;
            for(int i=at;i<groups.Count;i++)
            {
                var g=groups[i];if(g==own)continue;
                if(cap==1)
                {
                    var other=g[0].entry;
                    if(Milliseconds-entry.started>=settings.Matchmaking.BotFallbackSeconds*1000L||Milliseconds-other.started>=settings.Matchmaking.BotFallbackSeconds*1000L)continue;
                    if(!EloPolicy.PairingKeys(entry.elo,entry.losses,Milliseconds-entry.started>=30000)
                        .Intersect(EloPolicy.PairingKeys(other.elo,other.losses,Milliseconds-other.started>=30000)).Any())continue;
                }
                selected.Add(g);
                if(a+g.Count<=cap&&Fill(i+1,a+g.Count,b))return true;
                if(b+g.Count<=cap&&Fill(i+1,a,b+g.Count))return true;
                selected.RemoveAt(selected.Count-1);
            }
            return false;
        }
        if(!Fill(0,own.Count,0))return await QueueTicket(entry);
        var teams=new Dictionary<string,int>();
        bool Teams(int at,int a,int b)
        {
            if(at==selected.Count)return a==cap&&b==cap;
            var g=selected[at];
            foreach(int team in new[]{0,1})
            {
                if((team==0?a:b)+g.Count>cap)continue;
                teams[g[0].entry.group]=team;
                if(Teams(at+1,a+(team==0?g.Count:0),b+(team==1?g.Count:0)))return true;
            }
            return false;
        }
        if(!Teams(0,0,0))return await QueueTicket(entry);
        var seats=selected.SelectMany(g=>g).ToList();var host=seats.OrderBy(x=>x.entry.started).ThenBy(x=>x.entry.playerId,StringComparer.Ordinal).First().entry.playerId;
        var match=Guid.NewGuid().ToString("N");var roster=new MatchRoster{matchId=match,mode=(PlayMode)cap,region=entry.region,
            venue=(CourtVenue)System.Security.Cryptography.RandomNumberGenerator.GetInt32(2),members=seats.Select(x=>
            {var m=GameJson.Read<RoomMember>(GameJson.Write(x.entry.member));m.team=teams[x.entry.group];m.group=x.entry.group;return m;}).ToList()};
        foreach(var x in seats){x.entry.state="MATCHED";x.entry.matchId=match;x.entry.session="PKV-"+match;x.entry.host=host;x.entry.roster=roster;x.entry.assigned=Milliseconds;}
        await matchmaking.Compare(seats.Select(x=>x.raw).ToArray(),seats.Select(x=>x.entry).ToArray());
        return await QueueTicket(await matchmaking.Get(id,request.SearchId,false));
    }
    public async Task<RankedSearchTicket> PublishRankedRoom(Guid id,RankedSearchRequest request)
    {
        var entry=await matchmaking.Get(id,request.SearchId,false);await matchmaking.PublishRoom(entry);return await QueueTicket(entry);
    }
    public async Task<RankedSearchTicket> CancelRankedSearch(Guid id,RankedSearchRequest request)
    {
        QueueEntry entry;
        try{entry=await matchmaking.Get(id,request.SearchId,false);}catch(Services.Exceptions.AppException e) when(e.StatusCode==System.Net.HttpStatusCode.Conflict){return new(){state="CANCELLED"};}
        await matchmaking.Cancel(id,request.SearchId);
        if(entry.matchId.Length>0)
            await sql.Run("UPDATE gamematches SET status='CANCELLED',data=jsonb_set(data,'{phase}','\"CANCELLED\"'),updatedat=@now WHERE matchid=@match AND data->>'phase'='PREPARING'",P("match",Guid.Parse(entry.matchId)),P("now",Now));
        return new(){state="CANCELLED"};
    }
    public async Task<RoomMember> AssignRankedBot(Guid id,RankedBotRequest request)
    {
        if(request.SearchId==Guid.Empty||request.MatchId==Guid.Empty||string.IsNullOrWhiteSpace(request.Session)||request.Session.Length>100)throw Bad("Phòng dự phòng không hợp lệ.");
        await using var tx=await db.Database.BeginTransactionAsync();var s=await Load(id,await catalogs.Load());
        if(s.Player.pendingMatch!=null)throw Conflict("Bạn vẫn còn trận chưa hoàn tất.");
        var search=await matchmaking.Get(id,request.SearchId,false);
        if(search.mode!=1||Milliseconds<search.started+settings.Matchmaking.BotFallbackSeconds*1000L)throw Conflict($"Chưa đủ {settings.Matchmaking.BotFallbackSeconds} giây tìm đối thủ thật.");
        if(search.state=="BOT")
        {
            if(search.matchId!=request.MatchId.ToString("N")||search.session!=request.Session)throw Conflict("Bot đã được cấp cho phòng khác.");
            return search.roster!.members.Single(m=>m.bot);
        }
        if(search.state!="SEARCHING")throw Conflict("Lượt tìm trận đã có đối thủ thật.");
        var available=await sql.Query("""
            SELECT p.userid FROM playerprofiles p WHERE p.userid=ANY(@bots)
            AND NOT EXISTS(SELECT 1 FROM gamematchplayers gp JOIN gamematches m USING(matchid) WHERE gp.playerid=p.userid AND
              (m.data->>'phase'='PLAYING' OR m.data->>'phase'='PREPARING' AND m.createdat>@cutoff)) ORDER BY random()
            """,r=>r.GetGuid(0),P("bots",RankedBotAccounts.Ids.Select(Guid.Parse).ToArray()),P("cutoff",Now.AddSeconds(-settings.Matchmaking.ReadyConfirmationSeconds)));
        foreach(var bot in available)
        {
            if(await matchmaking.BotBusy(bot.ToString("N")))continue;
            var member=await sql.One("SELECT playername,ismale FROM playerprofiles WHERE userid=@bot",r=>new RoomMember{id=bot.ToString("N"),name=r.GetString(0),gender=r.GetBoolean(1)?"Male":"Female",elo=Math.Max(EloPolicy.Minimum,EloPolicy.BotElo(s.Player.elo,s.Player.rankLossStreak)),bot=true,team=1,group=bot.ToString("N")},P("bot",bot));
            var before=GameJson.Write(search);search.state="BOT";search.matchId=request.MatchId.ToString("N");search.session=request.Session;
            search.host=id.ToString("N");search.assigned=Milliseconds;search.botElo=member!.elo;
            var own=GameJson.Read<RoomMember>(GameJson.Write(search.member));own.team=0;
            search.roster=new(){matchId=search.matchId,mode=PlayMode.Singles,region=search.region,members=[own,member]};
            if(await matchmaking.Compare([before],[search],bot.ToString("N"),settings.Matchmaking.ReadyConfirmationSeconds+35)){await tx.CommitAsync();return member;}
            search=await matchmaking.Get(id,request.SearchId,false);
            if(search.state!="SEARCHING")throw Conflict("Lượt tìm trận vừa được xử lý. Vui lòng thử lại.");
        }
        throw Conflict("Các đối thủ dự phòng đang bận. Đang tiếp tục tìm trận.");
    }
}
