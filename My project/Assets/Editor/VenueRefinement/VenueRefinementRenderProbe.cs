using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PickleVerse.LocalMatch;
using PickleVerse.Settings;
using Newtonsoft.Json;

// Editor-only, offline render comparison. Never writes player data or calls APIs.
public static class VenueRefinementRenderProbe
{
    static readonly string Folder=Path.GetFullPath("../BlenderWork/VenueRefinement20261025");
    static Task task;static int lastFrame;static bool recording;static bool moving;static float caseTime;
    static readonly List<float> values=new List<float>();
    static readonly List<double> gpu=new List<double>();
    static readonly List<object> rows=new List<object>();
    static readonly List<string> errors=new List<string>();
    static readonly FrameTiming[] timings=new FrameTiming[1];
    static MatchActor[] actors;static string stage;
    static void Log(string condition,string trace,LogType type){if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)errors.Add(condition);}
    static void Tick()
    {
        if(!Application.isPlaying||Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;
        if(actors!=null&&moving)
        {
            caseTime+=Time.deltaTime;
            for(int i=0;i<actors.Length;i++)if(actors[i])actors[i].Step(new Vector2(Mathf.Cos(caseTime*2+i)*.2f,Mathf.Sin(caseTime*2+i)*.2f),false,Time.deltaTime);
        }
        if(!recording)return;
        values.Add(Time.unscaledDeltaTime*1000);
        FrameTimingManager.CaptureFrameTimings();
        if(FrameTimingManager.GetLatestTimings(1,timings)>0&&timings[0].gpuFrameTime>0)gpu.Add(timings[0].gpuFrameTime);
    }
    static void Save(string status,object extra=null)
    {
        File.WriteAllText(Path.Combine(Folder,stage+"-performance.json"),JsonConvert.SerializeObject(new{status,rows,errors,extra},Formatting.Indented));
    }
    public static object Start(string label)
    {
        if(!Application.isPlaying)throw new Exception("Enter the empty render scene in Play mode first.");
        if(task!=null&&!task.IsCompleted)throw new Exception("Probe already running");
        stage=label;rows.Clear();errors.Clear();task=Run();return new{started=true,path=Path.Combine(Folder,label+"-performance.json")};
    }
    static object Summary(float[] data)
    {
        Array.Sort(data);if(data.Length<30)throw new Exception("Insufficient rendered frames");
        return new{frames=data.Length,meanFps=1000/data.Average(),medianMs=data[data.Length/2],p95Ms=data[(int)((data.Length-1)*.95)],maxMs=data.Last(),over16ms=data.Count(x=>x>1000f/60)};
    }
    static async Task Run()
    {
        Save("running");var saved=JsonUtility.ToJson(GameSettings.Current);
        Camera cam=null;GameObject venue=null;Light sun=null;MatchRenderBudget budget=null;
        var originalMobile=MobileRenderPolicy.Preview;
        var oldSky=RenderSettings.skybox;
        Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
        try
        {
            var data=GameSettings.Current;data.fps=240;data.vsync=false;data.renderScale=1;data.msaa=2;
            SettingsRuntime.Apply(data);Application.runInBackground=true;
            var view=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));view.Show();view.Focus();
            cam=new GameObject("Venue comparison camera",typeof(Camera),typeof(UniversalAdditionalCameraData)).GetComponent<Camera>();
            cam.tag="MainCamera";cam.nearClipPlane=.12f;cam.farClipPlane=130;cam.fieldOfView=54;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.31f,.40f,.48f);
            sun=new GameObject("Venue daylight",typeof(Light)).GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.05f;sun.color=new Color(1,.99f,.97f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(45,-25,0);
            budget=cam.gameObject.AddComponent<MatchRenderBudget>();
            actors=new MatchActor[4];
            for(int i=0;i<4;i++)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Multiplayer/BotMatch/"+(i%2==0?"Female":"Male")+"Actor.prefab");
                if(!prefab)throw new Exception("Actor prefab absent");
                actors[i]=UnityEngine.Object.Instantiate(prefab).GetComponent<MatchActor>();actors[i].Initialize();actors[i].ResetAt(new Vector3(i%2==0?-1.3f:1.3f,0,i<2?-5:5),i<2?-1:1);
            }
            moving=true;
            foreach(bool mobile in new[]{false,true})
            {
                MobileRenderPolicy.Preview=mobile;budget.enabled=false;SettingsRuntime.Apply(data);budget.enabled=true;
                foreach(string id in new[]{"Professional","LowPoly"})
                {
                    var prefab=stage=="before"?AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/VenueRefinement/Baseline/"+id+".prefab"):Resources.Load<GameObject>("Courts/"+id);
                    if(!prefab)throw new Exception("Venue fixture missing: "+id+" / "+stage);
                    venue=UnityEngine.Object.Instantiate(prefab);var e=venue.GetComponent<StadiumEnvironment>();
                    foreach(bool night in new[]{false,true})
                    {
                        sun.enabled=!night;RenderSettings.ambientMode=AmbientMode.Trilight;
                        RenderSettings.ambientSkyColor=night?MatchEnvironmentLighting.NightAmbientSky:new Color(.52f,.63f,.76f);
                        RenderSettings.ambientEquatorColor=night?MatchEnvironmentLighting.NightAmbientEquator:new Color(.34f,.40f,.48f);
                        RenderSettings.ambientGroundColor=night?MatchEnvironmentLighting.NightAmbientGround:new Color(.2f,.23f,.25f);
                        RenderSettings.ambientIntensity=1;e.ApplyNight(night?1:0,true,.2f);
                        cam.transform.position=new Vector3(0,6.5f,-12);cam.transform.LookAt(new Vector3(0,0,.4f));
                        budget.enabled=false;budget.enabled=true;caseTime=0;
                        for(int i=0;i<4;i++)actors[i].ResetAt(new Vector3(i%2==0?-1.3f:1.3f,0,i<2?-5:5),i<2?-1:1);
                        await Task.Delay(3000);
                        caseTime=0;
                        for(int i=0;i<4;i++)actors[i].ResetAt(new Vector3(i%2==0?-1.3f:1.3f,0,i<2?-5:5),i<2?-1:1);
                        values.Clear();gpu.Clear();recording=true;await Task.Delay(5000);recording=false;
                        var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                        var renderers=e.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();
                        var row=new{venue=id,mobile,night,frames=Summary(values.ToArray()),gpuMs=gpu.Count>0?(double?)gpu.Average():null,
                            renderers=renderers.Length,submeshes=renderers.Sum(r=>r.sharedMaterials.Length),triangles=renderers.Sum(r=>r.GetComponent<MeshFilter>()?.sharedMesh.triangles.Length/3??0),
                            width=Screen.width,height=Screen.height,renderScale=pipeline.renderScale,msaa=pipeline.msaaSampleCount,drawCalls=UnityStats.drawCalls,
                            frameBudget60=values.Average()<=1000f/60&&values.OrderBy(v=>v).ElementAt((int)((values.Count-1)*.95))<=1000f/60};
                        rows.Add(row);Save("running");
                    }
                    UnityEngine.Object.Destroy(venue);venue=null;await Task.Delay(150);
                }
            }
            Save(errors.Count==0?"passed":"failed",new{hardware=SystemInfo.graphicsDeviceName,cpu=SystemInfo.processorType,
                scope="Controlled live Editor rendering, real venue prefabs, four moving actual actor prefabs, production MatchRenderBudget, day/night and desktop/mobile rendering policy. No backend or online session; mobile results are a desktop preview, not a phone benchmark."});
        }
        catch(Exception ex){errors.Add(ex.ToString());Save("failed");}
        finally
        {
            recording=moving=false;EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
            MobileRenderPolicy.Preview=originalMobile;JsonUtility.FromJsonOverwrite(saved,GameSettings.Current);SettingsRuntime.Apply(GameSettings.Current);
            if(venue)UnityEngine.Object.Destroy(venue);if(cam)UnityEngine.Object.Destroy(cam.gameObject);if(sun)UnityEngine.Object.Destroy(sun.gameObject);
            if(actors!=null)foreach(var a in actors)if(a)UnityEngine.Object.Destroy(a.gameObject);actors=null;RenderSettings.skybox=oldSky;
        }
    }
}

