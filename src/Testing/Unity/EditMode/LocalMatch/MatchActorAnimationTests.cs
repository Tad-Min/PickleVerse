using NUnit.Framework;
using PickleVerse.LocalMatch;
using UnityEngine;

namespace PickleVerse.Tests
{
    public sealed class MatchActorAnimationTests
    {
        MatchActor actor;
        AnimationClip clip;

        [SetUp]
        public void SetUp()
        {
            actor = new GameObject("MatchActor").AddComponent<MatchActor>();
            actor.visual = new GameObject("Rig").transform;
            actor.visual.SetParent(actor.transform, false);
            actor.paddleFace = new GameObject("PaddleFace").transform;
            actor.paddleFace.SetParent(actor.visual, false);
            clip = new AnimationClip { legacy = true };
            clip.SetCurve("PaddleFace", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
            actor.clips = new[]
            {
                new ClipEntry { name = "PKV_Idle_Ready", clip = clip },
                new ClipEntry { name = "PKV_Serve_FH_Underhand", clip = clip, contactPhase = .55f },
                new ClipEntry { name = "PKV_Groundstroke_FH", clip = clip, contactPhase = .55f },
                new ClipEntry { name = "PKV_Groundstroke_BH", clip = clip, contactPhase = .55f }
            };
            actor.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(actor.gameObject);
            Object.DestroyImmediate(clip);
        }

        [TestCase(ShotKind.Serve)]
        [TestCase(ShotKind.Drive)]
        public void ConfirmedContactKeepsTheVisibleSwingMoving(ShotKind kind)
        {
            var shot = new ShotIntent(kind);
            actor.RenderNetworkSwing(shot, Vector3.forward, shot.ContactDelay, shot.ContactDelay - .035f);
            actor.RenderNetworkPose(Vector3.zero, false, .06f);
            float beforeContact = actor.paddleFace.localPosition.x;

            actor.ConsumeContact(false, successful: false, preserveSwingTiming: true);
            Assert.That(actor.paddleFace.localPosition.x, Is.EqualTo(beforeContact).Within(.0001f));

            actor.RenderNetworkPose(Vector3.zero, false, 1f / 60f);
            Assert.That(actor.paddleFace.localPosition.x, Is.GreaterThan(beforeContact));
        }

        [Test]
        public void NextConfirmedSwingStartsDuringThePreviousFollowThrough()
        {
            var drive = new ShotIntent(ShotKind.Drive);
            var serve = new ShotIntent(ShotKind.Serve);
            actor.RenderNetworkSwing(drive, Vector3.forward, drive.ContactDelay, .75f);
            Assert.That(actor.Swing(serve, Vector3.forward), Is.False);

            actor.RenderNetworkSwing(serve, Vector3.forward, serve.ContactDelay, .01f);

            Assert.That(actor.CurrentClip, Is.EqualTo("PKV_Serve_FH_Underhand"));
            Assert.That(actor.SwingTime, Is.EqualTo(.01f).Within(.0001f));
        }
    }
}
