using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PickleVerse.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace PickleVerse.EditorTools
{
    /// <summary>Explicit GPU-rendered playback, loop, crossfade and lifetime verification.</summary>
    public static class AnimatedArtworkVerification
    {
        public static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/animated-artwork-20261102"));
        static readonly List<string> checks = new List<string>();
        static readonly List<object> phases = new List<object>();
        static readonly List<double> samples = new List<double>(12000);
        static readonly List<string> errors = new List<string>();
        static bool measuring, running;
        static int renderFrame;
        static double previous;
        public static string Stage { get; private set; }
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("FAILED: " + message);
            checks.Add(message);
        }
        static void Rendered(ScriptableRenderContext context, Camera[] cameras)
        {
            if (!measuring || renderFrame == Time.frameCount || !cameras.Any(c => c.cameraType == CameraType.Game)) return;
            renderFrame = Time.frameCount;
            double now = Time.realtimeSinceStartupAsDouble;
            if (previous > 0 && samples.Count < 12000) samples.Add((now - previous) * 1000);
            previous = now;
        }
        static void Log(string text, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert ||
                (type == LogType.Warning && text.Contains("Cinematic artwork"))) errors.Add(text);
        }
        static async Task Wait(Func<bool> condition, int milliseconds = 20000)
        {
            double until = EditorApplication.timeSinceStartup + milliseconds / 1000d;
            while (!condition())
            {
                if (EditorApplication.timeSinceStartup > until) throw new TimeoutException(Stage);
                await Task.Delay(15);
            }
        }
        static object Stats()
        {
            var sorted = samples.OrderBy(n => n).ToArray();
            return new { frames = sorted.Length, p50Ms = sorted[sorted.Length / 2],
                p95Ms = sorted[(int)(sorted.Length * .95)], p99Ms = sorted[(int)(sorted.Length * .99)],
                maxMs = sorted.Last(), over20ms = sorted.Count(n => n > 20), over33ms = sorted.Count(n => n > 33.34) };
        }
        static async Task Measure(string name, Func<bool> extra = null)
        {
            Stage = name; samples.Clear(); previous = 0; renderFrame = -1; measuring = true;
            try { await Wait(() => samples.Count >= 600 && (extra == null || extra())); }
            finally { measuring = false; }
            phases.Add(new { name, stats = Stats() });
            Check(samples.OrderBy(n => n).ElementAt((int)(samples.Count * .95)) < 16.67, name + ": p95 below a 60 FPS budget");
        }
        public static object Begin()
        {
            if (!Application.isPlaying || running) throw new InvalidOperationException("Enter Play; run one probe at a time.");
            Run(); return new { started = true, report = Path.Combine(Output, "playback.json") };
        }
        static async void Run()
        {
            running = true; checks.Clear(); phases.Clear(); errors.Clear();
            Directory.CreateDirectory(Output);
            int fps = Application.targetFrameRate, vsync = QualitySettings.vSyncCount;
            bool background = Application.runInBackground;
            float scale = Time.timeScale;
            LoadingScreenView view = null;
            CinematicArtwork login = Object.FindAnyObjectByType<CinematicArtwork>();
            Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0; Application.runInBackground = true;
            Application.logMessageReceived += Log;
            RenderPipelineManager.endFrameRendering += Rendered;
            try
            {
                Stage = "Login first frame";
                Check(login && login.clip && login.poster && !login.GetComponent<RawImage>().raycastTarget, "Login has movie/poster and does not intercept UI input");
                await Wait(() => login.IsReady);
                int loops = 0;
                VideoPlayer.EventHandler loop = p => loops++;
                login.Player.loopPointReached += loop;
                await Measure("Login animated", () => loops > 0);
                Check(login.Player.frame >= 0 && login.Player.texture == login.GetComponent<RawImage>().texture, "Login displays decoded video and loops");
                login.Player.loopPointReached -= loop;
                ScreenCapture.CaptureScreenshot(Path.Combine(Output, "login-animated.png"));
                await Task.Delay(300);
                login.enabled = false;
                await Task.Delay(400);
                await Measure("Login poster baseline");
                Check(!login.Player.isPlaying && !login.Player.clip, "Disabled Login releases its decoder clip");

                view = Object.Instantiate(Resources.Load<LoadingScreenView>("Loading/LoadingScreen"));
                view.Begin("Menu"); view.SetProgress(.49f, "Đang chuẩn bị hình ảnh");
                view.enabled = false;
                Check(view.movies.Length == 4 && view.movies.All(c => c) && view.pictures.All(p => p), "All four Loading GIF conversions are assigned");
                var motion = view.artwork.GetComponent<CinematicArtwork>();
                Time.timeScale = 0;
                for (int i = 0; i < view.movies.Length; i++)
                {
                    Stage = "Loading clip " + (i + 1);
                    motion.Play(view.movies[i], view.pictures[i]);
                    await Wait(() => motion.IsReady);
                    loops = 0; motion.Player.loopPointReached += loop;
                    await Measure("Loading PKV_" + (i + 1), () => loops > 0);
                    motion.Player.loopPointReached -= loop;
                    Check(motion.Player.isPlaying && !motion.Failed, "PKV_" + (i + 1) + " plays and loops while timeScale is zero");
                }
                Time.timeScale = scale;
                // Reset to a fresh view so the test also exercises production OnEnable/Begin ordering.
                Object.Destroy(view.gameObject); await Task.Delay(150);
                view = Object.Instantiate(Resources.Load<LoadingScreenView>("Loading/LoadingScreen"));
                view.Begin("Menu"); view.SetProgress(.49f, "Đang chuẩn bị hình ảnh");
                await Wait(() => view.artwork.GetComponent<CinematicArtwork>().IsReady);
                ScreenCapture.CaptureScreenshot(Path.Combine(Output, "loading-animated.png")); await Task.Delay(300);
                Stage = "Loading crossfade rotation"; samples.Clear(); previous = 0; measuring = true;
                var seen = new HashSet<VideoClip>();
                double until = EditorApplication.timeSinceStartup + 36;
                while (EditorApplication.timeSinceStartup < until)
                {
                    var current = view.artwork.GetComponent<CinematicArtwork>();
                    if (current.IsReady) seen.Add(current.clip);
                    CheckActivePlayers();
                    await Task.Delay(30);
                }
                measuring = false;
                phases.Add(new { name = "Loading complete rotation with crossfades", stats = Stats() });
                Check(samples.OrderBy(n => n).ElementAt((int)(samples.Count * .95)) < 16.67, "Loading crossfade p95 below a 60 FPS budget");
                Check(seen.Count == 4, "Production crossfade rotates through every new Loading GIF");
                Check(!view.artwork.GetComponent<CinematicArtwork>().Failed && !view.nextArtwork.GetComponent<CinematicArtwork>().Failed, "No decoder failures during crossfades");
                Object.Destroy(view.gameObject); await Task.Delay(300); view = null;
                Check(Object.FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None).All(p => !p.isPlaying && !p.clip), "No active video decoder remains after Loading is destroyed");
                Check(errors.Count == 0, "No Unity exceptions, video errors or fallback warnings");
                File.WriteAllText(Path.Combine(Output, "playback.json"), JsonConvert.SerializeObject(new { success = true, passed = checks.Count,
                    checks, phases, errors, environment = new { resolution = Screen.width + "x" + Screen.height, gpu = SystemInfo.graphicsDeviceName, editor = true } }, Formatting.Indented));
                Stage = "complete";
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(Output, "playback.json"), JsonConvert.SerializeObject(new { success = false, stage = Stage,
                    passed = checks.Count, checks, phases, errors, error = error.ToString() }, Formatting.Indented));
                Stage = "failed";
            }
            finally
            {
                measuring = running = false;
                RenderPipelineManager.endFrameRendering -= Rendered; Application.logMessageReceived -= Log;
                if (view) Object.Destroy(view.gameObject);
                if (login) login.enabled = true;
                Application.targetFrameRate = fps; QualitySettings.vSyncCount = vsync; Application.runInBackground = background; Time.timeScale = scale;
            }
        }
        static void CheckActivePlayers()
        {
            if (Object.FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None).Count(p => p.isPlaying) > 2)
                throw new Exception("More than two active decoders during crossfade.");
        }
    }
}
