using UnityEngine;

namespace PickleVerse.LocalMatch
{
    /// <summary>Shared movement for BotMatch, authoritative multiplayer and owner prediction.</summary>
    public static class MatchMovement
    {
        // Keep the player's body/paddle clear of the net in guided practice.
        // Match modes retain their existing court limits and referee net faults.
        public static Rect PracticeBounds(int end) => end < 0
            ? Rect.MinMaxRect(-3.1f, -8.1f, 3.1f, -.85f)
            : Rect.MinMaxRect(-3.1f, .85f, 3.1f, 8.1f);
        public static void Step(ref Vector3 position, ref Vector3 velocity, Vector2 movement, bool run,
            bool swinging, int end, float dt, float walkSpeed = 3.3f, float runSpeed = 5.8f, Rect? bounds = null)
        {
            var desired = new Vector3(movement.x, 0, movement.y) * (run ? runSpeed : walkSpeed) * (swinging ? .6f : 1f);
            velocity = Vector3.MoveTowards(velocity, desired, 24f * dt);
            var before = position;
            position += velocity * dt;
            position.x = Mathf.Clamp(position.x, -4.65f, 4.65f);
            position.z = end * Mathf.Clamp(position.z * end, .30f, 9.1f);
            if (bounds.HasValue)
            {
                var area = bounds.Value;
                position.x = Mathf.Clamp(position.x, area.xMin, area.xMax);
                position.z = Mathf.Clamp(position.z, area.yMin, area.yMax);
            }
            velocity = (position - before) / Mathf.Max(.0001f, dt);
        }
    }
}
