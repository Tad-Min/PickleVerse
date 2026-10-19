using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;
using PickleVerse.LocalMatch;
using PickleVerse.Progression;
using Newtonsoft.Json;

public sealed class PracticeFrameDiagnosisProbe:MonoBehaviour
{
    public TrainingDirector training;public TutorialDirector tutorial;public bool recording;
    public readonly Dictionary<string,ProfilerRecorder> recorders=new Dictionary<string,ProfilerRecorder>();
    public readonly List<object> frames=new List<object>(2000);
    readonly FrameTiming[] timings=new FrameTiming[1];
    void Update()
    {
        if(training){training.TouchMovement=new Vector2(Mathf.Sin(Time.time*1.2f),Mathf.Cos(Time.time*.8f));training.TouchRun=true;}
        if(tutorial){tutorial.TouchMovement=Vector2.up;tutorial.TouchRun=true;}
        if(!recording)return;
        FrameTimingManager.CaptureFrameTimings();bool valid=FrameTimingManager.GetLatestTimings(1,timings)>0;
        frames.Add(new{ms=Time.unscaledDeltaTime*1000,focused=Application.isFocused,cpu=valid?timings[0].cpuMainThreadFrameTime:0,gpu=valid?timings[0].gpuFrameTime:0,markers=recorders.ToDictionary(r=>r.Key,r=>r.Value.LastValue/1000000f)});
    }
}
public static class PracticeFrameDiagnosis
{
    const string Folder="../UnityWork/PracticeSmooth20261019";static Task running;
    static void Save(object value){Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/frame-diagnosis.json",JsonConvert.SerializeObject(value,Formatting.Indented));}
    static async Task Load(string scene){var op=SceneManager.LoadSceneAsync(scene);while(!op.isDone)await Task.Delay(30);await Task.Delay(400);}
    public static object Start(){if(!Application.isPlaying)throw new Exception("Play required");running=Run();return "Detailed frame diagnosis started";}
    public static object VisualStart(){if(!Application.isPlaying)throw new Exception("Play required");running=Visual();return "Boundary visual check started";}
    static async Task Visual()
    {
        try
        {
            await Load("Tutorial");var t=UnityEngine.Object.FindAnyObjectByType<TutorialDirector>();t.enabled=false;
            typeof(TutorialDirector).GetMethod("Begin",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(t,new object[]{7,true});
            for(int i=0;i<180;i++)t.Advance(1f/60,0,Vector2.up,true);
            await Task.Delay(150);ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder+"/tutorial-at-net.png"));await Task.Delay(200);
            File.WriteAllText(Folder+"/boundary-visual.json",JsonConvert.SerializeObject(new{position=t.player.transform.position.ToString("F3"),velocity=t.player.Velocity.ToString("F3"),t.player.CurrentClip,t.Feedback,t.CanContinue},Formatting.Indented));
        }
        finally{await Load("Menu");}
    }
    static async Task Run()
    {
        Save(new{status="running"});var runtime=ProgressionRuntime.Instance;string original=runtime.Player.id,qa="FrameQA"+Guid.NewGuid().ToString("N").Substring(0,8);var rows=new List<object>();PracticeFrameDiagnosisProbe p=null;
        try
        {
            runtime.SwitchPlayer(qa,"Frame diagnosis QA");p=new GameObject("Detailed frame diagnosis").AddComponent<PracticeFrameDiagnosisProbe>();UnityEngine.Object.DontDestroyOnLoad(p.gameObject);
            foreach(var key in new[]{"Main Thread","EditorLoop"})p.recorders[key]=ProfilerRecorder.StartNew(ProfilerCategory.Internal,key,1);
            p.recorders["PlayerLoop"]=ProfilerRecorder.StartNew(new ProfilerCategory("PlayerLoop"),"PlayerLoop",1);
            foreach(var key in new[]{"CPU Main Thread Frame Time","FrameTime.GPU","Gfx.WaitForPresentOnGfxThread","Gfx.WaitForRenderThread","Camera.Render"})p.recorders[key]=ProfilerRecorder.StartNew(ProfilerCategory.Render,key,1);
            p.recorders["WaitForTargetFPS"]=ProfilerRecorder.StartNew(new ProfilerCategory("VSync"),"WaitForTargetFPS",1);
            await Load("Training");p.training=UnityEngine.Object.FindAnyObjectByType<TrainingDirector>();runtime.UpdateWallet(x=>{foreach(var lesson in TrainingCurriculum.Lessons)TrainingEngine.Record(TrainingEngine.Ensure(x),lesson.id).bestStars=1;});p.training.Select("footwork");p.training.StartLesson(true);
            var gameView=UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));gameView.Show();gameView.Focus();
            for(int stage=0;stage<4;stage++)
            {
                if(stage==1)p.training.GetComponent<MatchEnvironmentLighting>().Apply(true);
                if(stage==2){p.training.Catalogue();p.training=null;await Load("Tutorial");p.tutorial=UnityEngine.Object.FindAnyObjectByType<TutorialDirector>();typeof(TutorialDirector).GetMethod("Begin",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p.tutorial,new object[]{7,true});}
                if(stage==3)p.tutorial.GetComponent<MatchEnvironmentLighting>().Apply(true);
                await Task.Delay(3000);p.frames.Clear();p.recording=true;await Task.Delay(5000);p.recording=false;
                rows.Add(new{stage=stage<2?"Training "+(stage==0?"day":"night"):"Tutorial "+(stage==2?"day":"night"),frames=p.frames.ToArray(),budgets=UnityEngine.Object.FindObjectsByType<PickleVerse.Settings.MatchRenderBudget>(FindObjectsSortMode.None).Select(b=>new{b.name,b.preferredFrameRate,b.Scale}).ToArray()});Save(new{status="running",resolution=Screen.width+"x"+Screen.height,rows});
            }
            Save(new{status="passed",resolution=Screen.width+"x"+Screen.height,rows});
        }
        catch(Exception e){Save(new{status="failed",error=e.ToString(),rows});Debug.LogException(e);}
        finally
        {
            if(p){p.recording=false;foreach(var r in p.recorders.Values)r.Dispose();UnityEngine.Object.Destroy(p.gameObject);}
            await Load("Menu");runtime.SwitchPlayer(original,null);string root=Path.Combine(Application.persistentDataPath,"LocalPlayers");foreach(string suffix in new[]{".json",".json.bak",".lock"}){string path=Path.Combine(root,qa+suffix);if(File.Exists(path))File.Delete(path);}
        }
    }
}
