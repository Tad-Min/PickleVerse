using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Multiplayer;
using PickleVerse.Multiplayer;
using PickleVerse.Multiplayer.Services;

// In-memory Sessions adapter: tests the real app methods without sending public matchmaking tickets.
public class ReadySdkProxy:DispatchProxy
{
    public Func<MethodInfo,object[],object> Call;
    protected override object Invoke(MethodInfo method,object[] args)=>Call(method,args);
    public static T Make<T>(Func<MethodInfo,object[],object> call)where T:class
    {var p=Create<T,ReadySdkProxy>();((ReadySdkProxy)(object)p).Call=call;return p;}
}
public static class MatchReadyIntegrationValidation
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    sealed class Identity:IPlayerIdentityService
    {public string PlayerId=>"qa-local";public string Profile=>"ReadyQA";public string DisplayName{get;set;}="QA";public Task InitializeAsync(string p,string e)=>Task.CompletedTask;public string ResolveName(string n)=>n;}
    sealed class Session
    {
        public readonly Dictionary<string,SessionProperty> props=new Dictionary<string,SessionProperty>();
        public readonly List<IReadOnlyPlayer> players=new List<IReadOnlyPlayer>();
        public readonly Dictionary<string,Dictionary<string,PlayerProperty>> data=new Dictionary<string,Dictionary<string,PlayerProperty>>();
        public IHostSession sdk;public bool Left;public int NetworkStarts;
        public readonly bool host;
        public NetworkState NetworkState=NetworkState.Stopped;
        public Session(int count,bool isHost=true)
        {
            host=isHost;
            var network=ReadySdkProxy.Make<IHostSessionNetwork>((m,a)=>m.Name=="get_State"?NetworkState:m.Name=="StartRelayNetworkAsync"?StartNetwork():null);
            var clientNetwork=ReadySdkProxy.Make<IClientSessionNetwork>((m,a)=>m.Name=="get_State"?NetworkState:null);
            for(int i=0;i<count;i++)
            {
                string id=i==0?"qa-local":"qa-"+i;var p=new Dictionary<string,PlayerProperty>();data[id]=p;
                players.Add(ReadySdkProxy.Make<IPlayer>((m,a)=>m.Name=="get_Id"?id:m.Name=="get_Properties"?p:m.Name=="SetProperty"?SetPlayer(p,a):null));
            }
            sdk=ReadySdkProxy.Make<IHostSession>((m,a)=>
            {
                switch(m.Name)
                {
                    case "get_IsHost":return host;
                    case "get_Network":return m.ReturnType==typeof(IHostSessionNetwork)?network:(object)clientNetwork;
                    case "get_Id":return "qa-session";
                    case "get_Properties":return props;
                    case "get_Players":return m.ReturnType==typeof(IReadOnlyList<IPlayer>)?players.Cast<IPlayer>().ToList():(object)players;
                    case "get_CurrentPlayer":return players[0];
                    case "AsHost":return sdk;
                    case "SetProperty":props[(string)a[0]]=(SessionProperty)a[1];return null;
                    case "LeaveAsync":Left=true;return Task.CompletedTask;
                    case "SavePropertiesAsync":case "RefreshAsync":case "SaveCurrentPlayerDataAsync":return Task.CompletedTask;
                    default:return m.ReturnType==typeof(bool)?(object)false:null;
                }
            });
        }
        Task StartNetwork(){NetworkStarts++;NetworkState=NetworkState.Started;return Task.CompletedTask;}
        static object SetPlayer(Dictionary<string,PlayerProperty> p,object[] args){p[(string)args[0]]=(PlayerProperty)args[1];return null;}
    }
    static void Set(object o,string property,object value)=>o.GetType().GetProperty(property).GetSetMethod(true).Invoke(o,new[]{value});
    static Task Invoke(MultiplayerApp a,string name,params object[] args)=>(Task)typeof(MultiplayerApp).GetMethod(name,Hidden).Invoke(a,args);
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first");
        var app=MultiplayerApp.Ensure();app.enabled=false;
        var identity=typeof(MultiplayerApp).GetField("identity",Hidden);var original=identity.GetValue(app);identity.SetValue(app,new Identity());
        var checks=new List<object>();Action<string,bool> check=(name,ok)=>{checks.Add(new{name,passed=ok});if(!ok)throw new Exception(name);};
        try
        {
            for(int test=0;test<4;test++)
            {
                int count=test==0?4:2;
                var session=new Session(count);var roster=new MatchRoster{matchId="qa-"+test,mode=count==4?PickleVerse.Multiplayer.PlayMode.Doubles:PickleVerse.Multiplayer.PlayMode.Singles};
                for(int i=0;i<count;i++)roster.members.Add(new RoomMember{id=i==0?"qa-local":"qa-"+i,team=i/(count/2)});
                Set(app,"Mode",roster.mode);Set(app,"Roster",roster);Set(app,"Match",session.sdk);Set(app,"State",FlowState.Connecting);
                await Invoke(app,"InitializeReadyAsync");
                check("accept works before gameplay transport "+test,app.CanAcceptMatch&&!app.Network.IsListening);
                await Invoke(app,"StartAcceptedNetworkAsync");check("no allocation before approval "+test,session.NetworkStarts==0);
                await Invoke(app,"TickReadyAsync",true);
                check("deadline and modal "+test,app.ReadyCheckActive&&app.CanAcceptMatch&&app.ReadySeconds>18);
                if(test==0)
                {
                    var window=UnityEditor.EditorWindow.GetWindow(AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.GameView")).First(t=>t!=null));window.Show();window.Focus();window.Repaint();await Task.Delay(500);
                    var overlay=app.GetComponent<MatchReadyOverlay>();var canvas=overlay.GetComponentsInChildren<Canvas>(true).Single();
                    var accept=canvas.transform.Find("SafeArea/Card/Accept").GetComponent<UnityEngine.UI.Button>();
                    var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,accept.transform.position)};var hits=new List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(data,hits);
                    check("accept receives pointer above room UI",hits.Count>0&&hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==accept);
                    var pixels=UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(window.position.position+new Vector2(0,40),(int)window.position.width,(int)window.position.height-40);var texture=new Texture2D((int)window.position.width,(int)window.position.height-40);texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes("../UnityWork/MultiplayerPresentation/match-found.png",texture.EncodeToPNG());UnityEngine.Object.Destroy(texture);
                }
                if(test!=2)await app.AcceptMatchAsync();
                if(test==0)
                {
                    check("accepted waits for every teammate",app.ReadyCheckAccepted&&app.ReadyCheckActive);
                    typeof(MultiplayerApp).GetMethod("RemovedFromRoom",Hidden).Invoke(app,null);
                    check("party removal preserves accepted doubles match",app.ReadyCheckAccepted&&app.ReadyCheckActive&&app.State==FlowState.Connecting);
                    for(int i=1;i<count;i++)session.data["qa-"+i]["readyDecision"]=new PlayerProperty("accepted");
                    await Invoke(app,"TickReadyAsync",true);
                    check("all four approve the same match",session.props["readyPhase"].Value=="go"&&!app.ReadyCheckActive);
                    await Invoke(app,"StartAcceptedNetworkAsync");await Invoke(app,"StartAcceptedNetworkAsync");check("transport starts once after every acceptance",session.NetworkStarts==1);
                    Set(app,"Match",null);
                }
                else if(test==1)
                {
                    session.data["qa-1"]["readyDecision"]=new PlayerProperty("declined");await Invoke(app,"TickReadyAsync",true);
                    check("accepted player automatically searches again",session.Left&&app.Match==null&&app.State==FlowState.Delay);
                }
                else if(test==2)
                {
                    await app.DeclineMatchAsync();
                    check("declining player stops searching",session.Left&&app.Match==null&&app.State==FlowState.WaitingRoom&&!app.Searching);
                }
                else
                {
                    session.props["readyDeadline"]=new SessionProperty(DateTime.UtcNow.AddSeconds(-1).Ticks.ToString());await Invoke(app,"TickReadyAsync",true);
                    check("timeout requeues the accepting player",session.Left&&app.Match==null&&app.State==FlowState.Delay);
                }
            }
            var guestSession=new Session(2,false);
            Set(app,"Roster",new MatchRoster{matchId="guest-qa",mode=PickleVerse.Multiplayer.PlayMode.Singles,members=new List<RoomMember>{new RoomMember{id="qa-local",team=0},new RoomMember{id="qa-1",team=1}}});
            Set(app,"Match",guestSession.sdk);Set(app,"State",FlowState.Connecting);
            await Invoke(app,"InitializeReadyAsync");await Invoke(app,"TickReadyAsync",false);
            check("guest can accept while host has not started countdown",app.CanAcceptMatch&&!app.Network.IsConnectedClient);
            await app.AcceptMatchAsync();check("guest publishes acceptance without gameplay connection",app.ReadyCheckAccepted);
            guestSession.props["readyPhase"]=new SessionProperty("go");await Invoke(app,"TickReadyAsync",true);
            check("guest observes common approval",!app.ReadyCheckActive);
            var report=new{passed=true,count=checks.Count,checks,scope="Actual MultiplayerApp ready methods with in-memory MPS interfaces; no live Relay."};Directory.CreateDirectory("../UnityWork/MultiplayerPresentation");File.WriteAllText("../UnityWork/MultiplayerPresentation/ready-integration.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
        }
        finally
        {
            typeof(MultiplayerApp).GetField("intentionalShutdown",Hidden).SetValue(app,true);Set(app,"Match",null);Set(app,"State",FlowState.Menu);identity.SetValue(app,original);app.Network.Shutdown();UnityEngine.Object.Destroy(app.Network.gameObject);UnityEngine.Object.Destroy(app.gameObject);
        }
    }
}
