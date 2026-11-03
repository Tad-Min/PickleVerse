using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PickleVerse.Settings
{
    // Budget the 3D view in pixels rather than treating a high-DPI phone like a desktop GPU.
    public static class MobileRenderPolicy
    {
#if UNITY_EDITOR
        public static bool Preview;
#endif
        public static bool Enabled => Application.isMobilePlatform
#if UNITY_EDITOR
            || Preview
#endif
            ;
        public static float ScaleCeiling(int width, int height)
        {
            return Mathf.Min(1, 1280f / Mathf.Max(1, Mathf.Max(width, height)), 720f / Mathf.Max(1, Mathf.Min(width, height)));
        }
        public static int FrameRate(int requested) => Enabled ? Mathf.Min(60, requested) : requested;
        public static void Configure(UniversalRenderPipelineAsset p)
        {
            p.msaaSampleCount = 1;
            p.supportsHDR = false;
            p.supportsCameraOpaqueTexture = false;
            SetShadowFeatures(p, false, false);
            p.shadowCascadeCount = 1;
            p.shadowDistance = Mathf.Min(p.shadowDistance, 16);
            p.mainLightShadowmapResolution = Mathf.Min(p.mainLightShadowmapResolution, 1024);
            p.additionalLightsShadowmapResolution = 256;
            // Keep the authored white stadium illumination; only extra shadow passes are removed.
            p.maxAdditionalLightsCount = Mathf.Min(p.maxAdditionalLightsCount, 4);
        }
        internal static void SetShadowFeatures(UniversalRenderPipelineAsset p, bool soft, bool additional)
        {
            if (p.supportsSoftShadows == soft && p.supportsAdditionalLightShadows == additional)
                return;
            // Unity 6.6 exposes these flags with internal setters. Only overwrite the runtime copy.
            JsonUtility.FromJsonOverwrite("{\"m_SoftShadowsSupported\":" + (soft ? "true" : "false") + ",\"m_AdditionalLightShadowsSupported\":" + (additional ? "true" : "false") + "}", p);
        }
    }

    public sealed class SettingsRuntime : MonoBehaviour
    {
        public static event System.Action<float, float, float> AudioLevelsChanged;
        public static float EffectsVolume { get; private set; } = 1;
        static SettingsRuntime instance;
        static Object presentationOwner;
        static float presentationScale = 1;
        static int presentationMsaa;
        RenderPipelineAsset originalPipeline;
        UniversalRenderPipelineAsset runtimePipeline;
        int originalFps, originalVsync, originalMaximumLOD;
        float originalVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            presentationOwner = null;
            presentationScale = 1;
            presentationMsaa = 0;
            AudioLevelsChanged = null;
            GameSettings.Reload();
            EffectsVolume = GameSettings.Current.effects;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
#if !UNITY_SERVER
            if (instance)
                return;
            var go = new GameObject("Settings Runtime");
            go.AddComponent<SettingsRuntime>();
            DontDestroyOnLoad(go);
#endif
        }
        void Awake()
        {
            if (instance && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            originalPipeline = QualitySettings.renderPipeline;
            originalFps = Application.targetFrameRate;
            originalVsync = QualitySettings.vSyncCount;
            originalVolume = AudioListener.volume;
            originalMaximumLOD = QualitySettings.maximumLODLevel;
            var source = (originalPipeline ? originalPipeline : GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            if (source)
            {
                runtimePipeline = Instantiate(source);
                runtimePipeline.name = "PickleVerse User Graphics";
                QualitySettings.renderPipeline = runtimePipeline;
            }
            Apply(GameSettings.Current);
        }
        public static void Apply(SettingsData data)
        {
            AudioListener.volume = data.master;
            EffectsVolume = data.effects;
            AudioLevelsChanged?.Invoke(data.master, data.music, data.effects);
            Application.targetFrameRate = MobileRenderPolicy.FrameRate(data.fps);
            QualitySettings.vSyncCount = data.vsync && !Application.isMobilePlatform ? 1 : 0;
            ApplyGraphics(data);
        }
        static void ApplyGraphics(SettingsData data)
        {
            if (!instance || !instance.runtimePipeline)
                return;
            QualitySettings.maximumLODLevel = MobileRenderPolicy.Enabled ? Mathf.Max(1, instance.originalMaximumLOD) : instance.originalMaximumLOD;
            var p = instance.runtimePipeline;
            p.renderScale = presentationOwner ? Mathf.Min(data.renderScale, presentationScale) : data.renderScale;
            p.msaaSampleCount = presentationOwner && presentationMsaa > 0 ? Mathf.Min(data.msaa, presentationMsaa) : data.msaa;
            p.shadowDistance = data.shadows ? new[] { 12f, 25f, 45f }[data.graphics] : 0;
            p.mainLightShadowmapResolution = new[] { 1024, 2048, 4096 }[data.graphics];
            p.additionalLightsShadowmapResolution = new[] { 512, 1024, 2048 }[data.graphics];
            if (presentationOwner is MatchRenderBudget)
            {
                p.shadowDistance = Mathf.Min(p.shadowDistance, 30);
                p.mainLightShadowmapResolution = Mathf.Min(p.mainLightShadowmapResolution, 2048);
                // Four authored 512px Y shadow maps fit the 1024px atlas.
                p.additionalLightsShadowmapResolution = Mathf.Min(p.additionalLightsShadowmapResolution, 1024);
            }
            if (MobileRenderPolicy.Enabled)
            {
                p.renderScale = Mathf.Min(p.renderScale, MobileRenderPolicy.ScaleCeiling(Screen.width, Screen.height));
                MobileRenderPolicy.Configure(p);
            }
            else
            {
                var source = (instance.originalPipeline ? instance.originalPipeline : GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
                if (source)
                {
                    p.supportsHDR = source.supportsHDR;
                    p.supportsCameraOpaqueTexture = source.supportsCameraOpaqueTexture;
                    MobileRenderPolicy.SetShadowFeatures(p, source.supportsSoftShadows, source.supportsAdditionalLightShadows);
                    p.shadowCascadeCount = source.shadowCascadeCount;
                    p.maxAdditionalLightsCount = source.maxAdditionalLightsCount;
                }
            }
        }
        public static void SetPresentationScale(Object owner, float scale, int msaaCeiling = 0)
        {
            if (!owner)
                return;
            presentationOwner = owner;
            presentationScale = Mathf.Clamp(scale, .25f, 1);
            presentationMsaa = msaaCeiling;
            ApplyGraphics(GameSettings.Current);
        }
        public static void ReleasePresentationScale(Object owner)
        {
            if (presentationOwner != owner)
                return;
            presentationOwner = null;
            presentationMsaa = 0;
            ApplyGraphics(GameSettings.Current);
        }
        void OnDestroy()
        {
            if (instance != this)
                return;
            if (QualitySettings.renderPipeline == runtimePipeline)
                QualitySettings.renderPipeline = originalPipeline;
            if (runtimePipeline)
                Destroy(runtimePipeline);
            Application.targetFrameRate = originalFps;
            QualitySettings.vSyncCount = originalVsync;
            AudioListener.volume = originalVolume;
            instance = null;
            QualitySettings.maximumLODLevel = originalMaximumLOD;
        }
    }
}
