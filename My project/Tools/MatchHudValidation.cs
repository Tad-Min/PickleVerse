using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using PickleVerse.LocalMatch;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using PickleVerse.Settings;
using Newtonsoft.Json;

public static class MatchHudValidation
{
    const string Folder="../UnityWork/MatchHud20261020";
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic,Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.FlattenHierarchy;
    static Task task;static readonly List<object> checks=new List<object>();static readonly List<object> layouts=new List<object>();
    static void Save(object data){Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/ui-validation.json",JsonConvert.SerializeObject(data,Formatting.Indented));}
    static void Check(string name,bool passed){checks.Add(new{name,passed});if(!passed)throw new Exception(name);}
    static async Task Wait(Func<bool> condition,int seconds=35){var until=DateTime.UtcNow.AddSeconds(seconds);while(!condition()){if(DateTime.UtcNow>until)throw new Exception("Timed out waiting");await Task.Delay(35);}}
    static void Loaded(Scene scene,LoadSceneMode mode){foreach(var b in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CourtBootstrap>(true)))b.enabled=false;}
    static async Task Load(string name){var op=SceneManager.LoadSceneAsync(name);while(!op.isDone)await Task.Delay(25);await Task.Delay(250);}
    public static object Start(){if(!Application.isPlaying)throw new Exception("Play Mode required");checks.Clear();layouts.Clear();task=Run();return "HUD interaction and responsive checks started";}
    static void Render(MonoBehaviour target){target.GetType().GetMethod("Update",Hidden).Invoke(target,null);Canvas.ForceUpdateCanvases();}
    static bool Inside(RectTransform rect)
    {var points=new Vector3[4];rect.GetWorldCorners(points);var canvas=rect.GetComponentInParent<Canvas>().rootCanvas;var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;return points.Select(p=>RectTransformUtility.WorldToScreenPoint(camera,p)).All(p=>p.x>=-1&&p.y>=-1&&p.x<=Screen.width+1&&p.y<=Screen.height+1);}
    static bool Hit(UnityEngine.UI.Button button)
    {var c=button.GetComponentInParent<Canvas>().rootCanvas;var camera=c.renderMode==RenderMode.ScreenSpaceOverlay?null:c.worldCamera;var point=RectTransformUtility.WorldToScreenPoint(camera,button.GetComponent<RectTransform>().TransformPoint(button.GetComponent<RectTransform>().rect.center));var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);return hits.Count>0&&(hits[0].gameObject==button.gameObject||hits[0].gameObject.transform.IsChildOf(button.transform));}
    static void Digits(MatchScoreboard board,string label)
    {
        var d=board.firstPoints;int count=d.TransitionCount;d.SetValue(7);d.Advance(1);d.SetValue(8);
        Check(label+" numeric rolls need no clipping masks",board.GetComponentsInChildren<UnityEngine.UI.RectMask2D>(true).Length==0);
        Check(label+" starts a score roll",d.IsAnimating&&d.current.text=="8"&&d.previous.text=="7");d.Advance(.13f);
        Check(label+" interpolates number position and alpha",d.currentOpacity.alpha>0&&d.currentOpacity.alpha<1&&d.current.rectTransform.anchoredPosition.y<0);
        d.SetValue(9);d.SetValue(10);d.Advance(1);Check(label+" repeated updates finish on latest value",d.Value==10&&d.current.text=="10"&&!d.IsAnimating&&!d.enabled);
        count=d.TransitionCount;for(int i=0;i<1000;i++)d.SetValue(10);Check(label+" stable score starts no animation",d.TransitionCount==count&&!d.enabled);
        d.SetValue(11);board.gameObject.SetActive(false);board.gameObject.SetActive(true);Check(label+" interruption settles cleanly",!d.IsAnimating&&d.currentOpacity.alpha==1&&d.current.rectTransform.anchoredPosition==Vector2.zero);
        d.SetValue(100);d.Advance(1);d.current.ForceMeshUpdate();Check(label+" extended deuce score remains legible",d.current.text=="100"&&!d.current.isTextTruncated);
        board.SetClock(CourtClockMode.Serve,2.04f);Check(label+" serve countdown and urgent color",board.clock.text=="2.1s"&&board.clock.color.r>board.clock.color.g&&Mathf.Abs(board.clockFill.fillAmount-.204f)<.001f);
        board.SetClock(CourtClockMode.Rally,0,true);Check(label+" rally removes obsolete countdown",board.clock.text==""&&board.clockFill.fillAmount==0&&board.phase.text=="BÓNG ĐÃ NẢY");
    }
    static async Task Responsive(MatchHudLayout layout,string label)
    {
        var assembly=typeof(UnityEditor.Editor).Assembly;var viewType=assembly.GetType("UnityEditor.GameView");var view=UnityEditor.EditorWindow.GetWindow(viewType);view.Show();view.Focus();
        var sizesType=assembly.GetType("UnityEditor.GameViewSizes");var sizes=sizesType.GetProperty("instance",Flags).GetValue(null);var group=sizesType.GetMethod("GetGroup",Flags).Invoke(sizes,new[]{viewType.GetProperty("currentSizeGroupType",Flags).GetValue(view)});var type=group.GetType();var selected=viewType.GetProperty("selectedSizeIndex",Flags);int prior=(int)selected.GetValue(view),custom=(int)type.GetMethod("GetCustomCount").Invoke(group,null);
        try
        {
            foreach(var size in new[]{new[]{1280,720},new[]{1920,1080},new[]{2340,1080},new[]{1024,768}})
            {
                var entry=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),size[0],size[1],"HUD QA");type.GetMethod("AddCustomSize").Invoke(group,new[]{entry});selected.SetValue(view,(int)type.GetMethod("GetTotalCount").Invoke(group,null)-1);view.Repaint();await Wait(()=>Screen.width==size[0]&&Screen.height==size[1],6);await Task.Delay(220);Canvas.ForceUpdateCanvases();
                var targets=new[]{layout.scoreboard,layout.settings,layout.help,layout.touch,layout.timeout,layout.notice,layout.technique};bool inside=targets.All(Inside);bool buttons=Hit(layout.settings.GetComponent<UnityEngine.UI.Button>())&&Hit(layout.help.GetComponent<UnityEngine.UI.Button>())&&Hit(layout.touch.GetComponent<UnityEngine.UI.Button>());
                var board=layout.scoreboard.GetComponent<MatchScoreboard>();board.game.ForceMeshUpdate();board.clock.ForceMeshUpdate();
                Check(label+" HUD fits "+size[0]+"x"+size[1],inside&&buttons);Check(label+" headers and timer are not truncated "+size[0],board.gameCaption.text=="GAME"&&!board.gameCaption.isTextTruncated&&!board.game.isTextTruncated&&!board.clock.isTextTruncated&&!layout.settingsKey.isTextTruncated);
                layouts.Add(new{scene=label,width=Screen.width,height=Screen.height,inside,buttons,game=board.game.text,clock=board.clock.text,parsedGame=board.game.GetParsedText(),parsedClock=board.clock.GetParsedText()});
                layout.ApplyInsets(28,32,12,20);Check(label+" safe area shifts both corners "+size[0],layout.scoreboard.anchoredPosition==new Vector2(46,-30)&&layout.settings.anchoredPosition==new Vector2(-50,-30));layout.ApplyInsets(0,0,0,0);
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder+"/"+label+"-"+size[0]+"x"+size[1]+".png"));await Task.Delay(100);
            }
        }
        finally{selected.SetValue(view,prior);int added=(int)type.GetMethod("GetCustomCount").Invoke(group,null)-custom;for(int i=0;i<added;i++)type.GetMethod("RemoveCustomSize").Invoke(group,new object[]{(int)type.GetMethod("GetTotalCount").Invoke(group,null)-1});view.Repaint();}
    }
    static async Task Settings(MatchHudLayout layout,ISettingsHost host,string label)
    {
        Check(label+" settings hit target",Hit(layout.settings.GetComponent<UnityEngine.UI.Button>()));layout.settings.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();await Wait(()=>SettingsOverlay.IsOpen&&!SettingsOverlay.Loading);
        Check(label+" settings opens and input is paused",host.IsPaused&&SceneManager.GetSceneByName("Settings").isLoaded);
        SettingsOverlay.Close();await Wait(()=>!SettingsOverlay.IsOpen&&!SettingsOverlay.Loading);Check(label+" settings restores HUD and input",!host.IsPaused&&layout.GetComponent<Canvas>().enabled&&layout.scoreboard.GetComponentInParent<Canvas>().enabled);
    }
    static async Task Shutdown(MultiplayerApp app)
    {if(!app)return;typeof(MultiplayerApp).GetField("intentionalShutdown",Hidden).SetValue(app,true);app.Network.Shutdown();await Task.Delay(200);UnityEngine.Object.Destroy(app.Network.gameObject);UnityEngine.Object.Destroy(app.gameObject);await Task.Delay(100);}
    static async Task Run()
    {
        Save(new{status="running"});var runtime=ProgressionRuntime.Instance;string original=runtime.Player.id,qa="HudUI"+Guid.NewGuid().ToString("N").Substring(0,8);File.WriteAllText(Folder+"/original-profile.txt",original);MultiplayerApp app=null;SceneManager.sceneLoaded+=Loaded;
        try
        {
            runtime.SwitchPlayer(qa,"HUD UI QA");await Load("BotMatch");var bot=UnityEngine.Object.FindAnyObjectByType<BotMatch>();bot.Continue();bot.opening?.Skip();bot.enabled=false;var hud=bot.hud;
            Check("Bot HUD uses shared scoreboard",hud.broadcast&&hud.layout&&!hud.scoreboard.gameObject.activeSelf&&!hud.serveClock.gameObject.activeSelf);
            Digits(hud.broadcast,"Bot");bot.Rules.Points[0]=8;bot.Rules.Points[1]=6;bot.Rules.Games[0]=1;typeof(BotMatch).GetProperty("Message").GetSetMethod(true).Invoke(bot,new object[]{"Bạn giao · Điểm gọi 8–6"});Render(hud);await Task.Delay(350);
            Check("Bot authoritative score and game wins",hud.broadcast.firstPoints.Value==8&&hud.broadcast.secondPoints.Value==6&&hud.broadcast.firstGames.Value==1);
            hud.layout.help.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();Check("Bot help still opens and pauses",hud.help.activeSelf&&bot.Paused);hud.ToggleHelp();Check("Bot help restores pause",!hud.help.activeSelf&&!bot.Paused);
            await Settings(hud.layout,bot,"Bot");Render(hud);await Responsive(hud.layout,"BotMatch");
            typeof(BotMatch).GetField("initialServer",Hidden).SetValue(bot,0);bot.Restart();bot.enabled=false;hud.Timeout();Render(hud);Check("Bot time-out still works",bot.Phase==MatchPhase.Timeout&&hud.broadcast.ClockMode==CourtClockMode.Timeout);bot.Continue();
            bot.Rules.Games[0]=1;bot.Rules.Points[0]=10;bot.Rules.Fail(1,PickleVerse.Rules.Fault.Out);typeof(BotMatch).GetMethod("BeginFault",Hidden).Invoke(bot,null);bot.enabled=true;
            await Wait(()=>bot.victory.Ready,20);Check("Result cinematic hides new scoreboard",bot.victory.Active&&!hud.broadcast.gameObject.activeInHierarchy&&hud.resultActions.activeSelf);
            for(int mode=0;mode<2;mode++)
            {
                await Load("MultiplayerCourt");app=MultiplayerApp.Ensure();app.enabled=false;var playMode=mode==0?PickleVerse.Multiplayer.PlayMode.Singles:PickleVerse.Multiplayer.PlayMode.Doubles;int count=mode+1,localTeam=mode;
                typeof(MultiplayerApp).GetField("roomData",Hidden).SetValue(app,new RoomData{custom=true,mode=playMode});var roster=new MatchRoster{matchId="hud-ui-"+Guid.NewGuid().ToString("N"),custom=true,mode=playMode};
                for(int i=0;i<count*2;i++){bool local=i==localTeam*count;roster.members.Add(new RoomMember{id=local?app.PlayerId:"HUD-UI-BOT"+i,name=local?"Kiểm tra UI":"Vận động viên "+i,gender=i%2==0?"Female":"Male",team=i/count,bot=!local,elo=500,group="qa"});}
                typeof(MultiplayerApp).GetProperty("Roster").GetSetMethod(true).Invoke(app,new object[]{roster});app.ClientIdentities.Clear();app.Network.NetworkConfig.ConnectionApproval=false;app.Network.NetworkConfig.EnableSceneManagement=false;app.Network.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",(ushort)(19280+mode),"127.0.0.1");Check("NGO host starts "+mode,app.Network.StartHost());app.ClientIdentities[app.Network.LocalClientId]=app.PlayerId;
                var go=UnityEngine.Object.Instantiate(app.Settings.matchPrefab);var match=go.GetComponent<NetworkMatch>();match.SetRoster(roster);go.GetComponent<NetworkObject>().Spawn();await Wait(()=>match.Started.Value&&!MultiplayerCourtUI.Instance.cinematic.PlayingIntro);match.enabled=false;
                var ui=MultiplayerCourtUI.Instance;var label=mode==0?"Multiplayer1v1":"Multiplayer2v2";Digits(ui.broadcast,label);match.ScoreA.Value=7;match.ScoreB.Value=5;match.GamesA.Value=1;match.ServerTeam.Value=localTeam;match.ServerNumber.Value=2;match.ServerIndex.Value=count-1;match.WaitingServe.Value=true;match.ServeElapsed.Value=2;Render(ui);await Task.Delay(350);
                Check(label+" local team occupies first row",ui.broadcast.firstPoints.Value==(localTeam==0?7:5)&&ui.broadcast.firstName.text.Contains("BẠN"));Check(label+" service marker follows local perspective",ui.broadcast.Serving==0&&ui.broadcast.serviceDetail.text.Contains("LƯỢT 2"));
                Check(label+" both teammates are named",mode==0||ui.broadcast.firstName.text.Contains("\n")&&ui.broadcast.secondName.text.Contains("\n"));
                ui.layout.help.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();Check(label+" help blocks local input",ui.help.activeSelf&&ui.InputBlocked);ui.ToggleHelp();await Settings(ui.layout,ui,label);Render(ui);await Responsive(ui.layout,label);
                match.Finished.Value=true;match.WinningTeam.Value=localTeam;ui.EnsureEndAnnouncement("Kiểm tra trình bày");await Wait(()=>ui.cinematic.OutcomeReady,20);Check(label+" end cinematic hides compact HUD",ui.cinematic.ShowingOutcome&&!ui.broadcast.gameObject.activeInHierarchy);
                await Shutdown(app);app=null;Save(new{status="running",checks,layouts});
            }
            Save(new{passed=true,count=checks.Count,checks,layouts,scope="Live Editor scenes and NGO local host; UI interaction, not Internet matchmaking"});
        }
        catch(Exception e){Save(new{passed=false,error=e.ToString(),checks,layouts});Debug.LogException(e);}
        finally
        {
            if(SettingsOverlay.IsOpen){SettingsOverlay.Close(true);await Wait(()=>!SettingsOverlay.Loading);}await Shutdown(app);SceneManager.sceneLoaded-=Loaded;await Load("ModeSelection");runtime.SwitchPlayer(original,null);string root=Path.Combine(Application.persistentDataPath,"LocalPlayers");foreach(var suffix in new[]{".json",".json.bak",".lock"}){string path=Path.Combine(root,qa+suffix);if(File.Exists(path))File.Delete(path);}
        }
    }
}
