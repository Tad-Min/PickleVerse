using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEditor;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Newtonsoft.Json;
using PickleVerse;
using PickleVerse.LocalMatch;
using PickleVerse.Multiplayer;
using PickleVerse.FrontEnd;
using PickleVerse.Progression;
using PickleVerse.Settings;
using Court=PickleVerse.Rules.Court;

public sealed class VenueFrameProbe:MonoBehaviour
{
 public bool recording;
 public readonly List<float> frames=new List<float>();
 void Update(){if(recording)frames.Add(Time.unscaledDeltaTime*1000);}
}
public static class NewStadiumValidation
{
 const string Folder="../UnityWork/NewStadiums20261019";
 static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 static readonly List<object> checks=new List<object>(),performance=new List<object>();
 static Task running;
 static void Check(string name,bool passed){checks.Add(new{name,passed});Save("checks",checks);if(!passed)throw new Exception(name);}
 static void Save(string name,object value){Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/"+name+".json",JsonConvert.SerializeObject(value,Formatting.Indented));}
 static void Set(object o,string property,object value)=>o.GetType().GetProperty(property).GetSetMethod(true).Invoke(o,new[]{value});
 static void Loaded(Scene s,LoadSceneMode mode)
 {
  foreach(var b in s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)))
   if(b&&(b is CourtBootstrap||b.GetType().Name=="PlayMultiplayerPresenter"))b.enabled=false;
 }
 static async Task Load(string scene){SceneManager.LoadScene(scene);await Task.Delay(600);Canvas.ForceUpdateCanvases();}
 static async Task Wait(Func<bool> ready)
 {var until=DateTime.UtcNow.AddSeconds(20);while(!ready()){if(DateTime.UtcNow>until)throw new Exception("Scene readiness timeout: "+SceneManager.GetActiveScene().name);await Task.Delay(100);}}
 static bool Hit(UnityEngine.UI.Button b)
 {
  var rt=(RectTransform)b.transform;var canvas=b.GetComponentInParent<Canvas>();var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rt.TransformPoint(rt.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);return hits.FirstOrDefault().gameObject?.GetComponentInParent<UnityEngine.UI.Button>()==b;
 }
 static async Task Click(UnityEngine.UI.Button b){var until=DateTime.UtcNow.AddSeconds(6);while(!Hit(b)&&DateTime.UtcNow<until){Canvas.ForceUpdateCanvases();await Task.Delay(80);}Check(b.name+" receives click",Hit(b));ExecuteEvents.Execute(b.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);}
 static async Task Picture(string name){await Task.Delay(250);ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder+"/"+name+".png"));await Task.Delay(250);}
 static void Geometry(StadiumEnvironment e,string tag)
 {
  var f=e.competitionSurface;var m=f.sharedMesh;var vertices=m.vertices.Select(f.transform.TransformPoint).ToArray();var materials=f.GetComponent<Renderer>().sharedMaterials;
  int white=Array.FindIndex(materials,x=>x.name.EndsWith("_White")||x.name.EndsWith("Line_White"));Check(tag+" white line material found",white>=0);
  var lines=m.GetTriangles(white).Select(i=>vertices[i]).ToArray();
  Check(tag+" regulation width",Mathf.Abs(lines.Max(p=>Mathf.Abs(p.x))-Court.HalfWidth)<.006f);
  Check(tag+" regulation baseline",Mathf.Abs(lines.Max(p=>Mathf.Abs(p.z))-Court.HalfLength)<.006f);
  Check(tag+" floor at physics plane",Mathf.Abs(lines.Max(p=>p.y))<.003f);
  Check(tag+" kitchen marking",lines.Any(p=>Mathf.Abs(Mathf.Abs(p.z)-Court.Kitchen)<.006f));
  var net=e.netStructure.sharedMesh.vertices.Select(e.netStructure.transform.TransformPoint).ToArray();
  Check(tag+" net centre height",Mathf.Abs(net.Where(v=>Mathf.Abs(v.x)<.03f).Max(v=>v.y)-Court.NetHeight(0))<.01f);
  Check(tag+" bounded fixture cost",e.poleIllumination.Length==4&&e.GetComponentsInChildren<Light>().Length==4&&e.poleIllumination.Count(l=>l.shadows!=LightShadows.None)<=2);
  Check(tag+" no duplicate visual stadium",UnityEngine.Object.FindObjectsByType<StadiumEnvironment>().Length==1);
 }
 static void Lighting(MatchEnvironmentLighting l,bool night,string tag)
 {
  l.SelectForMatch(night);Check(tag+" white spotlights",l.stadium.poleIllumination.All(x=>x.isActiveAndEnabled&&x.type==LightType.Spot&&x.color==Color.white&&!x.useColorTemperature&&x.intensity>0));
  Check(tag+" matching sky and sun",l.IsNight==night&&l.sun.enabled==!night&&RenderSettings.skybox==(night?l.nightSky:l.daySky));
  Check(tag+" beams cover kitchen and baselines",new[]{new Vector3(0,0,-6.7f),Vector3.zero,new Vector3(0,0,6.7f)}.All(p=>l.stadium.poleIllumination.Any(x=>Vector3.Distance(x.transform.position,p)<x.range&&Vector3.Angle(x.transform.forward,p-x.transform.position)<x.spotAngle*.5f)));
 }
 static async Task Frames(VenueFrameProbe probe,string scene,string variant,int ms=4000)
 {
  await Task.Delay(1500);probe.frames.Clear();probe.recording=true;await Task.Delay(ms);probe.recording=false;
  var values=probe.frames.OrderBy(v=>v).ToArray();if(values.Length<30)throw new Exception("No frames for "+scene);
  var row=new{scene,variant,frames=values.Length,meanFps=1000/values.Average(),medianMs=values[values.Length/2],p95Ms=values[(int)((values.Length-1)*.95)],maxMs=values.Last(),over16ms=values.Count(v=>v>1000f/60),width=Screen.width,height=Screen.height,scale=(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)?.renderScale};performance.Add(row);Save("performance",performance);
 }
 static void Cinematic(BotMatch m,string tag)
 {
  var intro=m.opening;intro.enabled=false;var cam=intro.followCamera.GetComponent<Camera>();
  foreach(float t in new[]{0f,1f,2.8f,4.6f,5.2f,7f,8.7f,9.5f})
  {
   intro.Seek(t);if(!intro.Stage.Contains("entrance"))continue;var actor=intro.Stage=="Opponent entrance"?m.bot:m.human;var point=cam.WorldToViewportPoint(actor.transform.position+Vector3.up*.9f);
   Check(tag+" entrance in frame @"+t,point.z>0&&point.x>.03f&&point.x<.97f&&point.y>.05f&&point.y<.95f);
   Check(tag+" grounded entrance @"+t,Mathf.Abs(actor.transform.position.y)<.001f);
  }
  intro.Skip();intro.enabled=true;Check(tag+" gameplay camera restored",intro.followCamera.enabled&&!intro.IsPlaying);
 }
 public static object Start()
 {
  if(!Application.isPlaying)throw new Exception("Play Menu first");if(running!=null&&!running.IsCompleted)throw new Exception("Already running");
  var view=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));view.Show();view.Focus();
  checks.Clear();performance.Clear();running=Run();return new{started=true,report=Path.GetFullPath(Folder+"/result.json")};
 }
 static async Task Run()
 {
  Save("result",new{status="running"});var prefs=new Dictionary<string,int?>();foreach(var key in new[]{"PKV.Bot.Difficulty","PKV.Bot.Night","PKV.Bot.Venue"})prefs[key]=PlayerPrefs.HasKey(key)?PlayerPrefs.GetInt(key):(int?)null;
  var runtime=ProgressionRuntime.Instance;string originalId=runtime.Player.id,id="VenueQA"+Guid.NewGuid().ToString("N").Substring(0,10);var random=UnityEngine.Random.state;MultiplayerApp app=null;
  var probe=new GameObject("Venue Frame Probe").AddComponent<VenueFrameProbe>();UnityEngine.Object.DontDestroyOnLoad(probe.gameObject);SceneManager.sceneLoaded+=Loaded;
  try
  {
   runtime.SwitchPlayer(id,"Venue QA");
   foreach(CourtVenue venue in new[]{CourtVenue.Professional,CourtVenue.LowPoly})
   {
    await Load("ModeSelection");var ui=UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>();await Click(ui.botButton);await Task.Delay(250);await Click(ui.venueButtons[(int)venue]);await Click(ui.environmentButtons[1]);
    Check(venue+" night preview",ui.SelectedVenue==venue&&ui.courtPreview.texture==ui.venueNightPreviews[(int)venue]);await Click(ui.environmentButtons[0]);
    Check(venue+" day preview",ui.courtPreview.texture==ui.venueDayPreviews[(int)venue]&&ui.venueSelected[(int)venue].gameObject.activeSelf);await Picture("setup-"+venue);
    await Click(ui.startButton);await Wait(()=>SceneManager.GetActiveScene().name=="BotMatch"&&UnityEngine.Object.FindAnyObjectByType<BotMatch>()?.opening.IsPlaying==true);var m=UnityEngine.Object.FindAnyObjectByType<BotMatch>();Check(venue+" launch opens match",m&&m.opening.IsPlaying);var selection=m.GetComponent<CourtVenueSelection>();Check(venue+" launch respects choice",selection.Selected==venue);Geometry(selection.current,"Bot "+venue);Cinematic(m,"Bot "+venue);m.autoPlayer=true;typeof(BotMatch).GetField("focused",Hidden).SetValue(m,true);m.Continue();
    foreach(bool night in new[]{false,true}){Lighting(m.GetComponent<MatchEnvironmentLighting>(),night,"Bot "+venue+" "+night);await Frames(probe,"BotMatch",venue+" "+(night?"night":"day"));await Picture("bot-"+venue+(night?"-night":"-day"));}
    m.RestartWithIntro();Check(venue+" rematch keeps venue",selection.Selected==venue&&m.opening.IsPlaying);m.opening.Skip();
   }
   for(int scenario=0;scenario<4;scenario++)
   {
    bool doubles=scenario>=2;var desired=scenario%2==0?CourtVenue.Professional:CourtVenue.LowPoly;await Load("MultiplayerCourt");app=MultiplayerApp.Ensure();app.enabled=false;
    typeof(MultiplayerApp).GetField("roomData",Hidden).SetValue(app,new RoomData{custom=doubles,mode=doubles?PickleVerse.Multiplayer.PlayMode.Doubles:PickleVerse.Multiplayer.PlayMode.Singles});
    var roster=new MatchRoster{matchId="venue-"+Guid.NewGuid().ToString("N"),mode=doubles?PickleVerse.Multiplayer.PlayMode.Doubles:PickleVerse.Multiplayer.PlayMode.Singles,custom=doubles};int perTeam=doubles?2:1;
    for(int i=0;i<perTeam*2;i++)roster.members.Add(new RoomMember{id=i==0?app.PlayerId:"QA-BOT"+i,name="QA "+i,gender=i%2==0?"Female":"Male",team=i/perTeam,bot=i!=0,elo=500,group="qa"});
    Set(app,"Roster",roster);app.ClientIdentities.Clear();app.Network.NetworkConfig.ConnectionApproval=false;app.Network.NetworkConfig.EnableSceneManagement=false;app.Network.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",(ushort)(18840+scenario),"127.0.0.1");
    Check("local host starts "+scenario,app.Network.StartHost());app.ClientIdentities[app.Network.LocalClientId]=app.PlayerId;
    var go=UnityEngine.Object.Instantiate(app.Settings.matchPrefab);var match=go.GetComponent<NetworkMatch>();match.SetRoster(roster);int seed=0;for(;seed<100;seed++){UnityEngine.Random.InitState(seed);if(UnityEngine.Random.Range(0,2)==(int)desired)break;}UnityEngine.Random.InitState(seed);go.GetComponent<NetworkObject>().Spawn();await Task.Delay(700);
    var choice=UnityEngine.Object.FindAnyObjectByType<CourtVenueSelection>();Check("host chooses shared venue "+scenario,match.Roster.venue==desired&&choice.Selected==desired);Geometry(choice.current,"Online "+scenario);
    var received=JsonConvert.DeserializeObject<MatchRoster>(match.RosterJson.Value.ToString());Check("client snapshot keeps venue "+scenario,received.venue==desired);
    var cinematic=MultiplayerCourtUI.Instance.cinematic;await Wait(()=>cinematic.PlayingIntro);Check("online intro starts "+scenario,cinematic.PlayingIntro&&!match.Started.Value);
    foreach(float t in new[]{2f,6f})
    {
     await Wait(()=>app.Network.ServerTime.Time-match.IntroStart.Value>=t);var group=NetworkPlayer.All.Where(p=>(p.Team.Value==0)==(t<4.6f)).ToArray();Check("online team framed "+scenario+" @"+t,group.Length==perTeam&&group.All(p=>{var v=Camera.main.WorldToViewportPoint(p.Actor.transform.position+Vector3.up);return v.z>0&&v.x>.03&&v.x<.97&&v.y>.05&&v.y<.95;}));await Picture("online-intro-"+scenario+"-"+(int)t);
    }
    await Wait(()=>match.Started.Value&&!cinematic.PlayingIntro);Check("online serve begins "+scenario,match.Started.Value&&!cinematic.PlayingIntro);
    foreach(bool night in new[]{false,true}){Lighting(UnityEngine.Object.FindAnyObjectByType<MatchEnvironmentLighting>(),night,"Online "+scenario+" "+night);await Frames(probe,"MultiplayerCourt",(doubles?"2v2 ":"1v1 ")+desired+" "+(night?"night":"day"),3000);}
    match.Rules.Forfeit(1,"QA");typeof(NetworkMatch).GetMethod("Finish",Hidden).Invoke(match,null);await Task.Delay(250);Check("end banner precedes outcome "+scenario,!cinematic.ShowingOutcome);await Task.Delay(4000);Check("local result framed "+scenario,cinematic.ShowingOutcome&&cinematic.OutcomeReady&&cinematic.PresentedCount==perTeam);await Picture("online-result-"+scenario);
    typeof(MultiplayerApp).GetField("intentionalShutdown",Hidden).SetValue(app,true);app.Network.Shutdown();await Task.Delay(350);UnityEngine.Object.Destroy(app.Network.gameObject);UnityEngine.Object.Destroy(app.gameObject);await Task.Delay(150);app=null;
   }
   foreach(string scene in new[]{"Menu","ModeSelection","SampleScene","Training","Settings","PlayMultiplayer","Shop","Collection","Tutorial","Replay"})
   {
    await Load(scene);Check(scene+" no missing scripts",SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0));
    if(scene=="Menu"){var home=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();Check("Menu uses Professional",home.stadium.name.Contains("Professional"));await Picture("menu");}
    await Frames(probe,scene,"scene smoke",2500);
   }
   Save("result",new{status="passed",count=checks.Count,performanceCases=performance.Count,scope="Editor rendered frames, actual bot launch, loopback NGO host in 1v1/2v2 with simulated peers. No cloud rooms or remote client."});
  }
  catch(Exception ex){Save("result",new{status="failed",error=ex.ToString(),count=checks.Count});Debug.LogException(ex);}
  finally
  {
   probe.recording=false;if(app){typeof(MultiplayerApp).GetField("intentionalShutdown",Hidden).SetValue(app,true);app.Network.Shutdown();await Task.Delay(300);UnityEngine.Object.Destroy(app.Network.gameObject);UnityEngine.Object.Destroy(app.gameObject);}
   SceneManager.sceneLoaded-=Loaded;SceneManager.LoadScene("Menu");await Task.Delay(400);runtime.SwitchPlayer(originalId,null);UnityEngine.Object.Destroy(probe.gameObject);
   foreach(var pair in prefs){if(pair.Value.HasValue)PlayerPrefs.SetInt(pair.Key,pair.Value.Value);else PlayerPrefs.DeleteKey(pair.Key);}PlayerPrefs.Save();BotMatchLaunchSettings.ConsumeLaunch();UnityEngine.Random.state=random;
   foreach(string suffix in new[]{".json",".json.bak",".lock"}){string path=Path.Combine(Application.persistentDataPath,"LocalPlayers",id+suffix);if(File.Exists(path))File.Delete(path);}
  }
 }
}
