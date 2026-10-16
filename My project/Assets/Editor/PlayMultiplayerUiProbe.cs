using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using PickleVerse.Multiplayer;
using PickleVerse.Multiplayer.UI;
using PlayMode = PickleVerse.Multiplayer.PlayMode;
public static class PlayMultiplayerUiProbe
{
    public static string Result = "idle";
    public static void Start() { Result="running"; Run(); }
    static async void Run() { try { Result=Newtonsoft.Json.JsonConvert.SerializeObject(await Basic()); } catch(Exception e){ Result="FAIL "+e; Debug.LogException(e); } }
    public static async Task<object> Basic()
    {
        var p=UnityEngine.Object.FindAnyObjectByType<PlayMultiplayerPresenter>();
        var app=MultiplayerApp.Instance;
        double until=Time.realtimeSinceStartupAsDouble+20;
        while(app.Busy && Time.realtimeSinceStartupAsDouble<until)await Task.Delay(100);
        if(app.State!=FlowState.WaitingRoom||!p.roomType.text.Contains("1v1")||!p.singlesPanel.activeSelf)throw new Exception("Default room failed: "+app.Notice);
        if(app.DisplayName!=PickleVerse.Progression.ProgressionRuntime.Instance.Player.playerName||p.singleSlot.name.text!=app.DisplayName)throw new Exception("Local profile name binding failed");
        p.doubles.onClick.Invoke(); while(app.Busy)await Task.Delay(100);
        if(app.Mode!=PlayMode.Doubles||!p.roomType.text.Contains("2v2")||!p.copy.gameObject.activeSelf||string.IsNullOrEmpty(app.RoomCode))throw new Exception("Doubles binding failed");
        p.copy.onClick.Invoke(); if(GUIUtility.systemCopyBuffer!=app.RoomCode)throw new Exception("Copy failed");
        p.play.onClick.Invoke(); await Task.Delay(2300);
        if(app.State!=FlowState.Delay||!p.play.interactable||p.play.GetComponentInChildren<TMPro.TMP_Text>().text!="Hủy")throw new Exception("Find/cancel button failed");
        string elapsed=p.matchTime.text; p.play.onClick.Invoke();
        until=Time.realtimeSinceStartupAsDouble+10;
        while(app.Searching && Time.realtimeSinceStartupAsDouble<until) await Task.Delay(100);
        if(app.Searching||app.State!=FlowState.WaitingRoom)throw new Exception("Cancel failed");
        p.singles.onClick.Invoke(); while(app.Busy)await Task.Delay(100);
        if(app.Mode!=PlayMode.Singles||!string.IsNullOrEmpty(app.RoomCode)||p.copy.gameObject.activeSelf)throw new Exception("Singles switch failed");
        // Private-room entry is now on ModeSelection; keep this backend smoke probe independent of that scene.
        await app.RoomService.SelectCustomAsync(PlayMode.Doubles);while(app.Busy)await Task.Delay(100);
        if(!app.Custom||app.Mode!=PlayMode.Doubles||p.play.interactable)throw new Exception("Custom host start guard failed");
        var result=new{passed=true,defaultRoom="1Vs1",name=app.DisplayName,duoCode=true,copy=true,cancelAt=elapsed,customNoReady=true};
        System.IO.File.WriteAllText("Docs/PlayMultiplayerUiTest.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        return result;
    }
}
