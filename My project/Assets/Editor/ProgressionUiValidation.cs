using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using PickleVerse.FrontEnd;
using PickleVerse.Progression;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public static class ProgressionUiValidation
{
    static readonly List<object> checks=new List<object>();
    public static string Status="Idle";
    static void Check(string name,bool passed) { checks.Add(new{name,passed}); if(!passed) throw new InvalidOperationException(name); }
    public static void Resolution(int width,int height)
    {
        var assembly=typeof(Editor).Assembly; const BindingFlags flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        var sizes=assembly.GetType("UnityEditor.GameViewSizes"); var instance=sizes.BaseType.GetProperty("instance",flags).GetValue(null);
        var group=sizes.GetProperty("currentGroup",flags).GetValue(instance);
        var size=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),width,height,"Progression "+width+"x"+height);
        int index=(int)group.GetType().GetMethod("GetBuiltinCount",flags).Invoke(group,null)+(int)group.GetType().GetMethod("GetCustomCount",flags).Invoke(group,null);
        group.GetType().GetMethod("AddCustomSize",flags).Invoke(group,new[]{size});
        var viewType=assembly.GetType("UnityEditor.GameView"); var window=EditorWindow.GetWindow(viewType);
        viewType.GetProperty("selectedSizeIndex",flags).SetValue(window,index); window.Focus(); window.Repaint();
    }
    static void LayoutCheck(PlayerProgressionMenu ui,string name)
    {
        Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(ui.content);
        var texts=ui.modal.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.gameObject.activeInHierarchy).ToArray();
        foreach(var text in texts) text.ForceMeshUpdate();
        var failures=texts.Where(t=>t.isTextOverflowing || !t.font.HasCharacters(new string(t.GetParsedText().Where(c=>!char.IsControl(c)).ToArray()))).Select(t=>t.name+": "+t.text).ToArray();
        Check(name+" labels fit with glyphs"+(failures.Length==0?"":" ["+string.Join("; ",failures)+"]"),failures.Length==0);
        var canvas=ui.GetComponent<Canvas>(); var corners=new Vector3[4]; ui.window.GetWorldCorners(corners);
        Check(name+" safe bounds",corners.Select(p=>RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,p)).All(p=>p.x>=-1&&p.y>=-1&&p.x<=Screen.width+1&&p.y<=Screen.height+1));
    }
    static void ScrollCheck(PlayerProgressionMenu ui,string name)
    {
        ui.scroll.verticalNormalizedPosition=0; Canvas.ForceUpdateCanvases();
        var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(ui.scroll.viewport,ui.content.GetChild(ui.content.childCount-1));
        var visible=ui.scroll.viewport.rect;
        Check(name+" final row reachable by scrolling",bounds.min.y>=visible.yMin-1&&bounds.max.y<=visible.yMax+1);
        ui.scroll.verticalNormalizedPosition=1;
    }
    static async Task ClickNameTag(PlayerProgressionMenu ui,RectTransform area,string name)
    {
        ui.Close();
        Canvas.ForceUpdateCanvases();
        var camera=ui.GetComponent<Canvas>().worldCamera;
        var target=RenderTexture.GetTemporary(Screen.width,Screen.height,24); var previous=camera.targetTexture;
        try { camera.targetTexture=target; camera.Render(); }
        finally { camera.targetTexture=previous; RenderTexture.ReleaseTemporary(target); }
        var point=RectTransformUtility.WorldToScreenPoint(camera,area.TransformPoint(area.rect.center));
        var hits=new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) {position=point},hits);
        Check(name+" pointer reaches NameTag",hits.Count>0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==ui.profileButton.gameObject);
        var mouse=Mouse.current??InputSystem.AddDevice<Mouse>();
        InputSystem.QueueStateEvent(mouse,new MouseState {position=point}); await Task.Delay(100);
        InputSystem.QueueStateEvent(mouse,new MouseState {position=point,buttons=1}); await Task.Delay(100);
        InputSystem.QueueStateEvent(mouse,new MouseState {position=point}); await Task.Delay(150);
        Check(name+" mouse click opens Profile",ui.modal.gameObject.activeSelf && ui.title.text=="HỒ SƠ");
    }
    public static async Task Run()
    {
        checks.Clear(); Status="Running";
        var runtime=ProgressionRuntime.Instance; string original=runtime.Player.id;
        float scale=Time.timeScale;
        var inputBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
        var focusBehavior=InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        try
        {
            Check("Play in Menu",Application.isPlaying && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="Menu");
            var ui=UnityEngine.Object.FindAnyObjectByType<PlayerProgressionMenu>();
            string testId="UIT"+Guid.NewGuid().ToString("N").Substring(0,20);
            ui.editButton.onClick.Invoke(); ui.editId.text=testId; ui.editForm.Find("Switch").GetComponent<Button>().onClick.Invoke();
            Check("Developer ID button creates player",runtime.Player.id==testId && runtime.Player.elo==100 && runtime.Player.level==1 && runtime.Player.coins==0);
            ui.editName.text="PLAYER TEST"; ui.editForm.Find("SaveName").GetComponent<Button>().onClick.Invoke();
            Check("Name editor persists shared name",runtime.Player.playerName=="PLAYER TEST" && ui.menuName.text=="PLAYER TEST");
            ui.Close();
            for(int i=0;i<5;i++)
            {
                var result=new MatchResult {id=Guid.NewGuid().ToString("N"),playerId=testId,kind=MatchKind.RankedSingles,outcome=MatchOutcome.Won,ownElo=runtime.Player.elo,opponentElo=runtime.Player.elo+40,startedUtcTicks=DateTime.UtcNow.AddMinutes(-8).Ticks,endedUtcTicks=DateTime.UtcNow.Ticks,points=20,validReturns=30,validServes=10,gamesWon=2};
                result.opponents.Add(new OpponentInfo {id="TESTOPP",name="Đối thủ thử",elo=result.opponentElo}); result.scores.Add(new GameScore(11,7,true)); result.scores.Add(new GameScore(12,10,true));
                runtime.Apply(result);
            }
            Check("Match rewards update Menu immediately",ui.menuLevel.text.Contains("LV "+runtime.Player.level));
            Resolution(1280,720); await Task.Delay(400);
            await ClickNameTag(ui,(RectTransform)ui.profileButton.transform,"NameTag before rewards"); ui.achievementTab.onClick.Invoke();
            var row=ui.content.Find("A.Win5"); Check("Achievement is claimable",row.Find("Claim").GetComponent<Button>().interactable);
            long coins=runtime.Player.coins; row.Find("Claim").GetComponent<Button>().onClick.Invoke();
            Check("Achievement claim refreshes wallet",runtime.Player.coins==coins+100 && ui.wallet.text.Contains(runtime.Player.coins.ToString("N0")));
            Check("Achievement cannot be claimed twice",!ui.content.Find("A.Win5/Claim").GetComponent<Button>().interactable);
            ui.careerButton.onClick.Invoke(); Check("Career shows five records",ui.content.childCount==5);
            var history=ui.content.GetChild(0); history.GetComponent<Button>().onClick.Invoke();
            Check("Career reveals individual games",history.Find("Detail").GetComponent<TMP_Text>().text.Contains("11–7")&&history.Find("Detail").GetComponent<TMP_Text>().text.Contains("12–10"));
            ui.missionsButton.onClick.Invoke(); Check("Daily lists seven missions",ui.content.childCount==7); ui.weeklyTab.onClick.Invoke(); Check("Weekly lists ten missions",ui.content.childCount==10);
            ui.dailyTab.onClick.Invoke(); coins=runtime.Player.coins; ui.content.Find("D.Play1/Claim").GetComponent<Button>().onClick.Invoke(); Check("Daily claim grants once",runtime.Player.coins==coins+50 && !ui.content.Find("D.Play1/Claim").GetComponent<Button>().interactable);
            Resolution(1280,720); await Task.Delay(400);
            // Hidden Game Views may not render a frame; render the camera to populate UI raycast depth.
            var camera=ui.GetComponent<Canvas>().worldCamera; var target=RenderTexture.GetTemporary(Screen.width,Screen.height,24); var previous=camera.targetTexture;
            try { Canvas.ForceUpdateCanvases(); camera.targetTexture=target; camera.Render(); }
            finally { camera.targetTexture=previous; RenderTexture.ReleaseTemporary(target); }
            var canvas=ui.GetComponent<Canvas>(); var rect=(RectTransform)ui.profileButton.transform; var hits=new List<RaycastResult>();
            canvas.GetComponent<GraphicRaycaster>().Raycast(new PointerEventData(EventSystem.current) {position=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,rect.TransformPoint(rect.rect.center))},hits);
            Check("Modal blocks Menu input",hits.Count>0 && (hits[0].gameObject.transform==ui.modal || hits[0].gameObject.transform.IsChildOf(ui.modal)));
            Check("Modal blocks Menu keyboard navigation",!ui.profileButton.IsInteractable()&&!ui.missionsButton.IsInteractable()&&!ui.careerButton.IsInteractable());
            var keyboard=Keyboard.current??InputSystem.AddDevice<Keyboard>(); InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape)); await Task.Delay(150);
            Check("Escape closes modal",!ui.modal.gameObject.activeSelf); InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            Check("Closing restores Menu navigation",ui.profileButton.IsInteractable()&&ui.missionsButton.IsInteractable()&&ui.careerButton.IsInteractable());
            foreach(var resolution in new[]{new[]{1280,720},new[]{1920,1080},new[]{1024,768},new[]{2560,1080}})
            {
                Resolution(resolution[0],resolution[1]); await Task.Delay(400);
                Check("Actual resolution "+resolution[0]+"x"+resolution[1],Screen.width==resolution[0]&&Screen.height==resolution[1]);
                string size=Screen.width+"x"+Screen.height;
                await ClickNameTag(ui,(RectTransform)ui.profileButton.transform,"NameTag background "+size);
                await ClickNameTag(ui,(RectTransform)ui.profileButton.transform.Find("ImgAvatar"),"NameTag avatar "+size);
                await ClickNameTag(ui,ui.menuName.rectTransform,"NameTag name "+size);
                LayoutCheck(ui,"Profile "+size);
                ui.achievementTab.onClick.Invoke(); LayoutCheck(ui,"Achievements "+Screen.width+"x"+Screen.height); ScrollCheck(ui,"Achievements "+Screen.width+"x"+Screen.height);
                ui.OpenMissions(); LayoutCheck(ui,"Daily "+Screen.width+"x"+Screen.height); ScrollCheck(ui,"Daily "+Screen.width+"x"+Screen.height); ui.weeklyTab.onClick.Invoke(); LayoutCheck(ui,"Weekly "+Screen.width+"x"+Screen.height); ScrollCheck(ui,"Weekly "+Screen.width+"x"+Screen.height);
                ui.OpenCareer(); LayoutCheck(ui,"Career "+Screen.width+"x"+Screen.height); ScrollCheck(ui,"Career "+Screen.width+"x"+Screen.height); ui.OpenEditor(); LayoutCheck(ui,"Player editor "+Screen.width+"x"+Screen.height); ui.Close();
            }
            runtime.SendMessage("OnApplicationFocus",true); Time.timeScale=0; double active=runtime.Player.activeSeconds; await Task.Delay(500);
            Check("Active time ignores timeScale",runtime.Player.activeSeconds>active+.3);
            runtime.SendMessage("OnApplicationFocus",false); active=runtime.Player.activeSeconds; await Task.Delay(500); Check("Unfocused time is excluded",Math.Abs(runtime.Player.activeSeconds-active)<.001);
            runtime.SendMessage("OnApplicationFocus",true); runtime.SendMessage("OnApplicationPause",true); active=runtime.Player.activeSeconds; await Task.Delay(500); Check("Background time is excluded",Math.Abs(runtime.Player.activeSeconds-active)<.001); runtime.SendMessage("OnApplicationPause",false);
            Status="Passed "+checks.Count+" UI checks";
        }
        catch(Exception e) { Status="FAILED: "+e.Message; Debug.LogError(Status); }
        finally
        {
            Time.timeScale=scale; runtime.SendMessage("OnApplicationFocus",Application.isFocused); runtime.SendMessage("OnApplicationPause",false);
            InputSystem.settings.editorInputBehaviorInPlayMode=inputBehavior; InputSystem.settings.backgroundBehavior=focusBehavior;
            runtime.SwitchPlayer(original,null); UnityEngine.Object.FindAnyObjectByType<PlayerProgressionMenu>()?.Close(); Resolution(1280,720);
            Directory.CreateDirectory("Docs/Progression"); File.WriteAllText("Docs/Progression/UI-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(new {Status,checks},Newtonsoft.Json.Formatting.Indented));
        }
    }
}
