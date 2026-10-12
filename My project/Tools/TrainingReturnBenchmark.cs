using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Unity.Profiling;
using PickleVerse.FrontEnd;
using PickleVerse.LocalMatch;

public sealed class TrainingReturnFrameProbe:MonoBehaviour
{
    public bool recording;
    public readonly List<float> frames=new List<float>();
    void Update(){if(recording)frames.Add(Time.unscaledDeltaTime*1000);}
}
public static class TrainingReturnBenchmark
{
    static object Summary(List<float> values)
    {
        var s=values.OrderBy(x=>x).ToArray();return new{frames=s.Length,meanMs=values.Average(),medianMs=s[s.Length/2],p95Ms=s[(int)((s.Length-1)*.95)],maxMs=s.Last(),meanFps=1000/values.Average(),below60=values.Count(v=>v>1000f/60)};
    }
    static async Task Ready(string name)
    {
        float limit=Time.realtimeSinceStartup+20;
        while(SceneManager.GetActiveScene().name!=name||UnityEngine.Object.FindAnyObjectByType<PickleVerse.Shop.LobbyNavigation>()?.Busy==true)
        {if(Time.realtimeSinceStartup>limit)throw new Exception("Navigation timeout "+name);await Task.Delay(30);}
    }
    static async Task<object> Capture(TrainingReturnFrameProbe probe,int wait=2000)
    {probe.frames.Clear();probe.recording=true;await Task.Delay(wait);probe.recording=false;return Summary(probe.frames);}
    public static async Task<object> Lighting()
    {
        if(!Application.isPlaying||SceneManager.GetActiveScene().name!="Menu")throw new Exception("Play Menu first.");
        var home=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();bool animate=home.animateAtmosphere;
        var probe=new GameObject("Training Return Lighting FPS QA").AddComponent<TrainingReturnFrameProbe>();UnityEngine.Object.DontDestroyOnLoad(probe.gameObject);
        var samples=new List<object>();
        try
        {
            home.animateAtmosphere=false;
            foreach(float night in new[]{0f,1f})
            {
                home.ApplyAtmosphere(night);await Task.Delay(800);var fresh=await Capture(probe,3000);
                home.playButton.onClick.Invoke();await Ready("ModeSelection");UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>().OpenTraining();await Ready("Training");
                UnityEngine.Object.FindAnyObjectByType<TrainingDirector>().BackToModes();await Ready("ModeSelection");UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>().Back();await Ready("Menu");await Task.Delay(500);
                var returned=await Capture(probe,3000);samples.Add(new{night,fresh,returned,sameLobby=home==UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>()});
            }
            var report=new{samples,scope="Day and night, selected character and original graphics preserved."};Directory.CreateDirectory("../UnityWork/TrainingReturn");File.WriteAllText("../UnityWork/TrainingReturn/day-night.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
        }
        finally{home.animateAtmosphere=animate;home.EvaluateCycle(home.CycleElapsed);UnityEngine.Object.Destroy(probe.gameObject);}
    }
    public static async Task<object> Shadows()
    {
        var home=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();bool animate=home.animateAtmosphere;
        var shadows=home.floodlights.Select(l=>l.shadows).ToArray();var probe=new GameObject("Lobby shadow cost QA").AddComponent<TrainingReturnFrameProbe>();
        var results=new List<object>();
        try
        {
            home.animateAtmosphere=false;home.ApplyAtmosphere(1);
            foreach(int count in new[]{4,2,0})
            {
                for(int i=0;i<4;i++)home.floodlights[i].shadows=i<count?shadows[i]:LightShadows.None;
                await Task.Delay(800);results.Add(new{shadowLights=count,stats=await Capture(probe,3000)});
            }
            return results;
        }
        finally{for(int i=0;i<4;i++)home.floodlights[i].shadows=shadows[i];home.animateAtmosphere=animate;home.EvaluateCycle(home.CycleElapsed);UnityEngine.Object.Destroy(probe.gameObject);}
    }
    public static async Task<object> ShadowQuality()
    {
        var home=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();bool animate=home.animateAtmosphere;
        var lights=home.floodlights.Select(l=>l.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>()).ToArray();
        var quality=lights.Select(l=>l.softShadowQuality).ToArray();var probe=new GameObject("Lobby soft shadow quality QA").AddComponent<TrainingReturnFrameProbe>();
        try
        {
            home.animateAtmosphere=false;home.ApplyAtmosphere(1);
            foreach(var light in lights)light.softShadowQuality=UnityEngine.Rendering.Universal.SoftShadowQuality.Low;
            await Task.Delay(800);return new{softShadows="All four lights / Low filter",stats=await Capture(probe,4000)};
        }
        finally{for(int i=0;i<lights.Length;i++)lights[i].softShadowQuality=quality[i];home.animateAtmosphere=animate;home.EvaluateCycle(home.CycleElapsed);UnityEngine.Object.Destroy(probe.gameObject);}
    }
    public static async Task<object> PresentationSync()
    {
        var home=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();bool animate=home.animateAtmosphere;
        int sync=QualitySettings.vSyncCount;var probe=new GameObject("Lobby presentation pacing QA").AddComponent<TrainingReturnFrameProbe>();
        try{home.animateAtmosphere=false;home.ApplyAtmosphere(1);QualitySettings.vSyncCount=0;await Task.Delay(800);return new{vsync=0,stats=await Capture(probe,4000)};}
        finally{QualitySettings.vSyncCount=sync;home.animateAtmosphere=animate;home.EvaluateCycle(home.CycleElapsed);UnityEngine.Object.Destroy(probe.gameObject);}
    }
    public static async Task<object> ShadowResolution()
    {
        var home=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();bool animate=home.animateAtmosphere;
        var lights=home.floodlights.Select(l=>l.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>()).ToArray();
        var tiers=lights.Select(l=>l.additionalLightsShadowResolutionTier).ToArray();var sizes=home.floodlights.Select(l=>l.shadowCustomResolution).ToArray();
        var qualities=lights.Select(l=>l.softShadowQuality).ToArray();
        var shadowModes=home.floodlights.Select(l=>l.shadows).ToArray();
        var probe=new GameObject("Lobby shadow resolution QA").AddComponent<TrainingReturnFrameProbe>();
        try
        {
            home.animateAtmosphere=false;home.ApplyAtmosphere(1);
            for(int i=0;i<lights.Length;i++){lights[i].additionalLightsShadowResolutionTier=-1;home.floodlights[i].shadowCustomResolution=512;home.floodlights[i].shadows=LightShadows.Hard;}
            await Task.Delay(800);return new{softShadows="All four / 512 / Hard",stats=await Capture(probe,4000)};
        }
        finally{for(int i=0;i<lights.Length;i++){lights[i].additionalLightsShadowResolutionTier=tiers[i];home.floodlights[i].shadowCustomResolution=sizes[i];home.floodlights[i].shadows=shadowModes[i];lights[i].softShadowQuality=qualities[i];}home.animateAtmosphere=animate;home.EvaluateCycle(home.CycleElapsed);UnityEngine.Object.Destroy(probe.gameObject);}
    }
    public static async Task<object> RenderScale()
    {
        var home=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();bool animate=home.animateAtmosphere;
        var pipeline=QualitySettings.renderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;float scale=pipeline.renderScale;
        var probe=new GameObject("Lobby render budget QA").AddComponent<TrainingReturnFrameProbe>();var results=new List<object>();
        try
        {
            home.animateAtmosphere=false;home.ApplyAtmosphere(1);
            foreach(float value in new[]{1f,.85f,.75f}){pipeline.renderScale=value;await Task.Delay(800);results.Add(new{scale=value,stats=await Capture(probe,3000)});}
            return results;
        }
        finally{pipeline.renderScale=scale;home.animateAtmosphere=animate;home.EvaluateCycle(home.CycleElapsed);UnityEngine.Object.Destroy(probe.gameObject);}
    }
    public static async Task<object> Run(string stage="before")
    {
        if(!Application.isPlaying||SceneManager.GetActiveScene().name!="Menu")throw new Exception("Play Menu first.");
        var probe=new GameObject("Training Return FPS QA").AddComponent<TrainingReturnFrameProbe>();UnityEngine.Object.DontDestroyOnLoad(probe.gameObject);
        var results=new List<object>();var original=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();
        try
        {
            UnityEditor.EditorApplication.isPaused=false;await Task.Delay(500);var baseline=await Capture(probe);
            for(int i=0;i<3;i++)
            {
                UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>().playButton.onClick.Invoke();await Ready("ModeSelection");
                UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>().OpenTraining();await Ready("Training");await Task.Delay(500);
                var training=UnityEngine.Object.FindAnyObjectByType<TrainingDirector>();training.BackToModes();await Ready("ModeSelection");
                await Task.Delay(100);probe.frames.Clear();probe.recording=true;
                UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>().Back();await Ready("Menu");await Task.Delay(250);probe.recording=false;
                var transition=Summary(probe.frames);await Task.Delay(500);var stable=await Capture(probe);
                var home=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();
                results.Add(new{pass=i,transition,stable,sameLobby=home==original,home.NightWeight,home.IsMaleSelected,sceneCount=SceneManager.sceneCount,cameras=UnityEngine.Object.FindObjectsByType<Camera>().Count(c=>c.enabled),listeners=UnityEngine.Object.FindObjectsByType<AudioListener>().Count(c=>c.enabled),events=UnityEngine.Object.FindObjectsByType<EventSystem>().Count(c=>c.enabled),trainingControllers=UnityEngine.Object.FindObjectsByType<TrainingDirector>().Length});
            }
            var graphics=PickleVerse.Settings.GameSettings.Current;
            var report=new{stage,baseline,results,settings=new{graphics.graphics,graphics.renderScale,graphics.msaa,graphics.fps,graphics.vsync},scope="Live Editor, same current graphics, music, animated sky and selected character."};
            Directory.CreateDirectory("../UnityWork/TrainingReturn");File.WriteAllText("../UnityWork/TrainingReturn/"+stage+".json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
        }
        finally{UnityEngine.Object.Destroy(probe.gameObject);}
    }
}
