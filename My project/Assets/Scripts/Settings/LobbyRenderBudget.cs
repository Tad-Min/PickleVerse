using System;
using UnityEngine;

namespace PickleVerse.Settings
{
    /// <summary>Scale only the lobby's 3D view to its frame budget; Overlay UI stays at display resolution.</summary>
    public sealed class LobbyRenderBudget : MonoBehaviour
    {
        public float minimumScale = .65f;
        public float Scale { get; private set; }
        readonly float[] samples = new float[120], sorted = new float[120];
        int count, index;
        double nextCheck;
        void OnEnable()
        {
            float display = MobileRenderPolicy.Enabled ? MobileRenderPolicy.ScaleCeiling(Screen.width, Screen.height) : 1;
            count = index = 0;
            Scale = Mathf.Min(MobileRenderPolicy.Enabled ? display : .75f, GameSettings.Current.renderScale);
            nextCheck = Time.realtimeSinceStartupAsDouble + 1;
            SettingsRuntime.SetPresentationScale(this, Scale);
        }
        void Update()
        {
            samples[index] = Time.unscaledDeltaTime * 1000;
            index = (index + 1) % samples.Length;
            count = Mathf.Min(count + 1, samples.Length);
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextCheck || count < 30)
                return;
            nextCheck = now + 1;
            Array.Copy(samples, sorted, count);
            Array.Sort(sorted, 0, count);
            float p95 = sorted[Mathf.Min(count - 1, (int)(count * .95f))];
            float display = MobileRenderPolicy.Enabled ? MobileRenderPolicy.ScaleCeiling(Screen.width, Screen.height) : 1;
            float maximum = Mathf.Min(display, GameSettings.Current.renderScale), minimum = Mathf.Min((MobileRenderPolicy.Enabled ? .7f : minimumScale) * display, maximum);
            float next = Mathf.Clamp(Scale, minimum, maximum);
            int frameRate = MobileRenderPolicy.FrameRate(GameSettings.Current.fps);
            float slow = frameRate > 0 && frameRate <= 60 ? 1000f / frameRate + 1.5f : 16;
            if (p95 > slow)
                next = Mathf.Max(minimum, next - .05f);
            else if (p95 < 10.5f)
                next = Mathf.Min(maximum, next + .025f);
            if (Mathf.Abs(next - Scale) < .001f)
                return;
            Scale = next;
            SettingsRuntime.SetPresentationScale(this, Scale);
        }
        void OnDisable()
        {
            SettingsRuntime.ReleasePresentationScale(this);
        }
    }
}
