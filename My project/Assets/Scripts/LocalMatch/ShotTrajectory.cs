using UnityEngine;
using PickleVerse.Rules;

namespace PickleVerse.LocalMatch
{
    /// <summary>Shared shot targeting and net clearance for BotMatch and online matches.</summary>
    public static class ShotTrajectory
    {
        public static Vector3 Target(MatchActor actor, ShotIntent shot, Vector3 ball, float serverX)
        {
            bool soft = shot.kind == ShotKind.Dink || shot.kind == ShotKind.Drop || shot.kind == ShotKind.Reset;
            float x = Mathf.Clamp(ball.x, -2.25f, 2.25f);
            if (Mathf.Abs(x) < .25f)
                x = .3f;
            if (shot.cross)
                x = -Mathf.Sign(x) * 1.9f;
            float depth = soft ? 1.45f : shot.kind == ShotKind.Smash ? 4.1f : 5.05f;
            if (shot.kind == ShotKind.Serve)
            {
                x = -Mathf.Sign(serverX) * (shot.cross ? 2.05f : 1.35f);
                depth = shot.power ? 5.8f : 4.75f;
            }
            return new Vector3(x, Court.BallRadius, -actor.end * depth);
        }
        public static Vector3 Launch(ShotIntent shot, Vector3 ball, Vector3 target)
        {
            bool soft = shot.kind == ShotKind.Dink || shot.kind == ShotKind.Drop || shot.kind == ShotKind.Reset;
            float apex = soft ? Mathf.Max(1.5f, ball.y + .18f) : shot.kind == ShotKind.Smash ? ball.y + .10f :
                shot.power ? Mathf.Max(1.65f, ball.y + .13f) : Mathf.Max(2.25f, ball.y + .3f);
            var velocity = BallFlight.Launch(ball, target, apex);
            float margin = shot.power ? .045f : soft ? .10f : .14f;
            for (int attempt = 0; attempt < 28; attempt++)
            {
                float time = -ball.z / velocity.z, x = ball.x + velocity.x * time;
                float y = ball.y + velocity.y * time - .5f * BallFlight.Gravity * time * time;
                if (time <= 0 || Mathf.Abs(x) > Court.NetHalfSpan || y >= Court.NetHeight(x) + Court.BallRadius + margin)
                    break;
                apex += .15f;
                velocity = BallFlight.Launch(ball, target, apex);
            }
            return velocity;
        }
    }
}
