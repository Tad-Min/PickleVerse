using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using PickleVerse.LocalMatch;
public static class OnlineCourtLightingFix
{
 const string Folder="../UnityWork/OnlineCourtLighting20261016";
 public static object Apply()
 {
  if(EditorApplication.isPlaying)throw new Exception("Stop Play first");Directory.CreateDirectory(Folder);string original=SceneManager.GetActiveScene().path;
  try
  {
   var scene=EditorSceneManager.OpenScene("Assets/Scenes/MultiplayerCourt.unity");
   if(!File.Exists(Folder+"/MultiplayerCourt-before.unity"))EditorSceneManager.SaveScene(scene,Folder+"/MultiplayerCourt-before.unity",true);
   var l=UnityEngine.Object.FindAnyObjectByType<MatchEnvironmentLighting>();l.minimumCourtIllumination=.65f;
   foreach(var light in l.stadium.poleIllumination.Concat(l.stadium.roofIllumination)){light.color=Color.white;light.useColorTemperature=false;EditorUtility.SetDirty(light);PrefabUtility.RecordPrefabInstancePropertyModifications(light);}
   l.Apply(l.selectedNight);EditorUtility.SetDirty(l);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   return new{scene=scene.path,l.minimumCourtIllumination,roofSources=l.stadium.roofIllumination.Length,poleSources=l.stadium.poleIllumination.Length,additionalLightsCreated=0};
  }
  finally{EditorSceneManager.OpenScene(original);}
 }
 public static object Capture(string name="before",bool night=true)
 {
  if(EditorApplication.isPlaying)throw new Exception("Stop Play first");Directory.CreateDirectory(Folder);string original=SceneManager.GetActiveScene().path;
  try
  {
   var scene=EditorSceneManager.OpenScene("Assets/Scenes/MultiplayerCourt.unity");var l=UnityEngine.Object.FindAnyObjectByType<MatchEnvironmentLighting>();l.SelectForMatch(night);var camera=l.matchCamera;camera.transform.position=new Vector3(0,5.3f,-10.4f);camera.transform.rotation=Quaternion.Euler(31,0,0);camera.fieldOfView=60;
   foreach(var canvas in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)))canvas.enabled=false;
   var target=RenderTexture.GetTemporary(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);var active=RenderTexture.active;var previous=camera.targetTexture;
   try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Folder+"/"+name+".png",image.EncodeToPNG());}
   finally{camera.targetTexture=previous;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(target);}
   return new{path=Path.GetFullPath(Folder+"/"+name+".png"),enabledLights=l.stadium.GetComponentsInChildren<Light>().Count(x=>x.isActiveAndEnabled),surfaceShader=l.stadium.competitionSurface.GetComponent<Renderer>().sharedMaterial.shader.name};
  }
  finally{EditorSceneManager.OpenScene(original);}
 }
 public static object Audit()
 {
  if(EditorApplication.isPlaying)throw new Exception("Stop Play first");if(SceneManager.GetActiveScene().isDirty)throw new Exception("Current scene has unsaved edits");Directory.CreateDirectory(Folder);string original=SceneManager.GetActiveScene().path;var rows=new List<object>();
  try
  {
   foreach(string name in new[]{"BotMatch","MultiplayerCourt"})
   {
    var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity");var l=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MatchEnvironmentLighting>(true)).ToArray();var env=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<StadiumEnvironment>(true)).ToArray();
    rows.Add(new{scene=name,controllers=l.Select(x=>new{x.name,active=x.isActiveAndEnabled,stadium=x.stadium?x.stadium.name:null,x.selectedNight,x.useLocalLaunchSettings,camera=x.matchCamera?x.matchCamera.name:null,sun=x.sun?new{x.sun.enabled,x.sun.intensity,color=x.sun.color.ToString()}:null}).ToArray(),stadiums=env.Select(e=>new{e.name,active=e.isActiveAndEnabled,position=e.transform.position.ToString(),scale=e.transform.lossyScale.ToString(),e.roofNightIntensity,e.poleNightIntensity,e.standNightIntensity,poles=e.poleLights?.Length??0,roofs=e.roofLights?.Length??0,activePoles=e.poleIllumination?.Count(x=>x)??0,activeRoofs=e.roofIllumination?.Count(x=>x)??0,lights=e.GetComponentsInChildren<Light>(true).Take(5).Select(x=>new{x.name,x.enabled,active=x.gameObject.activeInHierarchy,type=x.type.ToString(),bake=x.lightmapBakeType.ToString(),x.intensity,x.range,x.spotAngle,x.cullingMask,layers=x.renderingLayerMask,color=x.color.ToString(),position=x.transform.position.ToString(),forward=x.transform.forward.ToString()}).ToArray(),surface=e.competitionSurface?new{position=e.competitionSurface.transform.position.ToString(),bounds=e.competitionSurface.GetComponent<Renderer>().bounds.ToString(),layer=e.competitionSurface.gameObject.layer,renderingLayer=e.competitionSurface.GetComponent<Renderer>().renderingLayerMask}:null}).ToArray()});
   }
   File.WriteAllText(Folder+"/audit-before.json",Newtonsoft.Json.JsonConvert.SerializeObject(rows,Newtonsoft.Json.Formatting.Indented));return rows;
  }
  finally{EditorSceneManager.OpenScene(original);}
 }
}
