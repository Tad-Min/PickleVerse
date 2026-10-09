using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PickleVerse.Settings;
using PickleVerse.LocalMatch;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class SettingsValidation
{
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool passed){checks.Add(new{name,passed});if(!passed)throw new InvalidOperationException(name);}
    static async Task Wait(Func<bool> ready)
    {
        var until=DateTime.UtcNow.AddSeconds(10);
        while(!ready()){if(DateTime.UtcNow>until)throw new TimeoutException("Settings state timed out.");await Task.Delay(40);}
    }
    public static async Task<object> Run()
    {
        checks.Clear();var saved=GameSettings.Current.Copy();var stored=PlayerPrefs.GetString(GameSettings.PreferenceKey,"");
        var m=UnityEngine.Object.FindAnyObjectByType<BotMatch>();
        if(!Application.isPlaying||m==null)throw new InvalidOperationException("Play BotMatch first.");
        var keyboard=InputSystem.AddDevice<Keyboard>();
        try
        {
            if(!SettingsOverlay.IsOpen){m.Restart();m.hud.OpenSettings();}
            await Wait(()=>UnityEngine.Object.FindAnyObjectByType<SettingsScreen>()!=null&&!SettingsOverlay.Loading);await Task.Delay(200);
            var ui=UnityEngine.Object.FindAnyObjectByType<SettingsScreen>();
            Check("Settings loaded additively",SceneManager.sceneCount==2&&SceneManager.GetActiveScene().name=="BotMatch");
            Check("Bot paused and context actions correct",m.Paused&&ui.resumeButton.gameObject.activeSelf&&ui.leaveButton.gameObject.activeSelf&&!ui.signOutButton.gameObject.activeSelf);
            Check("Underlying HUD covered",!m.hud.GetComponent<Canvas>().enabled&&!m.hud.modal.activeSelf);
            float serve=m.ServeElapsed;var ball=m.BallPosition;await Task.Delay(300);
            Check("Ball and serve clock held",m.ServeElapsed==serve&&m.BallPosition==ball);
            Check("No extra runtime camera or EventSystem",UnityEngine.Object.FindObjectsByType<Camera>().Length==1&&UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Length==1);
            ui.ShowTab(2);ui.presets[0].onClick.Invoke();
            var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Check("Graphics preset changes runtime pipeline",pipeline&&Mathf.Abs(pipeline.renderScale-.7f)<.001f&&pipeline.msaaSampleCount==1);
            ui.shadowToggle.isOn=false;
            Check("Shadow control applies",pipeline.shadowDistance==0);
            ui.SetFps(30);Check("FPS selection applies",Application.targetFrameRate==30);
            ui.Discard();Check("Discard restores saved graphics",Mathf.Abs(pipeline.renderScale-saved.renderScale)<.001f);
            ui.ShowTab(3);ui.Keyboard();ui.BeginBinding((int)GameAction.Drive);
            Check("Conflicting key rejected",!ui.TryAssignKey(ui.Draft.KeyFor(GameAction.Dink))&&ui.Capturing);
            Check("Esc reserved",!ui.TryAssignKey(Key.Escape));
            Check("New Drive key accepted",ui.TryAssignKey(Key.F)&&ui.Draft.KeyFor(GameAction.Drive)==Key.F);
            ui.Touch();var old=ui.Draft.touchPositions[1];ui.MoveTouch(1,new Vector2(.68f,.28f));
            Check("Touch layout position editable",ui.Draft.touchPositions[1]!=old);
            ui.sliders[4].value=1.2f;ui.sliders[5].value=.65f;ui.leftToggle.isOn=true;
            Check("Touch size opacity and handedness editable",Mathf.Abs(ui.Draft.touchScale-1.2f)<.001f&&Mathf.Abs(ui.Draft.touchOpacity-.65f)<.001f&&ui.Draft.leftHanded);
            ui.sliders[0].value=.4f;ui.sliders[1].value=.3f;
            Check("Master audio preview applies",Mathf.Abs(AudioListener.volume-.4f)<.001f);
            ui.Apply();GameSettings.Reload();
            Check("Settings persisted",GameSettings.Current.KeyFor(GameAction.Drive)==Key.F&&GameSettings.Current.leftHanded&&Mathf.Abs(GameSettings.Current.music-.3f)<.001f);
            InputState.Change(keyboard,new KeyboardState(Key.F,Key.K));
            Check("Rebound physical keys produce Drop mask",GameControls.ShotMask==3&&GameControls.ShotKeys(3).Contains("F"));
            InputState.Change(keyboard,new KeyboardState());
            ui.Resume();await Wait(()=>!SettingsOverlay.IsOpen&&!SettingsOverlay.Loading);
            Check("Closing resumes same match and HUD",!m.Paused&&m.hud.GetComponent<Canvas>().enabled&&m.Rules.Points[0]==0);
            m.hud.ToggleTouch();await Task.Delay(100);
            var touch=m.hud.touchControls.GetComponentsInChildren<TouchControl>(true).Single(t=>t.shotMask==1&&!t.joystick);
            Check("Touch layout applies to gameplay",Mathf.Abs(touch.GetComponent<CanvasGroup>().alpha-.65f)<.001f);
            m.SetPaused(true);m.hud.OpenSettings();await Wait(()=>UnityEngine.Object.FindAnyObjectByType<SettingsScreen>()!=null&&!SettingsOverlay.Loading);
            ui=UnityEngine.Object.FindAnyObjectByType<SettingsScreen>();ui.RequestClose();await Wait(()=>!SettingsOverlay.IsOpen&&!SettingsOverlay.Loading);
            Check("Pre-existing pause preserved on close",m.Paused);m.SetPaused(false);
            m.hud.OpenSettings();await Wait(()=>UnityEngine.Object.FindAnyObjectByType<SettingsScreen>()!=null&&!SettingsOverlay.Loading);
            ui=UnityEngine.Object.FindAnyObjectByType<SettingsScreen>();ui.LeaveMatch();Check("Leave asks confirmation",ui.confirmOverlay.activeSelf);ui.CancelConfirm();
            Check("Cancel leaves game intact",SceneManager.GetActiveScene().name=="BotMatch");ui.LeaveMatch();ui.AcceptConfirm();
            await Wait(()=>SceneManager.GetActiveScene().name=="Menu");await Task.Delay(250);
            Check("Leave returns to lobby",!SettingsOverlay.IsOpen);
            var button=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include).Single(b=>b.name=="BtnSetting");button.onClick.Invoke();
            await Wait(()=>UnityEngine.Object.FindAnyObjectByType<SettingsScreen>()!=null&&!SettingsOverlay.Loading);ui=UnityEngine.Object.FindAnyObjectByType<SettingsScreen>();
            Check("Lobby context has logout not match actions",ui.signOutButton.gameObject.activeSelf&&!ui.leaveButton.gameObject.activeSelf&&!ui.resumeButton.gameObject.activeSelf);
            ui.ShowTab(1);Check("Audio controls available",ui.sliders.Take(3).All(s=>s.gameObject.activeInHierarchy));
            ui.ShowTab(3);ui.Keyboard();Check("PC bindings available",ui.bindingButtons.Length==15&&!ui.bindingButtons[14].interactable);
            ui.Touch();Check("Mobile layout has nine controls",ui.touchHandles.Length==9);
            Check("Settings UI no Missing Script",ui.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Sum(t=>UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))==0);
            var result=new{passed=true,count=checks.Count,checks=checks.ToArray()};Directory.CreateDirectory("../UnityWork/Settings");File.WriteAllText("../UnityWork/Settings/validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);GameSettings.Commit(saved);
            if(stored=="")PlayerPrefs.DeleteKey(GameSettings.PreferenceKey);else PlayerPrefs.SetString(GameSettings.PreferenceKey,stored);PlayerPrefs.Save();
        }
    }
}
