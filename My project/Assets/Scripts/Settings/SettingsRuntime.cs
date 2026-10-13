using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PickleVerse.Settings
{
    public sealed class SettingsRuntime : MonoBehaviour
    {
        public static event System.Action<float,float,float> AudioLevelsChanged;
        public static float EffectsVolume {get;private set;}=1;
        static SettingsRuntime instance;
        static Object presentationOwner;
        static float presentationScale=1;
        static int presentationMsaa;
        RenderPipelineAsset originalPipeline;
        UniversalRenderPipelineAsset runtimePipeline;
        int originalFps, originalVsync;
        float originalVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance=null;presentationOwner=null;presentationScale=1;presentationMsaa=0; AudioLevelsChanged=null; GameSettings.Reload(); EffectsVolume=GameSettings.Current.effects; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if(instance)return;
            var go=new GameObject("Settings Runtime");go.AddComponent<SettingsRuntime>();DontDestroyOnLoad(go);
        }
        void Awake()
        {
            if(instance&&instance!=this){Destroy(gameObject);return;}
            instance=this;
            originalPipeline=QualitySettings.renderPipeline;
            originalFps=Application.targetFrameRate;originalVsync=QualitySettings.vSyncCount;originalVolume=AudioListener.volume;
            var source=(originalPipeline?originalPipeline:GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            if(source){runtimePipeline=Instantiate(source);runtimePipeline.name="PickleVerse User Graphics";QualitySettings.renderPipeline=runtimePipeline;}
            Apply(GameSettings.Current);
        }
        public static void Apply(SettingsData data)
        {
            AudioListener.volume=data.master;
            EffectsVolume=data.effects;AudioLevelsChanged?.Invoke(data.master,data.music,data.effects);
            Application.targetFrameRate=data.fps;
            QualitySettings.vSyncCount=data.vsync&&!Application.isMobilePlatform?1:0;
            ApplyGraphics(data);
        }
        static void ApplyGraphics(SettingsData data)
        {
            if(!instance||!instance.runtimePipeline)return;
            var p=instance.runtimePipeline;
            p.renderScale=presentationOwner?Mathf.Min(data.renderScale,presentationScale):data.renderScale;
            p.msaaSampleCount=presentationOwner&&presentationMsaa>0?Mathf.Min(data.msaa,presentationMsaa):data.msaa;
            p.shadowDistance=data.shadows?new[]{12f,25f,45f}[data.graphics]:0;
            p.mainLightShadowmapResolution=new[]{1024,2048,4096}[data.graphics];
            p.additionalLightsShadowmapResolution=new[]{512,1024,2048}[data.graphics];
            if(presentationOwner is MatchRenderBudget)
            {
                p.shadowDistance=Mathf.Min(p.shadowDistance,30);
                p.mainLightShadowmapResolution=Mathf.Min(p.mainLightShadowmapResolution,2048);
                // Four authored 512px Y shadow maps fit the 1024px atlas.
                p.additionalLightsShadowmapResolution=Mathf.Min(p.additionalLightsShadowmapResolution,1024);
            }
        }
        public static void SetPresentationScale(Object owner,float scale,int msaaCeiling=0)
        {
            if(!owner)return;presentationOwner=owner;presentationScale=Mathf.Clamp(scale,.25f,1);presentationMsaa=msaaCeiling;
            ApplyGraphics(GameSettings.Current);
        }
        public static void ReleasePresentationScale(Object owner)
        {
            if(presentationOwner!=owner)return;presentationOwner=null;presentationMsaa=0;
            ApplyGraphics(GameSettings.Current);
        }
        void OnDestroy()
        {
            if(instance!=this)return;
            if(QualitySettings.renderPipeline==runtimePipeline)QualitySettings.renderPipeline=originalPipeline;
            if(runtimePipeline)Destroy(runtimePipeline);
            Application.targetFrameRate=originalFps;QualitySettings.vSyncCount=originalVsync;AudioListener.volume=originalVolume;instance=null;
        }
    }
}
