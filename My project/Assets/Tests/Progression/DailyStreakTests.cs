using System;
using NUnit.Framework;
using PickleVerse.Progression;
using UnityEngine;
namespace PickleVerse.Tests
{
    public sealed class DailyStreakTests
    {
        readonly DateTime start=new DateTime(2026,10,10,0,0,0,DateTimeKind.Utc);
        PlayerProfile player;
        ProgressionConfig config;
        ProgressionEngine engine;
        [SetUp]public void Setup(){player=new PlayerProfile{id="Streak",playerName="STREAK"};config=ProgressionConfig.Defaults();engine=new ProgressionEngine(config);DailyStreakEngine.Ensure(player,start);}
        [TearDown]public void Cleanup(){UnityEngine.Object.DestroyImmediate(config);}
        MatchResult Result(DateTime date,MatchKind kind=MatchKind.BotPractice,MatchOutcome outcome=MatchOutcome.Won,bool forfeit=false)
            =>new MatchResult{id=Guid.NewGuid().ToString("N"),playerId=player.id,endedUtcTicks=date.Ticks,kind=kind,outcome=outcome,localForfeit=forfeit};
        [Test]public void SameDayAndDuplicateCountOnce(){var r=Result(start);engine.Apply(player,r);engine.Apply(player,r);engine.Apply(player,Result(start.AddHours(23)));Assert.That(player.dailyStreak.current,Is.EqualTo(1));Assert.That(player.dailyStreak.days.Count,Is.EqualTo(1));}
        [Test]public void ConsecutiveDaysLossStillCounts(){engine.Apply(player,Result(start));engine.Apply(player,Result(start.AddDays(1),outcome:MatchOutcome.Lost));Assert.That(player.dailyStreak.current,Is.EqualTo(2));Assert.That(player.dailyStreak.best,Is.EqualTo(2));}
        [Test]public void GapExpiresAndNextMatchRestarts(){engine.Apply(player,Result(start));engine.Apply(player,Result(start.AddDays(1)));DailyStreakEngine.Refresh(player,start.AddDays(3));Assert.That(player.dailyStreak.current,Is.Zero);engine.Apply(player,Result(start.AddDays(3)));Assert.That(player.dailyStreak.current,Is.EqualTo(1));Assert.That(player.dailyStreak.best,Is.EqualTo(2));}
        [Test]public void UtcMidnightIsVietnamSeven(){engine.Apply(player,Result(start.AddHours(23).AddMinutes(59)));engine.Apply(player,Result(start.AddDays(1)));Assert.That(player.dailyStreak.current,Is.EqualTo(2));Assert.That(start.AddDays(1).AddHours(7).Hour,Is.EqualTo(7));}
        [Test]public void LateResultBridgesGap(){engine.Apply(player,Result(start));engine.Apply(player,Result(start.AddDays(2)));engine.Apply(player,Result(start.AddDays(1)));DailyStreakEngine.Refresh(player,start.AddDays(2));Assert.That(player.dailyStreak.current,Is.EqualTo(3));Assert.That(player.dailyStreak.best,Is.EqualTo(3));}
        [TestCase(MatchKind.Custom)][TestCase(MatchKind.MatchedDoubles)][TestCase(MatchKind.RankedSingles)][TestCase(MatchKind.RankedBot)][TestCase(MatchKind.BotPractice)]
        public void AllMatchModesCount(MatchKind kind){engine.Apply(player,Result(start,kind));Assert.That(player.dailyStreak.current,Is.EqualTo(1));}
        [Test]public void RemainingPlayerForfeitWinCounts(){var r=Result(start,MatchKind.Custom);r.reason="Đối thủ bỏ trận";engine.Apply(player,r);Assert.That(player.dailyStreak.current,Is.EqualTo(1));}
        [TestCase(MatchOutcome.Won,true)][TestCase(MatchOutcome.Lost,true)][TestCase(MatchOutcome.Interrupted,false)]
        public void ExitsAndInterruptionsDoNotCount(MatchOutcome outcome,bool forfeit){engine.Apply(player,Result(start,outcome:outcome,forfeit:forfeit));Assert.That(player.dailyStreak.days,Is.Empty);}
        [Test]public void MigrationDoesNotBackfillOrAlterExistingProfile(){player.dailyStreak=null;player.coins=123;player.gems=7;player.shopData="existing";player.totalXp=86;player.history.Add(Result(start.AddDays(-2)));string prior=JsonUtility.ToJson(player);var old=JsonUtility.FromJson<PlayerProfile>(prior);DailyStreakEngine.Ensure(old,start);Assert.That(old.dailyStreak.days,Is.Empty);Assert.That(old.history.Count,Is.EqualTo(1));Assert.That(old.coins,Is.EqualTo(123));Assert.That(old.shopData,Is.EqualTo("existing"));Assert.That(old.totalXp,Is.EqualTo(86));Assert.That(old.gems,Is.EqualTo(7));}
        [Test]public void ResultBeforeEnableDoesNotCount(){engine.Apply(player,Result(start.AddSeconds(-1)));Assert.That(player.dailyStreak.current,Is.Zero);}
        [Test]public void LateHistoricalResultDoesNotReviveExpiredStreak(){engine.Apply(player,Result(start));engine.Apply(player,Result(start.AddDays(1)));DailyStreakEngine.Refresh(player,start.AddDays(10));Assert.That(player.dailyStreak.current,Is.Zero);Assert.That(player.dailyStreak.best,Is.EqualTo(2));}
        [Test]public void SaveReopenAndSwitchProfilesRemainSeparate(){string dir=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"Streak"+Guid.NewGuid().ToString("N"));try{using(var store=new LocalPlayerStore(dir)){store.Open("Alpha");store.Transaction(p=>{DailyStreakEngine.Ensure(p,start);DailyStreakEngine.Apply(p,Result(start));});store.Switch("Beta");DailyStreakEngine.Ensure(store.Current,start);Assert.That(store.Current.dailyStreak.current,Is.Zero);store.Switch("Alpha");Assert.That(store.Current.dailyStreak.current,Is.EqualTo(1));}using(var store=new LocalPlayerStore(dir)){store.Open("Alpha");Assert.That(store.Current.dailyStreak.current,Is.EqualTo(1));}}finally{System.IO.Directory.Delete(dir,true);}}
    }
}
