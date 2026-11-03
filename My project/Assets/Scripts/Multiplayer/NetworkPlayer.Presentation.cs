using UnityEngine;

namespace PickleVerse.Multiplayer
{
    public sealed partial class NetworkPlayer
    {
        Vector3 visualCorrection;
        public Vector3 PresentedPosition => Actor ? Actor.transform.position : transform.position;
        void LateUpdate()
        {
            if (!IsSpawned || !Actor || IsServer) return;
            var match = NetworkMatch.Instance;
            if (MultiplayerCourtUI.Instance && MultiplayerCourtUI.Instance.cinematic && MultiplayerCourtUI.Instance.cinematic.OwnsPose) return;
            if (IsOwner)
            {
                visualCorrection *= Mathf.Exp(-Time.unscaledDeltaTime / .1f);
                if (visualCorrection.sqrMagnitude < .000001f) visualCorrection = Vector3.zero;
                Actor.transform.localPosition = transform.InverseTransformVector(visualCorrection);
            }
            else if (TryGetSnapshotsBuffers(out var fromBuffer, out var toBuffer, out float alpha))
            {
                var (from, to) = GetPropertyReader<PlayerFrame>(nameof(Frame)).Read(fromBuffer, toBuffer);
                var presented = from.motionEpoch == to.motionEpoch ? Vector3.Lerp(from.position, to.position, alpha) : to.position;
                Actor.transform.localPosition = transform.InverseTransformVector(presented - transform.position);
            }
            else Actor.transform.localPosition = Vector3.zero;
        }
    }
}
