using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.LocalMatch;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;

public static class NightLightingValidation
{
    static void Loaded(Scene s,LoadSceneMode mode)
    {if(s.name=="MultiplayerCourt"){var boot=UnityEngine.Object.FindAnyObjectByType<CourtBootstrap>();if(boot)boot.enabled=false;}}
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first");
        var checks=new List<object>();Action<string,bool> check=(name,ok)=>{checks.Add(new{name,passed=ok});if(!ok)throw new Exception(name);};
        var runtime=ProgressionRuntime.Instance;string original=runtime.Player.id,id="NightQA"+Guid.NewGuid().ToString("N").Substring(0,10);
        const string key="PKV.Bot.Night";bool existed=PlayerPrefs.HasKey(key);int preference=PlayerPrefs.GetInt(key,0);string signature=null;
        SceneManager.sceneLoaded+=Loaded;
        try
        {
            runtime.SwitchPlayer(id,"Lighting QA");
            foreach(string name in new[]{"BotMatch","Training","Tutorial","MultiplayerCourt"})
            {
                SceneManager.LoadScene(name);await Task.Delay(500);var l=UnityEngine.Object.FindAnyObjectByType<MatchEnvironmentLighting>();var e=l.stadium;l.Apply(true);
                check(name+" neutral white fixture light",e.poleLights.Concat(e.roofLights).All(x=>x.color==Color.white&&!x.useColorTemperature));
                check(name+" reduced roof and court intensity",e.roofIllumination.All(x=>Mathf.Approximately(x.intensity,80))&&e.poleIllumination.All(x=>Mathf.Approximately(x.intensity,45)));
                check(name+" stands have eight gentle outward lights",e.standIllumination.Length==8&&e.standIllumination.All(x=>x.enabled&&Mathf.Approximately(x.intensity,8)&&x.shadows==LightShadows.None&&x.transform.forward.x*x.transform.position.x>0));
                check(name+" sky, sun and fixture emission agree",RenderSettings.skybox==l.nightSky&&!l.sun.enabled&&e.roofEmitters.All(r=>r.sharedMaterial==e.lightsOn)&&e.lightsOn.GetColor("_EmissionColor")==Color.white*1.2f);
                string profile=JsonConvert.SerializeObject(new{roof=e.roofNightIntensity,pole=e.poleNightIntensity,stands=e.standNightIntensity,sky=RenderSettings.ambientSkyColor.ToString(),equator=RenderSettings.ambientEquatorColor.ToString(),ground=RenderSettings.ambientGroundColor.ToString(),colors=e.poleIllumination.Select(x=>x.color.ToString()).ToArray()});
                if(signature==null)signature=profile;check(name+" uses the same night profile",profile==signature);
                l.Apply(false);check(name+" daytime follows indoor lighting floor",l.sun.enabled&&e.poleIllumination.All(x=>x.enabled)&&e.standIllumination.All(x=>!x.enabled)&&e.roofIllumination.All(x=>x.enabled==(l.minimumCourtIllumination>.025f)&&Mathf.Approximately(x.intensity,e.roofNightIntensity*l.minimumCourtIllumination)));
                if(name=="MultiplayerCourt")
                {
                    check("Multiplayer starts with the shared launch preference",l.useLocalLaunchSettings);
                    var go=new GameObject("Network environment payload QA");var match=go.AddComponent<NetworkMatch>();
                    try
                    {
                        foreach(bool night in new[]{true,false})
                        {
                            PlayerPrefs.SetInt(key,night?0:1);l.SelectForMatch(!night);
                            var source=new MatchRoster{matchId="lighting-qa",mode=PickleVerse.Multiplayer.PlayMode.Singles,custom=true};
                            var received=JsonConvert.DeserializeObject<MatchRoster>(JsonConvert.SerializeObject(source.WithEnvironment(night)));
                            typeof(NetworkMatch).GetProperty("Roster").GetSetMethod(true).Invoke(match,new object[]{received});
                            typeof(NetworkMatch).GetMethod("ApplyEnvironment",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(match,null);
                            check("Host snapshot overrides opposite guest preference: "+night,l.IsNight==night&&e.nightWeight==(night?1:0)&&RenderSettings.skybox==(night?l.nightSky:l.daySky));
                        }
                    }
                    finally{UnityEngine.Object.Destroy(go);}
                }
            }
            var report=new{passed=true,count=checks.Count,checks,scope="All four shared scene profiles, delivered roster JSON through NetworkMatch environment callback; no live online transport."};
            File.WriteAllText("../UnityWork/NightLighting/runtime-validation.json",JsonConvert.SerializeObject(report,Formatting.Indented));return report;
        }
        finally
        {
            if(existed)PlayerPrefs.SetInt(key,preference);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();
            SceneManager.sceneLoaded-=Loaded;SceneManager.LoadScene("Menu");await Task.Delay(200);runtime.SwitchPlayer(original,null);
            foreach(var suffix in new[]{".json",".json.bak",".lock"}){string file=Path.Combine(Application.persistentDataPath,"LocalPlayers",id+suffix);if(File.Exists(file))File.Delete(file);}
        }
    }
}
