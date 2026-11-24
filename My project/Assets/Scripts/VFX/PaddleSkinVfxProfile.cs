using UnityEngine;

namespace PickleVerse.VFX
{
    /// <summary>
    /// The signature ball effect of one paddle skin (Assets/VFX/PaddleSkins/&lt;Skin&gt;). Shown only after a contact by a
    /// paddle wearing that skin; any other contact returns the ball to the standard trail.
    /// </summary>
    [CreateAssetMenu(menuName = "PickleVerse/VFX/Paddle skin VFX")]
    public sealed class PaddleSkinVfxProfile : ScriptableObject
    {
        [Tooltip("Shop catalogue id of the skin, e.g. paddle.gemstone.")]
        public string skinId = "paddle.gemstone";
        public string displayName = "Royal Court Sparkle";

        [Header("Trail ribbon")]
        public Material trailMaterial;
        [Min(.05f)] public float trailTime = .4f;
        [Min(0)] public float trailWidth = .085f;
        public AnimationCurve trailWidthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(.25f, .7f), new Keyframe(1, 0));
        public Gradient trailColor = new Gradient();

        [Header("Ball")]
        [Tooltip("Follows the ball while this skin owns the shot (sparkles, glitter shell). Particle systems should use world simulation.")]
        public GameObject ballEffect;

        [Header("Contact")]
        [Tooltip("Played once at the contact point (pooled).")]
        public GameObject impactEffect;
    }
}
