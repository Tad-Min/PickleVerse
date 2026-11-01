using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace PickleVerse.Settings
{
    public sealed partial class SceneLoading
    {
        CancellationTokenSource preparedCancellation;
        int preparedVersion;
        readonly List<EventSystem> suspendedInput = new List<EventSystem>();
        public static bool IsPreparedTransition => IsBlocking && instance.preparedCancellation != null && instance.preparedVersion == instance.flowVersion;

        /// <summary>Show Loading first; prepare data and load the destination concurrently, then warm and reveal it.</summary>
        public static bool LoadPrepared(string scene, Func<CancellationToken, Action<float, string>, Task> prepare,
            Action<Exception> failed, string fallbackScene, Func<bool> remainsReady = null)
        {
            if (IsBlocking || prepare == null || !Application.CanStreamedLevelBeLoaded(scene)
                || !Application.CanStreamedLevelBeLoaded("Loading") || !Application.CanStreamedLevelBeLoaded(fallbackScene))
                return false;
            var runner = Ensure();
            if (!FrontEndModalInput.TryAcquire(runner)) return false;
            runner.Begin(scene, false);
            runner.originalPriority = Application.backgroundLoadingPriority;
            runner.ownsPriority = true;
            Application.backgroundLoadingPriority = UnityEngine.ThreadPriority.Low;
            runner.preparedCancellation = new CancellationTokenSource();
            runner.preparedVersion = runner.flowVersion;
            _ = runner.Prepared(scene, prepare, failed, fallbackScene, remainsReady, runner.preparedCancellation);
            return true;
        }

        async Task Prepared(string scene, Func<CancellationToken, Action<float, string>, Task> prepare,
            Action<Exception> failed, string fallbackScene, Func<bool> remainsReady, CancellationTokenSource lifetime)
        {
            var token = lifetime.Token;
            int version = flowVersion;
            Task preparation = null;
            double deadline = Time.realtimeSinceStartupAsDouble + 90;
            try
            {
                // Single unloads Login. The Loading view survives as an opaque cover over the destination.
                var loading = SceneManager.LoadSceneAsync("Loading", LoadSceneMode.Single);
                while (!loading.isDone) { token.ThrowIfCancellationRequested(); await Task.Yield(); }
                if (!view) throw new InvalidOperationException("Loading screen is missing.");
                view.Begin(scene);
                Set(0, "Đang chuẩn bị đăng nhập");
                SuspendPreparedInput();
                // Give Loading a rendered frame before starting network work and destination activation.
                await Task.Yield();
                await Task.Yield();

                float preparationProgress = 0;
                string message = "Đang đăng nhập tài khoản";
                preparation = prepare(token, (amount, text) =>
                {
                    if (float.IsNaN(amount) || float.IsInfinity(amount)) return;
                    preparationProgress = Mathf.Max(preparationProgress, Mathf.Clamp01(amount));
                    message = text;
                });
                if (preparation == null) throw new InvalidOperationException("Preparation task is missing.");
                // No activation gate: a failed login can always drain the load and return to Login.
                // Cameras in the additive destination render beneath Loading while authentication continues.
                tracked = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
                bool inputSuspended = false;
                while (!tracked.isDone || !preparation.IsCompleted)
                {
                    token.ThrowIfCancellationRequested();
                    if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("Preparation timed out.");
                    if (preparation.IsFaulted || preparation.IsCanceled) await preparation;
                    if (tracked.isDone && !inputSuspended) { SuspendPreparedInput(); inputSuspended = true; }
                    Set(.03f + .27f * preparationProgress + .5f * Mathf.Clamp01(tracked.progress / .9f),
                        preparation.IsCompleted ? "Đang tải sảnh và nhân vật" : message);
                    await Task.Yield();
                }
                await preparation;
                token.ThrowIfCancellationRequested();
                if (remainsReady != null && !remainsReady()) throw new InvalidOperationException("Preparation became invalid.");
                var target = SceneManager.GetSceneByName(scene);
                if (!target.IsValid() || !target.isLoaded) throw new InvalidOperationException("Destination scene did not load.");
                SceneManager.SetActiveScene(target);
                // Active-scene listeners may instantiate the music widget and account UI.
                SuspendPreparedInput();
                var loadingScene = SceneManager.GetSceneByName("Loading");
                if (loadingScene.IsValid() && loadingScene.isLoaded)
                {
                    var unload = SceneManager.UnloadSceneAsync(loadingScene);
                    while (unload != null && !unload.isDone) { token.ThrowIfCancellationRequested(); await Task.Yield(); }
                }
                Set(.8f, "Đang chuẩn bị hình ảnh sảnh");
                await WarmPrepared(scene, remainsReady, deadline, token);
                if (remainsReady != null && !remainsReady()) throw new InvalidOperationException("Session changed before reveal.");
                flow = StartCoroutine(Finish());
                while (blocking && flowVersion == version)
                {
                    token.ThrowIfCancellationRequested();
                    if (remainsReady != null && !remainsReady()) throw new InvalidOperationException("Session changed before reveal.");
                    await Task.Yield();
                }
            }
            catch (Exception error)
            {
                lifetime.Cancel();
                if (flow != null) { StopCoroutine(flow); flow = null; }
                // Observe a late failure even when Unity is still draining its scene operation.
                if (preparation != null)
                    _ = preparation.ContinueWith(t => { var ignored = t.Exception; },
                        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                if (this && Application.isPlaying)
                {
                    try
                    {
                        failed?.Invoke(error);
                        if (view) view.group.alpha = 1;
                        Set(progress, "Đang trở lại đăng nhập");
                        var back = SceneManager.LoadSceneAsync(fallbackScene, LoadSceneMode.Single);
                        while (back != null && !back.isDone) await Task.Yield();
                    }
                    catch (Exception rollbackError) { Debug.LogException(rollbackError); }
                }
            }
            finally
            {
                if (blocking && this && flowVersion == version) Cleanup();
                lifetime.Cancel();
                lifetime.Dispose();
                if (ReferenceEquals(preparedCancellation, lifetime)) preparedCancellation = null;
            }
        }

        async Task WarmPrepared(string scene, Func<bool> remainsReady, double deadline, CancellationToken token)
        {
            var gate = new SceneWarmup();
            int observedFrame = -1;
            double lastRender = 0;
            void Rendered(Camera camera)
            {
                if (!camera || camera.cameraType != CameraType.Game || camera.gameObject.scene.name != scene || observedFrame == Time.frameCount) return;
                observedFrame = Time.frameCount;
                double now = Time.realtimeSinceStartupAsDouble;
                gate.Observe(lastRender > 0 ? (float)(now - lastRender) : Time.unscaledDeltaTime);
                lastRender = now;
            }
            void PipelineRendered(ScriptableRenderContext context, Camera camera) => Rendered(camera);
            RenderPipelineManager.endCameraRendering += PipelineRendered;
            Camera.onPostRender += Rendered;
            try
            {
                while (!gate.Ready(GameSettings.Current.fps))
                {
                    token.ThrowIfCancellationRequested();
                    if (remainsReady != null && !remainsReady()) throw new InvalidOperationException("Session changed during warm-up.");
                    if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("Render warm-up timed out.");
                    Set(.82f + .14f * gate.Progress, "Đang ổn định hình ảnh sảnh");
                    await Task.Yield();
                }
                LocalRenderReady = true;
                Set(.97f, "Sảnh đã sẵn sàng");
            }
            finally
            {
                RenderPipelineManager.endCameraRendering -= PipelineRendered;
                Camera.onPostRender -= Rendered;
            }
        }

        void SuspendPreparedInput()
        {
            foreach (var system in FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude))
            {
                if (!system.enabled || suspendedInput.Contains(system)) continue;
                suspendedInput.Add(system);
                system.enabled = false;
            }
        }
        void RestorePreparedInput()
        {
            foreach (var system in suspendedInput) if (system) system.enabled = true;
            suspendedInput.Clear();
        }
    }
}
