using System.Collections.Generic;
using NUnit.Framework;
using PickleVerse.Multiplayer;
using UnityEngine;
using C = PickleVerse.Multiplayer.MatchFoundChoreography;

namespace PickleVerse.Tests.Multiplayer
{
    public sealed class MatchFoundChoreographyTests
    {
        [Test]
        public void HitsBulletTimeAndLockHappenInOrderWithinThreeSeconds()
        {
            Assert.That(C.HitFrame1, Is.LessThan(C.HitFrame2));
            Assert.That(C.HitFrame2, Is.LessThan(C.HitFrame3));
            Assert.That(C.HitFrame3, Is.LessThan(C.LockFrame));
            // the second hit is in bullet time: the gap after it is longer in real frames than the first exchange
            Assert.That(C.HitFrame3 - C.HitFrame2, Is.GreaterThan(C.HitFrame2 - C.HitFrame1));
            Assert.That(C.LockSeconds, Is.InRange(2.5f, 3.2f), "the intro must not delay the decision for long");
        }

        [Test]
        public void ActionTimeAlwaysMovesForward()
        {
            float last = float.NegativeInfinity;
            for (float f = 0; f <= C.EndFrame + 10; f += .25f)
            {
                float a = C.ActionAt(f);
                Assert.That(a, Is.GreaterThan(last), "frame " + f);
                last = a;
            }
        }

        [Test]
        public void BallLocksAtTheCrossingWithVsFacingTheCamera()
        {
            var p = C.BallPosition(C.Lock, out float radius);
            Assert.That(Vector3.Distance(p, C.BallRest), Is.LessThan(1e-3f));
            Assert.That(radius, Is.EqualTo(C.HeroRadius).Within(1e-4f));
            Assert.That(Quaternion.Angle(C.BallRotation(C.Lock), Quaternion.identity), Is.LessThan(.5f));
        }

        [TestCase(-1, C.H1)]
        [TestCase(1, C.H2)]
        [TestCase(-1, C.H3)]
        public void PaddleFaceMeetsTheBallOnEveryHit(int side, float hit)
        {
            // In the paddle's own frame: Y is the face normal, Z runs from the grip to the top of the head.
            C.PaddlePose(side, hit, out var grip, out var rotation);
            var ball = C.BallPosition(hit, out float radius);
            var local = Quaternion.Inverse(rotation) * (ball - grip);
            Assert.That(Mathf.Abs(local.y), Is.LessThan(radius + .35f), "ball sits against the face");
            Assert.That(local.z, Is.InRange(.9f, 2.2f), "ball is on the head, not the handle");
            Assert.That(Mathf.Abs(local.x), Is.LessThan(.6f), "ball is within the face width");
        }

        [Test]
        public void PaddlesEndCrossedAsAMirroredX()
        {
            C.PaddlePose(-1, C.Lock, out var left, out var leftRot);
            C.PaddlePose(1, C.Lock, out var right, out var rightRot);
            Assert.That(left.x + right.x, Is.EqualTo(0).Within(.08f));
            var leftAxis = leftRot * Vector3.forward;
            var rightAxis = rightRot * Vector3.forward;
            Assert.That(Vector3.Angle(leftAxis, Vector3.forward), Is.EqualTo(42).Within(3));
            Assert.That(Vector3.Angle(rightAxis, Vector3.forward), Is.EqualTo(42).Within(3));
            Assert.That(leftAxis.x, Is.GreaterThan(0), "left grip below-left, head up-right");
            Assert.That(rightAxis.x, Is.LessThan(0));
        }

        [Test]
        public void CameraSettlesOnTheHeroShot()
        {
            var shots = C.Shots();
            C.CameraAt(shots, C.EndFrame, out var cam, out var target, out var lens);
            Assert.That(Vector3.Distance(cam, shots[shots.Length - 1].cam), Is.LessThan(1e-4f));
            Assert.That(lens, Is.EqualTo(40).Within(1e-3f));
            Assert.That(target.y - cam.y, Is.GreaterThan(5), "looking into the stage");
        }

        [Test]
        public void BlenderToUnityConversionKeepsRotationsConsistent()
        {
            Assert.That(C.ToUnity(new Vector3(0, 0, 1)), Is.EqualTo(Vector3.up));
            var random = new System.Random(5);
            for (int i = 0; i < 20; i++)
            {
                var axis = new Vector3((float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f);
                var q = C.AxisAngle(axis, (float)random.NextDouble() * 6);
                var v = new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
                var expected = C.ToUnity(q * v);
                var actual = C.ToUnity(q) * C.ToUnity(v);
                Assert.That(Vector3.Distance(expected, actual), Is.LessThan(1e-4f));
            }
        }

        [Test]
        public void VerticalFieldOfViewMatchesTheBlenderLens()
        {
            // 40 mm on a 36 mm sensor is ~48.5 deg horizontally; at 16:9 that is ~28.4 deg vertically.
            Assert.That(C.VerticalFov(40, 16f / 9f), Is.EqualTo(28.4f).Within(.3f));
        }
    }

    public sealed class MatchReadyOverlayRulesTests
    {
        static ReadyCheckSeat Seat(string id, int team, bool ready = false) => new ReadyCheckSeat { id = id, name = id, team = team, ready = ready };

        [Test]
        public void DoublesPutTheLocalTeamLeftWithTheLocalPlayerFirst()
        {
            var seats = new List<ReadyCheckSeat> { Seat("a", 1), Seat("b", 0), Seat("me", 1), Seat("c", 0) };
            var left = new List<ReadyCheckSeat>();
            var right = new List<ReadyCheckSeat>();
            MatchReadyOverlay.Arrange(seats, "me", left, right);
            Assert.That(left.ConvertAll(s => s.id), Is.EqualTo(new[] { "me", "a" }));
            Assert.That(right.ConvertAll(s => s.id), Is.EqualTo(new[] { "b", "c" }));
        }

        [Test]
        public void SinglesShowOnePlayerPerSide()
        {
            var seats = new List<ReadyCheckSeat> { Seat("me", 0), Seat("rival", 1) };
            var left = new List<ReadyCheckSeat>();
            var right = new List<ReadyCheckSeat>();
            MatchReadyOverlay.Arrange(seats, "me", left, right);
            Assert.That(left.Count, Is.EqualTo(1));
            Assert.That(right.Count, Is.EqualTo(1));
            Assert.That(right[0].id, Is.EqualTo("rival"));
        }

        [Test]
        public void TimeoutTextCountsWholeSecondsUp()
        {
            Assert.That(MatchReadyOverlay.TimerText(7.2f, false), Is.EqualTo("TỰ HỦY SAU 08 GIÂY"));
            Assert.That(MatchReadyOverlay.TimerText(0, false), Is.EqualTo("TỰ HỦY SAU 00 GIÂY"));
            Assert.That(MatchReadyOverlay.TimerText(3.0f, true), Is.EqualTo("CHỜ MỌI NGƯỜI XÁC NHẬN · 03 GIÂY"));
        }

        [Test]
        public void ControlsWaitForTheIntroWhenTheServerGrantsGrace()
        {
            float intro = MatchReadyOverlay.IntroSeconds;
            Assert.That(intro, Is.GreaterThan(C.LockSeconds), "buttons come after the VS lock");
            // server window = 4 s grace + 10 s confirmation, read ~0.2 s after it opened
            Assert.That(MatchReadyOverlay.ControlsDelay(13.8f, 10, intro, false), Is.EqualTo(intro).Within(1e-4f));
            // the countdown then starts full
            Assert.That(MatchReadyOverlay.VisibleSeconds(13.8f - intro, 10), Is.EqualTo(10).Within(1e-4f));
        }

        [Test]
        public void ControlsNeverEatIntoDecisionTime()
        {
            float intro = MatchReadyOverlay.IntroSeconds;
            Assert.That(MatchReadyOverlay.ControlsDelay(10, 10, intro, false), Is.EqualTo(0), "no grace from the server: show at once");
            Assert.That(MatchReadyOverlay.ControlsDelay(11.2f, 10, intro, false), Is.EqualTo(1.2f).Within(1e-4f), "late arrival: only the grace left");
            Assert.That(MatchReadyOverlay.ControlsDelay(14, 10, intro, true), Is.EqualTo(0), "already accepted");
            Assert.That(MatchReadyOverlay.VisibleSeconds(2.5f, 10), Is.EqualTo(2.5f));
            Assert.That(MatchReadyOverlay.VisibleSeconds(-1, 10), Is.EqualTo(0));
        }

        [Test]
        public void DefaultAvatarUsesTheFirstLetter()
        {
            Assert.That(MatchReadyOverlay.Initial("smashKing"), Is.EqualTo("S"));
            Assert.That(MatchReadyOverlay.Initial("  đạt"), Is.EqualTo("Đ"));
            Assert.That(MatchReadyOverlay.Initial(""), Is.EqualTo("?"));
        }

        [Test]
        public void SnapshotCountsReadyPlayers()
        {
            var snapshot = new ReadyCheckSnapshot();
            snapshot.seats.Add(Seat("a", 0, true));
            snapshot.seats.Add(Seat("b", 1));
            Assert.That(snapshot.ReadyCount, Is.EqualTo(1));
            Assert.That(snapshot.AllReady, Is.False);
            snapshot.seats[1].ready = true;
            Assert.That(snapshot.AllReady, Is.True);
        }
    }
}
