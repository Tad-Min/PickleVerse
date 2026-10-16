using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PickleVerse.FrontEnd;
using PickleVerse.Settings;
using PickleVerse.Shop;
using PickleVerse.Replays;
public static class AnimeScreensValidation
{
 static readonly List<object> results=new List<object>();
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.FlattenHierarchy;
 static void Check(string name,bool passed){results.Add(new{name,passed});if(!passed)throw new Exception(name);}
 static async Task Load(string scene){SceneManager.LoadScene(scene);await Task.Delay(500);Canvas.ForceUpdateCanvases();}
 static bool Inside(RectTransform child,RectTransform parent)
 {var corners=new Vector3[4];child.GetWorldCorners(corners);var bounds=parent.rect;return corners.All(c=>{var p=parent.InverseTransformPoint(c);return p.x>=bounds.xMin-1&&p.x<=bounds.xMax+1&&p.y>=bounds.yMin-1&&p.y<=bounds.yMax+1;});}
 static bool Hit(Button button)
 {
  var rect=(RectTransform)button.transform;var canvas=button.GetComponentInParent<Canvas>();var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center))};
  var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);return hits.FirstOrDefault().gameObject?.GetComponentInParent<Button>()==button;
 }
 static void VisibleButtons(Transform root,string tag)
 {var safe=(RectTransform)root;var buttons=root.GetComponentsInChildren<Button>().Where(b=>b.gameObject.activeInHierarchy&&b.interactable&&b.GetComponentsInParent<ScrollRect>().All(scroll=>Inside((RectTransform)b.transform,scroll.viewport))).ToArray();Check(tag+" buttons inside safe area",buttons.All(b=>Inside((RectTransform)b.transform,safe)));Check(tag+" pointer hit areas",buttons.All(Hit));}
 public static async Task<object> Run()
 {
  if(!Application.isPlaying)throw new Exception("Play first");results.Clear();
  var assembly=typeof(Editor).Assembly;var type=assembly.GetType("UnityEditor.GameView");var view=Resources.FindObjectsOfTypeAll(type).Cast<EditorWindow>().First();
  var st=assembly.GetType("UnityEditor.GameViewSizes");var sizes=st.GetProperty("instance",Flags).GetValue(null);var group=st.GetMethod("GetGroup",Flags).Invoke(sizes,new[]{type.GetProperty("currentSizeGroupType",Flags).GetValue(view)});var gt=group.GetType();
  var selection=type.GetProperty("selectedSizeIndex",Flags);int prior=(int)selection.GetValue(view),count=(int)gt.GetMethod("GetCustomCount").Invoke(group,null);
  try
  {
   foreach(var size in new[]{new[]{1280,720},new[]{1920,1080},new[]{2560,1080},new[]{2400,1080},new[]{1024,768}})
   {
    var entry=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),size[0],size[1],"Anime screens QA");gt.GetMethod("AddCustomSize").Invoke(group,new[]{entry});selection.SetValue(view,(int)gt.GetMethod("GetTotalCount").Invoke(group,null)-1);view.Repaint();
    var until=DateTime.UtcNow.AddSeconds(5);while(Screen.width!=size[0]||Screen.height!=size[1]){if(DateTime.UtcNow>until)throw new Exception("Resize timeout");await Task.Delay(50);}string tag=size[0]+"x"+size[1];
    await Load("ModeSelection");var mode=UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>();VisibleButtons(mode.safeArea,"Modes "+tag);
    var cards=mode.modePage.GetComponentsInChildren<Button>();Check("Four mode cards "+tag,cards.Length==4);foreach(var label in cards.SelectMany(c=>c.GetComponentsInChildren<TMP_Text>()).Where(t=>new[]{"ModeTag","ModeTitle","Description","Enter"}.Contains(t.name))){label.ForceMeshUpdate();Check("Mode label fits "+tag+" "+label.transform.parent.name+"/"+label.name,!label.isTextOverflowing);}
    mode.OpenRooms();await Task.Delay(280);VisibleButtons(mode.safeArea,"Room setup "+tag);mode.roomJoinCode.text="";mode.JoinPrivateRoom();Check("Blank code stays on setup "+tag,!mode.Loading&&mode.IsRoomSetup);mode.Back();Check("Room back returns to modes "+tag,mode.modePage.activeSelf&&!mode.IsRoomSetup);mode.OpenBot();await Task.Delay(250);VisibleButtons(mode.safeArea,"Bot setup "+tag);mode.Back();
    await Load("Settings");var settings=UnityEngine.Object.FindAnyObjectByType<SettingsScreen>();for(int tab=0;tab<4;tab++){settings.ShowTab(tab);await Task.Delay(260);Canvas.ForceUpdateCanvases();VisibleButtons(settings.safeArea,"Settings tab "+tab+" "+tag);}
    settings.ShowTab(1);float volume=settings.Draft.music;settings.SetSlider(1,.34f);Check("Audio draft editable "+tag,settings.Dirty&&Mathf.Approximately(settings.Draft.music,.34f));settings.Discard();Check("Discard restores draft "+tag,!settings.Dirty&&Mathf.Approximately(settings.Draft.music,volume));
    settings.ShowTab(3);settings.BeginBinding(5);Check("Keyboard capture opens "+tag,settings.Capturing&&settings.captureOverlay.activeSelf);settings.CancelBinding();settings.Touch();Check("Mobile layout opens "+tag,settings.touchPage.activeSelf);settings.Keyboard();
    await Load("Shop");var shop=UnityEngine.Object.FindAnyObjectByType<ShopScreen>();VisibleButtons(shop.safeArea,"Shop "+tag);shop.browser.ShowAll();await Task.Delay(280);Check("Product cards available "+tag,shop.browser.allContent.GetComponentsInChildren<ShopProductCard>().Length==shop.Items.Length);shop.OpenProduct(0);await Task.Delay(280);Check("3D studio remains full screen "+tag,shop.browser.Inspecting&&shop.browser.detailPresentation.Showing);VisibleButtons(shop.safeArea,"Product detail "+tag);shop.Back();Check("Detail back returns to browse "+tag,!shop.browser.Inspecting);
    await Load("Collection");shop=UnityEngine.Object.FindAnyObjectByType<ShopScreen>();VisibleButtons(shop.safeArea,"Collection "+tag);var card=shop.browser.allContent.GetComponentsInChildren<ShopProductCard>().First();card.SetState(false,false);Check("Unowned preview dims "+tag,card.thumbnail.color.r<.4f);card.SetState(true,true);Check("Owned selection shows equipped "+tag,card.action.text.Contains("ĐANG SỬ DỤNG"));
    if(size[0]==2400||size[0]==1024){ScreenCapture.CaptureScreenshot(Path.GetFullPath("../UnityWork/AnimeScreens20261016/Preview/collection-"+tag+".png"));await Task.Delay(150);}
   }
   await Load("ModeSelection");var fx=UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>().botButton.GetComponent<UiInteractionVfx>();fx.OnPointerEnter(new PointerEventData(EventSystem.current));await Task.Delay(80);var pulse=fx.GetComponentInChildren<UiPulseGraphic>();Check("Interaction sweep visible",pulse&&pulse.enabled);await Task.Delay(450);Check("Sweep stops when idle",!pulse.enabled);Check("VFX never blocks input",!pulse.raycastTarget);
   var path=Path.Combine(Application.persistentDataPath,"Replays");ReplayMetadata chosen=null;
   if(Directory.Exists(path))foreach(var file in Directory.GetFiles(path,"metadata.json",SearchOption.AllDirectories)){var info=JsonUtility.FromJson<ReplayMetadata>(File.ReadAllText(file));if(info!=null&&info.duration>2&&info.chunks.Count>0&&info.actors.Count>=2){chosen=info;break;}}
   if(chosen!=null){ReplayLaunch.Select(chosen.playerId,chosen.matchId);await Load("Replay");var replay=UnityEngine.Object.FindAnyObjectByType<ReplayView>();var pause=replay.GetComponentsInChildren<Button>().First(b=>b.name=="Play");pause.onClick.Invoke();double time=chosen.duration*.5;replay.Seek(time);Check("Replay timeline seeks existing recording",Math.Abs(replay.Playhead-time)<.05);var rate=replay.GetComponentsInChildren<Button>().First(b=>b.name=="Speed2");rate.onClick.Invoke();Check("Replay speed changes",replay.PlaybackSpeed==2);}
   return new{passed=true,checks=results.Count,resolutions=5,networkSessionsCreated=0};
  }
  finally
  {
   File.WriteAllText(Path.GetFullPath("../UnityWork/AnimeScreens20261016/ui-validation.json"),Newtonsoft.Json.JsonConvert.SerializeObject(results,Newtonsoft.Json.Formatting.Indented));
   selection.SetValue(view,prior);int builtins=(int)gt.GetMethod("GetBuiltinCount").Invoke(group,null);for(int i=(int)gt.GetMethod("GetCustomCount").Invoke(group,null)-1;i>=count;i--)gt.GetMethod("RemoveCustomSize").Invoke(group,new object[]{builtins+i});view.Repaint();
  }
 }
}
