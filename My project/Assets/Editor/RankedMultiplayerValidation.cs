using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class RankedMultiplayerValidation
{
    public static string Status="Idle";
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool passed)
    { checks.Add(new {name,passed}); if(!passed) throw new InvalidOperationException(name); }
    static async Task Wait(Func<bool> ready,string message,double seconds=35)
    { double until=Time.realtimeSinceStartupAsDouble+seconds; while(!ready() && Time.realtimeSinceStartupAsDouble<until) await Task.Delay(50); Check(message,ready()); }
    static void Render(MultiplayerCourtUI ui)
    {
        Canvas.ForceUpdateCanvases(); var camera=ui.GetComponent<Canvas>().worldCamera;
        var target=RenderTexture.GetTemporary(Screen.width,Screen.height,24); var previous=camera.targetTexture;
        try { camera.targetTexture=target; camera.Render(); }
        finally { camera.targetTexture=previous; RenderTexture.ReleaseTemporary(target); }
    }
    public static async Task Run()
    {
        Status="Running"; checks.Clear(); var runtime=ProgressionRuntime.Instance; string original=runtime.Player.id;
        string id="P6EDIT"+Guid.NewGuid().ToString("N").Substring(0,16);
        var inputBehavior=InputSystem.settings.editorInputBehaviorInPlayMode; var background=InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        var app=MultiplayerApp.Ensure();
        try
        {
            Check("Play started",Application.isPlaying);
            runtime.SwitchPlayer(id,"TEST PLAYER"); runtime.Player.elo=1990; runtime.Save();
            await app.PlayAsync(id); await app.FindAsync();
            // Exercise the real deadline branch without spending six minutes in the Editor; Windows smoke waits the real duration.
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            ((RankedSearchGate)typeof(MultiplayerApp).GetField("rankedSearch",flags).GetValue(app)).Start(Time.realtimeSinceStartupAsDouble-360);
            await Wait(()=>app.State==FlowState.InMatch,"Deadline launches multiplayer court");
            var match=NetworkMatch.Instance; var ui=MultiplayerCourtUI.Instance;
            Check("Bot uses MultiplayerCourt",UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="MultiplayerCourt" && UnityEngine.Object.FindAnyObjectByType<PickleVerse.LocalMatch.BotMatch>()==null);
            Check("One human and one named bot",app.Roster.HumanCount==1 && NetworkPlayer.All.Count==2 && app.Roster.members.Count(m=>m.bot && !string.IsNullOrEmpty(m.name))==1);
            Check("Local control is human despite server ownership",NetworkPlayer.All.Count(p=>p.IsOwner)==2 && NetworkPlayer.All.Count(p=>p.IsLocalHuman)==1);
            var human=NetworkPlayer.All.Single(p=>p.IsLocalHuman);
            Check("Camera follows human",Camera.main.GetComponent<CourtCamera>().target==human.transform);
            Check("Bot Elo and rank progression context",runtime.Player.pendingMatch.kind==MatchKind.RankedBot && app.Roster.members.Single(m=>m.bot).elo==2110);
            match.Rules.Forfeit(1,"Kiểm tra tổng kết sau trận"); typeof(NetworkMatch).GetMethod("Finish",flags).Invoke(match,null);
            await Wait(()=>ui.resultScreen.IsVisible && ui.resultScreen.RenderedMatchId==app.Roster.matchId,"Summary opens automatically after saved result");
            var result=runtime.Player.history.First(); var summary=ui.resultScreen;
            Check("Summary shows actual Elo gain and promoted rank",result.eloDelta==60 && runtime.Player.elo==2050 && summary.delta.text=="+60 ELO" && summary.rank.text.Contains("BẠCH KIM") && summary.elo.text.Contains(1990.ToString("N0")));
            Check("Summary opponent uses bot name",summary.opponent.text.Contains(app.Roster.members.Single(m=>m.bot).name));
            Check("Summary blocks gameplay",ui.InputBlocked);
            foreach(var resolution in new[]{new[]{1280,720},new[]{1920,1080},new[]{1024,768},new[]{2560,1080}})
            {
                ProgressionUiValidation.Resolution(resolution[0],resolution[1]); await Task.Delay(400); Render(ui);
                var labels=summary.panel.GetComponentsInChildren<TMP_Text>(true); foreach(var label in labels) label.ForceMeshUpdate();
                Check("Summary labels fit "+Screen.width+"x"+Screen.height,labels.All(l=>!l.isTextOverflowing && l.font.HasCharacters(new string(l.GetParsedText().Where(c=>!char.IsControl(c)).ToArray()))));
                var rect=(RectTransform)summary.safeArea.Find("Card"); var corners=new Vector3[4]; rect.GetWorldCorners(corners);
                var camera=ui.GetComponent<Canvas>().worldCamera;
                Check("Summary safe bounds "+Screen.width+"x"+Screen.height,corners.Select(p=>RectTransformUtility.WorldToScreenPoint(camera,p)).All(p=>Screen.safeArea.Contains(p)));
                var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) {position=new Vector2(5,5)},hits);
                Check("Summary blocks background pointer "+Screen.width+"x"+Screen.height,hits.Count>0 && hits[0].gameObject.transform.IsChildOf(summary.panel));
            }
            ProgressionUiValidation.Resolution(1280,720); await Task.Delay(400); Render(ui);
            var button=(RectTransform)summary.roomButton.transform; var point=RectTransformUtility.WorldToScreenPoint(ui.GetComponent<Canvas>().worldCamera,button.TransformPoint(button.rect.center));
            var mouse=Mouse.current??InputSystem.AddDevice<Mouse>(); InputSystem.QueueStateEvent(mouse,new MouseState {position=point}); await Task.Delay(100);
            InputSystem.QueueStateEvent(mouse,new MouseState {position=point,buttons=1}); await Task.Delay(100); InputSystem.QueueStateEvent(mouse,new MouseState {position=point});
            await Wait(()=>app.State==FlowState.WaitingRoom && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="PlayMultiplayer","Summary pointer click returns to waiting room");
            Check("Result is still applied once",runtime.Player.elo==2050 && runtime.Player.history.Count==1);
            Status="Passed "+checks.Count+" multiplayer UI checks";
        }
        catch(Exception e) { Status="FAILED: "+e.GetBaseException().Message; Debug.LogError(Status); }
        finally
        {
            await app.ReturnToRoomAsync("Kết thúc kiểm tra UI"); await app.LeaveRoomAsync(); runtime.SwitchPlayer(original,null);
            InputSystem.settings.editorInputBehaviorInPlayMode=inputBehavior; InputSystem.settings.backgroundBehavior=background;
            File.WriteAllText("Docs/Progression/RankedMultiplayerUI.json",Newtonsoft.Json.JsonConvert.SerializeObject(new {Status,checks},Newtonsoft.Json.Formatting.Indented));
        }
    }
}
