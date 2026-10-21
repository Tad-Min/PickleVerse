using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PickleVerse.Replays;
using Repositories.Context;
using Services.Exceptions;
using Services.Interfaces;
using static Services.Game.GameSql;

namespace Services.Game;

public sealed class ReplayChunkUpload { public string Data {get;set;}=""; }
public sealed class OnlineReplayService(PickleVerseContext db,GameSql sql,IStorageService storage)
{
    static AppException Bad(string message)=>new(message,HttpStatusCode.BadRequest);
    static AppException Missing()=>new("Không tìm thấy replay của tài khoản này.",HttpStatusCode.NotFound);
    async Task Guard(Guid player,Guid match)
    {
        if(await sql.One("SELECT 1 FROM gamematches m WHERE m.matchid=@match AND (m.ownerid=@player OR EXISTS(SELECT 1 FROM playermatchhistory h WHERE h.matchid=m.matchid AND h.playerid=@player) OR (m.data->>'phase' IN ('PLAYING','FINISHED') AND EXISTS(SELECT 1 FROM gamematchplayers p WHERE p.matchid=m.matchid AND p.playerid=@player)))",r=>r.GetInt32(0),P("match",match),P("player",player))!=1)throw Missing();
    }
    async Task Lock(Guid player)=>await sql.Query("SELECT userid FROM playerprofiles WHERE userid=@player FOR UPDATE",r=>r.GetGuid(0),P("player",player));
    public async Task<List<ReplayMetadata>> List(Guid player)=>await sql.Query("SELECT metadata::text FROM onlinereplays WHERE playerid=@player ORDER BY updatedat DESC LIMIT 20",r=>GameJson.Read<ReplayMetadata>(r.GetString(0)),P("player",player));
    public async Task<ReplayMetadata> Read(Guid player,Guid match)=>await sql.One("SELECT metadata::text FROM onlinereplays WHERE playerid=@player AND matchid=@match",r=>GameJson.Read<ReplayMetadata>(r.GetString(0)),P("player",player),P("match",match))??throw Missing();
    public async Task<ReplayMetadata> Save(Guid player,Guid match,ReplayMetadata input)
    {
        await using var tx=await db.Database.BeginTransactionAsync();await Lock(player);await Guard(player,match);
        if(input.version!=1||input.sampleRate!=30||input.playerId!=player.ToString("N")||!Guid.TryParse(input.matchId,out var id)||id!=match
            ||input.actors==null||input.actors.Count<2||input.actors.Count>4||input.actors.Any(a=>a==null||a.bones==null||a.bones.Length>1024||a.bones.Any(b=>b==null||b.Length>1000)
                ||a.visualId is not("Male" or "Female")||a.team is <0 or >1||a.name?.Length>100||a.paddlePath?.Length>1000))throw Bad("Metadata replay không hợp lệ.");
        var previous=await sql.One("SELECT metadata::text FROM onlinereplays WHERE playerid=@player AND matchid=@match",r=>GameJson.Read<ReplayMetadata>(r.GetString(0)),P("player",player),P("match",match));
        if(previous!=null && GameJson.Write(previous.actors)!=GameJson.Write(input.actors))throw Bad("Không được thay đội hình của replay đã ghi.");
        var chunks=await sql.Query("SELECT metadata::text FROM onlinereplaychunks WHERE playerid=@player AND matchid=@match ORDER BY chunkindex",r=>GameJson.Read<ReplayChunkInfo>(r.GetString(0)),P("player",player),P("match",match));
        input.chunks=chunks;input.duration=chunks.LastOrDefault()?.end??0;
        input.startedUtcTicks=await sql.One("SELECT createdat FROM gamematches WHERE matchid=@match",r=>r.GetDateTime(0).Ticks,P("match",match));
        input.complete|=previous?.complete??false;
        string key=$"replays/{player:N}/{match:N}/metadata.json";
        await using var stream=new MemoryStream(System.Text.Encoding.UTF8.GetBytes(GameJson.Write(input)));
        await storage.PutFileAsync(stream,key,"application/json");
        await sql.Run("INSERT INTO onlinereplays(playerid,matchid,objectkey,metadata) VALUES(@player,@match,@key,@data::jsonb) ON CONFLICT(matchid,playerid) DO UPDATE SET metadata=EXCLUDED.metadata,updatedat=CURRENT_TIMESTAMP",P("player",player),P("match",match),P("key",key),P("data",GameJson.Write(input)));
        await tx.CommitAsync();return input;
    }
    public async Task<ReplayChunkInfo> Upload(Guid player,Guid match,int index,ReplayChunkUpload input)
    {
        if(index<0||index>=10000||input.Data.Length>2800000)throw Bad("Chunk replay vượt giới hạn.");
        byte[] bytes;
        try{bytes=Convert.FromBase64String(input.Data);}catch(FormatException){throw Bad("Chunk replay không hợp lệ.");}
        ReplayChunk chunk;
        try
        {
            await using var gzip=new GZipStream(new MemoryStream(bytes),CompressionMode.Decompress);
            await using var decoded=new MemoryStream();var buffer=new byte[8192];int size;
            while((size=await gzip.ReadAsync(buffer))>0){if(decoded.Length+size>8*1024*1024)throw Bad("Chunk replay giải nén vượt giới hạn.");await decoded.WriteAsync(buffer.AsMemory(0,size));}
            chunk=GameJson.Read<ReplayChunk>(System.Text.Encoding.UTF8.GetString(decoded.ToArray()).TrimStart('\uFEFF'));
        }
        catch(InvalidDataException){throw Bad("Chunk replay không giải nén được.");}
        catch(System.Text.Json.JsonException){throw Bad("Nội dung chunk replay không hợp lệ.");}
        if(chunk.frames==null||chunk.frames.Count<1||chunk.frames.Count>120||chunk.events==null||chunk.events.Count>512
            ||chunk.frames.Any(f=>f==null||!double.IsFinite(f.time)||f.time<0||f.time>21600||f.ball?.Length!=3||f.ball.Any(v=>!float.IsFinite(v))))throw Bad("Snapshot replay không hợp lệ.");
        await using var tx=await db.Database.BeginTransactionAsync();await Lock(player);await Guard(player,match);
        var header=await Read(player,match);
        if(chunk.frames.Any(f=>f.actors==null||f.actors.Length!=header.actors.Count||f.actors.Where((a,i)=>a==null||a.root?.Length!=10||a.bones?.Length!=header.actors[i].bones.Length*10||a.root.Any(v=>!float.IsFinite(v))||a.bones.Any(v=>!float.IsFinite(v))).Any()))throw Bad("Pose replay không hợp lệ.");
        for(int i=1;i<chunk.frames.Count;i++)if(chunk.frames[i].time<chunk.frames[i-1].time)throw Bad("Thời gian replay không tăng dần.");
        var info=new ReplayChunkInfo{file=index.ToString("D6")+".json.gz",start=chunk.frames[0].time,end=chunk.frames[^1].time};
        string hash=Convert.ToHexString(SHA256.HashData(bytes));
        var existing=await sql.One("SELECT contenthash,metadata::text FROM onlinereplaychunks WHERE playerid=@player AND matchid=@match AND chunkindex=@index",r=>new{hash=r.GetString(0),info=GameJson.Read<ReplayChunkInfo>(r.GetString(1))},P("player",player),P("match",match),P("index",index));
        if(existing!=null){if(existing.hash!=hash)throw Bad("Chunk này đã được lưu với nội dung khác.");await tx.CommitAsync();return existing.info;}
        if(index!=header.chunks.Count||header.chunks.LastOrDefault()?.end>info.start)throw Bad("Thứ tự chunk replay không hợp lệ.");
        string key=$"replays/{player:N}/{match:N}/{index:D6}-{hash}.json.gz";
        await using var stream=new MemoryStream(bytes);await storage.PutFileAsync(stream,key,"application/gzip");
        await sql.Run("INSERT INTO onlinereplaychunks(matchid,playerid,chunkindex,objectkey,contenthash,metadata) VALUES(@match,@player,@index,@key,@hash,@data::jsonb)",P("match",match),P("player",player),P("index",index),P("key",key),P("hash",hash),P("data",GameJson.Write(info)));
        header.chunks.Add(info);header.duration=info.end;
        await sql.Run("UPDATE onlinereplays SET metadata=@data::jsonb,updatedat=CURRENT_TIMESTAMP WHERE playerid=@player AND matchid=@match",P("data",GameJson.Write(header)),P("player",player),P("match",match));
        await tx.CommitAsync();return info;
    }
    public async Task<ReplayChunkUpload> Chunk(Guid player,Guid match,int index)
    {
        string key=await sql.One("SELECT objectkey FROM onlinereplaychunks WHERE playerid=@player AND matchid=@match AND chunkindex=@index",r=>r.GetString(0),P("player",player),P("match",match),P("index",index))??throw Missing();
        await using var stream=await storage.GetFileAsync(key)??throw Missing();await using var bytes=new MemoryStream();await stream.CopyToAsync(bytes);
        return new ReplayChunkUpload{Data=Convert.ToBase64String(bytes.ToArray())};
    }
}
