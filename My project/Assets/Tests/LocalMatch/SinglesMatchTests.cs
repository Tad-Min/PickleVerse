using NUnit.Framework;
using PickleVerse.Rules;
using PickleVerse.LocalMatch;
using UnityEngine;

namespace PickleVerse.Tests
{
    public class SinglesMatchTests
    {
        static CourtPoint P(float x, float z, float y = .7f) => new CourtPoint(x,y,z);
        static SinglesRules Served()
        {
            var r = new SinglesRules(); r.PrepareServe(-1);
            Assert.AreEqual(Fault.None,r.Serve(0,P(1.2f,-7.2f),P(1.5f,-7.2f),P(1.4f,-6.9f),1)); return r;
        }
        static void Lose(SinglesRules r,int loser) { r.Fail(loser,Fault.Out); r.CommitRally(); r.PrepareServe(-1); }
        [Test] public void ServeKitchenLineIsFault() { var r=Served(); Assert.AreEqual(Fault.ServeShort,r.Bounce(P(-1,Court.Kitchen))); }
        [Test] public void BaselineAndSidelineAreIn() { var r=Served(); Assert.AreEqual(Fault.None,r.Bounce(P(-Court.HalfWidth,Court.HalfLength))); }
        [Test] public void WrongDiagonalIsFault() { var r=Served(); Assert.AreEqual(Fault.ServeWrongCourt,r.Bounce(P(1,4))); }
        [Test] public void ServeOnBaselineIsFootFault() { var r=new SinglesRules();r.PrepareServe(-1); Assert.AreEqual(Fault.ServeFoot,r.Serve(0,P(1,-Court.HalfLength),P(1.4f,-7),P(1,-7),1)); }
        [Test] public void ServeAtTenSecondsIsAllowedButAfterTenIsNot() { var r=new SinglesRules();r.PrepareServe(-1); Assert.AreEqual(Fault.ServeTimeout,r.Serve(0,P(1,-7.2f),P(1.4f,-7.2f),P(1,-7),10.001f)); }
        [Test] public void HighServeContactIsIllegal() { var r=new SinglesRules();r.PrepareServe(-1); Assert.AreEqual(Fault.ServeMotion,r.Serve(0,P(1,-7.2f),P(1.4f,-7.2f),P(1,-7,1.3f),1)); }
        [Test] public void ReturnCannotVolley() { var r=Served();Assert.AreEqual(Fault.TwoBounce,r.Hit(1,P(-1,3),false)); }
        [Test] public void ThirdShotMustBounceToo() { var r=Served();r.Bounce(P(-1,4));r.Hit(1,P(-1,4),false);Assert.AreEqual(Fault.TwoBounce,r.Hit(0,P(1,-4),false)); }
        static SinglesRules OpenRally() { var r=Served();r.Bounce(P(-1,4));r.Hit(1,P(-1,4),false);r.Bounce(P(1,-4));r.Hit(0,P(1,-4),false);return r; }
        [Test] public void VolleyKitchenFaultButBouncedDinkIsLegal() { var r=OpenRally();Assert.AreEqual(Fault.KitchenVolley,r.Hit(1,P(1,1.7f),true));r=OpenRally();r.Bounce(P(1,1.7f));Assert.AreEqual(Fault.None,r.Hit(1,P(1,1.7f),true)); }
        [Test] public void KitchenMomentumOverridesDeadBallBeforeScore() { var r=OpenRally();r.Hit(1,P(-1,3),false);r.Fail(0,Fault.DoubleBounce);r.Fail(1,Fault.KitchenMomentum,true);r.CommitRally();Assert.AreEqual(1,r.Points[0]); }
        [Test] public void ReceiverWinningOnlyChangesServe() { var r=Served();r.Fail(0,Fault.Out);r.CommitRally();Assert.AreEqual(1,r.Server);Assert.AreEqual(0,r.Points[1]); }
        [Test] public void ServerWinningScoresAndStaysServer() { var r=Served();r.Fail(1,Fault.Out);r.CommitRally();Assert.AreEqual(0,r.Server);Assert.AreEqual(1,r.Points[0]); }
        [Test] public void SecondBounceOutsideStillFaultsReceiver() { var r=Served();r.Bounce(P(-1,5));Assert.AreEqual(Fault.DoubleBounce,r.Bounce(P(-9,12)));Assert.AreEqual(1,r.FaultPlayer); }
        [Test] public void ElevenTenDoesNotWinAndTwelveTenDoes() { var r=new SinglesRules();r.PrepareServe(-1);for(int i=0;i<10;i++)Lose(r,1);Lose(r,0);for(int i=0;i<10;i++)Lose(r,0);Lose(r,1);Lose(r,1);Assert.IsFalse(r.GameFinished);Lose(r,1);Assert.IsTrue(r.GameFinished);Assert.AreEqual(12,r.Points[0]); }
        [Test] public void MatchRequiresTwoGameWins() { var r=new SinglesRules();r.PrepareServe(-1);for(int i=0;i<11;i++)Lose(r,1);Assert.AreEqual(-1,r.Winner);r.NextGame();Lose(r,1);for(int i=0;i<11;i++)Lose(r,1);Assert.AreEqual(0,r.Winner); }
        [Test] public void DecidingGameChangesEndsAtSixOnlyOnce() { var r=new SinglesRules();r.PrepareServe(-1);for(int i=0;i<11;i++)Lose(r,1);r.NextGame();for(int i=0;i<11;i++)Lose(r,0);r.NextGame();for(int i=0;i<5;i++)Lose(r,1);r.Fail(1,Fault.Out);r.CommitRally();Assert.IsTrue(r.EndChangeDue);r.PrepareServe(-1);r.Fail(1,Fault.Out);r.CommitRally();Assert.IsFalse(r.EndChangeDue); }
        [Test] public void TimeoutLimitedToTwoAndOnlyBeforeServe() { var r=new SinglesRules();r.PrepareServe(-1);Assert.IsTrue(r.TakeTimeout(0));Assert.IsTrue(r.TakeTimeout(0));Assert.IsFalse(r.TakeTimeout(0));Assert.IsFalse(Served().TakeTimeout(0)); }
        [TestCase(1,ShotKind.Drive)] [TestCase(2,ShotKind.Dink)] [TestCase(4,ShotKind.Volley)] [TestCase(3,ShotKind.Drop)] [TestCase(5,ShotKind.Smash)] [TestCase(6,ShotKind.Reset)]
        public void BaseAndComboCommitOnRelease(int bits,ShotKind kind) { var input=new ShotInput();Assert.IsFalse(input.Tick(bits,false,false,out _));Assert.IsTrue(input.Tick(0,false,false,out var shot));Assert.AreEqual(kind,shot.kind);Assert.IsFalse(input.Tick(0,false,false,out _)); }
        [Test] public void ReleasingOneComboKeyOnlyCommitsOnce() { var input=new ShotInput();input.Tick(1,false,false,out _);input.Tick(3,false,false,out _);Assert.IsTrue(input.Tick(2,false,false,out var shot));Assert.AreEqual(ShotKind.Drop,shot.kind);Assert.IsFalse(input.Tick(0,false,false,out _)); }
        [Test] public void ModifierMustStillBeHeldAtCommit() { var input=new ShotInput();input.Tick(9,false,false,out _);Assert.IsTrue(input.Tick(8,false,false,out var shot));Assert.IsTrue(shot.power);input.Tick(0,false,false,out _);input.Tick(9,false,false,out _);input.Tick(1,false,false,out _);Assert.IsTrue(input.Tick(0,false,false,out shot));Assert.IsFalse(shot.power); }
        [Test] public void ThreeKeysCancelWithoutAnySwing() { var input=new ShotInput();input.Tick(11,false,false,out _);Assert.IsFalse(input.Tick(0,false,false,out _)); }
        [Test] public void BackspaceOrLostFocusCannotFireOnRelease() { var input=new ShotInput();input.Tick(1,false,false,out _);input.Cancel();Assert.IsFalse(input.Tick(0,false,false,out _)); }
        [Test] public void ServeDoesNotAcceptDropCombo() { var input=new ShotInput();input.Tick(3,false,true,out _);Assert.IsFalse(input.Tick(0,false,true,out _)); }
        [Test] public void BallisticShotLandsAtRequestedSpot() { var from=new Vector3(1,.8f,-5);var target=new Vector3(-1,.037f,4.5f);var p=from;var v=BallFlight.Launch(from,target,2.2f);Vector3 previous=p;for(int i=0;i<1000&&p.y>.037f;i++){previous=p;BallFlight.Integrate(ref p,ref v,.002f);}Assert.Less(Vector2.Distance(new Vector2(p.x,p.z),new Vector2(target.x,target.z)),.025f); }
    }
}
