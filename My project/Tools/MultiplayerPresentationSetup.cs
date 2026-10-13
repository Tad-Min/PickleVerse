using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PickleVerse.LocalMatch;
using PickleVerse.Multiplayer;

public static class MultiplayerPresentationSetup
{
    public static object Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        var botScene=EditorSceneManager.OpenScene("Assets/Scenes/BotMatch.unity");
        var source=UnityEngine.Object.FindAnyObjectByType<MatchEnvironmentLighting>();
        var mpScene=EditorSceneManager.OpenScene("Assets/Scenes/MultiplayerCourt.unity",OpenSceneMode.Additive);
        var roots=mpScene.GetRootGameObjects();
        var lighting=roots.SelectMany(r=>r.GetComponentsInChildren<MatchEnvironmentLighting>(true)).Single();
        var stadium=roots.SelectMany(r=>r.GetComponentsInChildren<StadiumEnvironment>(true)).Single();
        var ui=roots.SelectMany(r=>r.GetComponentsInChildren<MultiplayerCourtUI>(true)).Single(x=>x.gameObject.activeInHierarchy);
        var cinematic=ui.GetComponent<MultiplayerCinematic>();if(!cinematic)cinematic=ui.gameObject.AddComponent<MultiplayerCinematic>();ui.cinematic=cinematic;
        AnimationClip Clip(string sex,string file)=>AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/LocalMatchContent/Clips/"+sex+"/"+file+".anim");
        cinematic.femaleWalk=Clip("female","Entrance_Walk");cinematic.maleWalk=Clip("male","Entrance_Walk");
        cinematic.femaleWin=Clip("female","PKV_Dance_Win");cinematic.maleSuperSaiyan=Clip("male","PKV_Dance_SuperSaiyan");cinematic.maleClap=Clip("male","PKV_Dance_Clap");
        if(!cinematic.femaleWalk||!cinematic.maleWalk||!cinematic.femaleWin||!cinematic.maleSuperSaiyan||!cinematic.maleClap)throw new Exception("Missing animation");
        lighting.stadium=stadium;lighting.daySky=source.daySky;lighting.nightSky=source.nightSky;lighting.matchCamera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).First(c=>c.CompareTag("MainCamera"));
        var sun=lighting.sun;sun.color=source.sun.color;sun.intensity=source.sun.intensity;sun.shadows=source.sun.shadows;sun.shadowStrength=source.sun.shadowStrength;sun.shadowBias=source.sun.shadowBias;sun.shadowNormalBias=source.sun.shadowNormalBias;
        sun.useColorTemperature=source.sun.useColorTemperature;sun.colorTemperature=source.sun.colorTemperature;sun.transform.rotation=source.sun.transform.rotation;
        stadium.roofNightIntensity=source.stadium.roofNightIntensity;stadium.poleNightIntensity=source.stadium.poleNightIntensity;stadium.standNightIntensity=source.stadium.standNightIntensity;
        lighting.floodlights=Array.Empty<Light>();lighting.lampFaces=Array.Empty<Renderer>();lighting.useLocalLaunchSettings=true;lighting.Apply(lighting.selectedNight);
        foreach(var component in new UnityEngine.Object[]{ui,cinematic,lighting,stadium,sun}){EditorUtility.SetDirty(component);PrefabUtility.RecordPrefabInstancePropertyModifications(component);}
        EditorSceneManager.MarkSceneDirty(mpScene);EditorSceneManager.SaveScene(mpScene);
        EditorSceneManager.CloseScene(botScene,true);EditorSceneManager.SetActiveScene(mpScene);
        return new{scene=mpScene.path,cinematic=true,clips=5,poles=stadium.poleIllumination.Length,roofs=stadium.roofIllumination.Length,stands=stadium.standIllumination.Length,sourceLighting="BotMatch"};
    }
}

