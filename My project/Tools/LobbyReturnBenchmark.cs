using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;
using PickleVerse.Shop;
using PickleVerse.FrontEnd;

public sealed class LobbyFrameSample : MonoBehaviour
{
    public readonly List<float> frames=new List<float>();
    public bool record;
    void Update(){if(record)frames.Add(Time.unscaledDeltaTime*1000);}
}
public static class LobbyReturnBenchmark
{
    static object Summary(List<float> values)
    {
        var sorted=values.OrderBy(x=>x).ToArray();if(sorted.Length==0)return new{frames=0};
        return new{frames=sorted.Length,meanMs=values.Average(),medianMs=sorted[sorted.Length/2],p95Ms=sorted[(int)((sorted.Length-1)*.95f)],maxMs=sorted.Last(),below40=values.Count(x=>x>25)};
    }
    static async Task<object> Capture(LobbyFrameSample probe,string from)
    {
        var home=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();home.animateAtmosphere=false;home.ApplyAtmosphere(0);
        await Task.Delay(800);probe.frames.Clear();probe.record=true;await Task.Delay(1800);probe.record=false;
        var day=Summary(probe.frames);home.ApplyAtmosphere(1);await Task.Delay(800);probe.frames.Clear();probe.record=true;await Task.Delay(1800);probe.record=false;
        var night=Summary(probe.frames);var main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1);await Task.Delay(50);long cpu=main.LastValue;main.Dispose();
        return new{from,day,night,mainMs=cpu/1000000.0,cameras=UnityEngine.Object.FindObjectsByType<Camera>().Count(c=>c.enabled),listeners=UnityEngine.Object.FindObjectsByType<AudioListener>().Count(a=>a.enabled),materials=Resources.FindObjectsOfTypeAll<Material>().Count(m=>m.name=="Home Day Night Sky (Instance)"),triangles=UnityEditor.UnityStats.triangles,drawCalls=UnityEditor.UnityStats.drawCalls};
    }
    public static async Task<object> Run(string phase="before")
    {
        if(!Application.isPlaying)throw new Exception("Play first.");var initialScene=SceneManager.GetActiveScene().name;
        string wallet=PlayerPrefs.GetString(LocalEconomyStorage.Key,"");
        var probe=new GameObject("Return FPS QA").AddComponent<LobbyFrameSample>();UnityEngine.Object.DontDestroyOnLoad(probe.gameObject);
        var results=new List<object>();var transitions=new List<object>();
        try
        {
            SceneManager.LoadScene("Menu");await Task.Delay(200);results.Add(await Capture(probe,"fresh Menu"));
            foreach(string name in new[]{"Shop","Collection"})
            {
                var launcher=UnityEngine.Object.FindObjectsByType<ShopLauncher>().First(l=>l.name==(name=="Shop"?"BtnStore":"BtnCollection"));
                if(name=="Shop")launcher.OpenShop();else launcher.OpenCollection();await Task.Delay(400);
                var ui=UnityEngine.Object.FindAnyObjectByType<ShopScreen>();ui.OpenProduct(name=="Shop"?0:1);await Task.Delay(200);
                probe.frames.Clear();probe.record=true;var sw=System.Diagnostics.Stopwatch.StartNew();ui.GoLobby();sw.Stop();
                await Task.Delay(450);probe.record=false;transitions.Add(new{from=name,callMs=sw.Elapsed.TotalMilliseconds,initialFrames=Summary(probe.frames)});
                results.Add(await Capture(probe,name+" → Menu"));
            }
            var report=new{phase,results,transitions,realWalletUntouched=wallet==PlayerPrefs.GetString(LocalEconomyStorage.Key,""),settings=PickleVerse.Settings.GameSettings.Current.Copy(),scope="Live Unity Editor, Game view resolution and user graphics preserved; frame time includes Editor overhead"};
            Directory.CreateDirectory("../UnityWork/LobbyPerformance");File.WriteAllText("../UnityWork/LobbyPerformance/"+phase+".json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
        }
        finally{UnityEngine.Object.Destroy(probe.gameObject);SceneManager.LoadScene(initialScene);}
    }
}
