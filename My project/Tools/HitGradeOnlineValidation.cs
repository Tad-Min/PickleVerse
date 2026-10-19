using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using PickleVerse.LocalMatch;
using PickleVerse.Multiplayer;
using PickleVerse.Audio;
using PickleVerse.Progression;
using Newtonsoft.Json;

public static class HitGradeOnlineValidation
{
    const string Folder="../UnityWork/HitGrades20261019";
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static Task task;
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool passed){checks.Add(new{name,passed});if(!passed)throw new Exception(name);}
    static void Save(string name,object value){Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/"+name+".json",JsonConvert.SerializeObject(value,Formatting.Indented));}
    static void Set(object target,string property,object value)=>target.GetType().GetProperty(property).GetSetMethod(true).Invoke(target,new[]{value});
    static void Loaded(Scene scene,LoadSceneMode mode)
    {foreach(var b in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)))if(b is CourtBootstrap)b.enabled=false;}
    static async Task Load(string name){var op=SceneManager.LoadSceneAsync(name);while(!op.isDone)await Task.Delay(30);await Task.Delay(250);}
    static async Task Wait(Func<bool> condition){var limit=DateTime.UtcNow.AddSeconds(20);while(!condition()){if(DateTime.UtcNow>limit)throw new Exception("Timed out");await Task.Delay(80);}}
    public static object Start(){checks.Clear();task=Run();return new{started=true};}
    static async Task Run()
    {
        Save("online",new{status="running"});var runtime=ProgressionRuntime.Instance;string original=runtime.Player.id,qa="HitOnlineQA"+Guid.NewGuid().ToString("N").Substring(0,10);
        var random=UnityEngine.Random.state;MultiplayerApp app=null;SceneManager.sceneLoaded+=Loaded;
        try
        {
            runtime.SwitchPlayer(qa,"Hit online QA");
            for(int scenario=0;scenario<2;scenario++)
            {
                bool doubles=scenario==1;await Load("MultiplayerCourt");app=MultiplayerApp.Ensure();app.enabled=false;
                typeof(MultiplayerApp).GetField("roomData",Hidden).SetValue(app,new RoomData{custom=true,mode=doubles?PickleVerse.Multiplayer.PlayMode.Doubles:PickleVerse.Multiplayer.PlayMode.Singles});
                var roster=new MatchRoster{matchId="hit-"+Guid.NewGuid().ToString("N"),mode=doubles?PickleVerse.Multiplayer.PlayMode.Doubles:PickleVerse.Multiplayer.PlayMode.Singles,custom=true};int perTeam=doubles?2:1;
                for(int i=0;i<perTeam*2;i++)roster.members.Add(new RoomMember{id=i==0?app.PlayerId:"HIT-QA"+i,name="QA "+i,gender=i%2==0?"Male":"Female",team=i/perTeam,bot=i!=0,elo=500,group="qa"});
                Set(app,"Roster",roster);app.ClientIdentities.Clear();app.Network.NetworkConfig.ConnectionApproval=false;app.Network.NetworkConfig.EnableSceneManagement=false;
                app.Network.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",(ushort)(18980+scenario),"127.0.0.1");
                Check("Local NGO host starts "+scenario,app.Network.StartHost());app.ClientIdentities[app.Network.LocalClientId]=app.PlayerId;
                var go=UnityEngine.Object.Instantiate(app.Settings.matchPrefab);var match=go.GetComponent<NetworkMatch>();match.SetRoster(roster);go.GetComponent<NetworkObject>().Spawn();
                await Wait(()=>match.Started.Value&&!MultiplayerCourtUI.Instance.cinematic.PlayingIntro);
                match.enabled=false;match.Paused.Value=true;
                var local=NetworkPlayer.All.Single(p=>p.IsLocalHuman);var actor=local.Actor;
                Check("Only local owner has grade UI "+scenario,actor.LocalHitFeedback&&actor.GetComponent<HitGradeFeedback>()&&NetworkPlayer.All.Where(p=>p!=local).All(p=>!p.Actor.LocalHitFeedback&&!p.Actor.GetComponent<HitGradeFeedback>()));
                foreach(var wanted in new[]{HitGrade.Perfect,HitGrade.Good})
                {
                    actor.ResetAt(new Vector3(.5f,0,-4),-1);actor.RefreshPose();var shot=new PickleVerse.LocalMatch.ShotIntent(PickleVerse.LocalMatch.ShotKind.Drive);
                    Vector3 target=actor.transform.TransformPoint(actor.ContactOffset(shot));
                    actor.Swing(shot,target);for(int i=0;i<52;i++)actor.Step(Vector2.zero,false,1f/120,false);
                    Vector3 point=actor.paddleFace.position;bool found=false;
                    foreach(Vector3 offset in wanted==HitGrade.Perfect?new[]{Vector3.zero}:new[]{Vector3.right*.25f,Vector3.left*.25f,Vector3.forward*.25f,Vector3.back*.25f})
                    {point=actor.paddleFace.position+offset;if(actor.CanContact(point,point,.45f,out _)&&(wanted==HitGrade.Perfect||offset!=Vector3.zero)){found=true;break;}}
                    Check("Actual hitbox contact "+scenario+" "+wanted,found);
                    int audio=SfxPlayer.PlayCounts[(int)GameSfx.HitNormal],fx=actor.GetComponent<PaddleImpactVfx>().PlayCount,shown=actor.GetComponent<HitGradeFeedback>().ShowCount;
                    local.ConsumeContact(false,point,true,Vector3.forward*6);
                    Check("Authoritative grade "+scenario+" "+wanted,actor.LastContactGrade==wanted&&actor.GetComponent<HitGradeFeedback>().LastGrade==wanted);
                    Check("Host RPC does not duplicate impact "+scenario+" "+wanted,SfxPlayer.PlayCounts[(int)GameSfx.HitNormal]==audio+1&&actor.GetComponent<PaddleImpactVfx>().PlayCount==fx+1&&actor.GetComponent<HitGradeFeedback>().ShowCount==shown+1);
                }
                Check("Contact RPC includes server grade "+scenario,typeof(NetworkPlayer).GetMethod("ContactClientRpc",Hidden).GetParameters().Last().ParameterType==typeof(byte));
                typeof(MultiplayerApp).GetField("intentionalShutdown",Hidden).SetValue(app,true);app.Network.Shutdown();await Task.Delay(300);UnityEngine.Object.Destroy(app.Network.gameObject);UnityEngine.Object.Destroy(app.gameObject);await Task.Delay(150);app=null;
            }
            Save("online",new{passed=true,checks,scope="Real local NGO host, 1v1 and 2v2; remote two-PC transport not exercised"});
        }
        catch(Exception e){Save("online",new{passed=false,error=e.ToString(),checks});Debug.LogException(e);}
        finally
        {
            if(app){typeof(MultiplayerApp).GetField("intentionalShutdown",Hidden).SetValue(app,true);app.Network.Shutdown();await Task.Delay(300);UnityEngine.Object.Destroy(app.Network.gameObject);UnityEngine.Object.Destroy(app.gameObject);}
            SceneManager.sceneLoaded-=Loaded;await Load("Menu");runtime.SwitchPlayer(original,null);UnityEngine.Random.state=random;
            string root=Path.Combine(Application.persistentDataPath,"LocalPlayers");foreach(string suffix in new[]{".json",".json.bak",".lock"}){string file=Path.Combine(root,qa+suffix);if(File.Exists(file))File.Delete(file);}
        }
    }
}
