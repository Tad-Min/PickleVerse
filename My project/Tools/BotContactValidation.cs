using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PickleVerse.Audio;
using PickleVerse.LocalMatch;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using PickleVerse.Rules;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using GameShot = PickleVerse.Multiplayer.ShotIntent;
using GameKind = PickleVerse.Multiplayer.ShotKind;

// Run through Unity CLI run_script in Play Mode. No cloud authentication or matchmaking needed.
public static class BotContactValidation
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly List<object> checks=new List<object>();
    sealed class FixedRandom : System.Random
    {
        public int calls;
        readonly double value;
        public FixedRandom(double value) { this.value=value; }
        public override double NextDouble() { calls++; return value; }
    }
    static void Check(string name,bool passed)
    { checks.Add(new {name,passed}); if(!passed) throw new InvalidOperationException(name); }
    static object Call(NetworkMatch match,string method,params object[] args) => typeof(NetworkMatch).GetMethod(method,Flags).Invoke(match,args);
    static void Set(NetworkMatch match,string field,object value) => typeof(NetworkMatch).GetField(field,Flags).SetValue(match,value);
    static void Rally(NetworkMatch match,int server)
    {
        Set(match,"<Rules>k__BackingField",new PickleballRules(PickleVerse.Multiplayer.PlayMode.Singles,1,1,server));
        Call(match,"BeginRally");
    }
    static void ReturnOpportunity(NetworkMatch match,NetworkPlayer human,NetworkPlayer bot)
    {
        Rally(match,0);
        Check("Fixture serve accepted",match.Rules.Serve(0,0,human.transform.position,new Vector3(1,.7f,-7),Vector3.up*.5f,Vector3.up,Vector3.up,true));
        match.Rules.Bounce(new Vector3(-1,Court.BallRadius,5));
        bot.Teleport(new Vector3(-1,0,5));
        bot.BeginSwing(new GameShot {kind=GameKind.Groundstroke},new Vector3(-1,.8f,5));
        bot.Simulate(bot.Actor.Intent.ContactDelay,true);
        Set(match,"ballPosition",bot.Actor.paddleFace.position); Set(match,"ballVelocity",new Vector3(0,-.1f,1));
        Check("Real paddle has a return opportunity",bot.Actor.ContactActive && new BotMatchGameplayService().CanContact(bot.Actor,match.BallPosition,match.BallPosition,1,MultiplayerApp.Instance.Settings.contactAssistRadius));
    }
    public static async Task<object> Run()
    {
        if(!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        checks.Clear(); var app=MultiplayerApp.Ensure(); var manager=app.Network;
        if(manager.IsListening || app.State!=FlowState.Menu) throw new InvalidOperationException("Run at Menu without an active network session.");
        var profile=ProgressionRuntime.Instance.Player;
        var before=new {profile.id,profile.elo,profile.coins,profile.gems,profile.totalXp,history=profile.history.Count,pending=profile.pendingMatch?.id};
        bool appEnabled=app.enabled; app.enabled=false;
        bool approval=manager.NetworkConfig.ConnectionApproval, scenes=manager.NetworkConfig.EnableSceneManagement;
        var approveConnection=manager.ConnectionApprovalCallback;
        manager.ConnectionApprovalCallback=null;
        manager.NetworkConfig.ConnectionApproval=false; manager.NetworkConfig.EnableSceneManagement=false;
        var transport=manager.GetComponent<UnityTransport>();
        var connection=transport.ConnectionData;
        transport.SetConnectionData("127.0.0.1",17913,"127.0.0.1");
        NetworkMatch match=null;
        try
        {
            Check("Loopback host starts",manager.StartHost());
            app.ClientIdentities[manager.LocalClientId]="contact-fixture-human";
            var roster=new MatchRoster {matchId="contact-fixture-"+Guid.NewGuid().ToString("N"),mode=PickleVerse.Multiplayer.PlayMode.Singles,
                members=new List<RoomMember> {new RoomMember {id="contact-fixture-human",name="TEST",team=0,gender="Female",elo=100},
                    new RoomMember {id="contact-fixture-bot",name="ANH",team=1,gender="Male",elo=220,bot=true}}};
            var go=UnityEngine.Object.Instantiate(app.Settings.matchPrefab); match=go.GetComponent<NetworkMatch>();
            match.enabled=false; match.SetRoster(roster); go.GetComponent<NetworkObject>().Spawn();
            foreach(var player in NetworkPlayer.All) player.enabled=false;
            // The fixture drives simulation manually, so its disabled match cannot advance
            // the entrance timer in FixedUpdate. Enter the rally explicitly for contact tests.
            Call(match,"BeginRally");match.Started.Value=true;
            Check("Named bot uses the normal multiplayer actors",match.Started.Value && NetworkPlayer.All.Count==2 && match.Roster.RankedBot);
            var human=NetworkPlayer.All.Single(p=>!p.Automated.Value); var bot=NetworkPlayer.All.Single(p=>p.Automated.Value);

            var miss=new FixedRandom(.9); Set(match,"botAccuracy",miss);
            ReturnOpportunity(match,human,bot);
            var position=match.BallPosition; var velocity=match.BallVelocity;
            int shots=match.BotShots, attempts=match.BotContactAttempts, misses=match.BotMisses;
            int sound=SfxPlayer.PlayCounts[(int)GameSfx.HitNormal];
            Call(match,"StepSimulation",0f);
            Check("Return miss leaves the ball flight unchanged",match.BallPosition==position && match.BallVelocity==velocity);
            Check("Return miss does not call rules Hit or record a shot",match.Rules.lastHitTeam==0 && match.Rules.strokeCount==1 && match.Rules.bounceCount==1 && match.BotShots==shots);
            Check("Return miss consumes only one contact opportunity",match.BotContactAttempts==attempts+1 && match.BotMisses==misses+1 && miss.calls==1 && !bot.Actor.ContactActive && !bot.Actor.VolleyMomentum);
            Check("Whiff does not play a ball impact sound",SfxPlayer.PlayCounts[(int)GameSfx.HitNormal]==sound);
            var tracker=(StrokeProgressTracker)typeof(NetworkMatch).GetField("progressStrokes",Flags).GetValue(match);
            Check("Miss grants no valid return or serve progress",tracker.Returns(2)==0 && tracker.Serves(2)==0);
            bot.Simulate(2f,true);
            var brains=(Dictionary<string,MultiplayerBot>)typeof(NetworkMatch).GetField("bots",Flags).GetValue(match);
            brains[bot.Identity.Value.ToString()].Step(match,bot,.5f,out _,out _);
            Check("Bot cannot re-swing to reroll the same missed ball",!bot.Actor.Swinging && miss.calls==1);
            bot.Teleport(new Vector3(2,0,5)); bot.Connected=false;
            for(int i=0;i<240 && !match.BetweenPoints.Value;i++) Call(match,"StepSimulation",1f/120);
            bot.Connected=true;
            Check("Unreturned ball ends through the normal second-bounce rule",match.BetweenPoints.Value && match.Rules.reason=="Bóng nảy hai lần" && match.Rules.score[0]==1 && match.BotShots==shots && miss.calls==1);

            var hit=new FixedRandom(.1); Set(match,"botAccuracy",hit);
            ReturnOpportunity(match,human,bot);
            var expected=new BotMatchGameplayService().Launch(bot.Actor,bot.Actor.Intent,match.BallPosition,match.Rules.serverWorldX);
            attempts=match.BotContactAttempts; misses=match.BotMisses; shots=match.BotShots;
            Call(match,"StepSimulation",0f);
            Check("Successful return uses the shared human trajectory",match.BallVelocity==expected && match.Rules.lastHitTeam==1 && match.Rules.strokeCount==2 && match.BotShots==shots+1 && match.BotReturns>0);
            Check("New rally resets the contact decision",match.BotContactAttempts==attempts+1 && match.BotMisses==misses && hit.calls==1);
            var p=match.BallPosition; var v=match.BallVelocity;
            for(int i=0;i<1500 && p.y>Court.BallRadius;i++) BallFlight.Integrate(ref p,ref v,.002f);
            Check("Successful shot lands inside the court",Court.InBounds(new CourtPoint(p.x,p.y,p.z)) && p.z<0);

            miss=new FixedRandom(.9); Set(match,"botAccuracy",miss); Rally(match,1);
            match.ServeElapsed.Value=1.2f; attempts=match.BotContactAttempts; misses=match.BotMisses; shots=match.BotShots;
            int serveSounds=SfxPlayer.PlayCounts[(int)GameSfx.Serve];
            for(int i=0;i<240 && !match.BetweenPoints.Value;i++) Call(match,"StepSimulation",1f/120);
            Check("Missed serve drops and ends instead of reattaching for a retry",match.BetweenPoints.Value && match.Rules.reason=="Giao bóng hụt" && match.Rules.strokeCount==0 && match.Rules.lastHitTeam==-1 && match.Rules.servingTeam==0);
            Check("Missed serve has one chance and no successful serve sound",match.BotContactAttempts==attempts+1 && match.BotMisses==misses+1 && match.BotShots==shots && miss.calls==1 && SfxPlayer.PlayCounts[(int)GameSfx.Serve]==serveSounds);

            Rally(match,0); match.ServeElapsed.Value=1.2f;
            human.BeginSwing(new GameShot {kind=GameKind.Serve,crossCourt=true},match.BallPosition);
            attempts=match.BotContactAttempts; shots=match.BotShots; miss=new FixedRandom(.9); Set(match,"botAccuracy",miss);
            for(int i=0;i<180 && match.Rules.waitingServe;i++) Call(match,"StepSimulation",1f/120);
            Check("Human serve bypasses bot accuracy",match.Rules.live && match.Rules.strokeCount==1 && match.Rules.lastHitTeam==0 && match.BotContactAttempts==attempts && match.BotShots==shots && miss.calls==0);

            Check("Real player progression remains untouched",profile.id==before.id && profile.elo==before.elo && profile.coins==before.coins && profile.gems==before.gems && profile.totalXp==before.totalXp && profile.history.Count==before.history && profile.pendingMatch?.id==before.pending);
            var report=new {passed=true,count=checks.Count,scope="Real NGO loopback host, multiplayer prefabs, authored swing/contact, rules and ball simulation. Does not wait six minutes or use cloud matchmaking.",checks};
            Directory.CreateDirectory("Docs/Progression");
            File.WriteAllText("Docs/Progression/BotContactValidation.json",JsonConvert.SerializeObject(report,Formatting.Indented));
            return report;
        }
        finally
        {
            typeof(MultiplayerApp).GetMethod("SetState",Flags).Invoke(app,new object[]{FlowState.Menu,""});
            if(manager.IsListening) manager.Shutdown();
            await Task.Delay(120);
            manager.NetworkConfig.ConnectionApproval=approval; manager.NetworkConfig.EnableSceneManagement=scenes;
            manager.ConnectionApprovalCallback=approveConnection;
            transport.ConnectionData=connection; app.ClientIdentities.Clear(); app.enabled=appEnabled;
        }
    }
}
