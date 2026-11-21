using UnityEngine;
namespace PickleVerse.LocalMatch
{
    public sealed class MatchCamera : MonoBehaviour
    {
        public MatchActor player;
        public float height => CourtFollowView.Height;
        public float followDistance => CourtFollowView.Distance;
        public float pitch => DesiredRotation.eulerAngles.x;
        public float fieldOfView
        {
            get
            {
                CourtFollowView.Frame(DesiredPosition, player.transform.position, player.end, false, CourtFollowView.FeetLine, out _, out var fov);
                return fov;
            }
        }
        public Vector3 DesiredPosition => CourtFollowView.Position(player.transform.position, player.end);
        public Quaternion DesiredRotation
        {
            get
            {
                CourtFollowView.Frame(DesiredPosition, player.transform.position, player.end, false, CourtFollowView.FeetLine, out var rotation, out _);
                return rotation;
            }
        }
        void OnEnable()
        {
            if (player)
                SnapToPlayer();
        }
        public void SnapToPlayer()
        {
            if (!player)
                return;
            CourtFollowView.Apply(GetComponent<Camera>(), player.transform.position, player.end, false, 0, true);
        }
        void LateUpdate()
        {
            if (!player)
                return;
            CourtFollowView.Apply(GetComponent<Camera>(), player.transform.position, player.end, false, Time.unscaledDeltaTime);
        }
    }
}
