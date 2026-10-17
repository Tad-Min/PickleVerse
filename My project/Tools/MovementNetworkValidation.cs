using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using PickleVerse.Rules;
using PlayMode=PickleVerse.Multiplayer.PlayMode;

public static class MovementNetworkValidation
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly List<object> checks=new List<object>();
    static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Hidden).Invoke(o,args);
    static void Check(string name,bool ok){checks.Add(new{name,passed=ok});if(!ok)throw new Exception(name);}
    static void Ready(NetworkMatch m,NetworkPlayer human,bool serve=false)
    {
        typeof(NetworkMatch).GetField("<Rules>k__BackingField",Hidden).SetValue(m,new PickleballRules(PlayMode.Doubles,2,2,0));
        Call(m,"BeginRally");
        if(!serve)Check("Rally fixture starts",m.Rules.Serve(0,0,human.transform.position,new Vector3(1,.7f,-7),Vector3.up*.5f,Vector3.up,Vector3.up,true));
    }
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Fresh Play required");
        SceneManager.LoadScene("Menu");await Task.Delay(200);checks.Clear();
        var app=MultiplayerApp.Ensure();var net=app.Network;var player=ProgressionRuntime.Instance.Player;
        if(net.IsListening||app.State!=FlowState.Menu)throw new Exception("No active online session allowed");
        var before=new{player.id,player.elo,player.coins,player.gems,count=player.history.Count,pending=player.pendingMatch?.id};
        bool enabled=app.enabled;app.enabled=false;
        bool approval=net.NetworkConfig.ConnectionApproval,scenes=net.NetworkConfig.EnableSceneManagement;
        var callback=net.ConnectionApprovalCallback;var transport=net.GetComponent<UnityTransport>();var connection=transport.ConnectionData;
        net.ConnectionApprovalCallback=null;net.NetworkConfig.ConnectionApproval=false;net.NetworkConfig.EnableSceneManagement=false;
        transport.SetConnectionData("127.0.0.1",17916,"127.0.0.1");
        try
        {
            Check("NGO loopback starts",net.StartHost());app.ClientIdentities[net.LocalClientId]="move-human";
            var roster=new MatchRoster{matchId="move-fixture-"+Guid.NewGuid().ToString("N"),mode=PlayMode.Doubles,members=new List<RoomMember>()};
            for(int i=0;i<4;i++)roster.members.Add(new RoomMember{id=i==0?"move-human":"move-bot-"+i,name="QA",team=i/2,gender=i%2==0?"Female":"Male",elo=100,bot=i!=0});
            var go=UnityEngine.Object.Instantiate(app.Settings.matchPrefab);var m=go.GetComponent<NetworkMatch>();m.enabled=false;m.SetRoster(roster);go.GetComponent<NetworkObject>().Spawn();
            foreach(var actor in NetworkPlayer.All)actor.enabled=false;
            var human=NetworkPlayer.All.Single(p=>!p.Automated.Value);var bot=NetworkPlayer.All.First(p=>p.Automated.Value);
            m.Started.Value=true;Ready(m,human);
            Check("Doubles has four real actors",NetworkPlayer.All.Count==4);
            // Invoke the real generated server RPC on the local owner, including its input snapshot.
            Call(human,"ShotServerRpc",(int)ShotKind.Groundstroke,false,false,Vector2.right,false);
            human.Simulate(1f/60,true);
            Check("Shot snapshot preserves movement held before a shot",human.Actor.Swinging&&human.Actor.CurrentClip.StartsWith("PKV_Groundstroke"));
            human.SetAutomatedInput(Vector2.up,false);human.Simulate(1f/60,true);
            Check("A later movement command retains authoritative swing",human.Actor.Swinging&&human.Actor.CurrentClip.StartsWith("PKV_Groundstroke")&&human.Actor.LocomotionClip=="PKV_Walk_Forward");

            human.BeginSwing(new ShotIntent{kind=ShotKind.Groundstroke},human.ContactPosition);
            var moveType=typeof(NetworkPlayer).GetNestedType("Move",BindingFlags.NonPublic);object saved=Activator.CreateInstance(moveType);
            moveType.GetField("axes").SetValue(saved,Vector2.up);moveType.GetField("dt").SetValue(saved,.05f);moveType.GetField("swinging").SetValue(saved,true);
            object[] first={Vector3.back*4,Vector3.forward*3.3f,saved};Call(human,"Predict",first);
            human.Actor.Reaction(false);object[] replay={Vector3.back*4,Vector3.forward*3.3f,saved};Call(human,"Predict",replay);
            Check("Completing a swing cannot rewrite queued movement prediction",(Vector3)first[0]==(Vector3)replay[0]&&(Vector3)first[1]==(Vector3)replay[1]);

            bot.BeginSwing(new ShotIntent{kind=ShotKind.Groundstroke},bot.ContactPosition);
            bot.SetAutomatedInput(Vector2.right,false);bot.Simulate(1f/60,true);
            Check("AI movement does not accidentally cancel its shot",bot.Actor.Swinging);

            human.BeginSwing(new ShotIntent{kind=ShotKind.Volley},human.ContactPosition);human.Simulate(.23f,true);
            human.ConsumeContact(true,human.ContactPosition);int effects=human.Actor.GetComponent<PickleVerse.LocalMatch.PaddleImpactVfx>().PlayCount;
            Call(human,"MoveServerRpc",1u,Vector2.left,true);human.Simulate(1f/60,true);
            Check("Movement RPC keeps follow-through, volley momentum and one impact",human.Actor.Swinging&&human.Actor.VolleyMomentum&&effects==human.Actor.GetComponent<PickleVerse.LocalMatch.PaddleImpactVfx>().PlayCount);

            foreach(var participant in NetworkPlayer.All)
            {
                participant.Teleport(new Vector3(0,0,participant.CourtEnd.Value*4));
                participant.BeginSwing(new ShotIntent{kind=ShotKind.Groundstroke},participant.ContactPosition);
                string clip=participant.Actor.CurrentClip;float clock=0;bool active=true;
                for(int i=0;i<18;i++)
                {
                    participant.SetAutomatedInput(i<3?Vector2.zero:i<9?Vector2.right:Vector2.down,i>=9);
                    participant.Simulate(1f/120,true);clock+=1f/120;
                    active&=participant.Actor.Swinging&&participant.Actor.CurrentClip==clip&&Math.Abs(participant.Actor.SwingTime-clock)<.0001f;
                }
                Check("Concurrent swing on doubles actor "+participant.Index.Value+" team "+participant.Team.Value,active&&participant.Actor.Velocity.sqrMagnitude>0);
            }

            Ready(m,human,true);Call(human,"ShotServerRpc",(int)ShotKind.Groundstroke,false,false,Vector2.zero,false);
            for(int i=0;i<80;i++)Call(m,"StepSimulation",1f/120);
            Call(human,"MoveServerRpc",2u,Vector2.right*.1f,false);human.Simulate(1f/120,true);
            Check("New movement after serve release keeps its contact window",human.Actor.Swinging&&human.Actor.Intent.kind==PickleVerse.LocalMatch.ShotKind.Serve&&human.Actor.SwingTime>.5f);
            for(int i=0;i<90&&m.Rules.waitingServe;i++)Call(m,"StepSimulation",1f/120);
            Check("Moving released serve reaches the rally",!m.Rules.waitingServe&&m.Rules.reason!="Giao bóng hụt");
            Check("Player progression untouched",player.id==before.id&&player.elo==before.elo&&player.coins==before.coins&&player.gems==before.gems&&player.history.Count==before.count&&player.pendingMatch?.id==before.pending);
            return new{passed=true,count=checks.Count,checks};
        }
        finally
        {
            Call(app,"SetState",FlowState.Menu,"");if(net.IsListening)net.Shutdown();await Task.Delay(150);
            net.NetworkConfig.ConnectionApproval=approval;net.NetworkConfig.EnableSceneManagement=scenes;net.ConnectionApprovalCallback=callback;
            transport.ConnectionData=connection;app.ClientIdentities.Clear();app.enabled=enabled;
            File.WriteAllText("../UnityWork/MovingStrike/network-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(checks,Newtonsoft.Json.Formatting.Indented));
            SceneManager.LoadScene("Menu");
        }
    }
}
