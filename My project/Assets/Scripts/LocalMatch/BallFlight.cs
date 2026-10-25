using UnityEngine;
using PickleVerse.Rules;

namespace PickleVerse.LocalMatch
{
    public static class BallFlight
    {
        public const float Gravity = 9.81f, Restitution = .73f;
        public static Vector3 Launch(Vector3 from, Vector3 target, float apex)
        {
            apex = Mathf.Max(apex, from.y + .08f);
            float up = Mathf.Sqrt(2f * Gravity * (apex - from.y));
            float duration = up / Gravity + Mathf.Sqrt(2f * (apex - target.y) / Gravity);
            return new Vector3((target.x - from.x) / duration, up, (target.z - from.z) / duration);
        }
        public static void Integrate(ref Vector3 position, ref Vector3 velocity, float dt)
        {
            position += velocity * dt + Vector3.down * (.5f * Gravity * dt * dt);
            velocity.y -= Gravity * dt;
        }
        public static void Rebound(ref Vector3 p, ref Vector3 v)
        {
            p.y = Court.BallRadius;
            v.y = Mathf.Abs(v.y) * Restitution;
            v.x *= .84f;
            v.z *= .84f;
        }
    }
}
