using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.FrontEnd;
using PickleVerse.Multiplayer;
using PickleVerse.Shop;

public sealed class ModeReturnProbe:MonoBehaviour
{
    public bool record;
    public readonly List<float> samples=new List<float>();
    void Update(){if(record)samples.Add(Time.unscaledDeltaTime*1000);}
}
public static class ModeReturnBenchmark
{
    static object Stats(List<float> frames)
    {var f=frames.OrderBy(x=>x).ToArray();return new{count=f.Length,meanMs=f.Average(),p95Ms=f[(int)((f.Length-1)*.95f)],maxMs=f.Last(),framesBelow50=f.Count(x=>x>20),framesBelow40=f.Count(x=>x>25)};}
    static async Task Ready(string name)
    {float end=Time.realtimeSinceStartup+10;while(SceneManager.GetActiveScene().name!=name||UnityEngine.Object.FindAnyObjectByType<LobbyNavigation>()?.Busy==true){if(Time.realtimeSinceStartup>end)throw new Exception("Scene transition timeout");await Task.Delay(10);}}
    static async Task<object> Sample(ModeReturnProbe probe)
    {probe.samples.Clear();probe.record=true;await Task.Delay(1200);probe.record=false;return Stats(probe.samples);}
    public static async Task<object> Run(string phase="before")
    {
        if(!Application.isPlaying)throw new Exception("Play first");
        var settings=PickleVerse.Settings.GameSettings.Current.Copy();string prefs=PlayerPrefs.GetString(PickleVerse.Settings.GameSettings.PreferenceKey,"");
        var probe=new GameObject("Mode Return Frame Probe").AddComponent<ModeReturnProbe>();UnityEngine.Object.DontDestroyOnLoad(probe.gameObject);
        try
        {
            SceneManager.LoadScene("Menu");await Task.Delay(250);await Ready("Menu");var initial=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();initial.animateAtmosphere=false;initial.ApplyAtmosphere(0);await Task.Delay(400);
            var baseline=await Sample(probe);var results=new List<object>();
            for(int pass=0;pass<3;pass++)
            {
                UnityEngine.Object.FindAnyObjectByType<MenuPlayMultiplayer>().Play();await Ready("ModeSelection");await Task.Delay(350);var modes=await Sample(probe);
                probe.samples.Clear();probe.record=true;UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>().Back();await Ready("Menu");await Task.Delay(650);probe.record=false;
                var transition=Stats(probe.samples);var home=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();home.animateAtmosphere=false;home.ApplyAtmosphere(0);await Task.Delay(300);var returned=await Sample(probe);
                results.Add(new{pass,mode=modes,transition,returned,sameLobby=home==initial,sceneCount=SceneManager.sceneCount});
            }
            var report=new{phase,baseline,results,prefsUnchanged=PlayerPrefs.GetString(PickleVerse.Settings.GameSettings.PreferenceKey,"")==prefs,settings=new{settings.graphics,settings.renderScale,settings.msaa,settings.fps,settings.vsync},scope="Unity Editor, user graphics unchanged; fixed day on both sides for a fair comparison"};
            Directory.CreateDirectory("../UnityWork/ModePerformance");File.WriteAllText("../UnityWork/ModePerformance/"+phase+".json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
        }
        finally{UnityEngine.Object.Destroy(probe.gameObject);SceneManager.LoadScene("Menu");}
    }
}
