using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using static Services.Game.GameSql;

namespace Services.Game;

public sealed partial class OnlineGameService
{
    public async Task<RankedSearchTicket> BeginRankedSearch(Guid id,RankedSearchRequest request)
    {
        if(request.SearchId==Guid.Empty)throw Bad("Mã tìm trận không hợp lệ.");
        await using var tx=await db.Database.BeginTransactionAsync();
        var s=await Load(id,await catalogs.Load());
        if(s.Player.pendingMatch!=null)throw Conflict("Bạn vẫn còn trận chưa hoàn tất.");
        var existing=await sql.One("SELECT playerid,startedat,cancelled FROM rankedsearches WHERE searchid=@search",r=>new{id=r.GetGuid(0),start=r.GetDateTime(1),cancelled=r.GetBoolean(2)},P("search",request.SearchId));
        if(existing!=null&&(existing.id!=id||existing.cancelled))throw Conflict("Lượt tìm trận không còn hiệu lực.");
        if(existing==null)
        {
            await sql.Run("UPDATE rankedsearches SET cancelled=true WHERE playerid=@id",P("id",id));
            await sql.Run("INSERT INTO rankedsearches(searchid,playerid,startedat) VALUES(@search,@id,@now)",P("search",request.SearchId),P("id",id),P("now",Now));
        }
        await tx.CommitAsync();return new RankedSearchTicket{remainingSeconds=Math.Max(0,360-(Now-(existing?.start??Now)).TotalSeconds)};
    }
    public async Task<RankedSearchTicket> CancelRankedSearch(Guid id,RankedSearchRequest request)
    {
        await using var tx=await db.Database.BeginTransactionAsync();
        await sql.One("SELECT userid FROM playerprofiles WHERE userid=@id FOR UPDATE",r=>r.GetGuid(0),P("id",id));
        await sql.Run("UPDATE rankedsearches SET cancelled=true WHERE searchid=@search AND playerid=@id",P("search",request.SearchId),P("id",id));
        await sql.Run("UPDATE gamematches SET status='CANCELLED',data=jsonb_set(data,'{phase}','\"CANCELLED\"'),updatedat=@now WHERE data->>'phase'='PREPARING' AND matchid IN (SELECT matchid FROM rankedsearches WHERE searchid=@search AND playerid=@id)",P("search",request.SearchId),P("id",id),P("now",Now));
        await tx.CommitAsync();return new RankedSearchTicket();
    }
    public async Task<RoomMember> AssignRankedBot(Guid id,RankedBotRequest request)
    {
        if(request.SearchId==Guid.Empty||request.MatchId==Guid.Empty||string.IsNullOrWhiteSpace(request.Session)||request.Session.Length>100)throw Bad("Phòng dự phòng không hợp lệ.");
        await using var tx=await db.Database.BeginTransactionAsync();
        var s=await Load(id,await catalogs.Load());
        if(s.Player.pendingMatch!=null)throw Conflict("Bạn vẫn còn trận chưa hoàn tất.");
        await sql.Run("SELECT pg_advisory_xact_lock(hashtextextended('ranked-bot-pool',0))");
        var search=await sql.One("SELECT startedat,cancelled,botid,matchid,photonsession,botelo FROM rankedsearches WHERE searchid=@search AND playerid=@id FOR UPDATE",
            r=>new{start=r.GetDateTime(0),cancelled=r.GetBoolean(1),bot=r.IsDBNull(2)?(Guid?)null:r.GetGuid(2),match=r.IsDBNull(3)?(Guid?)null:r.GetGuid(3),session=r.IsDBNull(4)?null:r.GetString(4),elo=r.IsDBNull(5)?0:r.GetInt32(5)},P("search",request.SearchId),P("id",id));
        if(search==null||search.cancelled)throw Conflict("Lượt tìm trận đã bị hủy.");
        if(Now<search.start.AddSeconds(360))throw Conflict("Chưa đủ 6 phút tìm đối thủ thật.");
        Guid bot;
        int elo=EloPolicy.BotElo(s.Player.elo,s.Player.rankLossStreak);
        if(search.bot.HasValue)
        {
            if(search.match!=request.MatchId||search.session!=request.Session)throw Conflict("Bot đã được cấp cho phòng khác.");
            bot=search.bot.Value;elo=search.elo;
        }
        else
        {
            var available=await sql.Query("""
                SELECT p.userid FROM playerprofiles p WHERE p.userid=ANY(@bots)
                AND NOT EXISTS(SELECT 1 FROM gamematchplayers gp JOIN gamematches m USING(matchid) WHERE gp.playerid=p.userid AND
                  (m.data->>'phase'='PLAYING' OR m.data->>'phase'='PREPARING' AND m.createdat>@cutoff))
                AND NOT EXISTS(SELECT 1 FROM rankedsearches rs WHERE rs.botid=p.userid AND NOT rs.cancelled AND rs.assignedat>@cutoff)
                ORDER BY random() LIMIT 1
                """,r=>r.GetGuid(0),P("bots",RankedBotAccounts.Ids.Select(Guid.Parse).ToArray()),P("cutoff",Now.AddMinutes(-2)));
            if(available.Count==0)throw Conflict("Các đối thủ dự phòng đang bận. Đang tiếp tục tìm trận.");
            bot=available[0];
            await sql.Run("UPDATE rankedsearches SET botid=@bot,botelo=@elo,matchid=@match,photonsession=@session,assignedat=@now WHERE searchid=@search",
                P("bot",bot),P("elo",elo),P("match",request.MatchId),P("session",request.Session),P("now",Now),P("search",request.SearchId));
        }
        var member=await sql.One("SELECT playername,ismale FROM playerprofiles WHERE userid=@bot",r=>new RoomMember{id=bot.ToString("N"),name=r.GetString(0),gender=r.GetBoolean(1)?"Male":"Female",elo=elo,bot=true,team=1,group=bot.ToString("N")},P("bot",bot));
        await tx.CommitAsync();return member!;
    }
}
