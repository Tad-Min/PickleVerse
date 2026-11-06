using System;
using System.Linq;
using NUnit.Framework;
using PickleVerse.Progression;
using UnityEngine;

namespace PickleVerse.Tests
{
    public sealed class TrainingTests
    {
        static PlayerProfile Player()=>new PlayerProfile{id="TrainingTest",playerName="Test",coins=1000,gems=9,currencyVersion=1};
        static TrainingAttempt Attempt(PlayerProfile p,string id,int successes=-1)
        {
            var d=TrainingCurriculum.Find(id);var a=TrainingEngine.Start(p,id);a.trials=d.trials;a.successes=successes<0?d.trials:successes;a.contacts=a.successes;a.timingTotal=a.contacts*.08;a.seconds=30;return a;
        }
        [Test]public void CurriculumCoversFifteenDrillsAndAllTwelveSkills()
        {
            Assert.AreEqual(15,TrainingCurriculum.Lessons.Length);Assert.AreEqual(12,TrainingCurriculum.Lessons.Select(d=>d.skill).Distinct().Count());
            Assert.IsTrue(TrainingCurriculum.Lessons.All(d=>d.required<=d.trials&&d.required>0&&d.reward>=150&&d.masks.All(m=>m>0&&m<32)));
            Assert.AreEqual(2,TrainingCurriculum.Lessons.Count(d=>d.Doubles));
        }
        [Test]public void NewDrillsUseExistingControlsAndDistinctPracticeGoals()
        {
            Assert.IsTrue(TrainingCurriculum.Find("serve-alternating").alternateTargets);
            CollectionAssert.AreEqual(new[]{18},TrainingCurriculum.Find("dink-cross").masks);
            Assert.IsTrue(TrainingCurriculum.Find("volley-recovery").requireRecovery);
            Assert.AreEqual(TrainingSkill.Volley,TrainingCurriculum.Find("volley-recovery").skill);
        }
        [Test]public void AttemptUsesItsOwnCatalogWithoutChangingOtherSessions()
        {
            var local=JsonUtility.FromJson<TrainingDrill>(JsonUtility.ToJson(TrainingCurriculum.Find("serve")));
            local.id="versioned-serve";local.trials=3;local.required=2;local.reward=77;
            var p=Player();var attempt=TrainingEngine.Start(p,local.id,false,new[]{local});
            attempt.trials=attempt.contacts=attempt.successes=3;attempt.timingTotal=.1;
            Assert.IsTrue(TrainingEngine.Complete(p,attempt,new[]{local}));
            Assert.AreEqual(77,attempt.rewardTokenpad);Assert.IsNull(TrainingCurriculum.Find(local.id));
            Assert.AreEqual(8,TrainingCurriculum.Find("serve").trials);
        }
        [Test]public void BasicLessonsAreAvailableAndAdvancedLessonsNeedPrerequisites()
        {
            var p=Player();var t=TrainingEngine.Ensure(p);Assert.AreEqual(4,TrainingCurriculum.Lessons.Count(d=>TrainingEngine.Unlocked(t,d)));
            Assert.Throws<InvalidOperationException>(()=>TrainingEngine.Start(p,"drop"));
            foreach(var id in new[]{"drive","dink"})TrainingEngine.Complete(p,Attempt(p,id));
            Assert.IsTrue(TrainingEngine.Unlocked(t,TrainingCurriculum.Find("drop")));
        }
        [Test]public void FirstCompletionPaysButReviewAndCompletionReplayDoNot()
        {
            var p=Player();var first=Attempt(p,"serve");Assert.IsTrue(TrainingEngine.Complete(p,first));Assert.AreEqual(1150,p.coins);
            Assert.IsFalse(TrainingEngine.Complete(p,first));Assert.AreEqual(1150,p.coins);
            Assert.AreEqual(150,first.rewardTokenpad);Assert.AreEqual(5,first.rewardXp);
            var next=Attempt(p,"serve");Assert.AreNotEqual(first.id,next.id);Assert.IsTrue(next.review);Assert.IsTrue(TrainingEngine.Complete(p,next));Assert.AreEqual(1150,p.coins);Assert.AreEqual(5,p.totalXp);
            Assert.AreEqual(0,next.rewardTokenpad+next.rewardXp);Assert.AreEqual(2,p.training.lessons.Single().completions);
            Assert.AreEqual(1,p.training.lessons.Single().retries);
        }
        [Test]public void LegacyMasteryAndOldCheckpointCannotClaimAgain()
        {
            var p=Player();var old=Attempt(p,"serve");TrainingEngine.Record(p.training,"serve").bestStars=3;
            var loaded=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(p));
            Assert.IsTrue(TrainingEngine.Complete(loaded,loaded.training.active));Assert.IsTrue(loaded.training.active.review);
            Assert.AreEqual(1000,loaded.coins);Assert.AreEqual(0,loaded.totalXp);Assert.AreEqual(0,loaded.training.active.rewardTokenpad);
        }
        [Test]public void FailedFirstAttemptCanStillEarnFirstCompletionReward()
        {
            var p=Player();TrainingEngine.Complete(p,Attempt(p,"drive",2));Assert.IsFalse(TrainingEngine.Completed(p.training,"drive"));
            var a=Attempt(p,"drive");Assert.IsFalse(a.review);TrainingEngine.Complete(p,a);Assert.AreEqual(1150,p.coins);
        }
        [Test]public void ReviewStillImprovesStarsAndLogsMistakesWithoutRewardsAfterReload()
        {
            var p=Player();TrainingEngine.Complete(p,Attempt(p,"serve",6));
            p=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(p));var a=Attempt(p,"serve");a.Error(TrainingError.WrongInput);
            TrainingEngine.Complete(p,a);Assert.AreEqual(1150,p.coins);Assert.AreEqual(5,p.totalXp);Assert.AreEqual(3,p.training.lessons.Single().bestStars);
            Assert.AreEqual(1,p.training.lessons.Single().errors.Single().count);Assert.AreEqual(2,p.training.lessons.Single().accuracyHistory.Count);
        }
        [Test]public void MissingTrialsAndOtherPlayerCannotClaimRewards()
        {
            var p=Player();var a=TrainingEngine.Start(p,"drive");Assert.IsFalse(TrainingEngine.Complete(p,a));a.trials=8;a.successes=8;a.playerId="SomeoneElse";
            Assert.IsFalse(TrainingEngine.Complete(p,a));Assert.AreEqual(1000,p.coins);
        }
        [Test]public void FailedDrillRecordsErrorsAndReceivesNoCurrency()
        {
            var p=Player();var a=Attempt(p,"drive",3);a.Error(TrainingError.Late);a.Error(TrainingError.Late);TrainingEngine.Complete(p,a);
            Assert.AreEqual(1000,p.coins);Assert.AreEqual(0,a.stars);Assert.IsFalse(a.passed);Assert.AreEqual(2,p.training.lessons.Single().errors.Single().count);
        }
        [TestCase(6,1)][TestCase(7,2)][TestCase(8,3)]public void StarsUseAccuracyAndTiming(int successes,int stars)
        {var p=Player();var a=Attempt(p,"serve",successes);TrainingEngine.Complete(p,a);Assert.AreEqual(stars,a.stars);}
        [Test]public void SlowTimingCannotReceiveThreeStars(){var p=Player();var a=Attempt(p,"drive");a.timingTotal=a.contacts*.3;TrainingEngine.Complete(p,a);Assert.AreEqual(2,a.stars);}
        [Test]public void TrainingAwardsXpWithoutChangingEloMatchesOrElite()
        {var p=Player();TrainingEngine.Complete(p,Attempt(p,"serve"));Assert.AreEqual(5,p.totalXp);Assert.AreEqual(100,p.elo);Assert.AreEqual(0,p.wins+p.losses);Assert.AreEqual(9,p.gems);Assert.AreEqual(0,p.lifetime.matches);}
        [Test]public void ResultsAndActiveCheckpointSurviveJsonReload()
        {
            var p=Player();TrainingEngine.Complete(p,Attempt(p,"serve"));var a=TrainingEngine.Start(p,"drive");a.trials=3;a.successes=2;a.Error(TrainingError.WrongInput);
            var saved=JsonUtility.ToJson(p);var loaded=JsonUtility.FromJson<PlayerProfile>(saved);Assert.AreEqual(a.id,loaded.training.active.id);Assert.AreEqual(3,loaded.training.active.trials);Assert.AreEqual("drive",TrainingEngine.Recommended(loaded.training));
            loaded.training.active.trials=8;loaded.training.active.successes=6;Assert.IsTrue(TrainingEngine.Complete(loaded,loaded.training.active));
            var completed=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(loaded));Assert.IsFalse(TrainingEngine.Complete(completed,completed.training.active));
        }
        [Test]public void EmptyLegacyCheckpointIsNotAResumableAttempt()
        {var p=Player();p.training.active=new TrainingAttempt();Assert.IsNull(TrainingEngine.Ensure(p).active);Assert.AreEqual("serve",TrainingEngine.Recommended(p.training));}
        [Test]public void HarderGroupsPayMoreAndHistoriesStayBounded()
        {
            Assert.AreEqual(150,TrainingCurriculum.Find("serve").reward);Assert.AreEqual(225,TrainingCurriculum.Find("drop").reward);Assert.AreEqual(300,TrainingCurriculum.Find("approach").reward);Assert.AreEqual(450,TrainingCurriculum.Find("doubles-cover").reward);
            var p=Player();for(int i=0;i<15;i++)TrainingEngine.Complete(p,Attempt(p,"drive"));Assert.AreEqual(8,p.training.lessons.Single().accuracyHistory.Count);Assert.AreEqual(14,p.training.lessons.Single().retries);
        }
    }
}
