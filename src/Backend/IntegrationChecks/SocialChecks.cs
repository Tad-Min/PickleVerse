using System.Net;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;
using Services.Exceptions;
using Services.Game;
using static Services.Game.GameSql;

static class SocialChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options,Action<bool,string> check)
    {
        async Task<T> Call<T>(Func<OnlineSocialService,Task<T>> call)
        {await using var db=new PickleVerseContext(options);return await call(new OnlineSocialService(db,new GameSql(db)));}
        async Task Act(Guid self,Guid other,string action,int request=0)=>await Call(async s=>{await s.Act(self,new SocialActionRequest{PlayerId=other,Action=action,RequestId=request});return true;});
        async Task Rejected(Func<Task> call,string name)
        {bool rejected=false;try{await call();}catch(AppException ex){rejected=ex.StatusCode==HttpStatusCode.Conflict;}check(rejected,name);}
        Guid a=Guid.NewGuid(),b=Guid.NewGuid(),c=Guid.NewGuid();
        await using(var db=new PickleVerseContext(options))
        {
            var sql=new GameSql(db);
            foreach(var item in new[]{(a,"SocialAlice",true),(b,"SocialBob",true),(c,"SocialUnverified",false)})
            {
                await sql.Run("INSERT INTO users(userid,email,passwordhash,roleid) SELECT @id,@email,'disabled',roleid FROM roles WHERE rolename='Player'",P("id",item.Item1),P("email",item.Item2+"@example.invalid"));
                await sql.Run("INSERT INTO playerprofiles(userid,playername) VALUES(@id,@name)",P("id",item.Item1),P("name",item.Item2));
                if(item.Item3)await sql.Run("INSERT INTO verificationemail(userid,email,token,expiresat,isverified) VALUES(@id,@email,'fixture',CURRENT_TIMESTAMP,true)",P("id",item.Item1),P("email",item.Item2+"@example.invalid"));
            }
            await sql.Run("""
                WITH added AS (
                  INSERT INTO users(email,passwordhash,roleid)
                  SELECT 'social-page-'||i||'@example.invalid','disabled',r.roleid FROM generate_series(1,51) i CROSS JOIN roles r WHERE r.rolename='Player'
                  RETURNING userid,email
                ), profiles AS (
                  INSERT INTO playerprofiles(userid,playername) SELECT userid,'SocialPage'||split_part(split_part(email,'-',3),'@',1) FROM added RETURNING userid
                )
                INSERT INTO verificationemail(userid,email,token,expiresat,isverified) SELECT a.userid,a.email,'fixture',CURRENT_TIMESTAMP,true FROM added a JOIN profiles p USING(userid);
                """);
        }
        var directory=await Call(s=>s.Read(a,"directory","Social",0));
        check(directory.Total==52&&!directory.Players.Any(p=>p.Id==a||p.Id==c)&&directory.HasMore,"online directory paginates verified real profiles and excludes self/unverified accounts");
        var second=await Call(s=>s.Read(a,"directory","Social",50));
        check(second.Players.Count==2&&!second.HasMore&&!second.Players.Any(p=>directory.Players.Any(x=>x.Id==p.Id)),"social pagination has stable ranks and no duplicated rows");
        var literal=await Call(s=>s.Read(a,"directory","%",0));check(literal.Total==0,"social search treats wildcard text literally");
        await Task.WhenAll(Act(a,b,"request"),Act(a,b,"request"));
        var outgoing=await Call(s=>s.Read(a,"friends",null,0));var incoming=await Call(s=>s.Read(b,"friends",null,0));
        var invite=incoming.Players.Single(p=>p.Id==a);
        check(outgoing.Players.Single(p=>p.Id==b).Relationship=="outgoing"&&invite.Relationship=="incoming","friend request remains pending until the other account accepts");
        await Rejected(()=>Act(a,b,"accept",invite.RequestId),"sender cannot accept their own outgoing request");
        await Task.WhenAll(Act(b,a,"accept",invite.RequestId),Act(b,a,"accept",invite.RequestId));
        var friends=await Call(s=>s.Read(a,"friends",null,0));
        check(friends.FriendCount==1&&friends.Players.Single().Relationship=="friend","concurrent acceptance creates exactly one friendship");
        await Act(a,b,"remove");
        await Rejected(()=>Act(b,a,"accept",invite.RequestId),"old acceptance cannot recreate a removed friendship");
        await Act(a,b,"request");
        var reissued=await Call(s=>s.Read(b,"friends",null,0));
        check(reissued.Players.Single().Relationship=="incoming"&&reissued.Players.Single().RequestId!=invite.RequestId,"new invitation has a fresh identity and no unique-key error");
        await Rejected(()=>Act(b,a,"accept",invite.RequestId),"a stale request ID cannot accept a new invitation");
        await Act(b,a,"decline",reissued.Players.Single().RequestId);await Act(a,b,"request");await Act(b,a,"block");
        await Rejected(()=>Act(a,b,"request"),"blocked users cannot reissue friend invitations");
        check((await Call(s=>s.Read(a,"directory","SocialBob",0))).Total==0&&(await Call(s=>s.Read(b,"friends",null,0))).Total==0,"blocking removes relationships and hides the blocked profile from lists");
    }
}
