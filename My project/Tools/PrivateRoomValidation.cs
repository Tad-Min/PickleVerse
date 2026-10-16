using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.Multiplayer;
using PickleVerse.Multiplayer.Services;
using PickleVerse.Multiplayer.UI;
using PlayMode=PickleVerse.Multiplayer.PlayMode;
public static class PrivateRoomValidation
{
 // Offline only: disable the scene presenter before Start, then supply synthetic room state.
 public sealed class PreviewState:IMultiplayerState
 {
  public PlayMode mode=PlayMode.Singles;public bool custom=true,captain=true,busy;
  public event Action Changed{add{}remove{}}
  public FlowState State=>FlowState.WaitingRoom;public PlayMode Mode=>mode;public bool Custom=>custom;public bool IsCaptain=>captain;public bool Busy=>busy;public bool Searching=>false;
  public string PlayerId=>"ui-preview-local";public string DisplayName{get;set;}="Bạn";public string Notice=>"Phòng riêng — chia sẻ mã để mời bạn bè.";public string Elapsed=>"00:00";public string RoomCode=>"QA1234";
  public List<RoomMember> members=new List<RoomMember>();public List<RoomMember> Members()=>members;
 }
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 public static PreviewState State;public static PlayMultiplayerPresenter Presenter;
 public static async Task Load()
 {
  void Prepare(Scene scene,LoadSceneMode mode){if(scene.name!="PlayMultiplayer")return;Presenter=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PlayMultiplayerPresenter>(true)).Single();Presenter.enabled=false;}
  SceneManager.sceneLoaded+=Prepare;
  try{SceneManager.LoadScene("PlayMultiplayer");await Task.Delay(300);}finally{SceneManager.sceneLoaded-=Prepare;}
  State=new PreviewState();typeof(PlayMultiplayerPresenter).GetField("state",Flags).SetValue(Presenter,State);
 }
 public static void Refresh(){typeof(PlayMultiplayerPresenter).GetMethod("Refresh",Flags).Invoke(Presenter,null);}
 static async Task Capture(string name){await Task.Delay(500);ScreenCapture.CaptureScreenshot(Path.GetFullPath("../UnityWork/AnimeScreens20261016/Preview/"+name+".png"));await Task.Delay(200);}
 public static async Task<object> Run()
 {
  if(!Application.isPlaying)throw new Exception("Play first");
  await Load();State.members.Add(new RoomMember{id=State.PlayerId,name="Bạn",team=0});Refresh();await Capture("12-Private-1v1");
  bool singles=Presenter.customSlots.Count(x=>x.name.transform.parent.gameObject.activeSelf)==2&&!Presenter.play.interactable;
  Presenter.ShowInvite();await Capture("13-Invite");Presenter.inviteDialog.SetActive(false);
  State.mode=PlayMode.Doubles;State.members.AddRange(new[]{new RoomMember{id="qa-a2",name="Đồng đội",team=0},new RoomMember{id="qa-b1",name="Đối thủ 1",team=1},new RoomMember{id="qa-b2",name="Đối thủ 2",team=1}});Refresh();await Capture("14-Private-2v2");
  bool doubles=Presenter.customSlots.Count(x=>x.name.transform.parent.gameObject.activeSelf)==4&&Presenter.play.interactable;
  State.captain=false;Refresh();bool guest=!Presenter.play.interactable;State.busy=true;Refresh();bool locked=!Presenter.swap.interactable&&!Presenter.join.interactable;
  RoomLaunchRequest.Create(PlayMode.Singles);var action=RoomLaunchRequest.Consume(out var code);bool once=action==RoomLaunchAction.CreateSingles&&RoomLaunchRequest.Consume(out code)==RoomLaunchAction.None;
  RoomLaunchRequest.Join(" abcd12 ");var join=RoomLaunchRequest.Consume(out code);bool normalized=join==RoomLaunchAction.Join&&code=="ABCD12";
  if(!singles||!doubles||!guest||!locked||!once||!normalized)throw new Exception("Offline room validation failed");
  var preview=new GameObject("Offline ready check preview");var ready=preview.AddComponent<MatchReadyOverlay>();ready.enabled=false;typeof(MatchReadyOverlay).GetMethod("Build",Flags).Invoke(ready,null);preview.transform.Find("MatchFound").gameObject.SetActive(true);await Capture("15-Ready-Check");UnityEngine.Object.Destroy(preview);
  var result=new{offlineOnly=true,singles,doubles,guest,locked,once,normalized,networkSessionsCreated=0};File.WriteAllText(Path.GetFullPath("../UnityWork/AnimeScreens20261016/room-validation.json"),Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
 }
 public static async Task<object> Layout()
 {
  if(!Application.isPlaying)throw new Exception("Play first");await Load();State.mode=PlayMode.Doubles;State.members.AddRange(new[]{new RoomMember{id=State.PlayerId,name="Bạn",team=0},new RoomMember{id="qa-a2",name="Đồng đội",team=0},new RoomMember{id="qa-b1",name="Đối thủ 1",team=1},new RoomMember{id="qa-b2",name="Đối thủ 2",team=1}});Refresh();
  var flags=Flags|BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy;var a=typeof(UnityEditor.Editor).Assembly;var type=a.GetType("UnityEditor.GameView");var view=Resources.FindObjectsOfTypeAll(type).Cast<UnityEditor.EditorWindow>().First();var st=a.GetType("UnityEditor.GameViewSizes");var sizes=st.GetProperty("instance",flags).GetValue(null);var group=st.GetMethod("GetGroup",flags).Invoke(sizes,new[]{type.GetProperty("currentSizeGroupType",flags).GetValue(view)});var gt=group.GetType();var select=type.GetProperty("selectedSizeIndex",flags);int prior=(int)select.GetValue(view),count=(int)gt.GetMethod("GetCustomCount").Invoke(group,null);int checks=0;
  try
  {
   foreach(var size in new[]{new[]{1024,768},new[]{2400,1080}})
   {
    var entry=Activator.CreateInstance(a.GetType("UnityEditor.GameViewSize"),Enum.Parse(a.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),size[0],size[1],"Room UI QA");gt.GetMethod("AddCustomSize").Invoke(group,new[]{entry});select.SetValue(view,(int)gt.GetMethod("GetTotalCount").Invoke(group,null)-1);view.Repaint();var until=DateTime.UtcNow.AddSeconds(5);while(Screen.width!=size[0]||Screen.height!=size[1]){if(DateTime.UtcNow>until)throw new Exception("Resize timeout");await Task.Delay(40);}await Task.Delay(150);Canvas.ForceUpdateCanvases();
    foreach(var slot in Presenter.customSlots){slot.name.ForceMeshUpdate();checks++;if(slot.name.isTextOverflowing)throw new Exception("Room name overflow "+size[0]);var image=slot.name.transform.parent.Find("ImgIcon").GetComponent<UnityEngine.UI.Image>();checks++;if(!image.preserveAspect)throw new Exception("Portrait aspect");}
    await Capture("room-"+size[0]+"x"+size[1]);Presenter.ShowInvite();await Capture("invite-"+size[0]+"x"+size[1]);checks++;if(Presenter.play.interactable||Presenter.copy.interactable||Presenter.back.interactable)throw new Exception("Modal behind controls active");Presenter.CloseInvite();
   }
   return new{passed=true,resolutions=2,checks,networkSessionsCreated=0};
  }
  finally{select.SetValue(view,prior);int builtins=(int)gt.GetMethod("GetBuiltinCount").Invoke(group,null);for(int i=(int)gt.GetMethod("GetCustomCount").Invoke(group,null)-1;i>=count;i--)gt.GetMethod("RemoveCustomSize").Invoke(group,new object[]{builtins+i});view.Repaint();}
 }
}
