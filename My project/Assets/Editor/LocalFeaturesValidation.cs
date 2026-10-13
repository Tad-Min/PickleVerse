using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PickleVerse.LocalMatch;
using PickleVerse.Progression;
using PickleVerse.Replays;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug=UnityEngine.Debug;
namespace PickleVerse.EditorTools
{
    public static class LocalFeaturesValidation
    {
        const string OriginalKey="PickleVerse.Features.OriginalProfile";
        public static string MeasureRecorder()
        {
            var root=new GameObject("Replay performance fixture");string dir=Path.Combine(Path.GetTempPath(),"ReplayPerf"+Guid.NewGuid().ToString("N"));
            try
            {
                var repo=new LocalReplayRepository(dir);var sources=new[]{"Female","Male"}.Select((gender,i)=>{var actor=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("ReplayActors/"+gender),root.transform);return new ReplayActorSource{root=actor.transform,id="P"+i,name=gender,visualId=gender,paddle=actor.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Paddle")};}).ToArray();
                var recorder=root.AddComponent<ReplayRecorder>();var meta=new ReplayMetadata{playerId="Fixture",matchId="Performance"};recorder.Begin(meta,sources,()=>Vector3.one,()=>new ReplayFrame(),repo);
                var samples=new List<double>();long memoryBefore=GC.GetTotalMemory(true);var timer=new Stopwatch();int maxBuffer=0;
                for(int i=0;i<180;i++){sources[0].root.localPosition=new Vector3(i*.01f,0,0);timer.Restart();recorder.Capture();timer.Stop();samples.Add(timer.Elapsed.TotalMilliseconds);maxBuffer=Math.Max(maxBuffer,recorder.BufferedFrames);System.Threading.Thread.Sleep(34);}
                recorder.Finish(false);long memoryAfter=GC.GetTotalMemory(true);samples.Sort();
                var report=new{unity=Application.unityVersion,actors=2,bones=meta.actors.Select(a=>a.bones.Length).ToArray(),captures=180,medianMs=samples[samples.Count/2],p95Ms=samples[(int)(samples.Count*.95)],maxMs=samples.Last(),maxBufferedFrames=maxBuffer,retainedManagedBytes=memoryAfter-memoryBefore,compressedBytes=Directory.GetFiles(Path.Combine(dir,"Fixture","Performance")).Where(p=>p.EndsWith(".gz")).Sum(p=>new FileInfo(p).Length),note="Editor, full visual rig, 34 ms requested capture interval; actual scheduling varies. Compression/file writes on worker. Capture timing only; completion drains last chunk. Not full-game FPS benchmark."};
                string json=Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented);Directory.CreateDirectory("Docs/LocalFeatures");File.WriteAllText("Docs/LocalFeatures/Recorder-performance.json",json);return json;
            }
            finally{UnityEngine.Object.DestroyImmediate(root);if(Directory.Exists(dir))Directory.Delete(dir,true);}
        }
        public static void BeginLiveBot()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode first.");
            var runtime=ProgressionRuntime.Instance;if(string.IsNullOrEmpty(SessionState.GetString(OriginalKey,"")))SessionState.SetString(OriginalKey,runtime.Player.id);runtime.SwitchPlayer("FeaturesValidation","FEATURES TEST");runtime.EditLocalPlayer("FEATURES TEST",2000,0,0);
            BotMatchLaunchSettings.Prepare(3,false);SceneManager.LoadScene("BotMatch");
        }
        public static void EndLiveBotAndReplay()
        {
            var match=UnityEngine.Object.FindAnyObjectByType<BotMatch>();match?.GetComponent<ReplayRecorder>()?.Capture();
            SceneManager.LoadScene("Replay");
        }
        public static void SelectCurrentReplay()
        {
            var runtime=ProgressionRuntime.Instance;var pending=runtime.Player.pendingMatch;string id=pending?.id??runtime.Player.history.First().id;ReplayLaunch.Select(runtime.Player.id,id);
        }
        public static string BuildReplayFixture(int actorCount=4)
        {
            if(actorCount!=2&&actorCount!=4)throw new ArgumentOutOfRangeException(nameof(actorCount));
            var root=new GameObject("Visual fixture");var meta=new ReplayMetadata{playerId=ProgressionRuntime.Instance.Player.id,matchId="Fixture"+Guid.NewGuid().ToString("N"),startedUtcTicks=DateTime.UtcNow.Ticks,complete=true};
            try
            {
                var actors=Enumerable.Range(0,actorCount).Select(i=>UnityEngine.Object.Instantiate(Resources.Load<GameObject>("ReplayActors/"+(i%2==0?"Female":"Male")),root.transform).transform).ToArray();
                var transforms=actors.Select(a=>a.GetComponentsInChildren<Transform>(true).Where(t=>t!=a).ToArray()).ToArray();
                for(int i=0;i<actorCount;i++)meta.actors.Add(new ReplayActorInfo{id="P"+i,name="Fixture "+i,visualId=i%2==0?"Female":"Male",team=i<actorCount/2?0:1,bones=transforms[i].Select(t=>ReplayRecorder.Path(actors[i],t)).ToArray(),paddlePath=ReplayRecorder.Path(actors[i],transforms[i].First(t=>t.name=="Paddle")),paddleId="paddle.default"});
                float[] Pose(Transform t,bool world){var p=world?t.position:t.localPosition;var q=world?t.rotation:t.localRotation;var s=t.localScale;return new[]{p.x,p.y,p.z,q.x,q.y,q.z,q.w,s.x,s.y,s.z};}
                var repo=ReplayStorage.Repository;repo.Save(meta);var chunk=new ReplayChunk();
                for(int frame=0;frame<=60;frame++)
                {
                    bool swapped=frame>=30;
                    for(int i=0;i<actorCount;i++){int end=i<actorCount/2?-1:1;if(swapped)end*=-1;actors[i].position=new Vector3(i%2==0?-1.2f:1.2f,0,end*4f);actors[i].rotation=Quaternion.Euler(0,end<0?0:180,0);}
                    chunk.frames.Add(new ReplayFrame{time=frame/30d,discontinuity=frame==30,ball=new[]{(float)Math.Sin(frame*.1),1f,(frame%20-10)*.3f},scoreA=frame>=30?7:6,scoreB=3,gamesA=1,status="Fixture "+actorCount+" người · đổi sân ở 1 giây",actors=actors.Select((a,i)=>new ReplayActorPose{root=Pose(a,true),bones=transforms[i].SelectMany(t=>Pose(t,false)).ToArray(),paddleVisible=true}).ToArray()});
                }
                chunk.events.Add(new ReplayEvent{kind="EndChange",time=1});repo.WriteChunk(meta,chunk);
                ProgressionRuntime.Instance.Apply(new MatchResult{id=meta.matchId,playerId=meta.playerId,startedUtcTicks=meta.startedUtcTicks,endedUtcTicks=DateTime.UtcNow.Ticks,kind=MatchKind.Custom,outcome=MatchOutcome.Interrupted});
                ReplayLaunch.Select(meta.playerId,meta.matchId);return meta.matchId;
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        public static void RestoreProfile()
        {
            string original=SessionState.GetString(OriginalKey,"");if(!string.IsNullOrEmpty(original)&&ProgressionRuntime.Exists)ProgressionRuntime.Instance.SwitchPlayer(original,null);
            SessionState.EraseString(OriginalKey);
        }
    }
}
