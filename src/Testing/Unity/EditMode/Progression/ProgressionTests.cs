using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PickleVerse.Progression;
using UnityEngine;

namespace PickleVerse.Tests
{
    public sealed class ProgressionTests
    {
        ProgressionConfig config;
        ProgressionEngine engine;
        PlayerProfile player;
        readonly DateTime sunday=new DateTime(2026,10,11,23,59,0,DateTimeKind.Utc);
        [SetUp] public void Setup()
        { config=ProgressionConfig.Defaults(); engine=new ProgressionEngine(config); player=new PlayerProfile {id="Test",playerName="ABCD"}; engine.RefreshPeriods(player,sunday); }
        [TearDown] public void Teardown() { UnityEngine.Object.DestroyImmediate(config); }
        MatchResult Match(bool win=true,MatchKind kind=MatchKind.RankedSingles,DateTime? ended=null)
        { return new MatchResult {id=Guid.NewGuid().ToString("N"),playerId=player.id,ownElo=player.elo,opponentElo=player.elo,kind=kind,outcome=win?MatchOutcome.Won:MatchOutcome.Lost,endedUtcTicks=(ended??sunday).Ticks,points=20,validReturns=30,validServes=10}; }

        [TestCase(200,240,true,20)] [TestCase(200,240,false,-20)]
        [TestCase(200,200,true,20)] [TestCase(200,200,false,-20)]
        [TestCase(400,360,true,10)] [TestCase(400,360,false,-160)]
        [TestCase(200,160,false,-100)] [TestCase(100,100,false,0)] [TestCase(200,201,true,1)]
        public void EloSixCasesRoundingAndFloor(int own,int opponent,bool win,int expected)
        { Assert.That(EloPolicy.LegacyDelta(own,opponent,win,0,0),Is.EqualTo(expected)); }
        [Test] public void EloStreaksAndComeback()
        {
            Assert.That(EloPolicy.LegacyDelta(1000,1000,true,2,0),Is.EqualTo(22)); Assert.That(EloPolicy.LegacyDelta(1000,1000,true,100,0),Is.EqualTo(30));
            Assert.That(EloPolicy.LegacyDelta(1000,1000,true,0,5),Is.EqualTo(15)); Assert.That(EloPolicy.LegacyDelta(1000,1000,true,0,100),Is.EqualTo(10));
            Assert.That(EloPolicy.LegacyDelta(1000,1000,false,0,1),Is.EqualTo(-22)); Assert.That(EloPolicy.LegacyDelta(1000,1000,false,0,100),Is.EqualTo(-30));
        }
        [TestCase(100,"Đồng")] [TestCase(499,"Đồng")] [TestCase(500,"Bạc")] [TestCase(999,"Bạc")]
        [TestCase(1000,"Vàng")] [TestCase(1999,"Vàng")] [TestCase(2000,"Bạch Kim")] [TestCase(3499,"Bạch Kim")]
        [TestCase(3500,"Kim Cương")] [TestCase(4999,"Kim Cương")] [TestCase(5000,"Cao Thủ")] [TestCase(6999,"Cao Thủ")] [TestCase(7000,"Huyền Thoại")]
        public void RankBoundaries(int elo,string rank) { Assert.That(EloPolicy.Rank(elo),Is.EqualTo(rank)); }
        [TestCase(0,25)] [TestCase(100,25)] [TestCase(220,26)] [TestCase(9000,99)] [TestCase(9100,100)] [TestCase(100000,100)]
        public void AccuracyThresholds(int elo,int accuracy) { Assert.That(EloPolicy.BotAccuracy(elo),Is.EqualTo(accuracy)); }
        [Test] public void BotRatingAndSeededDecisions()
        {
            Assert.That(EloPolicy.BotElo(100,4),Is.EqualTo(220)); Assert.That(EloPolicy.BotElo(100,5),Is.Zero);
            var random=new System.Random(20261011); int hits=0;
            for(int i=0;i<10000;i++) if(EloPolicy.BotDecision(random,220)) hits++;
            Assert.That(hits,Is.InRange(2470,2730)); for(int i=0;i<100;i++) Assert.That(EloPolicy.BotDecision(random,9100),Is.True);
        }
        [Test] public void MutualPairKeysEnforceBothPreferencesAndWindow()
        {
            bool Fits(int a,int la,bool ea,int b,int lb,bool eb) => EloPolicy.PairingKeys(a,la,ea).Intersect(EloPolicy.PairingKeys(b,lb,eb)).Any();
            Assert.That(Fits(200,0,false,240,0,false),Is.False); Assert.That(Fits(200,0,false,240,5,false),Is.True);
            Assert.That(Fits(200,5,false,240,0,false),Is.False); Assert.That(Fits(200,0,true,320,0,true),Is.True);
            Assert.That(Fits(200,0,true,321,0,true),Is.False); Assert.That(Fits(100,5,false,100,0,false),Is.True);
            Assert.That(EloPolicy.PairingKeys(100,5,false),Has.Count.EqualTo(1));
        }
        [Test] public void SearchDeadlineRaceCancellationAndRetry()
        {
            var gate=new RankedSearchGate(); gate.Start(50);
            Assert.That(gate.Expanded(79.999),Is.False); Assert.That(gate.Expanded(80),Is.True);
            Assert.That(gate.ClaimHuman(409.999),Is.True); Assert.That(gate.ClaimBot(410),Is.False);
            gate.RetryHuman(); Assert.That(gate.ClaimBot(410),Is.True); Assert.That(gate.ClaimHuman(411),Is.False);
            gate.Start(0); gate.Cancel(); Assert.That(gate.ClaimBot(360),Is.False);
            gate.Start(0); Assert.That(gate.ClaimBot(359.999),Is.False);
            Assert.That(gate.ClaimHuman(360),Is.False); Assert.That(gate.ClaimBot(360),Is.True); Assert.That(gate.ClaimBot(361),Is.False);
        }
        [Test] public void TicketArraysRespectServiceLimitWithoutDroppingCanonicalPairs()
        {
            foreach(int own in new[]{100,200,9000,int.MaxValue})
            foreach(int losses in new[]{0,5})
            foreach(bool expanded in new[]{false,true})
            {
                var all=EloPolicy.PairingKeys(own,losses,expanded);
                var parts=Enumerable.Range(0,3).Select(i=>EloPolicy.TicketKeys(own,losses,expanded,i)).ToList();
                Assert.That(parts.All(p=>p.Count<=100),Is.True); Assert.That(parts.SelectMany(p=>p).OrderBy(k=>k),Is.EqualTo(all.OrderBy(k=>k)));
            }
            Assert.That(EloPolicy.TicketKeys(200,0,false,0).Intersect(EloPolicy.TicketKeys(240,5,false,0)).Any(),Is.EqualTo((200+240)%3==0));
        }
        [Test] public void XpRemainderMultipleLevelsAndTotal()
        {
            ProgressionEngine.Grant(player,new RewardBundle(0,0,75)); Assert.That(player.level,Is.EqualTo(4)); Assert.That(player.levelXp,Is.EqualTo(15)); Assert.That(player.totalXp,Is.EqualTo(75));
            ProgressionEngine.Grant(player,new RewardBundle(0,0,25)); Assert.That(player.level,Is.EqualTo(5)); Assert.That(player.levelXp,Is.Zero);
        }
        [Test] public void ResultsAndRewardsAreIdempotentAndSurviveSerialization()
        {
            var result=Match(); player.pendingMatch=result;
            Assert.That(engine.Apply(player,result),Is.True); Assert.That(engine.Apply(player,result),Is.False); Assert.That(player.pendingMatch,Is.Null);
            Assert.That(player.wins,Is.EqualTo(1)); Assert.That(player.totalXp,Is.EqualTo(10)); Assert.That(engine.Claim(player,"D.Play1",sunday),Is.True);
            var saved=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(player)); Assert.That(engine.Claim(saved,"D.Play1",sunday),Is.False); Assert.That(engine.Apply(saved,result),Is.False);
            Assert.That(saved.coins,Is.EqualTo(50)); Assert.That(saved.totalXp,Is.EqualTo(15));
        }
        [Test] public void CatalogAndAchievementLifetimeClaims()
        {
            Assert.That(engine.Count(ProgressScope.Achievement),Is.EqualTo(10)); Assert.That(engine.Count(ProgressScope.Daily),Is.EqualTo(7)); Assert.That(engine.Count(ProgressScope.Weekly),Is.EqualTo(10));
            for(int i=0;i<5;i++) engine.Apply(player,Match()); Assert.That(engine.Claim(player,"A.Win5",sunday),Is.True); Assert.That(engine.Claim(player,"A.Win5",sunday),Is.False);
            engine.RefreshPeriods(player,sunday.AddDays(15)); Assert.That(engine.Claim(player,"A.Win5",sunday.AddDays(15)),Is.False);
        }
        [Test] public void OfflineResetAutoClaimsAllEligibleItemsAndBonusesOnce()
        {
            foreach(var d in config.definitions.Where(d=>d.scope!=ProgressScope.Achievement)) SetCounter(ProgressionEngine.Counters(player,d.scope),d.metric,10000);
            var expected=config.definitions.Where(d=>d.scope!=ProgressScope.Achievement).Select(d=>d.reward).ToList(); expected.Add(config.dailyBonus); expected.Add(config.weeklyBonus);
            engine.RefreshPeriods(player,sunday.AddDays(15)); Assert.That(player.coins,Is.EqualTo(expected.Sum(r=>r.coins))); Assert.That(player.gems,Is.EqualTo(expected.Sum(r=>r.gems))); Assert.That(player.totalXp,Is.EqualTo(expected.Sum(r=>r.xp)));
            Assert.That(player.daily.counters.matches,Is.Zero); Assert.That(player.weekly.counters.bestStreak,Is.Zero); var coins=player.coins; engine.RefreshPeriods(player,sunday.AddDays(15)); Assert.That(player.coins,Is.EqualTo(coins));
        }
        [Test] public void UtcMondayResetAndLateConfirmedResultUsesItsEndPeriod()
        {
            var monday=sunday.AddMinutes(1); Assert.That(ProgressionEngine.WeekKey(monday),Is.EqualTo("20261012")); Assert.That(ProgressionEngine.NextReset(ProgressScope.Daily,sunday),Is.EqualTo(monday));
            engine.RefreshPeriods(player,monday); engine.Apply(player,Match(ended:sunday)); Assert.That(player.daily.counters.matches,Is.Zero); Assert.That(player.weekly.counters.matches,Is.Zero);
            Assert.That(player.previousDays.Single(p=>p.key=="20261011").counters.matches,Is.EqualTo(1)); Assert.That(player.coins,Is.EqualTo(475));
        }
        [Test] public void CustomInterruptedForfeitAndSeparateRankStreaks()
        {
            engine.Apply(player,Match(kind:MatchKind.Custom)); Assert.That(player.totalXp,Is.Zero); Assert.That(player.wins,Is.Zero);
            var interrupted=Match(); interrupted.outcome=MatchOutcome.Interrupted; engine.Apply(player,interrupted); Assert.That(player.elo,Is.EqualTo(100));
            engine.Apply(player,Match(kind:MatchKind.BotPractice)); Assert.That(player.rankWinStreak,Is.Zero); engine.Apply(player,Match(kind:MatchKind.RankedBot)); Assert.That(player.rankWinStreak,Is.EqualTo(1));
            long xp=player.totalXp,counters=player.lifetime.matches; var exit=Match(false); exit.localForfeit=true; engine.Apply(player,exit);
            Assert.That(player.totalXp,Is.EqualTo(xp)); Assert.That(player.lifetime.matches,Is.EqualTo(counters)); Assert.That(player.losses,Is.EqualTo(1)); Assert.That(player.rankLossStreak,Is.EqualTo(1)); Assert.That(player.lifetime.streak,Is.Zero);
        }
        [Test] public void LateForfeitBreaksOnlyItsEndedMissionPeriod()
        {
            engine.Apply(player,Match(ended:sunday)); engine.RefreshPeriods(player,sunday.AddMinutes(1));
            player.daily.counters.streak=2; player.weekly.counters.streak=3;
            var exit=Match(false,ended:sunday); exit.localForfeit=true; engine.Apply(player,exit);
            Assert.That(player.daily.counters.streak,Is.EqualTo(2)); Assert.That(player.weekly.counters.streak,Is.EqualTo(3));
            Assert.That(player.previousDays.Single(p=>p.key=="20261011").counters.streak,Is.Zero);
            Assert.That(player.previousWeeks.Single(p=>p.key=="20261005").counters.streak,Is.Zero);
        }
        [Test] public void HistoryKeepsLatestTwentyAndPerGameScores()
        {
            for(int i=0;i<21;i++) { var r=Match(ended:sunday.AddSeconds(i)); r.scores.Add(new GameScore(11,9,true)); r.scores.Add(new GameScore(5,4,false)); engine.Apply(player,r); }
            Assert.That(player.history,Has.Count.EqualTo(20)); Assert.That(player.history[0].endedUtcTicks,Is.EqualTo(sunday.AddSeconds(20).Ticks)); Assert.That(player.history.Last().endedUtcTicks,Is.EqualTo(sunday.AddSeconds(1).Ticks)); Assert.That(player.history[0].scores[1].completed,Is.False);
        }
        [Test] public void ValidStrokesComeFromAcceptedFlightsOnce()
        {
            var tracker=new StrokeProgressTracker(); tracker.Launch(0,true,1); Assert.That(tracker.Serves(0),Is.Zero); tracker.AcceptFlight(); tracker.AcceptFlight(); Assert.That(tracker.Serves(0),Is.EqualTo(1));
            tracker.Launch(1,false,2); tracker.NewRally(); tracker.AcceptFlight(); Assert.That(tracker.Returns(1),Is.Zero); tracker.Launch(0,true,1); tracker.AcceptFlight(); Assert.That(tracker.Serves(0),Is.EqualTo(2));
        }
        [Test] public void StoreLeaseSelectionSwitchReopenRollbackAndBackup()
        {
            string root=Path.Combine(Path.GetTempPath(),"PKVTests"+Guid.NewGuid().ToString("N"));
            try
            {
                string id;
                using(var first=new LocalPlayerStore(root))
                {
                    first.Open(); id=first.Current.id; Assert.That(LocalPlayerStore.ValidId(id),Is.True); Assert.That(first.Current.playerName.Length,Is.EqualTo(4));
                    using(var second=new LocalPlayerStore(root)) { second.Open(); Assert.That(second.Current.id,Is.Not.EqualTo(id)); Assert.Throws<IOException>(()=>second.Switch(id)); }
                    first.Transaction(p=>p.coins=321); Assert.Throws<InvalidOperationException>(()=>first.Transaction(p=>{p.coins=999;throw new InvalidOperationException();})); Assert.That(first.Current.coins,Is.EqualTo(321));
                    first.Switch("Second","TEST"); Assert.That(first.Current.coins,Is.Zero); Assert.That(first.Current.elo,Is.EqualTo(100)); first.Switch(id); Assert.That(first.Current.coins,Is.EqualTo(321)); Assert.Throws<ArgumentException>(()=>first.Switch("../bad"));
                }
                using(var reopened=new LocalPlayerStore(root)) { reopened.Open(id); Assert.That(reopened.Current.coins,Is.EqualTo(321)); reopened.Save(); }
                File.WriteAllText(Path.Combine(root,id+".json"),"broken"); using(var recovered=new LocalPlayerStore(root)) { recovered.Open(id); Assert.That(recovered.Current.coins,Is.EqualTo(321)); }
            }
            finally { if(!Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected test directory"); if(Directory.Exists(root)) Directory.Delete(root,true); }
        }
        [Test] public void CompletedMatchRemainsClearedAfterStoreReopenAndRollback()
        {
            string root=Path.Combine(Path.GetTempPath(),"PKVTests"+Guid.NewGuid().ToString("N"));
            try
            {
                var result=Match();
                using(var store=new LocalPlayerStore(root)) { store.Open("Test"); store.Transaction(p=>{ p.pendingMatch=result; engine.Apply(p,result); }); }
                using(var store=new LocalPlayerStore(root))
                {
                    store.Open("Test"); Assert.That(store.Current.pendingMatch,Is.Null); Assert.That(store.Current.history,Has.Count.EqualTo(1)); Assert.That(engine.Apply(store.Current,result),Is.False);
                    Assert.Throws<InvalidOperationException>(()=>store.Transaction(p=>{p.pendingMatch=Match();throw new InvalidOperationException();})); Assert.That(store.Current.pendingMatch,Is.Null);
                    store.Transaction(p=>p.pendingMatch=Match());
                }
                using(var store=new LocalPlayerStore(root)) { store.Open("Test"); Assert.That(store.Current.pendingMatch.id,Is.Not.Empty); }
            }
            finally { if(Directory.Exists(root) && Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)) Directory.Delete(root,true); }
        }
        static void SetCounter(ProgressCounters c,ProgressMetric metric,int v)
        {
            switch(metric) { case ProgressMetric.Matches:c.matches=v;break;case ProgressMetric.Wins:c.wins=v;break;case ProgressMetric.Points:c.points=v;break;case ProgressMetric.Returns:c.returns=v;break;case ProgressMetric.Serves:c.serves=v;break;case ProgressMetric.BotMatches:c.botMatches=v;break;case ProgressMetric.HumanMatches:c.humanMatches=v;break;case ProgressMetric.HumanWins:c.humanWins=v;break;case ProgressMetric.RankedWins:c.rankedWins=v;break;case ProgressMetric.DoublesWins:c.doublesWins=v;break;default:c.bestStreak=v;break; }
        }
    }
}
