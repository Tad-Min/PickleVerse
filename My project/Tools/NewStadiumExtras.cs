using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using Newtonsoft.Json;
using PickleVerse;
using PickleVerse.LocalMatch;
using PickleVerse.FrontEnd;
using PickleVerse.Progression;
using PickleVerse.Shop;

public sealed class VenueReturnProbe:MonoBehaviour
{
 public bool recording;public readonly List<float> values=new List<float>();
 void Update(){if(recording)values.Add(Time.unscaledDeltaTime*1000);}
}
public static class NewStadiumExtras
{
 const string Folder="../UnityWork/NewStadiums20261019";
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.FlattenHierarchy;
 static readonly List<object> checks=new List<object>();static Task running;
 static void Save(string name,object value)=>File.WriteAllText(Folder+"/"+name+".json",JsonConvert.SerializeObject(value,Formatting.Indented));
 static void Check(string name,bool ok){checks.Add(new{name,passed=ok});if(!ok)throw new Exception(name);}
 static async Task Load(string scene){SceneManager.LoadScene(scene);await Task.Delay(700);Canvas.ForceUpdateCanvases();}
 static async Task Wait(Func<bool> ready){var until=DateTime.UtcNow.AddSeconds(15);while(!ready()){if(DateTime.UtcNow>until)throw new Exception("Readiness timeout");await Task.Delay(80);}}
 static async Task Picture(string name){await Task.Delay(300);ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder+"/"+name+".png"));await Task.Delay(300);}
 static bool Hit(UnityEngine.UI.Button button)
 {
  var r=(RectTransform)button.transform;var c=button.GetComponentInParent<Canvas>();var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(c.renderMode==RenderMode.ScreenSpaceOverlay?null:c.worldCamera,r.TransformPoint(r.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);return hits.FirstOrDefault().gameObject?.GetComponentInParent<UnityEngine.UI.Button>()==button;
 }
 static object Summary(List<float> values){var a=values.OrderBy(v=>v).ToArray();return new{frames=a.Length,meanFps=1000/a.Average(),medianMs=a[a.Length/2],p95Ms=a[(int)((a.Length-1)*.95)],maxMs=a.Last()};}
 public static object Start(){if(!Application.isPlaying)throw new Exception("Play first");checks.Clear();running=Run();return new{started=true};}
 static async Task Run()
 {
  Save("extras-result",new{status="running"});var assembly=typeof(Editor).Assembly;var type=assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(type);view.Show();view.Focus();
  var st=assembly.GetType("UnityEditor.GameViewSizes");var sizes=st.GetProperty("instance",Flags).GetValue(null);var group=st.GetMethod("GetGroup",Flags).Invoke(sizes,new[]{type.GetProperty("currentSizeGroupType",Flags).GetValue(view)});var gt=group.GetType();var selection=type.GetProperty("selectedSizeIndex",Flags);int prior=(int)selection.GetValue(view),count=(int)gt.GetMethod("GetCustomCount").Invoke(group,null);
  var originalPipeline=QualitySettings.renderPipeline;UniversalRenderPipelineAsset mobile=null;VenueReturnProbe probe=null;
  var runtime=ProgressionRuntime.Instance;string originalId=runtime.Player.id,id="VenueExtra"+Guid.NewGuid().ToString("N").Substring(0,10);
  try
  {
   runtime.SwitchPlayer(id,"Venue extra QA");
   await Load("ModeSelection");var ui=UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>();ui.OpenBot();
   foreach(var size in new[]{new[]{1280,720},new[]{1920,1080},new[]{2340,1080},new[]{1024,768},new[]{800,360}})
   {
    var entry=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),size[0],size[1],"Venue UI QA");gt.GetMethod("AddCustomSize").Invoke(group,new[]{entry});selection.SetValue(view,(int)gt.GetMethod("GetTotalCount").Invoke(group,null)-1);view.Repaint();await Wait(()=>Screen.width==size[0]&&Screen.height==size[1]);await Task.Delay(500);Canvas.ForceUpdateCanvases();
    foreach(var button in ui.venueButtons.Concat(ui.environmentButtons).Concat(ui.difficultyButtons).Append(ui.startButton))
    {
     var r=(RectTransform)button.transform;var corners=new Vector3[4];r.GetWorldCorners(corners);var canvas=button.GetComponentInParent<Canvas>();var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;Check(size[0]+" "+button.name+" inside screen",r.rect.width>0&&r.rect.height>0&&corners.All(p=>{var point=RectTransformUtility.WorldToScreenPoint(camera,p);return point.x>=-1&&point.y>=-1&&point.x<=Screen.width+1&&point.y<=Screen.height+1;}));Check(size[0]+" "+button.name+" clickable",Hit(button));
    }
    Check(size[0]+" venue text fits",ui.venueButtons.SelectMany(b=>b.GetComponentsInChildren<TMPro.TMP_Text>()).All(t=>!t.isTextOverflowing));await Picture("layout-"+size[0]);
   }
   selection.SetValue(view,prior);view.Repaint();await Task.Delay(500);
   await Load("BotMatch");var m=UnityEngine.Object.FindAnyObjectByType<BotMatch>();m.opening?.Skip();m.Continue();m.enabled=false;var selector=m.GetComponent<CourtVenueSelection>();mobile=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset"));QualitySettings.renderPipeline=mobile;await Task.Delay(1200);
   foreach(var venue in new[]{CourtVenue.Professional,CourtVenue.LowPoly})
   {
    selector.Select(venue);var lighting=m.GetComponent<MatchEnvironmentLighting>();lighting.SelectForMatch(true);await Task.Delay(1000);Check("Mobile "+venue+" all four white spots active",lighting.stadium.poleIllumination.All(l=>l.enabled&&l.color==Color.white));await Picture("mobile-"+venue+"-night");
   }
   QualitySettings.renderPipeline=originalPipeline;await Task.Delay(600);
   await Load("Menu");probe=new GameObject("Venue Return Probe").AddComponent<VenueReturnProbe>();UnityEngine.Object.DontDestroyOnLoad(probe.gameObject);
   LobbyNavigation.OpenModes();await Wait(()=>SceneManager.GetActiveScene().name=="ModeSelection"&&!LobbyNavigation.IsBusy);await Task.Delay(300);LobbyNavigation.OpenTraining();await Wait(()=>SceneManager.GetActiveScene().name=="Training"&&!LobbyNavigation.IsBusy);await Task.Delay(1200);LobbyNavigation.OpenModes();await Wait(()=>SceneManager.GetActiveScene().name=="ModeSelection"&&!LobbyNavigation.IsBusy);await Task.Delay(300);
   probe.values.Clear();probe.recording=true;LobbyNavigation.ReturnToLobby();await Wait(()=>SceneManager.GetActiveScene().name=="Menu"&&!LobbyNavigation.IsBusy);await Task.Delay(5000);probe.recording=false;
   Check("Return resumes one active stadium",UnityEngine.Object.FindObjectsByType<StadiumEnvironment>().Length==1);Check("Return resumes one EventSystem",UnityEngine.Object.FindObjectsByType<EventSystem>().Length==1);Check("Return uses Professional",UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>().stadium.name.Contains("Professional"));
   Save("return-performance",Summary(probe.values));await Picture("menu-return");Save("extras-result",new{status="passed",count=checks.Count,checks,renderer="Mobile pipeline in desktop Editor; no physical phone measurement",hardware=new{SystemInfo.graphicsDeviceName,SystemInfo.processorType,SystemInfo.systemMemorySize}});
  }
  catch(Exception ex){Save("extras-result",new{status="failed",error=ex.ToString(),count=checks.Count,checks});Debug.LogException(ex);}
  finally
  {
   if(probe)UnityEngine.Object.Destroy(probe.gameObject);QualitySettings.renderPipeline=originalPipeline;if(mobile)UnityEngine.Object.Destroy(mobile);
   selection.SetValue(view,prior);int builtins=(int)gt.GetMethod("GetBuiltinCount").Invoke(group,null);for(int i=(int)gt.GetMethod("GetCustomCount").Invoke(group,null)-1;i>=count;i--)gt.GetMethod("RemoveCustomSize").Invoke(group,new object[]{builtins+i});view.Repaint();
   SceneManager.LoadScene("Menu");await Task.Delay(500);runtime.SwitchPlayer(originalId,null);foreach(string suffix in new[]{".json",".json.bak",".lock"}){string path=Path.Combine(Application.persistentDataPath,"LocalPlayers",id+suffix);if(File.Exists(path))File.Delete(path);}
  }
 }
}
