using System.Net;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;
using Services.Exceptions;
using static Services.Game.GameSql;

namespace Services.Game;

public sealed class OnlineSocialPlayer
{
    public Guid Id;
    public string Name="", Relationship="none", Avatar="";
    public int Elo, Position, Level, RequestId;
}
public sealed class OnlineSocialPage
{
    public List<OnlineSocialPlayer> Players=[];
    public int Total, MyPosition, PlayerCount, FriendCount, Offset;
    public bool HasMore;
}
public sealed class SocialActionRequest
{
    public Guid PlayerId {get;set;}
    public string Action {get;set;}="";
    public int RequestId {get;set;}
}
public sealed class OnlineSocialService(PickleVerseContext db,GameSql sql)
{
    const string Visible="""
        WITH ranked AS (
          SELECT p.userid,p.playername,p.elo,p.avatar,p.level,
            row_number() OVER(ORDER BY p.elo DESC,p.userid)::int AS position
          FROM playerprofiles p JOIN users u ON u.userid=p.userid
          WHERE u.status='ACTIVE' AND COALESCE((SELECT v.isverified FROM verificationemail v WHERE v.userid=u.userid ORDER BY v.createdat DESC,v.verificationemailid DESC LIMIT 1),false)
        ), relations AS (
          SELECT r.*,COALESCE(fr.requestid,0) AS requestid,CASE
            WHEN r.userid=@self THEN 'self'
            WHEN EXISTS(SELECT 1 FROM friendships f WHERE f.userid1=LEAST(@self,r.userid) AND f.userid2=GREATEST(@self,r.userid)) THEN 'friend'
            WHEN fr.senderid=@self THEN 'outgoing' WHEN fr.receiverid=@self THEN 'incoming' ELSE 'none' END AS relationship
          FROM ranked r LEFT JOIN LATERAL (
            SELECT requestid,senderid,receiverid FROM friendrequests
            WHERE status='PENDING' AND ((senderid=@self AND receiverid=r.userid) OR (receiverid=@self AND senderid=r.userid))
            ORDER BY createdat DESC,requestid DESC LIMIT 1
          ) fr ON TRUE
          WHERE NOT EXISTS(SELECT 1 FROM blockedusers b WHERE (b.userid=@self AND b.blockeduserid=r.userid) OR (b.blockeduserid=@self AND b.userid=r.userid))
        )
        """;
    public async Task<OnlineSocialPage> Read(Guid self,string view,string? query,int offset)
    {
        if(view is not("friends" or "directory" or "leaderboard"))throw new AppException("Danh sách không hợp lệ.",HttpStatusCode.BadRequest);
        query=(query??"").Trim();if(query.Length>64||offset<0||offset>10000000)throw new AppException("Bộ lọc không hợp lệ.",HttpStatusCode.BadRequest);
        // Use one database snapshot for rows, counts and the player's global rank.
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        const string filter=" WHERE (@view<>'friends' OR relationship IN ('friend','incoming','outgoing')) AND (@view='leaderboard' OR userid<>@self) AND (strpos(lower(playername),lower(@q))>0 OR strpos(replace(userid::text,'-',''),replace(lower(@q),'-',''))>0)";
        var rows=await sql.Query(Visible+" SELECT userid,playername,elo,avatar,level,position,relationship,requestid FROM relations"+filter+" ORDER BY position LIMIT 50 OFFSET @offset",
            r=>new OnlineSocialPlayer{Id=r.GetGuid(0),Name=r.GetString(1),Elo=r.GetInt32(2),Avatar=r.IsDBNull(3)?"":r.GetString(3),Level=r.GetInt32(4),Position=r.GetInt32(5),Relationship=r.GetString(6),RequestId=r.GetInt32(7)},
            P("self",self),P("view",view),P("q",query),P("offset",offset));
        var total=await sql.One(Visible+" SELECT count(*)::int FROM relations"+filter,r=>r.GetInt32(0),P("self",self),P("view",view),P("q",query));
        var rank=await sql.One(Visible+" SELECT position FROM ranked WHERE userid=@self",r=>r.GetInt32(0),P("self",self));
        var count=await sql.One(Visible+" SELECT count(*)::int FROM ranked",r=>r.GetInt32(0),P("self",self));
        var friends=await sql.One(Visible+" SELECT count(*)::int FROM relations WHERE relationship='friend'",r=>r.GetInt32(0),P("self",self));
        await tx.CommitAsync();return new OnlineSocialPage{Players=rows,Total=total,MyPosition=rank,PlayerCount=count,FriendCount=friends,Offset=offset,HasMore=offset+rows.Count<total};
    }
    public async Task Act(Guid self,SocialActionRequest input)
    {
        Guid target=input.PlayerId;
        if(target==Guid.Empty||target==self)throw new AppException("Chọn người chơi khác.",HttpStatusCode.BadRequest);
        if(input.Action is not("request" or "accept" or "decline" or "remove" or "block" or "unblock"))throw new AppException("Thao tác không hợp lệ.",HttpStatusCode.BadRequest);
        await using var tx=await db.Database.BeginTransactionAsync();
        var users=await sql.Query("SELECT userid FROM playerprofiles WHERE userid IN (@self,@target) ORDER BY userid FOR UPDATE",r=>r.GetGuid(0),P("self",self),P("target",target));
        if(users.Count!=2)throw new AppException("Không tìm thấy người chơi.",HttpStatusCode.NotFound);
        bool blocked=await sql.One("SELECT EXISTS(SELECT 1 FROM blockedusers WHERE (userid=@self AND blockeduserid=@target) OR (userid=@target AND blockeduserid=@self))",r=>r.GetBoolean(0),P("self",self),P("target",target));
        bool friend=await sql.One("SELECT EXISTS(SELECT 1 FROM friendships WHERE userid1=LEAST(@self,@target) AND userid2=GREATEST(@self,@target))",r=>r.GetBoolean(0),P("self",self),P("target",target));
        if(input.Action is "request" or "accept" or "decline")
        {
            if(blocked)throw new AppException("Không thể kết bạn với người chơi này.",HttpStatusCode.Conflict);
            bool active=await sql.One("SELECT EXISTS(SELECT 1 FROM users u WHERE userid=@target AND status='ACTIVE' AND COALESCE((SELECT v.isverified FROM verificationemail v WHERE v.userid=u.userid ORDER BY v.createdat DESC,v.verificationemailid DESC LIMIT 1),false))",r=>r.GetBoolean(0),P("target",target));
            if(!active)throw new AppException("Người chơi chưa sẵn sàng nhận lời mời.",HttpStatusCode.BadRequest);
        }
        if(input.Action=="request"&&!friend)
        {
            var pending=await sql.One("SELECT requestid FROM friendrequests WHERE senderid=@target AND receiverid=@self AND status='PENDING'",r=>r.GetInt32(0),P("self",self),P("target",target));
            if(pending>0)throw new AppException("Người chơi đã gửi lời mời cho bạn. Hãy chấp nhận hoặc từ chối.",HttpStatusCode.Conflict);
            await sql.Run("INSERT INTO friendrequests(senderid,receiverid,status) VALUES(@self,@target,'PENDING') ON CONFLICT(senderid,receiverid) DO UPDATE SET requestid=nextval(pg_get_serial_sequence('friendrequests','requestid')),status='PENDING',createdat=CURRENT_TIMESTAMP,respondedat=NULL WHERE friendrequests.status<>'PENDING'",P("self",self),P("target",target));
        }
        else if(input.Action is "accept" or "decline")
        {
            string status=input.Action=="accept"?"ACCEPTED":"DECLINED";
            int updated=await sql.Run("UPDATE friendrequests SET status=@status,respondedat=CURRENT_TIMESTAMP WHERE requestid=@request AND senderid=@target AND receiverid=@self AND status='PENDING'",P("status",status),P("request",input.RequestId),P("self",self),P("target",target));
            if(updated==0)
            {
                bool same=await sql.One("SELECT EXISTS(SELECT 1 FROM friendrequests WHERE requestid=@request AND senderid=@target AND receiverid=@self AND status=@status)",r=>r.GetBoolean(0),P("status",status),P("request",input.RequestId),P("self",self),P("target",target));
                if(!same)throw new AppException("Lời mời không thuộc tài khoản này hoặc đã thay đổi.",HttpStatusCode.Conflict);
            }
            if(input.Action=="accept")await sql.Run("INSERT INTO friendships(userid1,userid2) VALUES(LEAST(@self,@target),GREATEST(@self,@target)) ON CONFLICT DO NOTHING",P("self",self),P("target",target));
        }
        else if(input.Action is "remove" or "block")
        {
            await sql.Run("DELETE FROM friendships WHERE userid1=LEAST(@self,@target) AND userid2=GREATEST(@self,@target)",P("self",self),P("target",target));
            await sql.Run("UPDATE friendrequests SET status='DECLINED',respondedat=CURRENT_TIMESTAMP WHERE (senderid=@self AND receiverid=@target) OR (senderid=@target AND receiverid=@self)",P("self",self),P("target",target));
            if(input.Action=="block")await sql.Run("INSERT INTO blockedusers(userid,blockeduserid) VALUES(@self,@target) ON CONFLICT DO NOTHING",P("self",self),P("target",target));
        }
        else if(input.Action=="unblock")await sql.Run("DELETE FROM blockedusers WHERE userid=@self AND blockeduserid=@target",P("self",self),P("target",target));
        await tx.CommitAsync();
    }
}
