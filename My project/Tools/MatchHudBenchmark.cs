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
using Unity.Profiling;
using PickleVerse.LocalMatch;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using Newtonsoft.Json;

public sealed class MatchHudFrameProbe:MonoBehaviour
{
    public bool recording;public int focused;
    public readonly List<float> frames=new List<float>(4096),gc=new List<float>(4096);
    public ProfilerRecorder allocations;
    void Update(){if(!recording)return;if(Application.isFocused)focused++;frames.Add(Time.unscaledDeltaTime*1000);gc.Add(allocations.LastValue);}
    public void Clear(){focused=0;frames.Clear();gc.Clear();}
}
public static class MatchHudBenchmark
{
    const string Folder="../UnityWork/MatchHud20261020";
    static Task task;static readonly BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    static void Save(string name,object data){Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/"+name+".json",JsonConvert.SerializeObject(data,Formatting.Indented));}
    public static object Before(){task=Run(false);return "Baseline started";}
    public static object After(){task=Run(true);return "Comparison started";}
    static void Loaded(Scene scene,LoadSceneMode mode){foreach(var b in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CourtBootstrap>(true)))b.enabled=false;}
    static async Task Load(string name){var op=SceneManager.LoadSceneAsync(name);while(!op.isDone)await Task.Delay(25);await Task.Delay(200);}
    static async Task Wait(Func<bool> predicate){var until=DateTime.UtcNow.AddSeconds(40);while(!predicate()){if(DateTime.UtcNow>until)throw new Exception("Court not ready");await Task.Delay(50);}}
    static object Micro(MonoBehaviour target)
    {
        var update=target.GetType().GetMethod("Update",Hidden);for(int i=0;i<30;i++)update.Invoke(target,null);
        long memory=GC.GetAllocatedBytesForCurrentThread();var timer=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<1200;i++)update.Invoke(target,null);timer.Stop();
        long allocated=GC.GetAllocatedBytesForCurrentThread()-memory;
        return new{microseconds=timer.Elapsed.TotalMilliseconds*1000/1200,bytes=allocated/1200.0,allocationCounterAvailable=allocated>0};
    }
    static async Task<object> Measure(MatchHudFrameProbe probe,string stage,MonoBehaviour controller)
    {
        var game=UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));game.Show();game.Focus();
        await Task.Delay(3000);probe.Clear();probe.recording=true;await Task.Delay(6000);probe.recording=false;var frames=probe.frames.OrderBy(x=>x).ToArray();
        var micro=Micro(controller);
        return new{stage,frames=frames.Length,probe.focused,width=Screen.width,height=Screen.height,fps=1000/frames.Average(),p95=frames[(int)((frames.Length-1)*.95)],max=frames.Last(),gcBytes=probe.gc.Average(),controller=micro};
    }
    static async Task Shutdown(MultiplayerApp app)
    {if(!app)return;typeof(MultiplayerApp).GetField("intentionalShutdown",Hidden).SetValue(app,true);app.Network.Shutdown();await Task.Delay(200);UnityEngine.Object.Destroy(app.Network.gameObject);UnityEngine.Object.Destroy(app.gameObject);await Task.Delay(100);}
    static async Task Run(bool after)
    {
        string file=after?"performance-after":"performance-before";Save(file,new{status="running"});
        var runtime=ProgressionRuntime.Instance;string original=runtime.Player.id,qa="HudQA"+Guid.NewGuid().ToString("N").Substring(0,8);File.WriteAllText(Folder+"/original-profile.txt",original);var rows=new List<object>();MatchHudFrameProbe probe=null;MultiplayerApp app=null;
        SceneManager.sceneLoaded+=Loaded;
        try
        {
            runtime.SwitchPlayer(qa,"HUD QA");probe=new GameObject("HUD frame probe").AddComponent<MatchHudFrameProbe>();UnityEngine.Object.DontDestroyOnLoad(probe.gameObject);probe.allocations=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
            await Load("BotMatch");var bot=UnityEngine.Object.FindAnyObjectByType<BotMatch>();bot.Continue();bot.opening?.Skip();bot.autoPlayer=true;
            rows.Add(await Measure(probe,"BotMatch day",bot.hud));bot.GetComponent<MatchEnvironmentLighting>().Apply(true);rows.Add(await Measure(probe,"BotMatch night",bot.hud));Save(file,new{status="running",rows});
            for(int mode=0;mode<2;mode++)
            {
                await Load("MultiplayerCourt");app=MultiplayerApp.Ensure();app.enabled=false;
                var playMode=mode==0?PickleVerse.Multiplayer.PlayMode.Singles:PickleVerse.Multiplayer.PlayMode.Doubles;
                typeof(MultiplayerApp).GetField("roomData",Hidden).SetValue(app,new RoomData{custom=true,mode=playMode});var roster=new MatchRoster{matchId="hud-"+Guid.NewGuid().ToString("N"),custom=true,mode=playMode};int perTeam=mode+1;
                for(int i=0;i<perTeam*2;i++)roster.members.Add(new RoomMember{id=i==0?app.PlayerId:"HUD-BOT"+i,name=i==0?"Người chơi thử":"Đối thủ "+i,gender=i%2==0?"Female":"Male",team=i/perTeam,bot=i!=0,elo=500,group="qa"});
                typeof(MultiplayerApp).GetProperty("Roster").GetSetMethod(true).Invoke(app,new object[]{roster});app.ClientIdentities.Clear();app.Network.NetworkConfig.ConnectionApproval=false;app.Network.NetworkConfig.EnableSceneManagement=false;app.Network.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",(ushort)(19240+mode),"127.0.0.1");
                if(!app.Network.StartHost())throw new Exception("Local host failed");app.ClientIdentities[app.Network.LocalClientId]=app.PlayerId;
                var go=UnityEngine.Object.Instantiate(app.Settings.matchPrefab);var match=go.GetComponent<NetworkMatch>();match.SetRoster(roster);go.GetComponent<NetworkObject>().Spawn();await Wait(()=>match.Started.Value&&!MultiplayerCourtUI.Instance.cinematic.PlayingIntro);
                rows.Add(await Measure(probe,"Multiplayer "+(mode==0?"1v1":"2v2"),MultiplayerCourtUI.Instance));Save(file,new{status="running",rows});await Shutdown(app);app=null;
            }
            Save(file,new{passed=true,device=SystemInfo.graphicsDeviceName,processor=SystemInfo.processorType,scope="Unity Editor real court render; NGO local host with bot teammates/opponents, not remote Internet latency",rows});
        }
        catch(Exception e){Save(file,new{passed=false,error=e.ToString(),rows});Debug.LogException(e);}
        finally
        {
            if(probe){probe.recording=false;probe.allocations.Dispose();UnityEngine.Object.Destroy(probe.gameObject);}await Shutdown(app);SceneManager.sceneLoaded-=Loaded;
            await Load("ModeSelection");runtime.SwitchPlayer(original,null);string root=Path.Combine(Application.persistentDataPath,"LocalPlayers");foreach(var suffix in new[]{".json",".json.bak",".lock"}){var path=Path.Combine(root,qa+suffix);if(File.Exists(path))File.Delete(path);}
        }
    }
}
