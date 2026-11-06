using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PickleVerse.LocalMatch;
using PickleVerse.Rules;

namespace PickleVerse.Tests
{
    public sealed class TutorialTests
    {
        sealed class Memory:ITutorialProgressStore
        {readonly Dictionary<string,int> data=new Dictionary<string,int>();public int Read(string key)=>data.TryGetValue(key,out var value)?value:0;public void Write(string key,int value){data[key]=value;}}
        [SetUp] public void Setup(){TutorialProgress.Configure(new Memory());}
        [TearDown] public void Reset(){TutorialProgress.Configure(null);}
        [Test] public void IntroductionOnlyRequiredUntilFinishedOrExplicitlyDeferred()
        {Assert.That(TutorialProgress.NeedsIntroduction);TutorialProgress.Skip();Assert.That(TutorialProgress.NeedsIntroduction,Is.False);Assert.That(TutorialProgress.Completed,Is.False);TutorialProgress.Complete();Assert.That(TutorialProgress.Completed);}
        [TestCase(1)] [TestCase(2)] [TestCase(4)] [TestCase(9)] [TestCase(10)] [TestCase(12)] [TestCase(17)] [TestCase(18)] [TestCase(20)]
        public void LessonInputsMatchActualGameplayResolver(int mask)
        {var input=new ShotInput();input.Tick(mask,false,false,out _);Assert.That(input.Tick(mask&24,false,false,out var shot));Assert.That(TutorialCurriculum.Matches(mask,input.CommittedMask,shot));Assert.That(TutorialCurriculum.Matches(mask,mask^8,shot),Is.False);}
        [Test] public void AdvancedBaseCombosAreNotInBasicCourse()
        {var shots=System.Array.FindAll(TutorialCurriculum.Lessons,l=>l.kind==TutorialKind.Shot);Assert.That(shots.Length,Is.EqualTo(9));foreach(var shot in shots)Assert.That(new[]{1,2,4},Does.Contain(shot.mask&7));}
        [TestCase(true,.93f,-4.4f)] [TestCase(false,.75f,-2.6f)]
        public void FeedReachesAuthoredPoseAndClearsNet(bool bounce,float height,float z)
        {
            var target=new Vector3(1.3f,height,z);var feed=TutorialFeed.Create(new Vector3(-1.2f,.95f,3.7f),target,bounce);
            Assert.That(Vector3.Distance(feed.At(feed.duration),target),Is.LessThan(.005f));
            float time=-feed.origin.z/feed.velocity.z;var crossing=feed.At(time);Assert.That(crossing.y,Is.GreaterThan(Court.NetHeight(crossing.x)+Court.BallRadius));
            if(bounce){Assert.That(feed.bouncePoint.z,Is.LessThan(0));Assert.That(feed.bouncePoint.y,Is.EqualTo(Court.BallRadius).Within(.0001f));}
        }
        [Test] public void VolleyPracticeStartsAfterTwoMandatoryBounces()
        {var rules=TutorialCurriculum.RallyReady();Assert.That(rules.Live);Assert.That(rules.Stroke,Is.EqualTo(3));Assert.That(rules.Hit(0,new CourtPoint(1,.8f,-2.9f),false),Is.EqualTo(Fault.None));}
        [Test] public void KitchenVolleyIsFlaggedAndBounceShotIsLegal()
        {var rules=TutorialCurriculum.RallyReady();Assert.That(rules.Hit(0,new CourtPoint(1,.8f,-1),true),Is.EqualTo(Fault.KitchenVolley));rules=TutorialCurriculum.RallyReady();rules.Bounce(new CourtPoint(1,Court.BallRadius,-1));Assert.That(rules.Hit(0,new CourtPoint(1,.8f,-1),true),Is.EqualTo(Fault.None));}
    }
}
