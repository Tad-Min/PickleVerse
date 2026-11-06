using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using UnityEngine;

public sealed class NetworkCoachingTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Field(NetworkMatch match, string name, object value) => typeof(NetworkMatch).GetField(name, Private).SetValue(match,value);
    static void Call(NetworkMatch match, string name, params object[] args) => typeof(NetworkMatch).GetMethod(name, Private).Invoke(match,args);
    [Test] public void AuthoritativeDeliveryDeduplicatesAndCannotCrossAccountOrSession()
    {
        var root = new GameObject("NetworkCoachingTest") {hideFlags=HideFlags.HideAndDontSave};
        try
        {
            var match = root.AddComponent<NetworkMatch>();
            var session = new CoachingSession {sessionId="match-a",playerId="player-a"};
            Field(match,"coachingSession",session);
            string Packet(string owner,string id) => JsonConvert.SerializeObject(new {sessionId=id,playerId=owner,
                shot=new ShotTelemetry {shotId="stable-shot",sequence=3,kind="Dink",technique="Dink",netClearance=.1f}});
            Call(match,"ReceiveCoachingShot",Packet("player-b","match-a"));
            Call(match,"ReceiveCoachingShot",Packet("player-a","match-b"));
            Assert.That(session.shots,Is.Empty);
            Call(match,"ReceiveCoachingShot",Packet("player-a","match-a"));
            Call(match,"ReceiveCoachingShot",Packet("player-a","match-a"));
            Assert.That(session.shots.Count,Is.EqualTo(1));
            Assert.That(session.shots[0].sequence,Is.EqualTo(3));
            session.closed=true;
            Call(match,"ReceiveCoachingShot",Packet("player-a","match-a").Replace("stable-shot","late-shot"));
            Assert.That(session.shots.Count,Is.EqualTo(1));
        }
        finally {Object.DestroyImmediate(root);}
    }
    [Test] public void LandingAndClearanceUseAuthoritativeFlightAndOnlyFirstBounce()
    {
        var root = new GameObject("NetworkCoachingTest") {hideFlags=HideFlags.HideAndDontSave};
        try
        {
            var match = root.AddComponent<NetworkMatch>();
            var rules = new PickleballRules(PickleVerse.Multiplayer.PlayMode.Singles) {lastHitPlayer=0,lastHitTeam=0};
            typeof(NetworkMatch).GetProperty("Rules").SetValue(match,rules);
            Field(match,"captureCoaching",true);
            var contacts=(Dictionary<int,ShotTelemetry>)typeof(NetworkMatch).GetField("coachingContacts",Private).GetValue(match);
            contacts[0]=new ShotTelemetry {shotId="shot"};
            Call(match,"CoachingNet",-.02f);
            Call(match,"CoachingLanding",new Vector3(1,.037f,5));
            rules.bounceCount=1;
            Call(match,"CoachingLanding",new Vector3(2,.037f,6));
            Assert.That(contacts[0].netClearance,Is.EqualTo(-.02f));
            Assert.That(contacts[0].landing,Is.EqualTo(new[]{1f,.037f,5f}));
            Call(match,"CoachingRallyEnded",0,"Bóng không qua sân đối phương");
            Assert.That(contacts[0].outcome,Is.EqualTo("Net"));
        }
        finally {Object.DestroyImmediate(root);}
    }
    [Test] public void FullTelemetryRecordFitsExistingPhotonMessageLimit()
    {
        var vector=new[]{float.MaxValue,-float.MaxValue,123.45678f};
        var shot=new ShotTelemetry {shotId=new string('a',100),kind="Drive",technique="Drive",source="fusion-authority-observed",
            contact=vector,leftFoot=vector,rightFoot=vector,landing=vector,intendedTarget=vector,
            netClearance=.8f,timingError=.03f,momentum=false,volley=true,kitchen=false,
            rawTechnique="Drive",rawCrossCourt=true,rawPower=true,rawPosition=vector};
        string delivery=JsonConvert.SerializeObject(new {sessionId=new string('b',32),playerId=new string('c',32),shot});
        var message=new PhotonMessage {target=1,kind="coaching-shot",payload=JsonConvert.SerializeObject(new[]{delivery})};
        Assert.That(JsonConvert.SerializeObject(message).Length,Is.LessThan(3500));
    }
}
