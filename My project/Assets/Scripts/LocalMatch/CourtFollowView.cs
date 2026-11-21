using UnityEngine;

namespace PickleVerse.LocalMatch
{
    /// <summary>The Training gameplay view, shared by all court modes and mirrored after changing ends.</summary>
    /// <remarks>
    /// Framing follows the broadcast-style camera used by tennis games: elevated behind the near baseline, the
    /// local player in the lower part of the frame and the far baseline in view. The player's feet never drop
    /// below <see cref="FeetLine"/>, which keeps them clear of the bottom HUD band (shot cue on PC, touch pad on
    /// Android). When that tilt would push the far baseline out of view, the view widens slightly instead.
    /// </remarks>
    public static class CourtFollowView
    {
        public const float Height = 5.3f, Distance = 4.25f, FieldOfView = 56f, Sharpness = 3f;
        /// <summary>Lowest viewport height for the player's feet: above the shot cue on PC, above the thumbs on touch screens.</summary>
        public const float DesktopFeetLine = .25f, TouchFeetLine = .32f;
        const float CourtHalfLength = 6.71f, OpponentHeadHeight = 1.8f, FarHeadroom = .92f, MaxExtraFov = 8f;
        public static float FeetLine => Application.isMobilePlatform ? TouchFeetLine : DesktopFeetLine;
        public static Vector3 Position(Vector3 player, int end, bool doubles = false)
            => new Vector3(doubles ? 0 : player.x, doubles ? 6.5f : Height, player.z + end * Distance);
        public static Vector3 LookAt(Vector3 player, int end, bool doubles = false)
            => new Vector3(doubles ? 0 : player.x, .6f, end * .5f);
        public static float Fov(bool doubles) => doubles ? 64f : FieldOfView;

        /// <summary>Rotation and vertical field of view that frame <paramref name="player"/> from <paramref name="camera"/>.</summary>
        public static void Frame(Vector3 camera, Vector3 player, int end, bool doubles, float feetLine, out Quaternion rotation, out float fov)
        {
            var look = Quaternion.LookRotation(LookAt(player, end, doubles) - camera);
            var farHead = new Vector3(doubles ? 0 : player.x, OpponentHeadHeight, -end * CourtHalfLength);
            fov = Fov(doubles);
            rotation = KeepFeetAbove(look, camera, player, fov, feetLine);
            for (float extra = 1; extra <= MaxExtraFov && ViewportY(rotation, camera, farHead, fov) > FarHeadroom; extra++)
            {
                fov = Fov(doubles) + extra;
                rotation = KeepFeetAbove(look, camera, player, fov, feetLine);
            }
        }

        /// <summary>Viewport height (0 bottom, 1 top) of a world point for a camera at <paramref name="camera"/>.</summary>
        public static float ViewportY(Quaternion rotation, Vector3 camera, Vector3 point, float fov)
        {
            var local = Quaternion.Inverse(rotation) * (point - camera);
            if (local.z <= .01f)
                return local.y >= 0 ? 2 : -1;
            return .5f + .5f * local.y / (local.z * Mathf.Tan(fov * .5f * Mathf.Deg2Rad));
        }

        /// <summary>Tilts the view down just enough for the feet to sit on or above <paramref name="feetLine"/>.</summary>
        public static Quaternion KeepFeetAbove(Quaternion rotation, Vector3 camera, Vector3 feet, float fov, float feetLine)
        {
            var local = Quaternion.Inverse(rotation) * (feet - camera);
            if (local.z <= .01f)
                return rotation;
            float below = Mathf.Atan2(-local.y, local.z);
            float allowed = Mathf.Atan((1 - 2 * feetLine) * Mathf.Tan(fov * .5f * Mathf.Deg2Rad));
            return below > allowed ? rotation * Quaternion.Euler((below - allowed) * Mathf.Rad2Deg, 0, 0) : rotation;
        }

        public static void Apply(Camera view, Vector3 player, int end, bool doubles, float dt, bool snap = false)
        {
            float weight = snap ? 1 : 1 - Mathf.Exp(-Mathf.Max(0, dt) * Sharpness);
            view.transform.position = Vector3.Lerp(view.transform.position, Position(player, end, doubles), weight);
            Frame(view.transform.position, player, end, doubles, FeetLine, out var rotation, out var fov);
            view.fieldOfView = Mathf.Lerp(view.fieldOfView, fov, weight);
            var smoothed = Quaternion.Slerp(view.transform.rotation, rotation, weight);
            // Smoothing may lag behind a fast retreat; the feet limit still holds every frame.
            view.transform.rotation = KeepFeetAbove(smoothed, view.transform.position, player, view.fieldOfView, FeetLine);
        }
    }
}
