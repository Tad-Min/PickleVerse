using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using PickleVerse.Multiplayer;
using PlayMode=PickleVerse.Multiplayer.PlayMode;
public static class MultiplayerFinalValidation
{
    public static object Assets()
    {
        var result=new List<object>();
        foreach(var path in new[]{"Assets/Scenes/MultiplayerTest.unity","Assets/Scenes/MultiplayerCourt.unity"})
        {
            var scene=SceneManager.GetSceneByPath(path); bool existing=scene.IsValid()&&scene.isLoaded;
            if(!existing) scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            int missing=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            if(missing>0) throw new Exception(path+" contains Missing Script");
            result.Add(new{path,missing}); if(!existing) EditorSceneManager.CloseScene(scene,true);
        }
        var settings=Resources.Load<MultiplayerSettings>("MultiplayerSettings");
        if(settings==null||settings.playerPrefab==null||settings.matchPrefab==null||settings.paddlePrefab==null||settings.maleModel==null||settings.femaleModel==null||settings.ballMaterial==null||settings.animations==null) throw new Exception("Missing multiplayer asset reference");
        foreach(var prefab in new[]{settings.playerPrefab,settings.matchPrefab})
        {
            int missing=prefab.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            if(missing>0) throw new Exception(prefab.name+" Missing Script"); result.Add(new{prefab=prefab.name,missing});
        }
        foreach(var controller in new[]{settings.maleAnimator,settings.femaleAnimator})
        {
            var animator=(UnityEditor.Animations.AnimatorController)controller;
            if(animator.layers[0].stateMachine.defaultState.name!="Idle_Ready") throw new Exception("Default animator is not Idle_Ready");
            result.Add(new{controller=controller.name,initial="Idle_Ready"});
        }
        string menuPath="Assets/Scenes/HomeScene/Menu.unity";
        var menu=SceneManager.GetSceneByPath(menuPath); bool menuExisting=menu.IsValid()&&menu.isLoaded;
        if(!menuExisting) menu=EditorSceneManager.OpenScene(menuPath,OpenSceneMode.Additive);
        var button=menu.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UnityEngine.UI.Button>(true)).First(b=>b.name=="BtnPlay");
        var calls=Enumerable.Range(0,button.onClick.GetPersistentEventCount()).Select(i=>new{target=button.onClick.GetPersistentTarget(i)?.GetType().Name,method=button.onClick.GetPersistentMethodName(i)}).ToArray();
        if(!calls.Any(c=>c.target=="MenuPlayMultiplayer"&&c.method=="Play")) throw new Exception("BtnPlay is not wired");
        result.Add(new{button="BtnPlay",calls}); if(!menuExisting) EditorSceneManager.CloseScene(menu,true);
        return result;
    }
    public static async Task<object> MenuDefault()
    {
        if(!Application.isPlaying) throw new Exception("Enter Play Mode first");
        var app=MultiplayerApp.Ensure(); await app.PlayAsync("EditorMenuValidation"); await app.SelectModeAsync(PlayMode.Doubles);
        var handler=app.gameObject.AddComponent<MenuPlayMultiplayer>(); handler.Play();
        await Task.Delay(4000);
        if(app.State!=FlowState.WaitingRoom||app.Mode!=PlayMode.Singles||app.ActiveProfile!="EditorMenuValidation") throw new Exception("Menu Play did not reset to 1vs1 with the same profile");
        UnityEngine.Object.Destroy(handler);
        return new{passed=true,mode=app.Mode.ToString(),app.ActiveProfile};
    }
    public static async Task<object> Timeout()
    {
        var app=MultiplayerApp.Ensure(); await app.PlayAsync("EditorTimeoutValidation");
        await app.FindAsync(); double began=Time.realtimeSinceStartupAsDouble;
        while(app.Searching && Time.realtimeSinceStartupAsDouble-began<135) await Task.Delay(100);
        double elapsed=Time.realtimeSinceStartupAsDouble-began;
        if(app.State!=FlowState.WaitingRoom || elapsed<125.9 || elapsed>130 || !app.Notice.Contains("thời gian")) throw new Exception("Timeout incorrect: "+elapsed+" / "+app.Notice);
        System.IO.File.WriteAllText("Docs/MultiplayerTimeoutTest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=true,elapsed,app.Notice}));
        return new{passed=true,elapsed,app.Notice};
    }
    public static string BeginTimeout() { _=CaptureTimeout(); return "Timeout check running; result in Docs/MultiplayerTimeoutTest.json"; }
    static async Task CaptureTimeout()
    {
        try { await Timeout(); }
        catch(Exception e) { System.IO.File.WriteAllText("Docs/MultiplayerTimeoutTest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=false,error=e.Message})); }
    }
}
