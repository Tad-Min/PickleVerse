using UnityEngine;
namespace PickleVerse.Multiplayer
{
    [CreateAssetMenu(menuName = "PickleVerse/Multiplayer Settings")]
    public sealed class MultiplayerSettings : ScriptableObject
    {
        public string environment = "pickleverse-test";
        public string singlesQueue = "PickleVerse-1v1", doublesQueue = "PickleVerse-2v2";
        public int maximumLatencyMs = 150;
        public float reconnectSeconds = 30;
        public GameObject playerPrefab, matchPrefab;
        public GameObject maleActorPrefab, femaleActorPrefab, ballVisualPrefab;
        public GameObject landingMarkerPrefab, aimMarkerPrefab;
        public float contactAssistRadius = .45f;
        public float cameraHeight = 5.3f, cameraDistance = 4.21f, cameraPitch = 31f, cameraFieldOfView = 60f;
        public Material ballMaterial,trailMaterial,teamAMaterial,teamBMaterial;
    }
}
