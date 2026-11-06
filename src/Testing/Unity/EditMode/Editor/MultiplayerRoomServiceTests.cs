using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PickleVerse.Multiplayer;
using PickleVerse.Multiplayer.Services;
using PickleVerse.Multiplayer.UI;

namespace PickleVerse.Tests
{
    public class MultiplayerRoomServiceTests
    {
        [Test] public void BlankNameGetsFourUppercaseLetters()
        {
            var identity = new PlayerIdentityService();
            for (int i=0;i<100;i++) Assert.That(identity.ResolveName("  "), Does.Match("^[A-Z]{4}$"));
        }
        [Test] public void NameDoesNotChangeAuthenticationProfile()
        { var identity = new PlayerIdentityService(); identity.DisplayName=identity.ResolveName(" Linh "); Assert.AreEqual("Linh",identity.DisplayName); Assert.IsNull(identity.Profile); }
        [TestCase(1,1)] [TestCase(1,2)] [TestCase(2,1)] [TestCase(2,2)]
        public void CustomCanStartWithoutReadiness(int a,int b)
        {
            var state=new TestState(a,b); Assert.IsTrue(RoomViewModel.CanStartCustom(state));
            Assert.IsTrue(new MatchRoster { mode=PlayMode.Doubles,custom=true,members=state.Members() }.IsValid());
        }
        [TestCase(0,2)] [TestCase(2,0)] [TestCase(3,1)]
        public void CustomCannotStartEmptyOrOverfilledTeam(int a,int b)
        { Assert.IsFalse(RoomViewModel.CanStartCustom(new TestState(a,b))); }
        [Test] public void GuestCannotStartCustom()
        { var s=new TestState(1,2) { captain=false }; Assert.IsFalse(RoomViewModel.CanStartCustom(s)); Assert.AreEqual("Chờ chủ phòng",RoomViewModel.PlayLabel(s)); }
        [Test] public void AutomaticDoublesStillRequiresFourPlayers()
        { var s=new TestState(1,2); Assert.IsFalse(new MatchRoster { mode=PlayMode.Doubles,members=s.Members() }.IsValid()); }
        [TestCase(1,2)] [TestCase(2,1)] [TestCase(1,1)]
        public void UnevenCustomNeverSelectsMissingServerOrReceiver(int a,int b)
        {
            var r=new PickleballRules(PlayMode.Doubles,a,b);
            for(int i=0;i<20;i++)
            {
                Assert.Less(r.serverIndex,r.teamSizes[r.servingTeam]); Assert.Less(r.receiverIndex,r.teamSizes[1-r.servingTeam]);
                r.Fault(r.servingTeam,"test side out");r.SettleRally();r.NewRally();
            }
        }
        [Test] public void SoloSideChangesServiceCourtAfterScoring()
        { var r=new PickleballRules(PlayMode.Doubles,1,2); Assert.IsTrue(r.RightServe);r.Fault(1,"out");r.SettleRally();r.NewRally();Assert.IsFalse(r.RightServe);Assert.AreEqual(0,r.serverIndex); }
        sealed class TestState : IMultiplayerState
        {
            readonly List<RoomMember> members;
            public bool captain=true;
            public TestState(int a,int b)
            { members=Enumerable.Range(0,a).Select(i=>new RoomMember{id="a"+i,team=0}).Concat(Enumerable.Range(0,b).Select(i=>new RoomMember{id="b"+i,team=1})).ToList(); }
            public event Action Changed { add {} remove {} }
            public FlowState State=>FlowState.WaitingRoom;
            public PlayMode Mode=>PlayMode.Doubles;
            public bool Custom=>true;
            public bool IsCaptain=>captain;
            public bool Busy=>false;
            public bool Searching=>false;
            public string PlayerId=>"a0";
            public string DisplayName {get;set;}
            public string Notice=>"";
            public string Elapsed=>"00:00";
            public string RoomCode=>"ABCDEF";
            public List<RoomMember> Members()=>members;
        }
    }
}
