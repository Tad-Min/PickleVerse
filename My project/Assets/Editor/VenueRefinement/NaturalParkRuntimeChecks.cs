using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using PickleVerse.Settings;
using Newtonsoft.Json;

public static class NaturalParkRuntimeChecks
{
    static readonly string Work=Path.GetFullPath("../BlenderWork/NaturalPark20261026");
    static Task task;
    static async Task Empty()
    {
        // The sign-in screen otherwise redirects an empty Play session to Menu.
        // Remove UI only from this temporary fixture, retaining signed-out services.
        foreach(var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(component.GetType().FullName=="PickleVerse.FrontEnd.AccountScreen")UnityEngine.Object.Destroy(component.gameObject);
        Time.timeScale=1;await Task.Delay(700);
        var old=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
        var empty=SceneManager.CreateScene("NaturalPark isolated QA "+Guid.NewGuid().ToString("N").Substring(0,6));SceneManager.SetActiveScene(empty);
        foreach(var scene in old){var op=SceneManager.UnloadSceneAsync(scene);while(op!=null&&!op.isDone)await Task.Yield();}
        await Task.Delay(250);
    }
    public static string Profile()
    {
        if(!Application.isPlaying)throw new Exception("Play mode required");
        task=ProfileRun();return "Preparing isolated four-actor render profile";
    }
    static async Task ProfileRun()
    {
        try{await Empty();File.WriteAllText(Work+"/profile-fixture.json",JsonConvert.SerializeObject(new{scene=SceneManager.GetActiveScene().name,sceneRoots=SceneManager.GetActiveScene().GetRootGameObjects().Length,source="Shared current venue prefabs; sign-in UI removed in temporary fixture"},Formatting.Indented));VenueRefinementRenderProbe.Start("natural-isolated-after");}
        catch(Exception e){File.WriteAllText(Work+"/profile-error.txt",e.ToString());}
    }
    public static string WindAndCloseup()
    {
        if(!Application.isPlaying)throw new Exception("Play mode required");
        task=WindRun();return "Testing actual GPU wind and close vegetation rendering";
    }
    static Texture2D Render(Camera c)
    {
        var target=RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var active=RenderTexture.active;var old=c.targetTexture;var image=new Texture2D(960,540,TextureFormat.RGB24,false);
        try{c.targetTexture=target;c.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();return image;}
        finally{c.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);}
    }
    static long Difference(Texture2D a,Texture2D b)
    {
        var x=a.GetPixels32();var y=b.GetPixels32();long total=0;
        for(int i=0;i<x.Length;i++)total+=Math.Abs(x[i].r-y[i].r)+Math.Abs(x[i].g-y[i].g)+Math.Abs(x[i].b-y[i].b);
        return total;
    }
    static async Task WindRun()
    {
        var checks=new List<object>();var perf=new List<object>();var created=new List<UnityEngine.Object>();var errors=new List<string>();
        string saved=JsonUtility.ToJson(GameSettings.Current);bool previousMobile=MobileRenderPolicy.Preview;
        Application.LogCallback log=(s,t,k)=>{if(k==LogType.Error||k==LogType.Exception||k==LogType.Assert)errors.Add(s);};
        Application.logMessageReceived+=log;
        try
        {
            await Empty();MobileRenderPolicy.Preview=false;
            var settings=GameSettings.Current;settings.fps=240;settings.vsync=false;settings.renderScale=1;SettingsRuntime.Apply(settings);
            var sun=new GameObject("QA daylight",typeof(Light));created.Add(sun);var light=sun.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;sun.transform.rotation=Quaternion.Euler(42,-30,0);
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.52f,.63f,.76f);RenderSettings.ambientEquatorColor=new Color(.34f,.40f,.48f);RenderSettings.ambientGroundColor=new Color(.20f,.23f,.25f);RenderSettings.fog=false;
            var camera=new GameObject("QA camera",typeof(Camera));created.Add(camera);var c=camera.GetComponent<Camera>();c.tag="MainCamera";c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.34f,.42f,.49f);c.nearClipPlane=.03f;
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EnvironmentContent/NaturalPark/NaturalVegetation.glb");
            var materials=new[]{"Bark","Leaf","Grass"}.ToDictionary(k=>k,k=>new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/EnvironmentContent/NaturalPark/"+k+".mat")));
            foreach(var m in materials.Values)created.Add(m);
            foreach(string name in new[]{"Broadleaf_A_LOD0","GrassPatch_LOD0"})
            {
                var proto=source.GetComponentsInChildren<Transform>().Single(t=>t.name==name);
                var plant=UnityEngine.Object.Instantiate(proto.gameObject);created.Add(plant);
                var renderer=plant.GetComponent<MeshRenderer>();renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>materials[m.name]).ToArray();
                if(name.StartsWith("Broadleaf")){c.transform.position=new Vector3(4.5f,3,-6);c.transform.LookAt(new Vector3(0,2.5f,0));c.fieldOfView=52;}
                else{c.transform.position=new Vector3(0,.32f,-1.8f);c.transform.LookAt(new Vector3(0,.06f,0));c.fieldOfView=58;}
                foreach(var m in materials.Values)m.SetFloat("_WindStrength",0);
                await Task.Delay(1000);var stillA=Render(c);await Task.Delay(400);var stillB=Render(c);long stationary=Difference(stillA,stillB);
                foreach(var pair in materials)pair.Value.SetFloat("_WindStrength",pair.Key=="Bark"?.035f:pair.Key=="Grass"?.055f:.075f);
                await Task.Delay(250);var windA=Render(c);await Task.Delay(750);var windB=Render(c);long motion=Difference(windA,windB);
                checks.Add(new{name=name+" actual GPU wind changes rendered silhouette",passed=motion>stationary+500,stationaryDifference=stationary,windDifference=motion});
                File.WriteAllBytes(Work+"/Wind-"+name+"-A.png",windA.EncodeToPNG());File.WriteAllBytes(Work+"/Wind-"+name+"-B.png",windB.EncodeToPNG());
                foreach(var image in new[]{stillA,stillB,windA,windB})UnityEngine.Object.Destroy(image);
                var mesh=plant.GetComponent<MeshFilter>().sharedMesh;var colors=mesh.colors;var positions=mesh.vertices;
                int roots=0;bool anchored=true;
                for(int i=0;i<positions.Length;i++)if(positions[i].y<.002f){roots++;if(colors[i].r>.001f)anchored=false;}
                checks.Add(new{name=name+" roots remain anchored",passed=roots>0&&anchored,roots});
                var samples=new List<float>();int last=Time.frameCount;double until=Time.realtimeSinceStartupAsDouble+3;
                while(Time.realtimeSinceStartupAsDouble<until){await Task.Yield();if(Time.frameCount!=last){last=Time.frameCount;samples.Add(Time.unscaledDeltaTime*1000);}}
                var ordered=samples.OrderBy(x=>x).ToArray();perf.Add(new{caseName=name,meanFps=1000/ordered.Average(),p95Ms=ordered[(int)(ordered.Length*.95)],frames=ordered.Length});
                UnityEngine.Object.Destroy(plant);await Task.Delay(100);
            }
            MobileRenderPolicy.Preview=true;SettingsRuntime.Apply(settings);
            checks.Add(new{name="Mobile policy selects LOD1 or lower",passed=QualitySettings.maximumLODLevel>=1});
        }
        catch(Exception e){errors.Add(e.ToString());}
        finally
        {
            Application.logMessageReceived-=log;
            foreach(var item in created)if(item)UnityEngine.Object.Destroy(item);
            MobileRenderPolicy.Preview=previousMobile;JsonUtility.FromJsonOverwrite(saved,GameSettings.Current);SettingsRuntime.Apply(GameSettings.Current);
            File.WriteAllText(Work+"/wind-closeup-validation.json",JsonConvert.SerializeObject(new{checks,performance=perf,errors},Formatting.Indented));
        }
    }
}
