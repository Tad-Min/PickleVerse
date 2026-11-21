using NUnit.Framework;
using UnityEngine;
using PickleVerse.LocalMatch;

namespace PickleVerse.Tests
{
    public sealed class ActorPoseMixerTests
    {
        GameObject root;
        Transform hips,leg,foot,spine,hand;
        ActorPoseMixer mixer;
        Transform Bone(string name,Transform parent)
        {var t=new GameObject("mixamorig:"+name).transform;t.SetParent(parent,false);return t;}
        [SetUp] public void SetUp()
        {
            root=new GameObject("Pose fixture");hips=Bone("Hips",root.transform);
            leg=Bone("LeftUpLeg",hips);foot=Bone("LeftFoot",leg);Bone("RightUpLeg",hips);
            spine=Bone("Spine",hips);hand=Bone("RightHand",spine);
            mixer=new ActorPoseMixer(root.GetComponentsInChildren<Transform>());
        }
        [TearDown] public void TearDown(){Object.DestroyImmediate(root);}
        [Test] public void MovingLegsKeepTheStrikeTorsoAndHandOrientation()
        {
            hips.localRotation=Quaternion.Euler(0,35,0);leg.localRotation=Quaternion.Euler(40,0,0);mixer.CaptureLegs();
            hips.localRotation=Quaternion.identity;spine.localRotation=Quaternion.Euler(10,-25,15);hand.localRotation=Quaternion.Euler(0,0,80);
            Quaternion torso=spine.rotation,wrist=hand.rotation;mixer.ApplyLegs(1);
            Assert.That(Quaternion.Angle(leg.localRotation,Quaternion.Euler(40,0,0)),Is.LessThan(.01f));
            Assert.That(Quaternion.Angle(spine.rotation,torso),Is.LessThan(.01f));
            Assert.That(Quaternion.Angle(hand.rotation,wrist),Is.LessThan(.01f));
        }
        [Test] public void LegMaskIncludesFeetButDoesNotReplaceArmLocalPose()
        {
            foot.localRotation=Quaternion.Euler(20,0,0);hand.localRotation=Quaternion.Euler(60,0,0);mixer.CaptureLegs();
            foot.localRotation=Quaternion.identity;hand.localRotation=Quaternion.Euler(0,45,0);mixer.ApplyLegs(1);
            Assert.That(Quaternion.Angle(foot.localRotation,Quaternion.Euler(20,0,0)),Is.LessThan(.01f));
            Assert.That(Quaternion.Angle(hand.localRotation,Quaternion.Euler(0,45,0)),Is.LessThan(.01f));
        }
        [Test] public void DirectionChangeStartsAtTheDisplayedLegPose()
        {
            leg.localRotation=Quaternion.Euler(15,0,0);mixer.BeginLegs(.1f);
            leg.localRotation=Quaternion.Euler(-70,0,0);mixer.CaptureLegs(0);mixer.ApplyLegs(1);
            Assert.That(Quaternion.Angle(leg.localRotation,Quaternion.Euler(15,0,0)),Is.LessThan(.01f));
        }
        [Test] public void DirectionChangeConvergesWithoutFreezingTheGait()
        {
            mixer.BeginLegs(.1f);leg.localRotation=Quaternion.Euler(60,0,0);mixer.CaptureLegs(.05f);mixer.ApplyLegs(1);
            Assert.That(Quaternion.Angle(Quaternion.identity,leg.localRotation),Is.EqualTo(30).Within(.02f));
            leg.localRotation=Quaternion.Euler(80,0,0);mixer.CaptureLegs(.05f);mixer.ApplyLegs(1);
            Assert.That(Quaternion.Angle(leg.localRotation,Quaternion.Euler(80,0,0)),Is.LessThan(.01f));
        }
        [Test] public void AnotherTurnBlendsFromTheCurrentPoseRatherThanAnOldDirection()
        {
            mixer.BeginLegs(.1f);leg.localRotation=Quaternion.Euler(60,0,0);mixer.CaptureLegs(.05f);mixer.ApplyLegs(1);
            var displayed=leg.localRotation;mixer.BeginLegs(.1f);
            leg.localRotation=Quaternion.Euler(-60,0,0);mixer.CaptureLegs(0);mixer.ApplyLegs(1);
            Assert.That(Quaternion.Angle(leg.localRotation,displayed),Is.LessThan(.01f));
        }
        [Test] public void ResetDoesNotReuseATransitionFromThePreviousRally()
        {
            mixer.BeginLegs(.1f);mixer.Reset();leg.localRotation=Quaternion.Euler(70,0,0);mixer.CaptureLegs();
            leg.localRotation=Quaternion.identity;mixer.ApplyLegs(1);
            Assert.That(Quaternion.Angle(leg.localRotation,Quaternion.Euler(70,0,0)),Is.LessThan(.01f));
        }
        [Test] public void ZeroLegWeightRetainsTheStandingStrikePose()
        {
            leg.localRotation=Quaternion.Euler(70,0,0);mixer.CaptureLegs();leg.localRotation=Quaternion.identity;mixer.ApplyLegs(0);
            Assert.That(Quaternion.Angle(leg.localRotation,Quaternion.identity),Is.LessThan(.01f));
        }
    }
}
