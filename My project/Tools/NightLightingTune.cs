using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PickleVerse.LocalMatch;

public static class NightLightingTune
{
    const string Folder="../UnityWork/NightLighting";
    public static object Capture(string name="BotMatch",bool overview=false)
    {
        Directory.CreateDirectory(Folder);
        EditorSceneManager.OpenScene(name=="Menu"?"Assets/Scenes/UI/Menu.unity":"Assets/Scenes/"+name+".unity");
        var lighting=UnityEngine.Object.FindAnyObjectByType<MatchEnvironmentLighting>();if(lighting)lighting.Apply(true);else UnityEngine.Object.FindAnyObjectByType<PickleVerse.FrontEnd.HomeLobbyPresentation>().ApplyAtmosphere(1);
        foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>())canvas.gameObject.SetActive(false);
        var camera=UnityEngine.Object.FindObjectsByType<Camera>().First(c=>c.enabled);
        if(overview){camera.transform.position=new Vector3(-3,17,-9);camera.transform.LookAt(new Vector3(0,1,0));camera.fieldOfView=80;}
        else if(name!="Menu"){camera.transform.position=new Vector3(0,5.3f,-10.4f);camera.transform.rotation=Quaternion.Euler(31,0,0);camera.fieldOfView=60;}
        var target=RenderTexture.GetTemporary(1280,720,24);var img=new Texture2D(1280,720,TextureFormat.RGB24,false);var active=RenderTexture.active;var previous=camera.targetTexture;
        try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;img.ReadPixels(new Rect(0,0,1280,720),0,0);img.Apply();var path=Folder+"/"+name+(overview?"-overview":"-court")+".png";File.WriteAllBytes(path,img.EncodeToPNG());return path;}
        finally{camera.targetTexture=previous;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(img);RenderTexture.ReleaseTemporary(target);}
    }
    public static object Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        const string prefab="Assets/LocalMatchContent/StadiumEnhanced/PlayableStadium.prefab";
        var root=PrefabUtility.LoadPrefabContents(prefab);
        int stands;
        try
        {
            var env=root.GetComponent<StadiumEnvironment>();env.ConfigureFixtureGroups();env.roofNightIntensity=80;env.poleNightIntensity=45;env.standNightIntensity=8;
            env.lightsOn.color=new Color(.94f,.94f,.94f);env.lightsOn.SetColor("_EmissionColor",Color.white*1.2f);EditorUtility.SetDirty(env.lightsOn);AssetDatabase.SaveAssetIfDirty(env.lightsOn);
            env.ApplyNight(0);stands=env.standIllumination.Length;PrefabUtility.SaveAsPrefabAsset(root,prefab);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/MultiplayerCourt.unity");var lighting=UnityEngine.Object.FindAnyObjectByType<MatchEnvironmentLighting>();lighting.useLocalLaunchSettings=true;lighting.Apply(lighting.selectedNight);EditorUtility.SetDirty(lighting);EditorSceneManager.SaveScene(scene);
        EditorSceneManager.OpenScene("Assets/Scenes/UI/Menu.unity");
        return new{roofIntensity=80,poleIntensity=45,standIntensity=8,standSources=stands,whiteLights=true};
    }
    public static object Survey()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play before survey");Directory.CreateDirectory(Folder);var rows=new List<object>();
        foreach(string name in new[]{"Menu","BotMatch","Training","Tutorial","MultiplayerCourt"})
        {
            var scene=EditorSceneManager.OpenScene(name=="Menu"?"Assets/Scenes/UI/Menu.unity":"Assets/Scenes/"+name+".unity");var env=UnityEngine.Object.FindAnyObjectByType<StadiumEnvironment>();var l=UnityEngine.Object.FindAnyObjectByType<MatchEnvironmentLighting>();
            rows.Add(new{scene=name,lighting=l?new {l.useLocalLaunchSettings,l.selectedNight,root=l.name,sun=l.sun?l.sun.name:null,day=l.daySky?l.daySky.name:null,night=l.nightSky?l.nightSky.name:null,stadium=l.stadium==env}:null,poles=env.poleIllumination.Select(x=>new{x.name,color=x.color.ToString(),x.intensity,x.useColorTemperature,temperature=x.colorTemperature,position=x.transform.position.ToString(),target=x.transform.forward.ToString()}).ToArray(),roof=env.roofNightIntensity});
        }
        File.WriteAllText(Folder+"/before.json",Newtonsoft.Json.JsonConvert.SerializeObject(rows,Newtonsoft.Json.Formatting.Indented));EditorSceneManager.OpenScene("Assets/Scenes/UI/Menu.unity");return rows;
    }
}
