using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using PickleVerse.Multiplayer;
using PickleVerse.LocalMatch;
using PickleVerse.Progression;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public static class MultiplayerPresentationValidation
{
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Set(object obj,string property,object value)=>obj.GetType().GetProperty(property).GetSetMethod(true).Invoke(obj,new[]{value});
    static void Loaded(Scene s,LoadSceneMode mode){if(s.name=="MultiplayerCourt"){var b=UnityEngine.Object.FindAnyObjectByType<CourtBootstrap>();if(b)b.enabled=false;}}
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first");
        Directory.CreateDirectory("../UnityWork/MultiplayerPresentation");
        var checks=new List<object>();Action<string,bool> check=(name,ok)=>{checks.Add(new{name,passed=ok});if(!ok)throw new Exception(name);};
        var runtime=ProgressionRuntime.Instance;string original=runtime.Player.id,id="MPView"+Guid.NewGuid().ToString("N").Substring(0,10);
        const string pref="PKV.Bot.Night";bool had=PlayerPrefs.HasKey(pref);int night=PlayerPrefs.GetInt(pref);
        MultiplayerApp app=null;
        SceneManager.sceneLoaded+=Loaded;
        try
        {
            runtime.SwitchPlayer(id,"Cinematic QA");runtime.Player.elo=500;
            var viewType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.GameView")).First(t=>t!=null);var window=UnityEditor.EditorWindow.GetWindow(viewType);window.Show();window.Focus();
            for(int scenario=0;scenario<4;scenario++)
            {
                bool doubles=scenario>=2,won=scenario%2==0,female=scenario%2!=0;
                SceneManager.LoadScene("MultiplayerCourt");await Task.Delay(400);
                app=MultiplayerApp.Ensure();app.enabled=false;
                typeof(MultiplayerApp).GetField("roomData",Hidden).SetValue(app,new RoomData{custom=doubles,mode=doubles?PickleVerse.Multiplayer.PlayMode.Doubles:PickleVerse.Multiplayer.PlayMode.Singles});
                var roster=new MatchRoster{matchId="view-"+Guid.NewGuid().ToString("N"),mode=doubles?PickleVerse.Multiplayer.PlayMode.Doubles:PickleVerse.Multiplayer.PlayMode.Singles,custom=doubles};
                int perTeam=doubles?2:1;
                for(int i=0;i<perTeam*2;i++)roster.members.Add(new RoomMember{id=i==0?app.PlayerId:"QA-BOT"+i,name="QA "+i,gender=i==0?(female?"Female":"Male"):i%2==0?"Female":"Male",team=i/perTeam,bot=i!=0,elo=i==0?runtime.Player.elo:620,group="qa"});
                Set(app,"Roster",roster);app.ClientIdentities.Clear();app.Network.NetworkConfig.ConnectionApproval=false;app.Network.NetworkConfig.EnableSceneManagement=false;
                var transport=app.Network.GetComponent<UnityTransport>();transport.SetConnectionData("127.0.0.1",(ushort)(18740+scenario),"127.0.0.1");
                check("loopback host "+scenario,app.Network.StartHost());app.ClientIdentities[app.Network.LocalClientId]=app.PlayerId;
                PlayerPrefs.SetInt(pref,doubles?1:0);
                var go=UnityEngine.Object.Instantiate(app.Settings.matchPrefab);var match=go.GetComponent<NetworkMatch>();match.SetRoster(roster);go.GetComponent<NetworkObject>().Spawn();
                await Task.Delay(600);var ui=MultiplayerCourtUI.Instance;var cinematic=ui.cinematic;
                check("intro starts before serve "+scenario,cinematic.PlayingIntro&&!match.Started.Value&&runtime.Player.pendingMatch==null);
                var server=NetworkPlayer.All.First(p=>p.Team.Value==match.ServerTeam.Value);
                match.TrySwing(server,new PickleVerse.Multiplayer.ShotIntent{kind=PickleVerse.Multiplayer.ShotKind.Groundstroke});
                check("shot ignored during intro "+scenario,!server.Actor.Swinging);
                // Sample both team entrances using the actual synchronized clock/pose path.
                foreach(float elapsed in new[]{2f,6f})
                {
                    await Task.Delay(elapsed<4.6f?1600:4000);
                    var group=NetworkPlayer.All.Where(p=>(p.Team.Value==0)==(elapsed<4.6f)).ToArray();
                    check("entrance visible "+scenario+" @"+elapsed,group.All(p=>{var v=Camera.main.WorldToViewportPoint(p.Actor.transform.position+Vector3.up);return v.z>0&&v.x>.03&&v.x<.97&&v.y>.08&&v.y<.93;}));
                }
                await Task.Delay(4200);
                check("serve starts after intro "+scenario,match.Started.Value&&!cinematic.PlayingIntro&&match.WaitingServe.Value&&runtime.Player.pendingMatch!=null);
                var environment=UnityEngine.Object.FindAnyObjectByType<MatchEnvironmentLighting>();var stadium=environment.stadium;
                check("day/night fixtures "+scenario,environment.IsNight==doubles&&stadium.poleIllumination.All(l=>l.enabled&&Mathf.Approximately(l.intensity,doubles?45:8))&&stadium.roofIllumination.All(l=>l.enabled==doubles)&&stadium.standIllumination.All(l=>l.enabled==doubles));
                match.Rules.Forfeit(won?1:0,"QA result");typeof(NetworkMatch).GetMethod("Finish",Hidden).Invoke(match,null);
                await Task.Delay(350);
                check("end banner holds camera "+scenario,ui.EndAnnouncementReceived&&!ui.EndAnnouncementFinished&&!cinematic.ShowingOutcome&&!ui.resultScreen.IsVisible);
                await Task.Delay(3300);window.Repaint();await Task.Delay(500);
                check("local team framed "+scenario,cinematic.ShowingOutcome&&cinematic.OutcomeReady&&cinematic.PlayerWon==won&&cinematic.PresentedCount==perTeam);
                var localTeam=NetworkPlayer.All.Where(p=>p.Team.Value==0).ToArray();
                File.WriteAllText("../UnityWork/MultiplayerPresentation/view-"+scenario+".json",Newtonsoft.Json.JsonConvert.SerializeObject(new{camera=Camera.main.transform.position.ToString(),fov=Camera.main.fieldOfView,aspect=Camera.main.aspect,team=localTeam.Select(p=>new{position=p.Actor.transform.position.ToString(),feet=Camera.main.WorldToViewportPoint(p.Actor.transform.position).ToString(),head=Camera.main.WorldToViewportPoint(p.Actor.transform.position+Vector3.up*2.1f).ToString()}).ToArray()},Newtonsoft.Json.Formatting.Indented));
                check("head and feet fit "+scenario,localTeam.All(p=>new[]{p.Actor.transform.position,p.Actor.transform.position+Vector3.up*2.1f}.All(pos=>{var v=Camera.main.WorldToViewportPoint(pos);return v.z>0&&v.x>.05&&v.x<.95&&v.y>.24&&v.y<.91;})));
                var title=cinematic.transform.Find("CourtCinematic/SafeArea/Outcome").GetComponent<TMPro.TMP_Text>();
                check("Vietnamese result and shine "+scenario,title.text==(won?"Thắng":"Thua")&&cinematic.transform.Find("CourtCinematic/DefeatShade").gameObject.activeSelf==!won);
                string image="../UnityWork/MultiplayerPresentation/"+(doubles?"doubles":"singles")+(won?"-win":"-loss")+".png";
                var shot=UnityEditor.Handles.GetMainGameViewSize();var capture=UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(window.position.position+new Vector2(0,40),(int)window.position.width,(int)window.position.height-40);
                var texture=new Texture2D((int)window.position.width,(int)window.position.height-40);texture.SetPixels(capture);texture.Apply();File.WriteAllBytes(image,texture.EncodeToPNG());UnityEngine.Object.Destroy(texture);
                var next=cinematic.transform.Find("CourtCinematic/SafeArea/Continue").GetComponent<UnityEngine.UI.Button>();
                var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,next.transform.position)};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
                check("continue receives pointer "+scenario,hits.Count>0&&hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==next);
                next.onClick.Invoke();await Task.Delay(200);
                var saved=runtime.Player.history.Find(r=>r.id==roster.matchId);
                check("continue opens actual saved Elo "+scenario,saved!=null&&ui.resultScreen.IsVisible&&ui.resultScreen.RenderedMatchId==saved.id&&ui.resultScreen.delta.text.Contains(saved.eloDelta.ToString()));
                typeof(MultiplayerApp).GetField("intentionalShutdown",Hidden).SetValue(app,true);app.Network.Shutdown();await Task.Delay(350);UnityEngine.Object.Destroy(app.Network.gameObject);UnityEngine.Object.Destroy(app.gameObject);await Task.Delay(100);app=null;
            }
            var report=new{passed=true,count=checks.Count,checks,scope="Loopback host, actual NGO spawn and clock; teammates/opponents simulated bots. No live Relay/matchmaking test."};
            File.WriteAllText("../UnityWork/MultiplayerPresentation/presentation-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
        }
        finally
        {
            if(app){typeof(MultiplayerApp).GetField("intentionalShutdown",Hidden).SetValue(app,true);app.Network.Shutdown();await Task.Delay(300);UnityEngine.Object.Destroy(app.Network.gameObject);UnityEngine.Object.Destroy(app.gameObject);}
            SceneManager.sceneLoaded-=Loaded;SceneManager.LoadScene("ModeSelection");await Task.Delay(300);runtime.SwitchPlayer(original,null);
            if(had)PlayerPrefs.SetInt(pref,night);else PlayerPrefs.DeleteKey(pref);PlayerPrefs.Save();
            foreach(var suffix in new[]{".json",".json.bak",".lock"}){var file=Path.Combine(Application.persistentDataPath,"LocalPlayers",id+suffix);if(File.Exists(file))File.Delete(file);}
        }
    }
}

