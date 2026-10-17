using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Multiplayer;
using PickleVerse.FrontEnd;
using PickleVerse.Multiplayer;
using PickleVerse.Multiplayer.UI;
using PlayMode=PickleVerse.Multiplayer.PlayMode;

// Authorized by the user: create and delete two empty rooms in pickleverse-test.
// Never starts matchmaking, sends invitations, or touches an existing player session.
public static class PrivateRoomLiveValidation
{
    [Serializable] public sealed class RoomResult
    {
        public string mode,sessionId;
        public int expectedCapacity,visibleSeats;
        public bool serverRefresh,correctMode,codeShown,inviteOpens,copyWorks,codeRetained,hostStartGuard,deleted,absentFromJoined;
    }
    [Serializable] public sealed class Report
    {
        public string environment="pickleverse-test",startedUtc,finishedUtc,error,cleanupError;
        public bool passed,networkTest=true,twoClientTest=false;
        public List<RoomResult> rooms=new List<RoomResult>();
    }
    static readonly string Folder=Path.GetFullPath("../UnityWork/AnimeScreens20261016");
    static Report report;
    static void Save(){File.WriteAllText(Path.Combine(Folder,"live-room-validation.json"),Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));}
    static void Check(string name,bool condition){if(!condition)throw new Exception(name);}
    static async Task Wait(Func<bool> condition,string step,int seconds=35)
    {
        var deadline=DateTime.UtcNow.AddSeconds(seconds);
        while(!condition()){if(DateTime.UtcNow>deadline)throw new TimeoutException(step+": "+MultiplayerApp.Instance?.Notice);await Task.Delay(100);}
    }
    static async Task Capture(string name)
    {Canvas.ForceUpdateCanvases();await Task.Delay(300);ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"Preview",name+".png"));await Task.Delay(250);}
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play first");
        var existing=MultiplayerApp.Instance;
        if(existing&&(existing.Room!=null||existing.Searching||existing.Busy||existing.State==FlowState.InMatch))throw new Exception("Existing activity detected; nothing changed.");
        var settings=Resources.Load<MultiplayerSettings>("MultiplayerSettings");
        if(!settings||settings.environment!="pickleverse-test")throw new Exception("Unexpected service environment; nothing created.");
        Directory.CreateDirectory(Path.Combine(Folder,"Preview"));report=new Report{startedUtc=DateTime.UtcNow.ToString("o")};Save();
        string clipboard=GUIUtility.systemCopyBuffer;
        bool ownsFlow=false;
        try
        {
            if(SceneManager.GetActiveScene().name!="ModeSelection"){SceneManager.LoadScene("ModeSelection");await Task.Delay(500);}
            foreach(var mode in new[]{PlayMode.Singles,PlayMode.Doubles})
            {
                await Wait(()=>UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>()!=null,"Mode screen");await Task.Delay(300);
                var menu=UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>();menu.OpenRooms();ownsFlow=true;
                var button=menu.roomPage.transform.Find(mode==PlayMode.Singles?"Create Singles":"Create Doubles").GetComponent<UnityEngine.UI.Button>();
                Check("Room type entry enabled",button&&button.interactable);button.onClick.Invoke();
                await Wait(()=>SceneManager.GetActiveScene().name=="PlayMultiplayer"&&MultiplayerApp.Instance&&MultiplayerApp.Instance.Custom&&MultiplayerApp.Instance.Mode==mode&&!MultiplayerApp.Instance.Busy&&MultiplayerApp.Instance.Room!=null&&!string.IsNullOrEmpty(MultiplayerApp.Instance.RoomCode),"Create "+mode);
                var app=MultiplayerApp.Instance;var session=app.Room;
                var item=new RoomResult{mode=mode==PlayMode.Singles?"1v1":"2v2",sessionId=session.Id,expectedCapacity=2*(int)mode};report.rooms.Add(item);Save();
                await session.RefreshAsync();item.serverRefresh=true;
                await Task.Delay(350);var ui=UnityEngine.Object.FindAnyObjectByType<PlayMultiplayerPresenter>();
                item.correctMode=app.Custom&&app.Mode==mode&&session.MaxPlayers==item.expectedCapacity;
                item.visibleSeats=ui.customSlots.Count(slot=>slot.name.transform.parent.gameObject.activeInHierarchy);
                item.codeShown=ui.roomCode.gameObject.activeSelf&&ui.roomCode.text.Contains(app.RoomCode);
                item.hostStartGuard=app.IsCaptain&&!ui.play.interactable;
                Check("Room mode/capacity",item.correctMode&&item.visibleSeats==item.expectedCapacity);
                Check("Room code and empty opponent guard",item.codeShown&&item.hostStartGuard);
                await Capture("live-"+item.mode);
                ui.invite.onClick.Invoke();item.inviteOpens=ui.inviteDialog.activeSelf&&ui.inviteCode.text==app.RoomCode;
                ui.inviteCopy.onClick.Invoke();item.copyWorks=GUIUtility.systemCopyBuffer==app.RoomCode;item.codeRetained=ui.inviteCode.text==app.RoomCode;
                Check("Invite and copy",item.inviteOpens&&item.copyWorks&&item.codeRetained);await Capture("live-invite-"+item.mode);ui.inviteClose.onClick.Invoke();Save();
                ui.back.onClick.Invoke();await Wait(()=>SceneManager.GetActiveScene().name=="ModeSelection"&&app.Room==null&&!app.Busy,"Delete room on return");
                item.deleted=true;
                var joined=await MultiplayerService.Instance.GetJoinedSessionIdsAsync();item.absentFromJoined=!joined.Contains(item.sessionId);
                Check("Deleted room absent from account sessions",item.absentFromJoined);Save();
            }
            report.passed=report.rooms.Count==2&&report.rooms.All(r=>r.deleted&&r.absentFromJoined);return new{passed=report.passed,environment=report.environment,created=report.rooms.Count,deleted=report.rooms.Count(r=>r.deleted),inviteAndCopy=report.rooms.All(r=>r.inviteOpens&&r.copyWorks&&r.codeRetained),twoClientTest=false};
        }
        catch(Exception e){report.error=e.GetType().Name+": "+e.Message;throw;}
        finally
        {
            try
            {
                var app=MultiplayerApp.Instance;
                if(ownsFlow&&app&&(app.Room!=null||SceneManager.GetActiveScene().name=="PlayMultiplayer"))
                {
                    string activeId=app.Room?.Id;await app.ReturnToModeSelectionAsync();
                    foreach(var item in report.rooms.Where(r=>r.sessionId==activeId))item.deleted=app.Room==null;
                }
            }
            catch(Exception e){report.cleanupError=e.Message;}
            GUIUtility.systemCopyBuffer=clipboard;report.finishedUtc=DateTime.UtcNow.ToString("o");Save();
        }
    }
}
