using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
namespace PickleVerse.Replays
{
    public interface IReplayRepository
    {
        ReplayMetadata Read(string playerId,string matchId);
        void Save(ReplayMetadata metadata);
        void WriteChunk(ReplayMetadata metadata, ReplayChunk chunk);
        ReplayChunk ReadChunk(ReplayMetadata metadata, ReplayChunkInfo chunk);
        void RecoverAndPrune(string playerId,IEnumerable<string> retained);
    }
    public sealed class LocalReplayRepository : IReplayRepository
    {
        public string Root {get;}
        public LocalReplayRepository(string root) {Root=Path.GetFullPath(root);}
        static string Id(string value)
        {
            if(string.IsNullOrEmpty(value)||value.Length>80||value.Any(c=>!(c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9')&&c!='-'&&c!='_')) throw new InvalidDataException("ID replay không hợp lệ.");
            return value;
        }
        string DirectoryFor(string player,string match)=>Path.Combine(Root,Id(player),Id(match));
        public ReplayMetadata Read(string playerId,string matchId)
        {
            string path=Path.Combine(DirectoryFor(playerId,matchId),"metadata.json");
            if(!File.Exists(path)&&!File.Exists(path+".bak")) return null;
            ReplayMetadata Parse(string file)
            {
                var m=JsonUtility.FromJson<ReplayMetadata>(File.ReadAllText(file));
                if(m!=null&&m.version!=1)throw new NotSupportedException("Phiên bản replay chưa được hỗ trợ: "+m.version);
                if(m==null||m.playerId!=playerId||m.matchId!=matchId||m.actors==null||m.chunks==null)throw new InvalidDataException("Metadata replay hỏng.");
                return m;
            }
            try{return Parse(path);}
            catch(NotSupportedException){throw;}
            catch(Exception original)
            {
                if(File.Exists(path+".bak"))try{return Parse(path+".bak");}catch{ }
                throw new InvalidDataException("Không đọc được metadata replay hoặc bản dự phòng.",original);
            }
        }
        public void Save(ReplayMetadata m)
        {
            string dir=DirectoryFor(m.playerId,m.matchId);Directory.CreateDirectory(dir);
            Atomic(Path.Combine(dir,"metadata.json"),Encoding.UTF8.GetBytes(JsonUtility.ToJson(m)));
        }
        static void Atomic(string path,byte[] bytes)
        {
            string temp=path+".tmp";
            using(var f=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){f.Write(bytes,0,bytes.Length);f.Flush(true);}
            if(File.Exists(path)) File.Replace(temp,path,path+".bak");else File.Move(temp,path);
        }
        public void WriteChunk(ReplayMetadata m,ReplayChunk chunk)
        {
            if(chunk.frames.Count==0)return;
            string name=m.chunks.Count.ToString("D6")+".json.gz";
            using(var buffer=new MemoryStream())
            {
                using(var gzip=new GZipStream(buffer,System.IO.Compression.CompressionLevel.Fastest,true))
                using(var writer=new StreamWriter(gzip,Encoding.UTF8))writer.Write(JsonUtility.ToJson(chunk));
                Atomic(Path.Combine(DirectoryFor(m.playerId,m.matchId),name),buffer.ToArray());
            }
            m.chunks.Add(new ReplayChunkInfo{file=name,start=chunk.frames[0].time,end=chunk.frames[chunk.frames.Count-1].time});
            m.duration=chunk.frames[chunk.frames.Count-1].time;Save(m);
        }
        public ReplayChunk ReadChunk(ReplayMetadata m,ReplayChunkInfo chunk)
        {
            if(chunk.file!=Path.GetFileName(chunk.file)||!chunk.file.EndsWith(".json.gz",StringComparison.Ordinal))throw new InvalidDataException("Đường dẫn replay không hợp lệ.");
            using(var file=File.OpenRead(Path.Combine(DirectoryFor(m.playerId,m.matchId),chunk.file)))
            using(var gzip=new GZipStream(file,CompressionMode.Decompress))
            using(var reader=new StreamReader(gzip))
            {
                var result=JsonUtility.FromJson<ReplayChunk>(reader.ReadToEnd());
                if(result?.frames==null||result.frames.Count==0||result.frames.Count>120)throw new InvalidDataException("Chunk replay không hợp lệ.");
                return result;
            }
        }
        public void RecoverAndPrune(string playerId,IEnumerable<string> retained)
        {
            string player=Path.Combine(Root,Id(playerId));if(!Directory.Exists(player))return;
            var keep=new HashSet<string>(retained);
            foreach(var dir in Directory.GetDirectories(player))
            {
                if(!keep.Contains(Path.GetFileName(dir))){Directory.Delete(dir,true);continue;}
                try
                {
                    var m=Read(playerId,Path.GetFileName(dir));if(m==null)continue;
                    foreach(var file in Directory.GetFiles(dir,"*.json.gz").OrderBy(x=>x,StringComparer.Ordinal))
                    {
                        if(m.chunks.Any(c=>c.file==Path.GetFileName(file)))continue;
                        try{var info=new ReplayChunkInfo{file=Path.GetFileName(file)};var chunk=ReadChunk(m,info);
                        info.start=chunk.frames[0].time;info.end=chunk.frames[chunk.frames.Count-1].time;m.chunks.Add(info);m.duration=Math.Max(m.duration,info.end);}
                        catch(Exception e){Debug.LogWarning("Bỏ qua chunk replay hỏng: "+e.Message);}
                    }
                    m.chunks=m.chunks.OrderBy(c=>c.start).ToList();
                    Save(m);
                }
                catch(Exception e){Debug.LogWarning("Không phục hồi được replay: "+e.Message);}
            }
        }
    }
    public static class ReplayStorage
    {
        static IReplayRepository repository;
        public static IReplayRepository Repository => repository??(repository=new OnlineReplayRepository());
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){repository=null;}
        public static string Availability(string player,string match)
        {
            try {var m=Repository.Read(player,match);return m==null||m.chunks.Count==0?"Chưa có replay":m.complete?"Xem replay":"Xem replay · chưa hoàn tất";}
            catch {return "Replay không đọc được";}
        }
    }
}
