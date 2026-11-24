using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PickleVerse.ArtDirection
{
    /// <summary>
    /// The Art Bible's scene → Look table (Doc/ArtBible/03-Lighting-Color.md). One asset in Resources/ArtDirection drives
    /// post-processing, the cool shadow tint, the warm rim and wind for every scene, so all scenes share one art direction.
    /// </summary>
    [CreateAssetMenu(menuName = "PickleVerse/Art Direction/Looks")]
    public sealed class ArtDirectionLooks : ScriptableObject
    {
        public const string ResourcePath = "ArtDirection/ArtDirectionLooks";

        [Serializable]
        public sealed class Look
        {
            public string name = "Look";
            [Tooltip("Scene names (SceneManager active scene) that use this look.")]
            public string[] scenes = Array.Empty<string>();
            public VolumeProfile profile;
            [Tooltip("Blended in by the venue night weight (_PV_NightGlow) published by StadiumEnvironment.")]
            public VolumeProfile nightProfile;
            [Tooltip("Enable post-processing on the scene's 3D base cameras that render to the screen.")]
            public bool postProcess = true;
            [Tooltip("Cool colour for light the key does not reach (rgb) and its strength (a).")]
            public Color shadowTint = new Color(.70f, .80f, 1f, .35f);
            [Tooltip("Warm rim multiplier (rgb) and global rim scale (a).")]
            public Color rimTint = new Color(1f, .9f, .78f, 1f);
            [Tooltip("Multiplier on probe/trilight ambient.")]
            public Color ambientBoost = Color.white;
            [Tooltip("xy wind direction (xz plane), z strength scale, w speed scale.")]
            public Vector4 wind = new Vector4(.91f, .41f, 1f, 1f);
            [Range(0, 1)] public float wetness;
            [Tooltip("Ambient particles / light shafts spawned into the scene (Medium and High presets only). Must stay off the court.")]
            public GameObject atmosphere;
        }

        public Look[] looks = Array.Empty<Look>();
        [Tooltip("Shallow depth of field and a slightly stronger vignette, blended in during cinematics only.")]
        public VolumeProfile cinematicProfile;
        [Tooltip("Overrides for the Low graphics preset and mobile: no bloom, vignette or depth of field.")]
        public VolumeProfile lowQualityProfile;
        [Tooltip("Renderers whose Screen Space Ambient Occlusion feature runs only on the High preset in Look scenes.")]
        public UnityEngine.Rendering.Universal.ScriptableRendererData[] ssaoRenderers = Array.Empty<UnityEngine.Rendering.Universal.ScriptableRendererData>();

        public Look Find(string scene)
        {
            if (string.IsNullOrEmpty(scene))
                return null;
            foreach (var look in looks)
                if (look != null && look.scenes != null)
                    foreach (var s in look.scenes)
                        if (string.Equals(s, scene, StringComparison.Ordinal))
                            return look;
            return null;
        }
    }
}
