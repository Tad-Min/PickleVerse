using System.Linq;
using NUnit.Framework;
using PickleVerse.VFX;
using UnityEngine;

namespace PickleVerse.Tests.VFX
{
    public sealed class DragonBallMotionTests
    {
        GameObject ball;
        DragonBallMotion motion;
        MeshRenderer body;

        [SetUp]
        public void SetUp()
        {
            ball = new GameObject("Ball");
            var root = new GameObject("Fx");
            root.transform.SetParent(ball.transform, false);
            body = new GameObject("Dragon", typeof(MeshFilter), typeof(MeshRenderer)).GetComponent<MeshRenderer>();
            body.transform.SetParent(root.transform, false);
            body.enabled = false;
            motion = root.AddComponent<DragonBallMotion>();
            motion.renderers = new Renderer[] { body };
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(ball);

        void Fly(float from, float to, float step = .1f)
        {
            for (float z = from; z <= to + 1e-4f; z += step)
            {
                ball.transform.position = new Vector3(0, 1, z);
                motion.Advance(ball.transform.position, 1 / 60f, null);
            }
        }

        [Test]
        public void TheDragonWeavesOnASineWave()
        {
            const float a = .2f, wavelength = 1.1f, ramp = .75f, flown = 6;
            // fully open part of the flight: exactly A sin(2 pi x / wavelength), x = distance from the paddle
            for (float x = 1; x < 5; x += .05f)
                Assert.That(DragonBallMotion.WaveOffset(flown - x, flown, a, wavelength, ramp), Is.EqualTo(a * Mathf.Sin(2 * Mathf.PI * x / wavelength)).Within(1e-4f), "x=" + x);
        }

        [Test]
        public void TheWaveStartsOnThePaddleAndEndsOnTheBall()
        {
            Assert.That(DragonBallMotion.WaveOffset(0, 5, .2f, 1.1f, .75f), Is.EqualTo(0).Within(1e-6f), "at the ball");
            Assert.That(DragonBallMotion.WaveOffset(5, 5, .2f, 1.1f, .75f), Is.EqualTo(0).Within(1e-6f), "at the contact");
            Assert.That(DragonBallMotion.WaveOffset(7, 5, .2f, 1.1f, .75f), Is.EqualTo(0).Within(1e-6f), "before the contact");
            for (float b = 0; b < 5; b += .02f)
                Assert.That(Mathf.Abs(DragonBallMotion.WaveOffset(b, 5, .2f, 1.1f, .75f)), Is.LessThanOrEqualTo(.2f + 1e-5f));
        }

        [Test]
        public void TheDragonFollowsThePathTheBallFlew()
        {
            ball.transform.position = new Vector3(0, 1, 0);
            motion.Restart(ball.transform.position);
            motion.SetShown(true);
            Fly(0, 4);
            Assert.That(motion.Flown, Is.EqualTo(4).Within(.05f));
            Assert.That(Vector3.Distance(motion.PathPoint(0), new Vector3(0, 1, 4)), Is.LessThan(.05f));
            Assert.That(Vector3.Distance(motion.PathPoint(1.5f), new Vector3(0, 1, 2.5f)), Is.LessThan(.05f));
            Assert.That(Vector3.Distance(motion.PathPoint(99), new Vector3(0, 1, 0)), Is.LessThan(.05f), "never before the paddle");
            var wave = Enumerable.Range(0, 40).Select(i => motion.WavePoint(1 + i * .05f) - motion.PathPoint(1 + i * .05f)).ToArray();
            Assert.That(wave.Max(w => w.magnitude), Is.InRange(.15f, .21f));
            Assert.That(wave.Any(w => Vector3.Dot(w, wave[0]) < 0), "swings to both sides of the path");
        }

        [Test]
        public void TheDragonShowsOnlyWhileItsPaddleOwnsTheShot()
        {
            Assert.IsFalse(body.enabled);
            ball.transform.position = Vector3.up;
            motion.Restart(ball.transform.position);
            motion.SetShown(true);
            Fly(0, 2);
            Assert.IsTrue(body.enabled);
            Assert.That(motion.Fade, Is.EqualTo(1));
            motion.SetShown(false);                       // the opponent returned it with another paddle
            for (int i = 0; i < 40; i++)
                motion.Advance(ball.transform.position, 1 / 60f, null);
            Assert.That(motion.Fade, Is.EqualTo(0));
            Assert.IsFalse(body.enabled);
        }

        [Test]
        public void EveryContactStartsANewFlight()
        {
            ball.transform.position = Vector3.up;
            motion.Restart(ball.transform.position);
            motion.SetShown(true);
            Fly(0, 3);
            motion.Restart(new Vector3(0, 1, 3));
            Assert.That(motion.Flown, Is.LessThan(.1f));
            Assert.That(Vector3.Distance(motion.PathPoint(5), new Vector3(0, 1, 3)), Is.LessThan(.05f));
            // a ball that is placed elsewhere for the next point does not drag the dragon across the court
            ball.transform.position = new Vector3(8, 1, -6);
            motion.Advance(ball.transform.position, 1 / 60f, null);
            Assert.That(motion.Flown, Is.LessThan(.1f));
        }

        [Test]
        public void TheWaveStaysAboveTheCourt()
        {
            ball.transform.position = new Vector3(0, .12f, 0);
            motion.Restart(ball.transform.position);
            motion.SetShown(true);
            for (float z = 0; z < 4; z += .1f)
            {
                ball.transform.position = new Vector3(0, .12f, z);
                motion.Advance(ball.transform.position, 1 / 60f, null);
            }
            for (float b = 0; b < 4; b += .05f)
                Assert.That(motion.WavePoint(b).y, Is.GreaterThanOrEqualTo(motion.floor - 1e-4f));
        }
    }
}
