using UnityEngine;
using PickleVerse.LocalMatch;
namespace PickleVerse.Multiplayer
{
    public sealed class CourtCamera : MonoBehaviour
    {
        public Transform target;
        int previousEnd;
        public void SnapToPlayer()
        {
            if (!target || !MultiplayerApp.Instance)
                return;
            var player = target.GetComponent<NetworkPlayer>();
            if (player && !player.IsSpawned)
                return;
            int end = target.GetComponent<NetworkPlayer>()?.CourtEnd ?? (target.forward.z > 0 ? -1 : 1);
            CourtFollowView.Apply(GetComponent<Camera>(), player ? player.PresentedPosition : target.position, end, Doubles, 0, true);
            previousEnd = end;
        }
        void LateUpdate()
        {
            if (target == null || !MultiplayerApp.Instance)
                return;
            var player = target.GetComponent<NetworkPlayer>();
            if (player && !player.IsSpawned)
                return;
            int end = target.GetComponent<NetworkPlayer>()?.CourtEnd ?? (target.forward.z > 0 ? -1 : 1);
            if (previousEnd != end)
                SnapToPlayer();
            CourtFollowView.Apply(GetComponent<Camera>(), player ? player.PresentedPosition : target.position, end, Doubles, Time.unscaledDeltaTime);
        }
        bool Doubles => NetworkMatch.Instance?.Roster?.mode == PlayMode.Doubles;
    }
}
