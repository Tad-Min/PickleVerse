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

public sealed class PracticeFrameProbe:MonoBehaviour
{
    public bool recording,moving;public TrainingDirector training;public TutorialDirector tutorial;
    public readonly List<float> frames=new List<float>(8192),sample=new List<float>(8192),scripts=new List<float>(8192),skin=new List<float>(8192),gc=new List<float>(8192);
    public ProfilerRecorder animation,behaviours,skinning,allocations;
    void Update()
    {
        if(training){training.TouchMovement=moving?new Vector2(Mathf.Sin(Time.time*1.2f),Mathf.Cos(Time.time*.8f)):Vector2.zero;training.TouchRun=moving;}
        if(tutorial){tutorial.TouchMovement=moving?Vector2.up:Vector2.zero;tutorial.TouchRun=moving;}
        if(!recording)return;
        frames.Add(Time.unscaledDeltaTime*1000);sample.Add(animation.LastValue/1000000f);scripts.Add(behaviours.LastValue/1000000f);skin.Add(skinning.LastValue/1000000f);gc.Add(allocations.LastValue);
    }
    public void Clear(){frames.Clear();sample.Clear();scripts.Clear();skin.Clear();gc.Clear();}
}
public static class PracticePerformance
{
    const string Folder="../UnityWork/PracticeSmooth20261019";static Task running;static bool after;
    static void Save(string name,object value){Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/"+name+".json",JsonConvert.SerializeObject(value,Formatting.Indented));}
    static async Task Load(string scene){var op=SceneManager.LoadSceneAsync(scene);while(!op.isDone)await Task.Delay(30);await Task.Delay(400);}
    public static object Before(){if(!Application.isPlaying)throw new Exception("Play Mode required");after=false;running=Run();return "Started baseline";}
    public static object After(){if(!Application.isPlaying)throw new Exception("Play Mode required");after=true;running=Run();return "Started comparison";}
    static async Task<object> Measure(PracticeFrameProbe p,string stage,int warmup=1200,int duration=4200)
    {
        await Task.Delay(warmup);p.Clear();p.recording=true;await Task.Delay(duration);p.recording=false;
        var f=p.frames.OrderBy(v=>v).ToArray();var a=p.training?p.training.player:p.tutorial?p.tutorial.player:null;
        return new{stage,frames=f.Length,fps=1000/f.Average(),p95=f[(int)((f.Length-1)*.95)],max=f.Last(),animationMs=p.sample.Average(),scriptsMs=p.scripts.Average(),skinMs=p.skin.Average(),gcBytesPerFrame=p.gc.Average(),scale=(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset).renderScale,actor=a?new{position=a.transform.position.ToString("F3"),velocity=a.Velocity.ToString("F3"),a.CurrentClip,a.LocomotionClip}:null};
    }
    sealed class Memory:ITutorialProgressStore
    {readonly Dictionary<string,int> values=new Dictionary<string,int>();public int Read(string key)=>values.TryGetValue(key,out var v)?v:0;public void Write(string key,int value){values[key]=value;}}
    public static object Survey(){if(!Application.isPlaying)throw new Exception("Play Mode required");running=RunSurvey();return "Started every-lesson frame survey";}
    static async Task RunSurvey()
    {
        const string file="performance-all-lessons";Save(file,new{status="running"});var runtime=ProgressionRuntime.Instance;string original=runtime.Player.id,qa="PracticeAll"+Guid.NewGuid().ToString("N").Substring(0,8);var rows=new List<object>();PracticeFrameProbe p=null;
        try
        {
            runtime.SwitchPlayer(qa,"Practice all QA");p=new GameObject("Practice lesson frame probe").AddComponent<PracticeFrameProbe>();UnityEngine.Object.DontDestroyOnLoad(p.gameObject);
            p.animation=ProfilerRecorder.StartNew(ProfilerCategory.Animation,"GameObject.SampleAnimation",1);p.behaviours=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"BehaviourUpdate",1);p.skinning=ProfilerRecorder.StartNew(ProfilerCategory.Render,"SkinnedMeshUpdateAllNeeded",1);p.allocations=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
            await Load("Training");p.training=UnityEngine.Object.FindAnyObjectByType<TrainingDirector>();runtime.UpdateWallet(x=>{foreach(var lesson in TrainingCurriculum.Lessons)TrainingEngine.Record(TrainingEngine.Ensure(x),lesson.id).bestStars=1;});
            foreach(var lesson in TrainingCurriculum.Lessons)
            {
                p.training.Select(lesson.id);p.training.StartLesson(true);p.moving=true;
                rows.Add(await Measure(p,"Training "+lesson.id,1200,1800));Save(file,new{status="running",rows});
            }
            p.training.Catalogue();p.training=null;p.moving=false;TutorialProgress.Configure(new Memory());await Load("Tutorial");p.tutorial=UnityEngine.Object.FindAnyObjectByType<TutorialDirector>();
            var begin=typeof(TutorialDirector).GetMethod("Begin",BindingFlags.Instance|BindingFlags.NonPublic);
            for(int index=0;index<TutorialCurriculum.Lessons.Length;index++)
            {
                begin.Invoke(p.tutorial,new object[]{index,true});p.moving=p.tutorial.Lesson.kind==TutorialKind.Movement||p.tutorial.Lesson.kind==TutorialKind.Shot;
                rows.Add(await Measure(p,"Tutorial "+index+" "+p.tutorial.Lesson.kind,1200,1800));Save(file,new{status="running",rows});
            }
            Save(file,new{status="passed",resolution=Screen.width+"x"+Screen.height,rows});
        }
        catch(Exception e){Save(file,new{status="failed",error=e.ToString(),rows});Debug.LogException(e);}
        finally
        {
            TutorialProgress.Configure(null);
            if(p){p.recording=false;p.animation.Dispose();p.behaviours.Dispose();p.skinning.Dispose();p.allocations.Dispose();UnityEngine.Object.Destroy(p.gameObject);}
            await Load("Menu");runtime.SwitchPlayer(original,null);string root=Path.Combine(Application.persistentDataPath,"LocalPlayers");foreach(string suffix in new[]{".json",".json.bak",".lock"}){string path=Path.Combine(root,qa+suffix);if(File.Exists(path))File.Delete(path);}
        }
    }
    static async Task Run()
    {
        string file=after?"performance-after":"performance-before";Save(file,new{status="running"});var runtime=ProgressionRuntime.Instance;string original=runtime.Player.id,qa="PracticePerf"+Guid.NewGuid().ToString("N").Substring(0,8);var rows=new List<object>();PracticeFrameProbe p=null;
        try
        {
            runtime.SwitchPlayer(qa,"Practice perf QA");p=new GameObject("Practice performance probe").AddComponent<PracticeFrameProbe>();UnityEngine.Object.DontDestroyOnLoad(p.gameObject);
            p.animation=ProfilerRecorder.StartNew(ProfilerCategory.Animation,"GameObject.SampleAnimation",1);p.behaviours=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"BehaviourUpdate",1);p.skinning=ProfilerRecorder.StartNew(ProfilerCategory.Render,"SkinnedMeshUpdateAllNeeded",1);p.allocations=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
            await Load("Training");p.training=UnityEngine.Object.FindAnyObjectByType<TrainingDirector>();
            runtime.UpdateWallet(x=>{foreach(var lesson in TrainingCurriculum.Lessons)TrainingEngine.Record(TrainingEngine.Ensure(x),lesson.id).bestStars=1;});
            rows.Add(await Measure(p,"Training catalogue"));
            foreach(string lesson in new[]{"footwork","doubles-transition"})
            {p.training.Select(lesson);p.training.StartLesson(true);p.moving=true;rows.Add(await Measure(p,"Training "+lesson+" day"));p.training.GetComponent<MatchEnvironmentLighting>().Apply(true);rows.Add(await Measure(p,"Training "+lesson+" night"));p.training.GetComponent<MatchEnvironmentLighting>().Apply(false);Save(file,new{status="running",rows});}
            p.training.Catalogue();p.training=null;p.moving=false;await Load("Tutorial");p.tutorial=UnityEngine.Object.FindAnyObjectByType<TutorialDirector>();
            var begin=typeof(TutorialDirector).GetMethod("Begin",BindingFlags.Instance|BindingFlags.NonPublic);int movement=Array.FindIndex(TutorialCurriculum.Lessons,l=>l.kind==TutorialKind.Movement);begin.Invoke(p.tutorial,new object[]{movement,true});p.moving=true;
            rows.Add(await Measure(p,"Tutorial movement holding forward"));p.moving=false;rows.Add(await Measure(p,"Tutorial released movement"));
            begin.Invoke(p.tutorial,new object[]{6,true});rows.Add(await Measure(p,"Tutorial kitchen demo"));
            Save(file,new{status="passed",resolution=Screen.width+"x"+Screen.height,rows});
        }
        catch(Exception e){Save(file,new{error=e.ToString(),rows});Debug.LogException(e);}
        finally
        {
            if(p){p.recording=false;p.animation.Dispose();p.behaviours.Dispose();p.skinning.Dispose();p.allocations.Dispose();UnityEngine.Object.Destroy(p.gameObject);}
            await Load("Menu");runtime.SwitchPlayer(original,null);string root=Path.Combine(Application.persistentDataPath,"LocalPlayers");foreach(string suffix in new[]{".json",".json.bak",".lock"}){string path=Path.Combine(root,qa+suffix);if(File.Exists(path))File.Delete(path);}
        }
    }
}
