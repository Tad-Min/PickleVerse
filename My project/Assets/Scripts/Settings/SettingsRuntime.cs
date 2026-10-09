using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PickleVerse.Settings
{
    public sealed class SettingsRuntime : MonoBehaviour
    {
        static SettingsRuntime instance;
        RenderPipelineAsset originalPipeline;
        UniversalRenderPipelineAsset runtimePipeline;
        int originalFps, originalVsync;
        float originalVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance=null; GameSettings.Reload(); }
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
            Application.targetFrameRate=data.fps;
            QualitySettings.vSyncCount=data.vsync&&!Application.isMobilePlatform?1:0;
            if(!instance||!instance.runtimePipeline)return;
            var p=instance.runtimePipeline;
            p.renderScale=data.renderScale;p.msaaSampleCount=data.msaa;p.shadowDistance=data.shadows?new[]{12f,25f,45f}[data.graphics]:0;
            p.mainLightShadowmapResolution=new[]{1024,2048,4096}[data.graphics];
            p.additionalLightsShadowmapResolution=new[]{512,1024,2048}[data.graphics];
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
