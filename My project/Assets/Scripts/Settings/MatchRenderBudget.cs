using System;
using UnityEngine;

namespace PickleVerse.Settings
{
    /// <summary>Keep stadium rendering inside the 60 FPS budget without scaling Overlay HUDs or the simulation.</summary>
    [DefaultExecutionOrder(-250)]
    public sealed class MatchRenderBudget : MonoBehaviour
    {
        public float minimumScale = .6f;
        // Zero preserves the match budget. Guided practice can prefer a higher
        // presentation rate without changing the user's saved graphics settings.
        public int preferredFrameRate;
        public static void ConfigurePractice(GameObject host)
        {
            var budget = host.GetComponent<MatchRenderBudget>();
            if (!budget)
                budget = host.AddComponent<MatchRenderBudget>();
            budget.minimumScale = .5f;
            budget.preferredFrameRate = 120;
        }
        public float Scale { get; private set; }
        public float FrameP95 { get; private set; }
        readonly float[] samples = new float[90], sorted = new float[90];
        int count, index, fastChecks;
        double nextCheck;
        Camera renderCamera;
        float DisplayScale()
        {
            if (!renderCamera)
                renderCamera = Camera.main ? Camera.main : FindAnyObjectByType<Camera>();
            int width = renderCamera ? renderCamera.pixelWidth : Screen.width, height = renderCamera ? renderCamera.pixelHeight : Screen.height;
            if (MobileRenderPolicy.Enabled)
                return MobileRenderPolicy.ScaleCeiling(width, height);
            return Mathf.Min(1, Mathf.Sqrt(1920f * 1080 / Mathf.Max(1, (float)width * height)));
        }
        void OnEnable()
        {
            count = index = fastChecks = 0;
            Scale = Mathf.Min((MobileRenderPolicy.Enabled ? 1 : .75f) * DisplayScale(), GameSettings.Current.renderScale);
            nextCheck = Time.realtimeSinceStartupAsDouble + 1;
            SettingsRuntime.SetPresentationScale(this, Scale, 2);
        }
        void Update()
        {
            samples[index] = Time.unscaledDeltaTime * 1000;
            index = (index + 1) % samples.Length;
            count = Mathf.Min(count + 1, samples.Length);
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextCheck || count < 30)
                return;
            nextCheck = now + .75;
            Array.Copy(samples, sorted, count);
            Array.Sort(sorted, 0, count);
            FrameP95 = sorted[Mathf.Min(count - 1, (int)(count * .95f))];
            float display = DisplayScale(), maximum = Mathf.Min(display, GameSettings.Current.renderScale), minimum = Mathf.Min((MobileRenderPolicy.Enabled ? .7f : minimumScale) * display, maximum);
            float next = Mathf.Clamp(Scale, minimum, maximum);
            // A user-selected 30 FPS cap is intentional. Keep headroom below 16.67 ms otherwise.
            int frameRate = MobileRenderPolicy.FrameRate(GameSettings.Current.fps);
            float slow = frameRate <= 60 ? 1000f / frameRate + 1.5f : 14.5f;
            float fast = frameRate == 30 ? 30 : 10.5f;
            if (preferredFrameRate > 0)
            {
                int target = Mathf.Max(30, Mathf.Min(frameRate, preferredFrameRate));
                slow = 1000f / target + 1.2f;
                fast = 1000f / target * .82f;
            }
            if (FrameP95 > slow)
            {
                next = Mathf.Max(minimum, next - .075f);
                fastChecks = 0;
            }
            else if (FrameP95 < fast)
            {
                if (++fastChecks >= 3)
                {
                    next = Mathf.Min(maximum, next + .025f);
                    fastChecks = 0;
                }
            }
            else fastChecks = 0;
            if (Mathf.Abs(next - Scale) < .001f)
                return;
            Scale = next;
            SettingsRuntime.SetPresentationScale(this, Scale, 2);
        }
        void OnDisable()
        {
            SettingsRuntime.ReleasePresentationScale(this);
        }
    }
}
