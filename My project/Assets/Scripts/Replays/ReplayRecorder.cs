using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;
namespace PickleVerse.Replays
{
    public sealed class ReplayActorSource
    {
        public Transform root, paddle;
        public string id, name, visualId, paddleId;
        public int team;
    }
    [DefaultExecutionOrder(32000)]
    public sealed class ReplayRecorder : MonoBehaviour
    {
        IReplayRepository repository;
        ReplayMetadata metadata;
        ReplayChunk buffer = new ReplayChunk();
        ReplayActorSource[] sources;
        Transform[][] bones;
        Func<ReplayFrame> scoreboard;
        Func<Vector3> ball;
        Transform ballVisual;
        double began, nextSample;
        bool stopped, discontinuity;
        Task writing;
        public bool BackgroundWrites { get; set; } = true;
        public string MatchId => metadata?.matchId;
        public int BufferedFrames => buffer.frames.Count;
        public double Duration => Time.realtimeSinceStartupAsDouble-began;
        public void Begin(ReplayMetadata info,ReplayActorSource[] actors,Func<Vector3> ballPosition,Func<ReplayFrame> state,IReplayRepository storage=null,Transform visualBall=null)
        {
            ballVisual=visualBall;
            repository=storage??ReplayStorage.Repository;metadata=info;sources=actors;ball=ballPosition;scoreboard=state;
            began=Time.realtimeSinceStartupAsDouble;nextSample=began;stopped=false;buffer=new ReplayChunk();
            bones=actors.Select(a=>a.root.GetComponentsInChildren<Transform>(true).Where(t=>t!=a.root).ToArray()).ToArray();
            metadata.actors=actors.Select((a,i)=>new ReplayActorInfo{id=a.id,name=a.name,team=a.team,visualId=a.visualId,paddleId=a.paddleId,paddlePath=a.paddle?Path(a.root,a.paddle):"",skinName=a.paddle?a.paddle.Cast<Transform>().FirstOrDefault(t=>t.name.StartsWith("Equipped Paddle",StringComparison.Ordinal))?.name:"",bones=bones[i].Select(t=>Path(a.root,t)).ToArray()}).ToList();
            try{repository.Save(metadata);}catch(Exception e){Fail(e);}
        }
        public static string Path(Transform root,Transform child)
        {
            if(root==child)return "";
            var parts=new List<string>();while(child&&child!=root){parts.Add(Uri.EscapeDataString(child.name));child=child.parent;}
            parts.Reverse();return string.Join("/",parts);
        }
        public static Transform Find(Transform root,string path)
        {
            if(string.IsNullOrEmpty(path))return root;
            foreach(var part in path.Split('/')){string name=Uri.UnescapeDataString(part);root=root.Cast<Transform>().FirstOrDefault(t=>t.name==name);if(!root)return null;}
            return root;
        }
        public void Event(string kind,string detail="")
        {
            if(stopped||metadata==null)return;
            if(buffer.events.Count<512)buffer.events.Add(new ReplayEvent{time=Duration,kind=kind,detail=detail});
            if(kind=="EndChange"||kind=="NewRally")discontinuity=true;
        }
        void LateUpdate()
        {
            if(stopped||metadata==null||Time.realtimeSinceStartupAsDouble<nextSample)return;
            nextSample+=1d/30;if(nextSample<Time.realtimeSinceStartupAsDouble)nextSample=Time.realtimeSinceStartupAsDouble+1d/30;Capture();
        }
        public void Capture()
        {
            if(stopped||metadata==null||sources.Any(a=>!a.root))return;
            try
            {
                var frame=scoreboard();frame.time=Duration;frame.discontinuity=discontinuity;discontinuity=false;
                var b=ball();frame.ball=new[]{b.x,b.y,b.z};
                if(ballVisual){var q=ballVisual.rotation;var s=ballVisual.lossyScale;frame.ballRotation=new[]{q.x,q.y,q.z,q.w};frame.ballScale=new[]{s.x,s.y,s.z};}
                frame.actors=sources.Select((a,i)=>new ReplayActorPose{root=Pose(a.root,true),bones=bones[i].SelectMany(t=>t?Pose(t,false):new float[10]).ToArray(),paddleVisible=a.paddle&&a.paddle.gameObject.activeSelf}).ToArray();
                buffer.frames.Add(frame);
                if(frame.time-buffer.frames[0].time>=2||buffer.frames.Count>=64)Flush();
            }
            catch(Exception e){Fail(e);}
        }
        static float[] Pose(Transform t,bool world)
        {
            Vector3 p=world?t.position:t.localPosition;Quaternion q=world?t.rotation:t.localRotation;Vector3 s=t.localScale;
            return new[]{p.x,p.y,p.z,q.x,q.y,q.z,q.w,s.x,s.y,s.z};
        }
        void Flush()
        {
            if(buffer.frames.Count==0)return;
            // At most one immutable chunk is in flight and one is being collected.
            // Stop if the disk cannot commit a chunk during the next two seconds.
            if(writing!=null){if(!writing.IsCompleted)throw new IOException("Ổ lưu trữ chưa ghi kịp chunk replay.");writing.GetAwaiter().GetResult();writing=null;}
            var chunk=buffer;buffer=new ReplayChunk();
            if(BackgroundWrites)writing=Task.Run(()=>repository.WriteChunk(metadata,chunk));else repository.WriteChunk(metadata,chunk);
        }
        void CommitPending(){writing?.GetAwaiter().GetResult();writing=null;}
        public void Finish(bool complete)
        {
            if(stopped||metadata==null)return;
            try{CommitPending();Capture();CommitPending();Flush();CommitPending();metadata.complete=complete;repository.Save(metadata);stopped=true;}
            catch(Exception e){Fail(e);}
        }
        void Fail(Exception e){stopped=true;Debug.LogWarning("Replay ngừng ghi: "+e.Message);}
        void OnApplicationPause(bool pause){if(pause&&!stopped&&metadata!=null)try{CommitPending();Flush();CommitPending();}catch(Exception e){Fail(e);}}
        void OnApplicationQuit(){Finish(false);}
        void OnDestroy(){Finish(false);}
    }
}
