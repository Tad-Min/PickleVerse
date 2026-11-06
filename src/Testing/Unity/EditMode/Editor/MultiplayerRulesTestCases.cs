using NUnit.Framework;
using PickleVerse.Multiplayer;
using UnityEngine;
using PlayMode = PickleVerse.Multiplayer.PlayMode;

namespace PickleVerse.Tests
{
    public class MultiplayerRulesTests
    {
        public static object RunAll()
        {
            var results=new System.Collections.Generic.List<object>(); int passed=0,failed=0;
            foreach(var method in typeof(MultiplayerRulesTests).GetMethods())
            {
                var cases=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.OfType<TestCaseAttribute>(method.GetCustomAttributes(typeof(TestCaseAttribute),false)));
                if(cases.Length==0 && method.GetCustomAttributes(typeof(TestAttribute),false).Length==0) continue;
                var arguments=cases.Length>0?System.Linq.Enumerable.Select(cases,c=>c.Arguments):new[]{new object[0]};
                foreach(var args in arguments)
                {
                    try { method.Invoke(new MultiplayerRulesTests(),args); passed++; results.Add(new{name=method.Name,passed=true}); }
                    catch(System.Exception e) { failed++; results.Add(new{name=method.Name,passed=false,error=e.GetBaseException().Message}); }
                }
            }
            var report=new{passed,failed,results};
            System.IO.File.WriteAllText("Docs/MultiplayerRulesTestResults.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
            return report;
        }
        [Test] public void SearchBeginsAtSixAndExpiresAfter120SearchingSeconds()
        {
            var clock=new SearchClock(); clock.Start(100);
            Assert.That(clock.Display(100),Is.EqualTo("00:00"));
            Assert.That(clock.Display(105.999),Is.EqualTo("00:05")); Assert.That(clock.CanSubmit(105.999),Is.False);
            Assert.That(clock.CanSubmit(106),Is.True); Assert.That(clock.Display(106),Is.EqualTo("00:06"));
            Assert.That(clock.TimedOut(225.999),Is.False); Assert.That(clock.TimedOut(226),Is.True);
            clock.Stop(); Assert.That(clock.CanSubmit(300),Is.False); clock.Start(300); Assert.That(clock.Display(300),Is.EqualTo("00:00"));
        }
        [Test] public void RequeueKeepsElapsedTime()
        { var c=new SearchClock(); c.Start(100); Assert.That(c.Display(151),Is.EqualTo("00:51")); Assert.That(c.CanSubmit(151),Is.True); }
        [Test] public void OpeningDoublesServerIsSecondThenSideOutStartsFirstServer()
        { var r=new PickleballRules(PlayMode.Doubles); Assert.That(r.ScoreCall,Is.EqualTo("0–0–2")); r.Fault(0,"miss"); r.SettleRally(); Assert.That(r.servingTeam,Is.EqualTo(1)); Assert.That(r.serverNumber,Is.EqualTo(1)); r.NewRally(); r.Fault(1,"miss"); r.SettleRally(); Assert.That(r.servingTeam,Is.EqualTo(1)); Assert.That(r.serverNumber,Is.EqualTo(2)); Assert.That(r.serverIndex,Is.EqualTo(1)); }
        [Test] public void DoublesSecondServerStaysInLeftCourtUntilScoring()
        {
            var r=new PickleballRules(PlayMode.Doubles); r.Fault(0,"miss"); r.SettleRally(); r.NewRally();
            r.Fault(1,"miss"); r.SettleRally(); r.NewRally();
            Assert.That(r.serverIndex,Is.EqualTo(1)); Assert.That(r.RightServe,Is.False); Assert.That(r.serverWorldX,Is.GreaterThan(0));
            r.Fault(0,"miss"); r.SettleRally(); r.NewRally();
            Assert.That(r.score[1],Is.EqualTo(1)); Assert.That(r.RightServe,Is.True); Assert.That(r.serverWorldX,Is.LessThan(0));
        }
        [Test] public void DoublesSideOutFirstServerStartsRightEvenWithOddScore()
        {
            var r=new PickleballRules(PlayMode.Doubles); r.Fault(1,"miss"); r.SettleRally(); r.NewRally();
            r.Fault(0,"miss"); r.SettleRally(); r.NewRally();
            r.Fault(1,"miss"); r.SettleRally(); r.NewRally(); r.Fault(1,"miss"); r.SettleRally(); r.NewRally();
            Assert.That(r.servingTeam,Is.Zero); Assert.That(r.score[0],Is.EqualTo(1)); Assert.That(r.serverNumber,Is.EqualTo(1));
            Assert.That(r.serverIndex,Is.EqualTo(1)); Assert.That(r.RightServe,Is.True);
        }
        [Test] public void OnlyServingSideScoresAndTenAllRequiresTwelveTen()
        {
            var r=new PickleballRules(PlayMode.Singles); r.score[0]=r.score[1]=10;
            r.Fault(1,"out"); r.SettleRally(); Assert.That(r.complete,Is.False); Assert.That(r.score[0],Is.EqualTo(11));
            r.NewRally(); r.Fault(1,"out"); r.SettleRally(); Assert.That(r.gameFinished,Is.True); Assert.That(r.games[0],Is.EqualTo(1)); Assert.That(r.complete,Is.False); Assert.That(r.score[0],Is.EqualTo(12));
            r=new PickleballRules(PlayMode.Singles); r.Fault(0,"out"); r.SettleRally(); Assert.That(r.score[1],Is.Zero); Assert.That(r.servingTeam,Is.EqualTo(1));
        }
        [Test] public void BestOfThreeOnlyEndsAfterTwoGameWins()
        {
            var r=new PickleballRules(PlayMode.Singles);
            WinGame(r,0); Assert.That(r.complete,Is.False); Assert.That(r.games[0],Is.EqualTo(1));
            r.NextGame(); Assert.That(r.score,Is.EqualTo(new[]{0,0})); Assert.That(r.ends[0],Is.EqualTo(1)); Assert.That(r.servingTeam,Is.EqualTo(1));
            WinGame(r,1); Assert.That(r.complete,Is.False); Assert.That(r.games,Is.EqualTo(new[]{1,1}));
            r.NextGame(); Assert.That(r.GameNumber,Is.EqualTo(3)); Assert.That(r.ends[0],Is.EqualTo(-1));
            WinGame(r,0); Assert.That(r.complete,Is.True); Assert.That(r.winner,Is.Zero); Assert.That(r.games,Is.EqualTo(new[]{2,1}));
        }
        [Test] public void DecidingGameChangesEndsAtSixOnceWithoutResettingScoresOrServe()
        {
            var r=new PickleballRules(PlayMode.Doubles); WinGame(r,0); r.NextGame(); WinGame(r,1); r.NextGame();
            r.score[0]=5; r.Fault(1,"miss"); r.SettleRally(); Assert.That(r.endChangeDue,Is.True);
            int server=r.serverIndex; r.SwapEnds(); r.NewRally();
            Assert.That(r.score[0],Is.EqualTo(6)); Assert.That(r.serverIndex,Is.EqualTo(server)); Assert.That(r.servingTeam,Is.Zero);
            Assert.That(r.serverWorldX,Is.EqualTo((r.RightServe?1:-1)*r.TeamFacing(0)*PickleballRules.HalfWidth*.5f));
            r.Fault(1,"miss"); r.SettleRally(); Assert.That(r.endChangeDue,Is.False); Assert.That(r.ends[0],Is.EqualTo(1));
        }
        static void WinGame(PickleballRules r,int team)
        {
            r.servingTeam=team; r.score[team]=10; r.score[1-team]=0; r.NewRally(); r.Fault(1-team,"miss"); r.SettleRally();
        }
        [Test] public void ServeMustBeDiagonalPastKitchenAndLinesAreIn()
        {
            var r=new PickleballRules(PlayMode.Singles);
            Assert.That(Serve(r),Is.True); r.Bounce(new Vector3(-1.5f,0,PickleballRules.Kitchen)); r.SettleRally(); Assert.That(r.servingTeam,Is.EqualTo(1));
            r=new PickleballRules(PlayMode.Singles); Serve(r); r.Bounce(new Vector3(-PickleballRules.HalfWidth,0,4)); Assert.That(r.live,Is.True); Assert.That(r.openingBounce[1],Is.True);
        }
        [Test] public void ServeAndReturnBothMustBounceBeforeVolley()
        {
            var r=new PickleballRules(PlayMode.Singles); Serve(r);
            Assert.That(r.Hit(1,0,new Vector3(0,0,4),new Vector3(0,1,4),ShotKind.Volley),Is.False); Assert.That(r.pendingFault,Is.EqualTo(1));
            r=new PickleballRules(PlayMode.Singles); Serve(r); r.Bounce(new Vector3(-1.5f,0,4)); Assert.That(r.Hit(1,0,new Vector3(-1.5f,0,4),new Vector3(-1.5f,1,4),ShotKind.Groundstroke),Is.True);
            Assert.That(r.Hit(0,0,new Vector3(0,0,-4),new Vector3(0,1,-4),ShotKind.Volley),Is.False); Assert.That(r.pendingFault,Is.EqualTo(0));
        }
        [Test] public void KitchenMomentumOverridesDeadBallAndScoresOnce()
        {
            var r=new PickleballRules(PlayMode.Singles); r.live=true; r.waitingServe=false; r.openingBounce[0]=r.openingBounce[1]=true; r.lastHitTeam=1;
            Assert.That(r.Hit(0,0,new Vector3(0,0,-3),new Vector3(0,1,-3),ShotKind.Volley),Is.True);
            r.Fault(1,"opponent miss"); r.SettleRally(); Assert.That(r.score[0],Is.Zero);
            r.CheckMomentum(0,0,new Vector3(0,0,-2),Vector3.forward); r.SettleRally(); Assert.That(r.score[0],Is.Zero); Assert.That(r.servingTeam,Is.EqualTo(1));
            r.SettleRally(); Assert.That(r.score[0],Is.Zero);
        }
        [Test] public void IllegalTechniqueDoesNotTurnIntoAutomaticLegalShot()
        { var r=new PickleballRules(PlayMode.Singles); r.live=true; r.waitingServe=false; r.lastHitTeam=1; r.bounceCount=1; r.openingBounce[0]=true; Assert.That(r.Hit(0,0,new Vector3(0,0,-4),new Vector3(0,1,-4),ShotKind.Volley),Is.False); Assert.That(r.live,Is.True); }
        [Test] public void SecondBounceAndBodyContactEndRallyOnce()
        { var r=new PickleballRules(PlayMode.Singles); Serve(r); r.Bounce(new Vector3(-1.5f,0,4)); r.Bounce(new Vector3(-1.5f,0,4)); r.BodyContact(1); r.SettleRally(); r.SettleRally(); Assert.That(r.score[0],Is.EqualTo(1)); }
        [Test] public void WrongReceiverStillFaultsAfterServeBounce()
        {
            var r=new PickleballRules(PlayMode.Doubles); Serve(r); r.Bounce(new Vector3(-1.5f,0,4));
            Assert.That(r.Hit(1,1,new Vector3(-1.5f,0,4),new Vector3(-1.5f,1,4),ShotKind.Groundstroke),Is.False);
            Assert.That(r.pendingFault,Is.EqualTo(1));
        }
        [Test] public void BallMustCrossNetEntirelyBeforeContact()
        {
            var r=new PickleballRules(PlayMode.Singles); r.live=true; r.waitingServe=false; r.lastHitTeam=1; r.openingBounce[0]=true;
            Assert.That(r.Hit(0,0,new Vector3(0,0,-3),new Vector3(0,1,-.01f),ShotKind.Volley),Is.False);
            Assert.That(r.pendingFault,Is.EqualTo(0));
        }
        [Test] public void SecondBounceOutsideStillLosesForReceiver()
        {
            var r=new PickleballRules(PlayMode.Singles); Serve(r); r.Bounce(new Vector3(-1.5f,0,4));
            r.Bounce(new Vector3(-1.5f,0,8)); r.SettleRally(); Assert.That(r.score[0],Is.EqualTo(1));
        }
        [Test] public void ActualGroundContactOutsideSidelineIsOut()
        {
            var r=new PickleballRules(PlayMode.Singles); Serve(r); r.Bounce(new Vector3(-PickleballRules.HalfWidth-.02f,0,4));
            Assert.That(r.pendingFault,Is.EqualTo(0));
        }
        [TestCase(0f)] [TestCase(.0254f)]
        public void ServeCentreLineMatchesBotMatch(float x)
        {
            var r=new PickleballRules(PlayMode.Singles);Assert.IsTrue(Serve(r));
            r.Bounce(new Vector3(x,0,4));Assert.IsTrue(r.live);Assert.AreEqual(1,r.bounceCount);
        }
        [TestCase(.10f)] [TestCase(1.06f)]
        public void ServeContactHeightMatchesBotMatch(float height)
        {
            var r=new PickleballRules(PlayMode.Singles);
            Assert.IsFalse(r.Serve(0,0,new Vector3(1.5f,0,-7.2f),new Vector3(1.5f,height,-6.9f),new Vector3(0,.5f,0),new Vector3(0,.6f,0),Vector3.up));
        }
        [Test] public void DinkAfterBounceCanBePlayedFromBaselineLikeBotMatch()
        {
            var r=new PickleballRules(PlayMode.Singles);Serve(r);r.Bounce(new Vector3(-1.5f,0,4));
            Assert.IsTrue(r.Hit(1,0,new Vector3(-1.5f,0,6.1f),new Vector3(-1.5f,1,6.1f),ShotKind.Dink));
        }
        [Test] public void TimeoutsAreLimitedAndResetPerGame()
        {
            var r=new PickleballRules(PlayMode.Singles);
            Assert.IsTrue(r.TakeTimeout(0));Assert.IsTrue(r.TakeTimeout(0));Assert.IsFalse(r.TakeTimeout(0));
            r.live=true;r.waitingServe=false;Assert.IsFalse(r.TakeTimeout(1));
            r.gameFinished=true;r.games[0]=1;r.NextGame();Assert.AreEqual(2,r.timeouts[0]);Assert.IsTrue(r.TakeTimeout(0));
        }
        [Test] public void AlternatingFirstServerStartsFromRandomlySelectedTeam()
        {
            var r=new PickleballRules(PlayMode.Singles,1,1,1);Assert.AreEqual(1,r.servingTeam);
            r.gameFinished=true;r.games[1]=1;r.NextGame();Assert.AreEqual(0,r.servingTeam);
        }
        static bool Serve(PickleballRules r)=>r.Serve(0,0,new Vector3(1.5f,0,-7.2f),new Vector3(1.5f,.8f,-6.9f),new Vector3(1.5f,.8f,-6.9f),new Vector3(1.5f,.9f,-7),Vector3.up);
    }
}
