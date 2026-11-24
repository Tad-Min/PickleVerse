using UnityEngine;

namespace PickleVerse.ArtDirection
{
    /// <summary>
    /// Scales an ambient particle system with the venue's day/night weight (_PV_NightGlow, published by
    /// StadiumEnvironment): sunlit motes and falling leaves fade out at night, floodlight insects fade in.
    /// Presentation only; checks twice a second.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class AtmosphereNightResponse : MonoBehaviour
    {
        static readonly int NightGlowId = Shader.PropertyToID("_PV_NightGlow");
        [Tooltip("Emission multiplier by day.")] public float day = 1;
        [Tooltip("Emission multiplier at night.")] public float night;
        ParticleSystem system;
        float baseRate, nextCheck, applied = -1;

        /// <summary>Emission multiplier for a night weight (pure, unit-tested).</summary>
        public static float Multiplier(float dayValue, float nightValue, float nightWeight) => Mathf.Lerp(dayValue, nightValue, Mathf.Clamp01(nightWeight));

        void Awake()
        {
            system = GetComponent<ParticleSystem>();
            baseRate = system.emission.rateOverTimeMultiplier;
        }

        void Update()
        {
            if (Time.unscaledTime < nextCheck)
                return;
            nextCheck = Time.unscaledTime + .5f;
            float k = Multiplier(day, night, Shader.GetGlobalFloat(NightGlowId));
            if (Mathf.Abs(k - applied) < .02f)
                return;
            applied = k;
            var emission = system.emission;
            emission.rateOverTimeMultiplier = baseRate * k;
            if (k <= .001f && system.particleCount > 0 && !system.isStopped)
                system.Clear();
        }
    }
}
