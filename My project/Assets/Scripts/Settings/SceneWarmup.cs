using System;
using UnityEngine;

namespace PickleVerse.Settings
{
    // Readiness is measured from rendered frames, not a decorative countdown.
    public sealed class SceneWarmup
    {
        readonly float[] frames = new float[36], sorted = new float[36];
        int count, index; float elapsed;
        public float Elapsed => elapsed;
        public float Progress => Mathf.Min(.95f, Mathf.Min(count / (float)frames.Length, elapsed / .8f));
        public void Observe(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0)
                return;
            elapsed += seconds;
            frames[index] = seconds * 1000;
            index = (index + 1) % frames.Length;
            count = Mathf.Min(frames.Length, count + 1);
        }
        public bool Ready(int preferredFps)
        {
            if (count < frames.Length || elapsed < .8f)
                return false;
            Array.Copy(frames, sorted, count);
            Array.Sort(sorted, 0, count);
            float median = sorted[count / 2], p95 = sorted[(int)((count - 1) * .95f)], maximum = sorted[count - 1];
            float budget = 1000f / Mathf.Clamp(preferredFps, 30, 60);
            // Prefer 60 FPS (or an intentional 30 FPS cap). A slower device may
            // finish once it settles; a stalled frame never counts as settled.
            bool smooth = p95 <= budget * 1.2f;
            bool settled = elapsed >= 6 && p95 <= sorted[count / 10] * 1.5f && maximum <= median * 1.8f;
            return (smooth || settled) && maximum <= Mathf.Max(budget * 2.5f, median * 1.8f);
        }
    }
}
