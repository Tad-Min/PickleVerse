using UnityEngine;
using UnityEngine.Rendering;

namespace PickleVerse.LocalMatch
{
    // The mode-selection scene selects the sky. Points and games keep this choice.
    [DefaultExecutionOrder(-300)]
    public sealed class MatchEnvironmentLighting : MonoBehaviour
    {
        public Material daySky, nightSky;
        public Camera matchCamera;
        public Light sun;
        public Light[] floodlights;
        public Renderer[] lampFaces;
        public Material lampsOff, lampsOn;
        public StadiumEnvironment stadium;
        public bool selectedNight;
        public bool useLocalLaunchSettings = true;
        [Tooltip("Minimum indoor court floodlight level, independent of the shared day/night sky.")]
        [Range(0, 1)]
        public float minimumCourtIllumination;
        public bool IsNight { get; private set; }
        public static readonly Color NightAmbientSky = new Color(.070f, .078f, .090f);
        public static readonly Color NightAmbientEquator = new Color(.050f, .057f, .065f);
        public static readonly Color NightAmbientGround = new Color(.035f, .040f, .045f);

        void Awake()
        {
            if (stadium && !GetComponent<PickleVerse.Settings.MatchRenderBudget>())
                gameObject.AddComponent<PickleVerse.Settings.MatchRenderBudget>();
            if (useLocalLaunchSettings)
                selectedNight = BotMatchLaunchSettings.Night;
            Apply(selectedNight);
        }

        public void SelectForMatch(bool night)
        {
            selectedNight = night;
            Apply(night);
        }

        public void Apply(bool night)
        {
            IsNight = night;
            RenderSettings.skybox = night ? nightSky : daySky;
            if (matchCamera)
                matchCamera.clearFlags = CameraClearFlags.Skybox;
            if (sun)
                sun.enabled = !night;
            RenderSettings.sun = night ? null : sun;
            if (floodlights != null)
                foreach (var light in floodlights)
                    if (light)
                        light.enabled = night;
            if (lampFaces != null)
                foreach (var face in lampFaces)
                    if (face && lampsOn && lampsOff)
                        face.sharedMaterial = night ? lampsOn : lampsOff;
            if (stadium)
                stadium.ApplyNight(night ? 1 : 0, true, minimumCourtIllumination);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = night ? NightAmbientSky : new Color(.52f, .63f, .76f);
            RenderSettings.ambientEquatorColor = night ? NightAmbientEquator : new Color(.34f, .40f, .48f);
            RenderSettings.ambientGroundColor = night ? NightAmbientGround : new Color(.20f, .23f, .25f);
            RenderSettings.ambientIntensity = 1;
            RenderSettings.reflectionIntensity = night ? .20f : .65f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = night ? new Color(.025f, .040f, .070f) : new Color(.43f, .52f, .66f);
            RenderSettings.fogStartDistance = 38;
            RenderSettings.fogEndDistance = night ? 90 : 140;
            if (Application.isPlaying)
                DynamicGI.UpdateEnvironment();
        }
    }
}
