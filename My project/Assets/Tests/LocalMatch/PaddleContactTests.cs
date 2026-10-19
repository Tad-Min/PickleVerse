using NUnit.Framework;
using UnityEngine;
using PickleVerse.LocalMatch;

namespace PickleVerse.Tests
{
    public sealed class PaddleContactTests
    {
        [Test] public void FastBallCrossingPaddleIsDetectedBetweenSamples()
        {
            Assert.That(PaddleContact.Distance(Vector3.back,Vector3.forward,Vector3.zero,Vector3.zero),Is.LessThan(.00001f));
        }
        [Test] public void SwingCrossingBallIsDetectedBetweenSamples()
        {
            Assert.That(PaddleContact.Distance(Vector3.zero,Vector3.zero,Vector3.left,Vector3.right),Is.LessThan(.00001f));
        }
        [Test] public void ObjectsPassingSameLocationAtDifferentTimesDoNotContact()
        {
            Assert.That(PaddleContact.Distance(Vector3.zero,Vector3.right,Vector3.right,Vector3.right*2),Is.EqualTo(1).Within(.00001f));
        }
        [Test] public void StationaryNearMissRetainsItsRealDistance()
        {
            Assert.That(PaddleContact.Distance(Vector3.up,Vector3.up,Vector3.zero,Vector3.zero),Is.EqualTo(1).Within(.00001f));
        }
        [TestCase(0f)][TestCase(.1f)][TestCase(.5f)][TestCase(1f)]
        public void TranslatingCourtDoesNotChangeContact(float fraction)
        {
            Vector3 a=new Vector3(-.2f,1,-2),b=a+Vector3.forward,face=a+Vector3.forward*fraction;
            var shift=new Vector3(37,2,-19);
            Assert.That(PaddleContact.Distance(a,b,face,face),Is.EqualTo(PaddleContact.Distance(a+shift,b+shift,face+shift,face+shift)).Within(.00001f));
        }
        static bool Evaluate(Vector3 ball, Vector3 face, float time, out PaddleHit hit) =>
            PaddleContact.TryEvaluate(ball,ball,face,face,Vector3.zero,Vector3.zero,Quaternion.identity,time,time,.4f,.45f,false,out hit);

        [Test] public void AccuratePaddleContactIsPerfect()
        {
            Assert.That(Evaluate(new Vector3(.3f,1,.65f),new Vector3(.32f,1,.65f),.4f,out var hit),Is.True);
            Assert.That(hit.grade,Is.EqualTo(HitGrade.Perfect));
        }
        [Test] public void FrontAssistContactIsGood()
        {
            Assert.That(Evaluate(new Vector3(.1f,1,.65f),new Vector3(.4f,1,.65f),.4f,out var hit),Is.True);
            Assert.That(hit.grade,Is.EqualTo(HitGrade.Good));
        }
        [Test] public void ValidEarlyContactStillHitsAndIsRatedGood()
        {
            Assert.That(Evaluate(new Vector3(.1f,1,.65f),new Vector3(.4f,1,.65f),.35f,out var hit),Is.True);
            Assert.That(hit.grade,Is.EqualTo(HitGrade.Good));
        }
        [Test] public void PhysicalContactOutsidePerfectTimingIsGood()
        {
            Assert.That(Evaluate(new Vector3(.3f,1,.65f),new Vector3(.32f,1,.65f),.35f,out var hit),Is.True);
            Assert.That(hit.grade,Is.EqualTo(HitGrade.Good));
        }
        [TestCase(.32f)][TestCase(.48f)] public void ContactOutsideSwingWindowMisses(float time)
        {
            Assert.That(Evaluate(new Vector3(.3f,1,.65f),new Vector3(.32f,1,.65f),time,out _),Is.False);
        }
        [TestCase(.2f,1f,-.3f,.55f,1f,-.3f)]
        [TestCase(.9f,1f,.6f,.55f,1f,.6f)]
        [TestCase(.1f,.08f,.6f,.45f,.08f,.6f)]
        [TestCase(.1f,1f,1.5f,.45f,1f,1.5f)]
        public void FrontAssistRejectsBehindSideGroundAndDistantBall(float x,float y,float z,float fx,float fy,float fz)
        { Assert.That(Evaluate(new Vector3(x,y,z),new Vector3(fx,fy,fz),.4f,out _),Is.False); }

        [Test] public void SweptContactClipsTimeBeforeCheckingPaddle()
        {
            // The ball crossed the paddle BEFORE the valid window, then moved far away.
            Assert.That(PaddleContact.TryEvaluate(new Vector3(0,1,.6f),new Vector3(0,1,3),new Vector3(0,1,.6f),new Vector3(0,1,.6f),
                Vector3.zero,Vector3.zero,Quaternion.identity,.1f,.4f,.4f,.45f,false,out _),Is.False);
        }
        [TestCase(1f)][TestCase(-1f)] public void MovingPlayersAtEitherEndUseTheSameZones(float end)
        {
            Quaternion facing=Quaternion.Euler(0,end==1?180:0,0);
            Vector3 bodyBefore=new Vector3(2,0,5),bodyNow=bodyBefore+Vector3.right*.1f;
            Vector3 ballBefore=bodyBefore+facing*new Vector3(.1f,1,.65f),ballNow=bodyNow+facing*new Vector3(.1f,1,.65f);
            Vector3 faceBefore=bodyBefore+facing*new Vector3(.4f,1,.65f),faceNow=bodyNow+facing*new Vector3(.4f,1,.65f);
            Assert.That(PaddleContact.TryEvaluate(ballBefore,ballNow,faceBefore,faceNow,bodyBefore,bodyNow,facing,.395f,.405f,.4f,.45f,false,out var hit),Is.True);
            Assert.That(hit.grade,Is.EqualTo(HitGrade.Good));
        }
        [Test] public void FastPreciseCrossingSurvivesLowPresentationFrameRate()
        {
            Assert.That(PaddleContact.TryEvaluate(new Vector3(.3f,1,1.4f),new Vector3(.3f,1,0),new Vector3(.3f,1,.7f),new Vector3(.3f,1,.7f),
                Vector3.zero,Vector3.zero,Quaternion.identity,.38f,.42f,.4f,.45f,false,out var hit),Is.True);
            Assert.That(hit.grade,Is.EqualTo(HitGrade.Perfect));
            Assert.That(hit.point.z,Is.EqualTo(.7f).Within(.0001));
        }
        [Test] public void ActualUnderhandReleaseAtHipIsNotRejectedByFrontBox()
        {
            // Recorded from a real BotMatch following its entrance cinematic.
            var ball=new Vector3(-.394f,.389f,.1042f);var face=new Vector3(-.391f,.622f,.0448f);
            Assert.That(PaddleContact.TryEvaluate(ball,ball,face,face,Vector3.zero,Vector3.zero,Quaternion.identity,
                .8f,.808333f,.8f,.45f,false,out var hit,true),Is.True);
            Assert.That(hit.grade,Is.EqualTo(HitGrade.Good));
        }
        [TestCase(-.06f)][TestCase(-.03f)][TestCase(0f)][TestCase(.03f)][TestCase(.06f)]
        public void NormalCatchRadiusWorksAcrossWholeContactWindow(float timing)
        {
            Assert.That(Evaluate(new Vector3(-.1f,1,.08f),new Vector3(-.35f,1,.08f),.4f+timing,out var hit),Is.True);
            Assert.That(hit.grade,Is.EqualTo(HitGrade.Good));
        }
        [Test] public void SmashDoesNotUseBroadFrontAssistBeforePaddleArrives()
        {
            var ball=new Vector3(.1f,1.9f,.65f);var face=new Vector3(.4f,1.9f,.65f);
            Assert.That(PaddleContact.TryEvaluate(ball,ball,face,face,Vector3.zero,Vector3.zero,Quaternion.identity,
                .35f,.35f,.4f,.45f,true,out _),Is.False);
        }
    }
}
