using UnityEngine;
using PickleVerse.Rules;

namespace PickleVerse.LocalMatch
{
    /// <summary>Shared swept contact and bounded anticipation for the custom ballistic simulation.</summary>
    public static class PaddleContact
    {
        public const float TimingWindow = .065f;
        public const float MaxAssist = .30f;

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
            float best = float.PositiveInfinity, time = shot.ContactDelay;
            Vector3 forehand=actor.transform.TransformPoint(actor.ContactOffset(shot,false));
            Vector3 backhand=actor.transform.TransformPoint(actor.ContactOffset(shot,true));
            const float step = 1f / 120;
            for (float t = step; t <= shot.ContactDelay + .08f; t += step)
            {
                BallFlight.Integrate(ref p, ref v, step);
                if (p.y <= Court.BallRadius && v.y < 0) BallFlight.Rebound(ref p, ref v);
                Vector3 movement = Vector3.ClampMagnitude(actor.Velocity * (t * .6f), .65f);
                float error = Mathf.Min((p-forehand-movement).sqrMagnitude,(p-backhand-movement).sqrMagnitude);
                if (t >= .10f && error < best) { best = error; time = t; predicted=p; }
            }
            // Do not schedule a swing against a distant or out-of-reach future ball.
            if(best <= .85f * .85f)return Mathf.Clamp(time, .10f, shot.ContactDelay + .08f);
            predicted=ball+velocity*shot.ContactDelay+Vector3.down*(.5f*BallFlight.Gravity*shot.ContactDelay*shot.ContactDelay);
            return shot.ContactDelay;
        }
    }
}
