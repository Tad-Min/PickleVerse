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

        // Day matches are played at golden hour: a warm sun 21 degrees above the horizon, behind-left of the
        // near-end camera, so faces are lit and long shadows rake across the court. The district's baked
        // ground shadows (StadiumEnvironment.sunShadowMask) were rendered for exactly this direction.
        public static readonly Quaternion GoldenHourSun = Quaternion.Euler(21f, 62f, 0f);
        public static readonly Color GoldenHourSunColor = new Color(1f, .74f, .50f);
        public const float GoldenHourSunIntensity = 1.75f;
        public static readonly Color GoldenAmbientSky = new Color(.40f, .47f, .62f);
        public static readonly Color GoldenAmbientEquator = new Color(.58f, .47f, .40f);
        public static readonly Color GoldenAmbientGround = new Color(.22f, .19f, .16f);
        public static readonly Color GoldenHaze = new Color(.80f, .62f, .50f);
        const float DistrictFarClip = 460f;
        static readonly int SunMaskOnId = Shader.PropertyToID("_PV_SunMaskOn");
        static Material goldenHourSky;
        /// <summary>Procedural golden-hour sky (Resources/Lighting). Falls back to the authored day sky.</summary>
        public static Material GoldenHourSky => goldenHourSky ? goldenHourSky : goldenHourSky = Resources.Load<Material>("Lighting/GoldenHourSky");

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
            var golden = GoldenHourSky;
            RenderSettings.skybox = night ? nightSky : golden ? golden : daySky;
            if (matchCamera)
            {
                matchCamera.clearFlags = CameraClearFlags.Skybox;
                // The surrounding district and skyline reach ~210 m; fog hides everything beyond.
                matchCamera.farClipPlane = Mathf.Max(matchCamera.farClipPlane, DistrictFarClip);
            }
            if (sun)
            {
                sun.enabled = !night;
                if (!night)
                {
                    sun.transform.rotation = GoldenHourSun;
                    sun.useColorTemperature = false;
                    sun.color = GoldenHourSunColor;
                    sun.intensity = GoldenHourSunIntensity;
                    sun.shadowStrength = .82f;
                }
            }
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
            RenderSettings.ambientSkyColor = night ? NightAmbientSky : GoldenAmbientSky;
            RenderSettings.ambientEquatorColor = night ? NightAmbientEquator : GoldenAmbientEquator;
            RenderSettings.ambientGroundColor = night ? NightAmbientGround : GoldenAmbientGround;
            RenderSettings.ambientIntensity = 1;
            RenderSettings.reflectionIntensity = night ? .20f : .55f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = night ? new Color(.025f, .040f, .070f) : GoldenHaze;
            RenderSettings.fogStartDistance = night ? 38 : 55;
            // A longer night haze keeps the lit city silhouette visible behind the court.
            RenderSettings.fogEndDistance = night ? 210 : 330;
            // Lamp/window glow, lamp pools and sky reflections were published by stadium.ApplyNight above
            // (shared with the Menu's day/night cycle). The baked golden-hour ground shadows are valid only
            // under this scene's sun, so only the match lighting switches them on.
            if (!stadium)
                StadiumEnvironment.PublishEnvironmentGlobals(null, night ? 1 : 0);
            Shader.SetGlobalFloat(SunMaskOnId, !night && stadium && stadium.sunShadowMask ? 1 : 0);
            if (Application.isPlaying)
                DynamicGI.UpdateEnvironment();
        }
    }
}
