using UnityEngine;

namespace PickleVerse.LocalMatch
{
    /// <summary>The Training gameplay view, shared by all court modes and mirrored after changing ends.</summary>
    public static class CourtFollowView
    {
        public const float Height = 5.3f, Distance = 4.25f, FieldOfView = 56f, Sharpness = 3f;
        public static Vector3 Position(Vector3 player, int end, bool doubles = false)
            => new Vector3(doubles ? 0 : player.x, doubles ? 6.5f : Height, player.z + end * Distance);
        public static Vector3 LookAt(Vector3 player, int end, bool doubles = false)
            => new Vector3(doubles ? 0 : player.x, .6f, end * .5f);
        public static float Fov(bool doubles) => doubles ? 64f : FieldOfView;
        public static void Apply(Camera view, Vector3 player, int end, bool doubles, float dt, bool snap = false)
        {
            float weight = snap ? 1 : 1 - Mathf.Exp(-Mathf.Max(0, dt) * Sharpness);
            view.transform.position = Vector3.Lerp(view.transform.position, Position(player, end, doubles), weight);
            var rotation = Quaternion.LookRotation(LookAt(player, end, doubles) - view.transform.position);
            view.transform.rotation = Quaternion.Slerp(view.transform.rotation, rotation, weight);
            view.fieldOfView = Mathf.Lerp(view.fieldOfView, Fov(doubles), weight);
        }
    }
}
