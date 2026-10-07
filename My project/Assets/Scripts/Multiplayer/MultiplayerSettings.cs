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
        public GameObject maleModel, femaleModel, paddlePrefab;
        public RuntimeAnimatorController maleAnimator, femaleAnimator;
        public ShotAnimationLibrary animations;
        public Material ballMaterial,trailMaterial,teamAMaterial,teamBMaterial;
    }
}
