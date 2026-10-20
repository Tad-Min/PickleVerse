using UnityEngine;
using PickleVerse.Rules;

namespace PickleVerse.LocalMatch
{
    public enum HitGrade : byte { None, Good, Perfect }

    public struct PaddleHit
    {
        public HitGrade grade;
        public Vector3 point;
        public float distance, timingError;
    }

    /// <summary>Shared swept contact and bounded anticipation for the custom ballistic simulation.</summary>
    public static class PaddleContact
    {
        public const float TimingWindow = .065f;
        public const float MaxAssist = .30f;
        public const float PaddleRadius = .145f;
        public const float PerfectWindow = .030f;
        public const float FrontAssist = .38f;
        public static readonly Vector3 FrontCenter = new Vector3(0, .95f, .64f);
        public static readonly Vector3 FrontHalfSize = new Vector3(.68f, .70f, .53f);

        public static HitGrade Grade(float distance, float timingError) =>
            distance <= PaddleRadius && Mathf.Abs(timingError) <= PerfectWindow ? HitGrade.Perfect : HitGrade.Good;

        // Two analytical hitboxes, sampled by the same authoritative ball simulation:
        // a small swept paddle sphere, and a bounded box in front of the moving body.
        // Clip BOTH space and time before measuring; a fast crossing must not be
        // accepted at a different instant from its swing's contact window.
        public static bool TryEvaluate(Vector3 ballBefore, Vector3 ballNow, Vector3 faceBefore, Vector3 faceNow,
            Vector3 bodyBefore, Vector3 bodyNow, Quaternion facing, float timeBefore, float timeNow,
            float contactDelay, float assist, bool overhead, out PaddleHit hit, bool serving = false)
        {
            hit = default;
            float lo = 0, hi = 1;
            if (!Clip(timeBefore, timeNow, contactDelay - TimingWindow, contactDelay + TimingWindow, ref lo, ref hi)) return false;
            float t = ClosestInRange(ballBefore, ballNow, faceBefore, faceNow, lo, hi);
            // Preserve the existing catch volume independently of the tighter grade.
            // In particular, the underhand serve releases beside the hip, rather than
            // inside the front rally box. A Good contact must still launch that ball.
            float catchRadius = Mathf.Min(Mathf.Max(0, assist), serving ? .45f : overhead ? .16f : MaxAssist);
            if (Measure(ballBefore, ballNow, faceBefore, faceNow, bodyBefore, bodyNow, timeBefore, timeNow, contactDelay, t, catchRadius, out hit)) return true;

            // The front assist uses the FULL legal swing window, including wind-up.
            // Delaying it until the nominal contact beat makes earlier valid balls
            // pass through the player. Perfect remains a rating, never a hit gate.
            // Overheads retain their calibrated, smaller reach; a broad early
            // catch would reverse the ball before the raised paddle gets there.
            if (serving || overhead) return false;
            Quaternion inverse = Quaternion.Inverse(facing);
            Vector3 a = inverse * (ballBefore - bodyBefore), b = inverse * (ballNow - bodyNow);
            Vector3 center = overhead ? new Vector3(0, 1.9f, FrontCenter.z) : FrontCenter;
            if (!Clip(a.x, b.x, center.x - FrontHalfSize.x, center.x + FrontHalfSize.x, ref lo, ref hi) ||
                !Clip(a.y, b.y, center.y - FrontHalfSize.y, center.y + FrontHalfSize.y, ref lo, ref hi) ||
                !Clip(a.z, b.z, center.z - FrontHalfSize.z, center.z + FrontHalfSize.z, ref lo, ref hi)) return false;
            t = ClosestInRange(ballBefore, ballNow, faceBefore, faceNow, lo, hi);
            return Measure(ballBefore, ballNow, faceBefore, faceNow, bodyBefore, bodyNow, timeBefore, timeNow,
                contactDelay, t, Mathf.Min(Mathf.Max(0, assist), FrontAssist), out hit);
        }
        static float ClosestInRange(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float lo, float hi) =>
            Mathf.Clamp(ClosestTime(a, b, c, d), lo, hi);
        static bool Measure(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 bodyBefore, Vector3 bodyNow,
            float before, float now, float delay, float t, float radius, out PaddleHit hit)
        {
            hit = default;
            Vector3 point = Vector3.Lerp(a, b, t);
            float distance = Vector3.Distance(point, Vector3.Lerp(c, d, t));
            if (distance > radius || Vector3.Distance(point, Vector3.Lerp(bodyBefore, bodyNow, t) + Vector3.up * .9f) > 1.55f) return false;
            float error = Mathf.Abs(Mathf.Lerp(before, now, t) - delay);
            hit = new PaddleHit { point = point, distance = distance, timingError = error, grade = Grade(distance, error) };
            return true;
        }
        static bool Clip(float a, float b, float min, float max, ref float lo, ref float hi)
        {
            float delta = b - a;
            if (Mathf.Abs(delta) < .000001f) return a >= min && a <= max;
            float enter = (min - a) / delta, exit = (max - a) / delta;
            if (enter > exit) { float swap = enter; enter = exit; exit = swap; }
            lo = Mathf.Max(lo, enter); hi = Mathf.Min(hi, exit);
            return lo <= hi;
        }

        // Sweep both objects in relative space, rather than testing a stationary paddle
        // against just the final ball position. This also works at low presentation FPS.
        public static float Distance(Vector3 ballBefore, Vector3 ballNow, Vector3 faceBefore, Vector3 faceNow)
        {
            Vector3 a = ballBefore - faceBefore, d = ballNow - faceNow - a;
            float t = ClosestTime(ballBefore,ballNow,faceBefore,faceNow);
            return (a + d * t).magnitude;
        }
        public static float ClosestTime(Vector3 ballBefore, Vector3 ballNow, Vector3 faceBefore, Vector3 faceNow)
        {
            Vector3 a=ballBefore-faceBefore,d=ballNow-faceNow-a;
            return Mathf.Clamp01(-Vector3.Dot(a,d)/Mathf.Max(.000001f,d.sqrMagnitude));
        }

        public static float Anticipation(MatchActor actor, ShotIntent shot, Vector3 ball, Vector3 velocity, out Vector3 predicted)
        {
            predicted=ball;
            if (shot.kind == ShotKind.Serve) return shot.ContactDelay;
            Vector3 p = ball, v = velocity;
            Vector3 body=actor.transform.position,bodyVelocity=actor.Velocity,start=body;
            float best = float.PositiveInfinity, time = shot.ContactDelay;
            Vector3 forehand=actor.transform.TransformPoint(actor.ContactOffset(shot,false));
            Vector3 backhand=actor.transform.TransformPoint(actor.ContactOffset(shot,true));
            const float step = 1f / 120;
            for (float t = step; t <= shot.ContactDelay + .08f; t += step)
            {
                BallFlight.Integrate(ref p, ref v, step);
                if (p.y <= Court.BallRadius && v.y < 0) BallFlight.Rebound(ref p, ref v);
                MatchMovement.Step(ref body,ref bodyVelocity,actor.MovementIntent,actor.MovementRun,true,actor.end,step,actor.walkSpeed,actor.runSpeed,actor.MovementBounds);
                Vector3 movement=body-start;
                float error = Mathf.Min((p-forehand-movement).sqrMagnitude,(p-backhand-movement).sqrMagnitude);
                if (t >= .10f && error < best) { best = error; time = t; predicted=p; }
            }
            // Do not schedule a swing against a distant or out-of-reach future ball.
            if(best <= .85f * .85f)return Mathf.Clamp(time, .10f, shot.ContactDelay + .08f);
            predicted=ball+velocity*shot.ContactDelay+Vector3.down*(.5f*BallFlight.Gravity*shot.ContactDelay*shot.ContactDelay);
            return shot.ContactDelay;
        }
        public static Vector3 PredictMovement(MatchActor actor,float time)
        {
            Vector3 position=actor.transform.position,velocity=actor.Velocity,start=position;
            while(time>.00001f)
            {
                float dt=Mathf.Min(time,1f/120);
                MatchMovement.Step(ref position,ref velocity,actor.MovementIntent,actor.MovementRun,true,actor.end,dt,actor.walkSpeed,actor.runSpeed,actor.MovementBounds);
                time-=dt;
            }
            return position-start;
        }
    }
}
