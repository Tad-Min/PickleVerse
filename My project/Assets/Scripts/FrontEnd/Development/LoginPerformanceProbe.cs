#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using PickleVerse.Settings;

namespace PickleVerse.FrontEnd
{
    /// <summary>Opt-in development-player benchmark. Inert without --login-benchmark and excluded from release builds.</summary>
    public sealed class LoginPerformanceProbe : MonoBehaviour
    {
        [Serializable] public sealed class Sample
        {
            public int frames, gpuFrames;
            public double frameMsP50, frameMsP95, mainThreadMsP50, mainThreadMsP95, gpuMsP50, gpuMsP95;
            public double synchronizedRenderMsP50, synchronizedRenderMsP95;
            public long gcBytesP50, gcBytesP95, drawsP50;
        }
        [Serializable] public sealed class Report
        {
            public string environment, gpu, resolution;
            public bool frameTimingEnabled, success;
            public Sample login, uiHiddenBaseline, loading;
            public bool loginAnimationReady, loginAnimationAdvanced, loadingAnimationReady, loadingAnimationAdvanced;
            public int loadingClips;
        }
        const int Frames=600;
        readonly double[] frame=new double[Frames], cpu=new double[Frames], gpu=new double[Frames];
        readonly double[] render=new double[Frames];
        readonly long[] gc=new long[Frames], draws=new long[Frames];
        readonly FrameTiming[] timing=new FrameTiming[1];
        ProfilerRecorder mainRecorder,gcRecorder,drawRecorder;
        string destination;
        GameObject ui;
        double warmUntil;
        int phase,index,gpuCount;
        Report report;
        Camera renderCamera;
        RenderTexture target;
        bool screenshotSaved;
        CinematicArtwork playback;
        LoadingScreenView loading;
        long firstVideoFrame = -1;
        bool videoAdvanced;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(Application.isEditor) return;
            var args=Environment.GetCommandLineArgs(); int at=Array.IndexOf(args,"--login-benchmark");
            if(at<0||at+1>=args.Length) return;
            var probe=new GameObject("Login Performance Probe").AddComponent<LoginPerformanceProbe>();
            probe.destination=args[at+1]; probe.Begin(); DontDestroyOnLoad(probe.gameObject);
        }
        void Begin()
        {
            Application.runInBackground=true; Application.targetFrameRate=-1; QualitySettings.vSyncCount=0;
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            ui=FindAnyObjectByType<AccountPanelView>().GetComponentInParent<Canvas>().rootCanvas.gameObject;
            playback=ui.GetComponentInChildren<CinematicArtwork>();
            mainRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread");
            gcRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame");
            drawRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count");
            // Hidden windows can skip presentation. Render explicitly to a 1080p target and
            // synchronize one pixel with GPU readback; this is a conservative render cost,
            // not an onscreen/presentation FPS claim.
            target=new RenderTexture(1920,1080,24); target.Create();
            renderCamera=FindAnyObjectByType<Camera>(); renderCamera.enabled=false;
            renderCamera.targetTexture=target; renderCamera.cullingMask=1<<5; renderCamera.orthographic=true;
            renderCamera.orthographicSize=400; renderCamera.transform.position=new Vector3(0,0,-10);
            var canvas=ui.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=renderCamera; canvas.planeDistance=1;
            report=new Report{environment="Windows development player, uncapped, synchronized offscreen GPU render at 1080p, 2 seconds warm-up per phase, 600 frames per phase (Login, UI-hidden baseline, Loading); includes 1-pixel GPU readback, excludes window presentation",gpu=SystemInfo.graphicsDeviceName+" / "+SystemInfo.graphicsDeviceType,resolution=target.width+"x"+target.height,frameTimingEnabled=FrameTimingManager.IsFeatureEnabled()};
            warmUntil=Time.realtimeSinceStartupAsDouble+2;
        }
        void Update()
        {
            Canvas.ForceUpdateCanvases();
            long started=System.Diagnostics.Stopwatch.GetTimestamp();
            renderCamera.Render();
            var readback=AsyncGPUReadback.Request(target,0,0,1,0,1,0,1,TextureFormat.RGBA32);
            readback.WaitForCompletion();
            double renderMs=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000d/System.Diagnostics.Stopwatch.Frequency;
            if(readback.hasError) throw new InvalidOperationException("Benchmark GPU readback failed.");
            if(phase==2) playback=loading.artwork.GetComponent<CinematicArtwork>();
            if(!screenshotSaved && phase!=1 && playback && playback.IsReady)
            {
                screenshotSaved=true;
                var prior=RenderTexture.active; RenderTexture.active=target;
                var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply(); RenderTexture.active=prior;
                File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(destination),phase==0?"PlayerLogin-1920x1080.png":"PlayerLoading-1920x1080.png"),image.EncodeToPNG()); Destroy(image);
                warmUntil=Time.realtimeSinceStartupAsDouble+2;
            }
            FrameTimingManager.CaptureFrameTimings();
            if(Time.realtimeSinceStartupAsDouble<warmUntil) return;
            if(playback && playback.IsReady)
            {
                if(firstVideoFrame<0) firstVideoFrame=playback.Player.frame;
                else if(playback.Player.frame!=firstVideoFrame) videoAdvanced=true;
            }
            frame[index]=Time.unscaledDeltaTime*1000d;
            cpu[index]=mainRecorder.LastValue/1000000d; gc[index]=gcRecorder.LastValue; draws[index]=drawRecorder.LastValue;
            render[index]=renderMs;
            if(FrameTimingManager.GetLatestTimings(1,timing)>0&&timing[0].gpuFrameTime>0) gpu[gpuCount++]=timing[0].gpuFrameTime;
            if(++index<Frames) return;
            Array.Sort(frame); Array.Sort(cpu); Array.Sort(gc); Array.Sort(draws); Array.Sort(gpu,0,gpuCount); Array.Sort(render);
            var sample=new Sample{frames=index,gpuFrames=gpuCount,frameMsP50=frame[index/2],frameMsP95=frame[(int)(index*.95)],mainThreadMsP50=cpu[index/2],mainThreadMsP95=cpu[(int)(index*.95)],gcBytesP50=gc[index/2],gcBytesP95=gc[(int)(index*.95)],drawsP50=draws[index/2],gpuMsP50=gpuCount>0?gpu[gpuCount/2]:0,gpuMsP95=gpuCount>0?gpu[(int)(gpuCount*.95)]:0};
            sample.synchronizedRenderMsP50=render[index/2]; sample.synchronizedRenderMsP95=render[(int)(index*.95)];
            if(phase==0)
            {
                report.login=sample; report.loginAnimationReady=playback && playback.IsReady;
                report.loginAnimationAdvanced=videoAdvanced;
                ui.SetActive(false); NextPhase(); return;
            }
            if(phase==1)
            {
                report.uiHiddenBaseline=sample;
                loading=Instantiate(Resources.Load<LoadingScreenView>("Loading/LoadingScreen"));
                loading.Begin("Menu"); loading.SetProgress(.5f,"Đang chuẩn bị hình ảnh");
                foreach(var movie in loading.movies) if(movie)report.loadingClips++;
                var canvas=loading.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceCamera;
                canvas.worldCamera=renderCamera; canvas.planeDistance=1; renderCamera.cullingMask=~0;
                NextPhase(); return;
            }
            report.loading=sample; report.loadingAnimationReady=playback && playback.IsReady;
            report.loadingAnimationAdvanced=videoAdvanced;
            report.success=report.loginAnimationReady&&report.loginAnimationAdvanced&&report.loadingAnimationReady&&report.loadingAnimationAdvanced&&report.loadingClips==4;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.WriteAllText(destination,JsonUtility.ToJson(report,true));
            enabled=false; mainRecorder.Dispose(); gcRecorder.Dispose(); drawRecorder.Dispose(); Application.Quit();
        }
        void NextPhase()
        {
            phase++; index=gpuCount=0; warmUntil=Time.realtimeSinceStartupAsDouble+2;
            firstVideoFrame=-1; videoAdvanced=screenshotSaved=false;
        }
    }
}
#endif
