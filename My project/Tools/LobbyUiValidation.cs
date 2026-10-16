using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using PickleVerse.FrontEnd;
using PickleVerse.Progression;
using PickleVerse.Settings;
using PickleVerse.Shop;
using PickleVerse.Social;
using Button=UnityEngine.UI.Button;
public static class LobbyUiValidation
{
    const string Root="../UnityWork/LobbyRefresh20261016/",Qa="LobbyUiQA20261016";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.FlattenHierarchy;
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool ok){checks.Add(new{name,passed=ok});if(!ok)throw new Exception(name);}
    static void Save(string name,object result){File.WriteAllText(Root+name+".json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));}
    public static object Prepare()
    {
        if(!Application.isPlaying)throw new Exception("Play Menu.");
        var r=ProgressionRuntime.Instance;
        if(r.Player.id!=Qa){File.WriteAllText(Root+"original-profile.txt",r.Player.id);r.SwitchPlayer(Qa,"Người chơi");}
        return new{qa=r.Player.id==Qa};
    }
    static async Task Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var rect=(RectTransform)button.transform;
        var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        Check(button.name+" receives pointer",hits.Count>0&&(hits[0].gameObject==button.gameObject||hits[0].gameObject.transform.IsChildOf(button.transform)));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerClickHandler);
        await Task.Delay(90);
    }
    static async Task WaitScene(string scene)
    {
        var until=DateTime.UtcNow.AddSeconds(15);
        while(SceneManager.GetActiveScene().name!=scene||LobbyNavigation.IsBusy)
        {if(DateTime.UtcNow>until)throw new TimeoutException(scene);await Task.Delay(50);}
        await Task.Delay(150);
    }
    static async Task Capture(string name)
    {ScreenCapture.CaptureScreenshot(Path.GetFullPath(Root+name+".png"));await Task.Delay(180);}
    public static async Task<object> Flow()
    {
        checks.Clear();var shell=UnityEngine.Object.FindAnyObjectByType<HomeMenuShell>();var ui=shell.GetComponent<PlayerProgressionMenu>();var social=shell.social;
        try
        {
            Check("Header banner and branding hidden",!shell.safe.Find("Head/HeaderBackdrop").gameObject.activeSelf&&!shell.safe.Find("Head/Brand").gameObject.activeSelf&&!shell.safe.Find("Head/Brand Caption").gameObject.activeSelf);
            Check("Missions in six item dock",ui.missionsButton.transform.parent==shell.nav&&shell.nav.GetComponentsInChildren<Button>().Length==6);
            await Click(ui.profileButton);Check("Profile opens saved identity",ui.modal.gameObject.activeSelf&&ui.identityName.text.Contains("Người chơi"));ui.Close();
            await Click(ui.progressionButton);Check("Training progress opens",ui.title.text.Contains("LUYỆN TẬP"));ui.Close();
            await Click(ui.missionsButton);Check("Daily missions opens",ui.title.text.Contains("NHIỆM VỤ"));await Click(ui.weeklyTab);Check("Weekly missions opens",ui.content.childCount>0);ui.Close();
            await Click(ui.careerButton);Check("Match history opens",ui.title.text.Contains("LỊCH SỬ"));ui.Close();
            await Click(social.friendsButton);float start=social.window.anchoredPosition.x;await Task.Delay(280);
            Check("Drawer slides right into view",social.IsOpen&&social.window.anchoredPosition.x>start&&Math.Abs(social.window.anchoredPosition.x-12)<1);
            Check("Drawer owns input",FrontEndModalInput.IsOwnedBy(social)&&!shell.playButton.IsInteractable());
            await Capture("desktop-friends");
            await Click(social.leaderboardTab);var expected=OfflineSocialSession.Directory.Leaderboard(ProgressionRuntime.Instance.Player);
            Check("Elo leaderboard is rendered in descending order",social.content.childCount==expected.Count&&social.content.Cast<Transform>().Select(t=>t.name).SequenceEqual(expected.Select(e=>e.Player.Id)));
            await Capture("desktop-leaderboard");
            social.search.text="DEMO-001";await Task.Delay(100);Check("Leaderboard search filters actual rows",social.content.childCount==1&&social.content.GetChild(0).name=="DEMO-001");
            social.search.text="";social.ShowMyPosition();Check("Own Elo position is visible",social.selfRow&&RectTransformUtility.RectangleContainsScreenPoint(social.scroll.viewport,RectTransformUtility.WorldToScreenPoint(null,social.selfRow.TransformPoint(social.selfRow.rect.center)),null));
            await Click(social.closeButton);await Task.Delay(260);Check("Close releases drawer and restores controls",!social.modal.gameObject.activeSelf&&!FrontEndModalInput.IsOpen&&shell.playButton.IsInteractable());
            var shop=shell.nav.GetComponentsInChildren<Button>().Single(b=>b.name=="BtnStore");await Click(shop);await WaitScene("Shop");LobbyNavigation.ReturnToLobby();await WaitScene("Menu");
            Check("Shop return retains the same lobby shell",shell&&shell.playButton.IsInteractable());
            var collection=shell.nav.GetComponentsInChildren<Button>().Single(b=>b.name=="BtnCollection");await Click(collection);await WaitScene("Collection");LobbyNavigation.ReturnToLobby();await WaitScene("Menu");
            Check("Collection return retains the same lobby shell",shell&&shell.playButton.IsInteractable());
            var setting=shell.nav.GetComponentsInChildren<Button>().Single(b=>b.name=="BtnSetting");await Click(setting);
            var until=DateTime.UtcNow.AddSeconds(10);while(!SettingsOverlay.IsOpen){if(DateTime.UtcNow>until)throw new TimeoutException("Settings");await Task.Delay(50);}
            Check("Settings stays accessible",SettingsOverlay.IsOpen);SettingsOverlay.Close();await Task.Delay(500);
            await Capture("desktop-menu");
            await Click(shell.playButton);Check("Play waits for the authored paddle animation",shell.IsPlayingAnimation&&SceneManager.GetActiveScene().name=="Menu");
            await Task.Delay(65);Check("Paddle and ball actually animate",Quaternion.Angle(Quaternion.identity,shell.paddle.localRotation)>1||shell.ball.anchoredPosition.sqrMagnitude>.01f);
            await Capture("play-strike");await WaitScene("ModeSelection");Check("Play opens mode selection",SceneManager.GetActiveScene().name=="ModeSelection");
            LobbyNavigation.ReturnToLobby();await WaitScene("Menu");Check("Play works again after returning",shell.playButton.IsInteractable()&&!shell.IsPlayingAnimation);
            return new{passed=true,count=checks.Count};
        }
        finally{Save("flow",checks);ui.Close();social.Close();}
    }
    static bool Inside(RectTransform child,RectTransform parent)
    {
        var corners=new Vector3[4];child.GetWorldCorners(corners);var rect=parent.rect;
        return corners.All(c=>{var p=parent.InverseTransformPoint(c);return p.x>=rect.xMin-.5f&&p.x<=rect.xMax+.5f&&p.y>=rect.yMin-.5f&&p.y<=rect.yMax+.5f;});
    }
    static Rect ScreenRect(RectTransform r){var c=new Vector3[4];r.GetWorldCorners(c);return Rect.MinMaxRect(c.Min(v=>v.x),c.Min(v=>v.y),c.Max(v=>v.x),c.Max(v=>v.y));}
    public static async Task<object> Layout()
    {
        checks.Clear();var shell=UnityEngine.Object.FindAnyObjectByType<HomeMenuShell>();var social=shell.social;
        var assembly=typeof(Editor).Assembly;var type=assembly.GetType("UnityEditor.GameView");var view=Resources.FindObjectsOfTypeAll(type).Cast<EditorWindow>().First();
        var st=assembly.GetType("UnityEditor.GameViewSizes");var sizes=st.GetProperty("instance",Flags).GetValue(null);
        var group=st.GetMethod("GetGroup",Flags).Invoke(sizes,new[]{type.GetProperty("currentSizeGroupType",Flags).GetValue(view)});var gt=group.GetType();
        var selection=type.GetProperty("selectedSizeIndex",Flags);int prior=(int)selection.GetValue(view),count=(int)gt.GetMethod("GetCustomCount").Invoke(group,null);
        Touchscreen touch=null;
        try
        {
            var cases=new[]{new[]{1280,720,0,0},new[]{1920,1080,0,0},new[]{2560,1080,0,0},new[]{1024,768,0,0},new[]{1334,750,1,326},new[]{2400,1080,1,400},new[]{2532,1170,1,460},new[]{2048,1536,1,264},new[]{1080,1920,1,420}};
            foreach(var size in cases)
            {
                if(size[2]==1&&touch==null)touch=InputSystem.AddDevice<Touchscreen>("Lobby UI QA touch");
                if(touch!=null)touch.MakeCurrent();
                shell.editorPreviewDpi=size[3];
                var entry=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),size[0],size[1],"Lobby refresh QA");
                gt.GetMethod("AddCustomSize").Invoke(group,new[]{entry});selection.SetValue(view,(int)gt.GetMethod("GetTotalCount").Invoke(group,null)-1);view.Focus();view.Repaint();
                var until=DateTime.UtcNow.AddSeconds(8);while(Screen.width!=size[0]||Screen.height!=size[1]){if(DateTime.UtcNow>until)throw new TimeoutException("Game view resize");await Task.Delay(50);}
                await Task.Delay(200);Canvas.ForceUpdateCanvases();string tag=size[0]+"x"+size[1];
                var controls=new[]{shell.profile,shell.wallet,shell.nav,shell.play,shell.character,shell.atmosphere,shell.drawerHandle};
                Check("Main controls within safe area "+tag,controls.All(r=>Inside(r,shell.safe)));
                Check("Profile and wallet separated "+tag,!ScreenRect(shell.profile).Overlaps(ScreenRect(shell.wallet)));
                Check("Play separated from social handle "+tag,!ScreenRect(shell.play).Overlaps(ScreenRect(shell.drawerHandle)));
                Check("Navigation fits "+tag,shell.nav.GetComponentsInChildren<Button>().All(b=>Inside((RectTransform)b.transform,shell.nav)));
                Check("Navigation labels fit "+tag,shell.nav.GetComponentsInChildren<TMPro.TMP_Text>().All(t=>{t.ForceMeshUpdate();return !t.isTextOverflowing;}));
                if(size[2]==1)
                {
                    float pixelsPerDp=shell.Dpi>=100?shell.Dpi/160f:Mathf.Min(Screen.width,Screen.height)/360f;
                    var buttons=shell.nav.GetComponentsInChildren<Button>().Select(b=>(RectTransform)b.transform).Concat(new[]{shell.play,shell.drawerHandle});
                    Check("Touch targets >=44dp "+tag,buttons.All(r=>{var rect=ScreenRect(r);return rect.width/pixelsPerDp>=43.9f&&rect.height/pixelsPerDp>=43.9f;}));
                }
                social.OpenLeaderboard();await Task.Delay(300);
                Check("Drawer within safe area "+tag,Inside(social.window,social.modalSafe));
                Check("Drawer controls fit "+tag,new[]{(RectTransform)social.search.transform,(RectTransform)social.closeButton.transform,(RectTransform)social.friendsTab.transform,(RectTransform)social.leaderboardTab.transform}.All(r=>Inside(r,social.window)));
                Check("Drawer has useful scroll area "+tag,social.scroll.viewport.rect.height>100);
                if(size[0]==2400||size[0]==1080)await Capture("leaderboard-"+tag);
                social.Close();await Task.Delay(280);await Capture("menu-"+tag);
                // Simulated landscape notch: keep the same screen size and inset the safe root.
                if(size[0]==2532)
                {
                    var min=shell.safe.anchorMin;var max=shell.safe.anchorMax;
                    shell.safe.anchorMin=new Vector2(.045f,.025f);shell.safe.anchorMax=new Vector2(.965f,.975f);await Task.Delay(100);
                    Check("Simulated notch keeps controls inside",controls.All(r=>Inside(r,shell.safe)));await Capture("menu-notch");
                    shell.safe.anchorMin=min;shell.safe.anchorMax=max;await Task.Delay(100);
                }
            }
            return new{passed=true,count=checks.Count,resolutions=cases.Length};
        }
        finally
        {
            social.Close();selection.SetValue(view,prior);
            int builtins=(int)gt.GetMethod("GetBuiltinCount").Invoke(group,null);
            for(int i=(int)gt.GetMethod("GetCustomCount").Invoke(group,null)-1;i>=count;i--)gt.GetMethod("RemoveCustomSize").Invoke(group,new object[]{builtins+i});
            shell.editorPreviewDpi=0;view.Repaint();if(touch!=null)InputSystem.RemoveDevice(touch);Save("layout",checks);
        }
    }
    public static object Budget()
    {
        var shell=UnityEngine.Object.FindAnyObjectByType<HomeMenuShell>();var actions=new List<Action>();
        foreach(var b in shell.GetComponentsInChildren<LobbyUiFeedback>())actions.Add((Action)Delegate.CreateDelegate(typeof(Action),b,b.GetType().GetMethod("Update",Flags)));
        actions.Add((Action)Delegate.CreateDelegate(typeof(Action),shell,typeof(HomeMenuShell).GetMethod("LateUpdate",Flags)));
        for(int i=0;i<200;i++)foreach(var action in actions)action();
        var timer=new System.Diagnostics.Stopwatch();long before=GC.GetAllocatedBytesForCurrentThread();timer.Start();
        for(int i=0;i<10000;i++)foreach(var action in actions)action();
        timer.Stop();long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
        var texture=shell.theme.panel.texture;var result=new{iterations=10000,components=actions.Count,milliseconds=timer.Elapsed.TotalMilliseconds,managedBytes=bytes,textureBytes=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture),textureWidth=texture.width,textureHeight=texture.height,format=texture.format.ToString(),renderTextureComponents=shell.GetComponentsInChildren<UnityEngine.UI.RawImage>(true).Length};
        Save("budget",result);return result;
    }
    public static async Task<object> ContactPose()
    {
        var shell=UnityEngine.Object.FindAnyObjectByType<HomeMenuShell>();bool prior=shell.enabled;
        shell.enabled=false;
        try{shell.Play();shell.AdvancePlay(.17f);await Capture("play-contact-pose");return new{paddleX=shell.paddle.anchoredPosition.x,ballX=shell.ball.anchoredPosition.x,flash=shell.flash.color.a};}
        finally{typeof(HomeMenuShell).GetMethod("ResetPlay",Flags).Invoke(shell,null);shell.enabled=prior;}
    }
    public static async Task<object> Restore()
    {
        if(SceneManager.GetActiveScene().name!="Menu"){LobbyNavigation.ReturnToLobby();await WaitScene("Menu");}
        var r=ProgressionRuntime.Instance;var original=File.ReadAllText(Root+"original-profile.txt").Trim();r.SwitchPlayer(original,null);
        string path=Path.Combine(Application.persistentDataPath,"LocalPlayers",Qa);
        foreach(var suffix in new[]{".json",".json.bak",".lock"})if(File.Exists(path+suffix))File.Delete(path+suffix);
        return new{restored=r.Player.id==original};
    }
}

