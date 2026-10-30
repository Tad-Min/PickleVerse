using System.IO.Compression;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using PickleVerse.Replays;
using Repositories.Context;
using Services.Exceptions;
using Services.Game;
using static Services.Game.GameSql;

static class SharedReplayChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, TimeProvider clock, Action<bool,string> check)
    {
        var storage = new TestReplayStorage();
        MatchReplayService Replays(PickleVerseContext db) => new(db,new GameSql(db),storage,NullLogger<MatchReplayService>.Instance);
        async Task<T> Replay<T>(Func<MatchReplayService,Task<T>> call)
        { await using var db=new PickleVerseContext(options); return await call(Replays(db)); }
        async Task<T> Game<T>(Func<OnlineGameService,Task<T>> call)
        {
            await using var db=new PickleVerseContext(options);var sql=new GameSql(db);var settings=TestSettings.Load();
            return await call(new OnlineGameService(db,sql,new GameCatalogService(sql,settings),clock,settings,TestRedis.State,TestRedis.Leaderboard,Replays(db)));
        }
        async Task Reject(Func<Task> call,HttpStatusCode status,string name)
        {
            bool rejected=false;
            try { await call(); } catch(AppException error) when(error.StatusCode==status) { rejected=true; }
            check(rejected,name);
        }
        var a=Guid.NewGuid();var b=Guid.NewGuid();var outsider=Guid.NewGuid();
        await using var context=new PickleVerseContext(options);var query=new GameSql(context);
        foreach(var id in new[]{a,b,outsider})
        {
            await query.Run("INSERT INTO users(userid,email,passwordhash,roleid) SELECT @id,@email,'disabled',roleid FROM roles WHERE rolename='Player'",P("id",id),P("email",id+"@example.invalid"));
            await query.Run("INSERT INTO playerprofiles(userid,playername) VALUES(@id,@name)",P("id",id),P("name","Replay"+id.ToString("N")[..8]));
        }
        check(await query.One("SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('onlinereplays','onlinereplaychunks')",r=>r.GetInt64(0))==0,
            "fresh schema has no per-player replay or chunk tables");
        var match=Guid.NewGuid();
        var request=new OnlineMatchRequest {HostPlayerId=a.ToString("N"),Session="shared-replay",Roster=new MatchRoster{
            matchId=match.ToString("N"),mode=PlayMode.Singles,custom=true,region="asia",members=[new(){id=a.ToString("N"),team=0},new(){id=b.ToString("N"),team=1}]}};
        await Game(s=>s.PrepareOnline(a,request));await Game(s=>s.PrepareOnline(b,request));await Game(s=>s.StartOnline(a,request));
        ReplayMetadata Meta(Guid writer,Guid matchId,Guid opponent) => new(){playerId=writer.ToString("N"),matchId=matchId.ToString("N"),actors=[
            new(){id=writer.ToString("N"),name="Recorder",team=0,visualId="Male",bones=["Root"]},
            new(){id=opponent.ToString("N"),name="Opponent",team=1,visualId="Female",bones=["Root"]}]};
        var metadata=Meta(a,match,b);
        await Replay(s=>s.Save(a,match,metadata));
        check(storage.Objects.ContainsKey(MatchReplayService.ManifestKey(match)),"recording starts with a shared S3 manifest while match is still live");
        check(await query.One("SELECT count(*) FROM matchhistoryreplay WHERE matchid=@match",r=>r.GetInt64(0),P("match",match))==0,"no match-history replay is inserted before match history exists");
        var guest=GameJson.Read<ReplayMetadata>(GameJson.Write(metadata));guest.playerId=b.ToString("N");
        await Reject(()=>Replay(s=>s.Save(b,match,guest)),HttpStatusCode.Forbidden,"another participant cannot overwrite the designated recording");
        await Reject(()=>Replay(s=>s.Save(outsider,match,metadata)),HttpStatusCode.NotFound,"outsider cannot start recording a known match");
        var invalid=GameJson.Read<ReplayMetadata>(GameJson.Write(metadata));invalid.actors[1].id=outsider.ToString("N");
        await Reject(()=>Replay(s=>s.Save(a,match,invalid)),HttpStatusCode.BadRequest,"recording cannot substitute an actor outside the match roster");
        var chunk=new ReplayChunk {frames=[new(){time=0,ball=[0,1,0],actors=[new(){root=new float[10],bones=new float[10]},new(){root=new float[10],bones=new float[10]}]}]};
        ReplayChunkUpload Compress()
        {
            using var bytes=new MemoryStream();
            using(var zip=new GZipStream(bytes,CompressionLevel.Fastest,true)) {var raw=System.Text.Encoding.UTF8.GetBytes(GameJson.Write(chunk));zip.Write(raw);}
            return new(){Data=Convert.ToBase64String(bytes.ToArray())};
        }
        var payload=Compress();
        storage.FailNextManifestWrite=true;
        bool interrupted=false;
        try { await Replay(s=>s.Upload(a,match,0,payload)); } catch(IOException) { interrupted=true; }
        check(interrupted,"storage failure after chunk upload can be retried without a SQL chunk row");
        var retries=await Task.WhenAll(Replay(s=>s.Upload(a,match,0,payload)),Replay(s=>s.Upload(a,match,0,payload)));
        check(retries[0].file==retries[1].file && storage.Objects.Count==2,"concurrent retries use one content-addressed chunk and one manifest");
        await Reject(()=>Replay(s=>s.Upload(b,match,0,payload)),HttpStatusCode.Forbidden,"non-recorder participant cannot upload chunks");
        await Reject(()=>Replay(s=>s.Upload(a,match,2,payload)),HttpStatusCode.BadRequest,"chunk manifest enforces contiguous upload order");
        await Reject(()=>Replay(s=>s.Read(a,match)),HttpStatusCode.NotFound,"live recording is not exposed as completed match history");
        metadata.complete=true;metadata.chunks=[];await Replay(s=>s.Save(a,match,metadata));
        async Task Finish(Guid player,bool won)
        {
            var state=await Game(s=>s.Read(player));var result=GameJson.Read<MatchResult>(GameJson.Write(state.player.pendingMatch));
            result.outcome=won?MatchOutcome.Won:MatchOutcome.Lost;result.gamesWon=won?2:0;result.gamesLost=won?0:2;result.points=won?22:0;
            result.scores=[new(won?11:0,won?0:11,true),new(won?11:0,won?0:11,true)];
            await Game(s=>s.ReportOnline(player,new(){RequestId=Guid.NewGuid().ToString("N"),Result=result}));
        }
        await Finish(a,true);await Finish(b,false);
        check(await query.One("SELECT count(*) FROM matchhistoryreplay WHERE matchid=@match",r=>r.GetInt64(0),P("match",match))==1,
            "match completion publishes one replay even when recording finished first");
        var own=await Replay(s=>s.Read(a,match));var other=await Replay(s=>s.Read(b,match));
        check(GameJson.Write(own)==GameJson.Write(other) && other.playerId==a.ToString("N"),"both participants read the same manifest without impersonating its recorder");
        check((await Replay(s=>s.Chunk(b,match,0))).Data==payload.Data,"another participant can replay the shared chunk");
        check((await Replay(s=>s.List(a))).Count==1 && (await Replay(s=>s.List(b))).Count==1 && (await Replay(s=>s.List(outsider))).Count==0,
            "history lists include shared recordings only for authorized participants");
        await Reject(()=>Replay(s=>s.Read(outsider,match)),HttpStatusCode.NotFound,"outsider cannot read a published manifest");
        await Reject(()=>Replay(s=>s.Chunk(outsider,match,0)),HttpStatusCode.NotFound,"outsider cannot read published chunk data");
        check((await Replay(s=>s.Read(outsider,match,true))).matchId==match.ToString("N"),"admin can inspect shared replay evidence without occupying a player seat");
        await Replay(s=>s.Upload(a,match,0,payload));
        await Reject(()=>Replay(s=>s.Upload(a,match,1,payload)),HttpStatusCode.BadRequest,"sealed replay accepts identical retries but no new chunks");
        string chunkKey=$"replays/{match:N}/{own.chunks[0].file}";
        var original=storage.Objects[chunkKey];storage.Objects[chunkKey]=[1,2,3];
        await Reject(()=>Replay(s=>s.Chunk(b,match,0)),HttpStatusCode.BadRequest,"stored chunk hash is checked before replay download");
        storage.Objects[chunkKey]=original;

        var practice=await Game(s=>s.StartBot(a,new(){RequestId=Guid.NewGuid().ToString("N"),Kind=MatchKind.BotPractice}));
        var botResult=practice.player.pendingMatch;var botMatch=Guid.Parse(botResult.id);
        await Replay(s=>s.Save(a,botMatch,Meta(a,botMatch,Guid.Parse(botResult.opponents[0].id))));
        await Replay(s=>s.Upload(a,botMatch,0,payload));
        botResult.outcome=MatchOutcome.Won;botResult.gamesWon=2;botResult.scores=[new(11,0,true),new(11,0,true)];
        storage.FailReads=true;
        var completed=await Game(s=>s.FinishBot(a,new(){RequestId=Guid.NewGuid().ToString("N"),Result=botResult}));
        storage.FailReads=false;
        check(completed.player.pendingMatch==null,"S3 publication outage does not roll back completed gameplay");
        check((await Replay(s=>s.Read(a,botMatch))).chunks.Count==1 && await query.One("SELECT count(*) FROM matchhistoryreplay WHERE matchid=@match",r=>r.GetInt64(0),P("match",botMatch))==1,
            "authorized read repairs a deferred replay reference after storage recovery");
        check(await query.One("SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='matchhistoryreplay'",r=>r.GetInt64(0))==2,
            "only MatchId and ObjectKey are persisted in MatchHistoryReplay");
    }
}
