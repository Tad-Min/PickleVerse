using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using PickleVerse.LocalMatch;
using PickleVerse.Multiplayer;
using Newtonsoft.Json;

// Exercise the actual received-roster lighting callback without creating cloud sessions.
public static class OnlineCourtLightingValidation
{
 const string Folder="../UnityWork/OnlineCourtLighting20261016";
 static readonly List<object> checks=new List<object>();
 static void Check(string name,bool passed){checks.Add(new{name,passed});if(!passed)throw new Exception(name);}
 static void Prepare(Scene scene,LoadSceneMode mode){if(scene.name=="MultiplayerCourt")foreach(var bootstrap in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CourtBootstrap>(true)))bootstrap.enabled=false;}
 static async Task Capture(Camera camera,string name)
 {
  // Compare completed GameView frames using the active runtime renderer.
  await Task.Delay(1200);ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder+"/"+name+".png"));await Task.Delay(500);
 }
 public static async Task<object> Run()
 {
  if(!Application.isPlaying)throw new Exception("Play first");checks.Clear();Directory.CreateDirectory(Folder);string originalScene=SceneManager.GetActiveScene().name;
  SceneManager.sceneLoaded+=Prepare;RenderPipelineAsset original=QualitySettings.renderPipeline;UniversalRenderPipelineAsset mobile=null;GameObject probe=null;
  try
  {
   SceneManager.LoadScene("MultiplayerCourt");await Task.Delay(700);var l=UnityEngine.Object.FindAnyObjectByType<MatchEnvironmentLighting>();var e=l.stadium;int lights=e.GetComponentsInChildren<Light>(true).Length;
   Check("Online scene has an indoor lighting floor",Mathf.Approximately(l.minimumCourtIllumination,.65f));
   Check("Existing fixture budget preserved",e.roofIllumination.Length==16&&e.poleIllumination.Length==8&&e.GetComponentsInChildren<Light>(true).Length==166);
   l.matchCamera.transform.SetPositionAndRotation(new Vector3(0,5.3f,-10.4f),Quaternion.Euler(31,0,0));l.matchCamera.fieldOfView=60;
   foreach(var canvas in l.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)))canvas.enabled=false;
   probe=new GameObject("Received roster lighting probe");var match=probe.AddComponent<NetworkMatch>();
   mobile=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset"));
   foreach(string pipeline in new[]{"Desktop","Mobile"})
   {
    QualitySettings.renderPipeline=pipeline=="Desktop"?original:mobile;
    await Task.Delay(600);
    foreach(bool night in new[]{false,true})
    {
     l.SelectForMatch(!night);var payload=JsonConvert.DeserializeObject<MatchRoster>(JsonConvert.SerializeObject(new MatchRoster{matchId="court-light-qa",mode=PickleVerse.Multiplayer.PlayMode.Singles,custom=true}.WithEnvironment(night)));
     typeof(NetworkMatch).GetProperty("Roster").GetSetMethod(true).Invoke(match,new object[]{payload});typeof(NetworkMatch).GetMethod("ApplyEnvironment",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(match,null);
     string tag=pipeline+(night?" night":" day");
     Check(tag+" host time of day applied",l.IsNight==night&&e.nightWeight==(night?1:0)&&RenderSettings.skybox==(night?l.nightSky:l.daySky));
     Check(tag+" roof spotlights illuminate the court",e.roofIllumination.All(x=>x.isActiveAndEnabled&&x.type==LightType.Spot&&x.intensity>=52-.01f&&x.shadows==LightShadows.None));
     Check(tag+" neutral white light",e.roofIllumination.Concat(e.poleIllumination).All(x=>x.color==Color.white&&!x.useColorTemperature));
     Check(tag+" sun and stand lights follow sky",l.sun.enabled==!night&&e.standIllumination.All(x=>x.enabled==night));
     Check(tag+" fixture beams reach centre court",e.roofIllumination.All(x=>{var target=new Vector3(0,0,0)-x.transform.position;return target.magnitude<x.range&&Vector3.Angle(target,x.transform.forward)<x.spotAngle*.5f;}));
     await Capture(l.matchCamera,"runtime-"+pipeline.ToLowerInvariant()+"-"+(night?"night":"day"));
    }
   }
   e.ApplyNight(0,true,0);Check("Default day profile remains available",e.roofIllumination.All(x=>!x.enabled));e.ApplyNight(0,false,.65f);Check("Changing only indoor floor invalidates lighting cache",e.roofIllumination.All(x=>x.enabled&&Mathf.Approximately(x.intensity,52)));
   e.ApplyNight(1,true,.65f);Check("Night intensity stays at the tuned level",e.roofIllumination.All(x=>Mathf.Approximately(x.intensity,80))&&e.poleIllumination.All(x=>Mathf.Approximately(x.intensity,45)));
   Check("No extra lights allocated during transitions",e.GetComponentsInChildren<Light>(true).Length==lights);
   var report=new{passed=true,count=checks.Count,checks,scope="Received roster JSON through NetworkMatch.ApplyEnvironment on desktop and mobile renderers; no live transport/cloud sessions"};File.WriteAllText(Folder+"/runtime-validation.json",JsonConvert.SerializeObject(report,Formatting.Indented));return new{passed=true,count=checks.Count,renderers=2,cloudSessionsCreated=0};
  }
  finally
  {
   QualitySettings.renderPipeline=original;if(mobile)UnityEngine.Object.Destroy(mobile);if(probe)UnityEngine.Object.Destroy(probe);SceneManager.sceneLoaded-=Prepare;SceneManager.LoadScene(originalScene);
   File.WriteAllText(Folder+"/checks.json",JsonConvert.SerializeObject(checks,Formatting.Indented));
  }
 }
}
