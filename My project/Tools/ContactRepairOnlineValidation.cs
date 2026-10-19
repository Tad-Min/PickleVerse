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
using PickleVerse.Progression;
using Newtonsoft.Json;
using LocalShot=PickleVerse.LocalMatch.ShotIntent;
using LocalKind=PickleVerse.LocalMatch.ShotKind;

public static class ContactRepairOnlineValidation
{
    const string Folder="../UnityWork/ContactRepair20261019";
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static Task running;
    static bool smashOnly;
    static readonly List<object> rows=new List<object>();
    static void Save(object value){Directory.CreateDirectory(Folder);File.WriteAllText(Folder+(smashOnly?"/online-smash.json":"/online.json"),JsonConvert.SerializeObject(value,Formatting.Indented));}
    static void Set(object target,string property,object value)=>target.GetType().GetProperty(property).GetSetMethod(true).Invoke(target,new[]{value});
    static void Field(object target,string field,object value)=>target.GetType().GetField(field,Hidden).SetValue(target,value);
    static void Loaded(Scene scene,LoadSceneMode mode){foreach(var b in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)))if(b is CourtBootstrap)b.enabled=false;}
    static async Task Load(){var op=SceneManager.LoadSceneAsync("MultiplayerCourt");while(!op.isDone)await Task.Delay(20);await Task.Delay(250);}
    static async Task<bool> Wait(Func<bool> ready,float seconds){var end=DateTime.UtcNow.AddSeconds(seconds);while(!ready()&&DateTime.UtcNow<end)await Task.Delay(8);return ready();}
    static void Press(NetworkPlayer player,int mask,bool serve)
    {var tick=typeof(NetworkPlayer).GetMethod("TickShot",Hidden);tick.Invoke(player,new object[]{0,false,serve});tick.Invoke(player,new object[]{mask,false,serve});tick.Invoke(player,new object[]{mask&24,false,serve});}
    public static object Start(){rows.Clear();running=Run();return new{started=true};}
    public static object SmashStart(){smashOnly=true;rows.Clear();running=Run();return new{started=true};}
    static async Task Run()
    {
        Save(new{status="running"});var runtime=ProgressionRuntime.Instance;string original=runtime.Player.id,qa="NetContactQA"+Guid.NewGuid().ToString("N").Substring(0,10);
        MultiplayerApp app=null;var random=UnityEngine.Random.state;SceneManager.sceneLoaded+=Loaded;
        try
        {
            runtime.SwitchPlayer(qa,"Online contact QA");
            for(int scenario=0;scenario<2;scenario++)
            {
                await Load();app=MultiplayerApp.Ensure();app.enabled=false;bool doubles=scenario==1;var mode=doubles?PickleVerse.Multiplayer.PlayMode.Doubles:PickleVerse.Multiplayer.PlayMode.Singles;
                Field(app,"roomData",new RoomData{custom=true,mode=mode});var roster=new MatchRoster{matchId="contact-"+Guid.NewGuid().ToString("N"),mode=mode,custom=true};int size=doubles?2:1;
                for(int i=0;i<size*2;i++)roster.members.Add(new RoomMember{id=i==0?app.PlayerId:"CONTACT-BOT"+i,name="Contact QA "+i,gender=i%2==0?"Female":"Male",team=i/size,bot=i!=0,elo=500,group="qa"});
                Set(app,"Roster",roster);app.ClientIdentities.Clear();app.Network.NetworkConfig.ConnectionApproval=false;app.Network.NetworkConfig.EnableSceneManagement=false;
                app.Network.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",(ushort)(19080+scenario),"127.0.0.1");if(!app.Network.StartHost())throw new Exception("Local host failed");app.ClientIdentities[app.Network.LocalClientId]=app.PlayerId;
                var go=UnityEngine.Object.Instantiate(app.Settings.matchPrefab);var match=go.GetComponent<NetworkMatch>();match.SetRoster(roster);go.GetComponent<NetworkObject>().Spawn();
                if(!await Wait(()=>match.Started.Value&&!MultiplayerCourtUI.Instance.cinematic.PlayingIntro,20))throw new Exception("Intro did not finish");
                match.enabled=false;
                // Disable random bot decisions for these deterministic technique cases.
                ((System.Collections.IDictionary)typeof(NetworkMatch).GetField("bots",Hidden).GetValue(match)).Clear();
                foreach(var player in NetworkPlayer.All){player.Automated.Value=false;player.ResetInput();}
                var begin=typeof(NetworkMatch).GetMethod("BeginRally",Hidden);
                foreach(bool female in new[]{true,false})
                foreach(int end in new[]{-1,1})
                {
                    var player=NetworkPlayer.All.First(p=>p.Female.Value==female);var actor=player.Actor;
                    var venue=end<0?PickleVerse.CourtVenue.Professional:PickleVerse.CourtVenue.LowPoly;
                    UnityEngine.Object.FindAnyObjectByType<CourtVenueSelection>().Select(venue);
                    foreach(int mask in smashOnly?Array.Empty<int>():new[]{1,9,17})
                    {
                        match.enabled=false;var rules=new PickleballRules(mode,size,size,player.Team.Value);rules.ends[player.Team.Value]=end;rules.ends[1-player.Team.Value]=-end;rules.serverIndex=player.Index.Value;rules.courtRight[player.Team.Value]=player.Index.Value;
                        Set(match,"Rules",rules);match.EndsSwapped.Value=rules.ends[0]>0;match.BetweenGames.Value=match.BetweenPoints.Value=match.Finished.Value=match.Paused.Value=false;begin.Invoke(match,null);
                        await Task.Delay(60);Press(player,mask,true);bool began=actor.Swinging&&actor.Intent.kind==LocalKind.Serve;match.enabled=true;
                        bool done=await Wait(()=>rules.strokeCount>0||rules.pendingFault>=0,2);
                        bool passed=began&&done&&rules.strokeCount>0&&rules.pendingFault<0&&actor.LastContactGrade!=HitGrade.None;
                        rows.Add(new{mode=mode.ToString(),test="Serve",female,end,mask,began,passed,stroke=rules.strokeCount,reason=rules.reason,grade=actor.LastContactGrade.ToString()});Save(new{status="running",rows});
                    }
                    foreach(int mask in smashOnly?new[]{5}:new[]{1,2,4,3,5,6,9,10,12,17,18,20})
                    {
                        match.enabled=false;var rules=new PickleballRules(mode,size,size,player.Team.Value);rules.ends[player.Team.Value]=end;rules.ends[1-player.Team.Value]=-end;Set(match,"Rules",rules);begin.Invoke(match,null);
                        rules.waitingServe=false;rules.live=true;rules.lastHitTeam=1-player.Team.Value;rules.strokeCount=3;rules.openingBounce[0]=rules.openingBounce[1]=true;
                        var input=new ShotInput();input.Tick(mask,false,false,out _);input.Tick(mask&24,false,false,out var shot);rules.bounceCount=shot.AfterBounce?1:0;
                        player.Teleport(new Vector3(.6f,0,end*3.5f));actor.RefreshPose();
                        Vector3 target=actor.transform.TransformPoint(actor.ContactOffset(shot));Vector3 velocity=new Vector3(0,0,end*6);
                        Vector3 origin=target-velocity*shot.ContactDelay+Vector3.up*(.5f*BallFlight.Gravity*shot.ContactDelay*shot.ContactDelay);
                        Field(match,"ballPosition",origin);Field(match,"ballVelocity",velocity);await Task.Delay(40);
                        Press(player,mask,false);bool began=actor.Swinging&&actor.Intent.kind==shot.kind;match.enabled=true;
                        bool done=await Wait(()=>rules.strokeCount>3||rules.pendingFault>=0,1.5f);
                        bool passed=began&&done&&rules.strokeCount>3&&rules.pendingFault<0&&actor.LastContactGrade!=HitGrade.None;
                        rows.Add(new{mode=mode.ToString(),test="Rally",female,end,mask,kind=shot.kind.ToString(),began,passed,stroke=rules.strokeCount,reason=rules.reason,grade=actor.LastContactGrade.ToString()});Save(new{status="running",rows});
                    }
                }
                Field(app,"intentionalShutdown",true);app.Network.Shutdown();await Task.Delay(300);UnityEngine.Object.Destroy(app.Network.gameObject);UnityEngine.Object.Destroy(app.gameObject);await Task.Delay(180);app=null;
            }
            Save(new{passed=rows.All(r=>(bool)r.GetType().GetProperty("passed").GetValue(r)),tests=rows.Count,scope="Local NGO host, real FixedUpdate simulation, actual input-to-launch paths; no two-PC network test",rows});
        }
        catch(Exception e){Save(new{passed=false,error=e.ToString(),rows});Debug.LogException(e);}
        finally
        {
            if(app){Field(app,"intentionalShutdown",true);app.Network.Shutdown();await Task.Delay(300);UnityEngine.Object.Destroy(app.Network.gameObject);UnityEngine.Object.Destroy(app.gameObject);}
            SceneManager.sceneLoaded-=Loaded;SceneManager.LoadScene("Menu");await Task.Delay(300);runtime.SwitchPlayer(original,null);UnityEngine.Random.state=random;
            string root=Path.Combine(Application.persistentDataPath,"LocalPlayers");foreach(string suffix in new[]{".json",".json.bak",".lock"}){string file=Path.Combine(root,qa+suffix);if(File.Exists(file))File.Delete(file);}
        }
    }
}
