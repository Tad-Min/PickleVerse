using UnityEngine;

namespace PickleVerse.LocalMatch
{
    /// <summary>The authored BotMatch hand release, shared by local and host simulation.</summary>
    public static class ServeBallMotion
    {
        public static void Step(MatchActor actor, ref bool released, ref Vector3 origin,
            ref Vector3 position, ref Vector3 velocity, float dt=1f/120f)
        {
            if(released&&!actor.Swinging)
            { BallFlight.Integrate(ref position,ref velocity,dt);return; }
            if (!actor.Swinging || actor.Intent.kind != ShotKind.Serve)
            { position = actor.HeldBallPosition; velocity = Vector3.zero; return; }
            if (actor.SwingTime >= .5f && !released)
            { released = true; origin = actor.HeldBallPosition; }
            if (!released)
            { position = actor.HeldBallPosition; velocity = Vector3.zero; return; }
            float time = actor.SwingTime - .5f;
            position = origin + Vector3.down * (.5f * BallFlight.Gravity * time * time);
            velocity = Vector3.down * (BallFlight.Gravity * time);
        }
    }
}
