using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.LocalMatch;
using PickleVerse.Progression;
using PickleVerse.Settings;

// Live rendered frames, without raw Profiler frame deserialization.
public sealed class StadiumFrameProbe:MonoBehaviour
{
    public bool recording,automate,recordGpu;
    public readonly List<float> frames=new List<float>();
    public readonly List<double> gpu=new List<double>(),cpu=new List<double>();
    readonly FrameTiming[] timings=new FrameTiming[1];
    public TrainingDirector training;
    public MatchActor[] doubles;
    int trial=-1;bool issued;
    void Update()
    {
        if(recording){frames.Add(Time.unscaledDeltaTime*1000);if(recordGpu){FrameTimingManager.CaptureFrameTimings();if(FrameTimingManager.GetLatestTimings(1,timings)>0){gpu.Add(timings[0].gpuFrameTime);cpu.Add(timings[0].cpuMainThreadFrameTime);}}}
        if(!automate)return;
        if(training&&training.Session!=null)
        {
            var d=training;if(d.Phase==TrainingPhase.Finished){d.StartLesson(true);trial=-1;}
            if(d.Session.trials!=trial){trial=d.Session.trials;issued=false;}
            Vector3 goal=d.Phase==TrainingPhase.Recovery?d.RecoveryPosition:d.ReceivePosition;
            Vector3 delta=goal-d.player.transform.position;Vector2 move=new Vector2(delta.x,delta.z).normalized*Mathf.Clamp01(delta.magnitude*3);
            int tap=0,mask=0;
            if(d.Phase==TrainingPhase.Practice&&!d.PartnerTurn&&!issued&&(d.Lesson.id=="serve"||d.Receiving&&d.ReleaseIn<=.012f))
            {tap=d.ExpectedMask;mask=d.ExpectedMask&24;issued=true;}
            d.Advance(Mathf.Min(Time.deltaTime,.05f),mask,move,true,tap);
        }
        if(doubles!=null)for(int i=0;i<doubles.Length;i++)doubles[i].Step(new Vector2(Mathf.Sin(Time.time*2+i),0)*.25f,false,Time.deltaTime);
    }
}
public static class StadiumPerformanceBenchmark
{
    public static GameObject PlayerFemale,PlayerMale;
    static object Summary(List<float> values)
    {
        var s=values.OrderBy(x=>x).ToArray();if(s.Length==0)throw new Exception("No rendered frames");
        return new{frames=s.Length,meanFps=1000/values.Average(),medianMs=s[s.Length/2],p95Ms=s[(int)((s.Length-1)*.95)],maxMs=s.Last(),below60=values.Count(v=>v>1000f/60)};
    }
    static async Task<object> Capture(StadiumFrameProbe p,int milliseconds=6000)
    {p.frames.Clear();p.gpu.Clear();p.cpu.Clear();p.recording=true;await Task.Delay(milliseconds);p.recording=false;return new{frame=Summary(p.frames),gpu=Timing(p.gpu),cpu=Timing(p.cpu)};}
    static object Timing(List<double> values){var s=values.OrderBy(x=>x).ToArray();return s.Length==0?null:new {mean=s.Average(),p95=s[(int)((s.Length-1)*.95)],max=s.Last()};}
    static void Loaded(Scene s,LoadSceneMode mode)
    {
        if(s.name!="MultiplayerCourt")return;
        var bootstrap=UnityEngine.Object.FindAnyObjectByType<PickleVerse.Multiplayer.CourtBootstrap>();if(bootstrap)bootstrap.enabled=false;
    }
    static async Task Setup(string scene,StadiumFrameProbe probe)
    {
        probe.training=null;probe.doubles=null;
        SceneManager.LoadScene(scene);await Task.Delay(800);
        if(scene=="BotMatch"){var m=UnityEngine.Object.FindAnyObjectByType<BotMatch>();m.opening?.Skip();m.autoPlayer=true;m.Continue();}
        if(scene=="Training")
        {var d=UnityEngine.Object.FindAnyObjectByType<TrainingDirector>();d.Select("drive");d.StartLesson(true);d.enabled=false;probe.training=d;}
        if(scene=="Tutorial")
        {var d=UnityEngine.Object.FindAnyObjectByType<TutorialDirector>();while(d.LessonIndex<2){d.Advance(5);d.Next();}}
        if(scene=="MultiplayerCourt")
        {
            // Rendering load of four real animated actor prefabs; no online traffic claimed.
            probe.doubles=new MatchActor[4];for(int i=0;i<4;i++)
            {
#if UNITY_EDITOR
                var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Multiplayer/BotMatch/"+(i%2==0?"Female":"Male")+"Actor.prefab");
#else
                var prefab=i%2==0?PlayerFemale:PlayerMale;
#endif
                var go=UnityEngine.Object.Instantiate(prefab);var a=go.GetComponent<MatchActor>();a.Initialize();a.ResetAt(new Vector3(i%2==0?-1.4f:1.4f,0,i<2?-5.4f:5.4f),i<2?-1:1);probe.doubles[i]=a;
            }
        }
    }
    public static async Task<object> Run(string stage="before")
    {
        if(!Application.isPlaying)throw new Exception("Enter Play first");
        var runtime=ProgressionRuntime.Instance;string original=runtime.Player.id,id="StadFPS"+Guid.NewGuid().ToString("N").Substring(0,10);
        var probe=new GameObject("Stadium Performance QA").AddComponent<StadiumFrameProbe>();UnityEngine.Object.DontDestroyOnLoad(probe.gameObject);
        var results=new List<object>();SceneManager.sceneLoaded+=Loaded;
        try
        {
            runtime.SwitchPlayer(id,"FPS QA");probe.automate=true;
            foreach(string scene in new[]{"BotMatch","Training","Tutorial","MultiplayerCourt"})
            {
                await Setup(scene,probe);
                foreach(bool night in new[]{false,true})
                {
                    UnityEngine.Object.FindAnyObjectByType<MatchEnvironmentLighting>().Apply(night);await Task.Delay(2000);
                    var stats=await Capture(probe);
                    var p=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                    var camera=UnityEngine.Object.FindObjectsByType<Camera>().First(c=>c.enabled);
                    results.Add(new{scene,night,stats,scale=p.renderScale,msaa=p.msaaSampleCount,lights=UnityEngine.Object.FindObjectsByType<Light>().Count(l=>l.enabled),cameraPixels=new[]{camera.pixelWidth,camera.pixelHeight},actors=UnityEngine.Object.FindObjectsByType<MatchActor>().Length});
                    Directory.CreateDirectory("../UnityWork/StadiumPerformance");File.WriteAllText("../UnityWork/StadiumPerformance/"+stage+"-partial.json",Newtonsoft.Json.JsonConvert.SerializeObject(results,Newtonsoft.Json.Formatting.Indented));
                }
            }
            var g=GameSettings.Current;
            var report=new{stage,results,scope="Live Editor frames, automatic BotMatch rallies, Training drive exercise, Tutorial serve demonstration; MultiplayerCourt four moving actual actor prefabs (render-only, no network traffic).",settings=new{g.graphics,g.renderScale,g.msaa,g.fps,g.vsync}};
            Directory.CreateDirectory("../UnityWork/StadiumPerformance");File.WriteAllText("../UnityWork/StadiumPerformance/"+stage+".json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
        }
        finally
        {
            SceneManager.sceneLoaded-=Loaded;probe.automate=false;SceneManager.LoadScene("Menu");await Task.Delay(200);runtime.SwitchPlayer(original,null);UnityEngine.Object.Destroy(probe.gameObject);
            foreach(var suffix in new[]{".json",".json.bak",".lock"}){string file=Path.Combine(Application.persistentDataPath,"LocalPlayers",id+suffix);if(File.Exists(file))File.Delete(file);}
        }
    }
}
