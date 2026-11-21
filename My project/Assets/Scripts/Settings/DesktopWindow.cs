using System;
using UnityEngine;

namespace PickleVerse.Settings
{
    /// <summary>16:9 window sizes offered on the current monitor (pure; covered by EditMode tests).</summary>
    public static class WindowSizes
    {
        public const int MinimumWidth = 960, MinimumHeight = 540;
        // Room for the taskbar and a visible margin around a centred window.
        public const int HorizontalMargin = 40, VerticalMargin = 80;
        static readonly Vector2Int[] candidates =
        {
            new Vector2Int(960, 540), new Vector2Int(1152, 648), new Vector2Int(1280, 720), new Vector2Int(1366, 768),
            new Vector2Int(1600, 900), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3200, 1800), new Vector2Int(3840, 2160)
        };

        /// <summary>The three largest sizes that fit the monitor, smallest first.</summary>
        public static Vector2Int[] Choices(int monitorWidth, int monitorHeight)
        {
            int fit = 0;
            for (int i = 0; i < candidates.Length; i++)
                if (Fits(candidates[i], monitorWidth, monitorHeight))
                    fit = i + 1;
            if (fit == 0)
                return new[] { candidates[0] };
            int count = Mathf.Min(3, fit);
            var result = new Vector2Int[count];
            Array.Copy(candidates, fit - count, result, 0, count);
            return result;
        }
        public static bool Fits(Vector2Int size, int monitorWidth, int monitorHeight) =>
            size.x <= monitorWidth - HorizontalMargin && size.y <= monitorHeight - VerticalMargin;

        /// <summary>The stored size when it still fits this monitor, otherwise the largest offered size.</summary>
        public static Vector2Int Resolve(int width, int height, int monitorWidth, int monitorHeight)
        {
            var stored = new Vector2Int(width, height);
            if (width >= MinimumWidth && height >= MinimumHeight && Fits(stored, monitorWidth, monitorHeight))
                return stored;
            var choices = Choices(monitorWidth, monitorHeight);
            return choices[choices.Length - 1];
        }
        public static string Label(Vector2Int size) => size.x + " × " + size.y;
    }

    /// <summary>
    /// Applies the desktop display mode and exposes the area the PickleVerse title bar leaves to the UI.
    /// Only the Windows player changes the real window; the Editor and phones keep their own presentation.
    /// </summary>
    public static class DesktopWindow
    {
        /// <summary>Title bar height in 96-DPI pixels.</summary>
        public const float TitleBarHeight = 34;
        public static readonly Color Accent = new Color32(58, 128, 236, 255);
#if UNITY_EDITOR
        /// <summary>Draws the window frame inside the Game view so its layout can be checked in Play Mode.</summary>
        public static bool EditorPreview;
#endif
        public static bool Supported => Application.platform == RuntimePlatform.WindowsPlayer;
        public static bool PreviewFrame
        {
            get
            {
#if UNITY_EDITOR
                return EditorPreview;
#else
                return false;
#endif
            }
        }
        /// <summary>Physical pixels hidden by the title bar while the custom frame is showing.</summary>
        public static float TopInset { get; internal set; }
        public static bool FrameVisible => TopInset > 0;
        /// <summary>Screen.safeArea minus the title bar; UI layouts use this instead of Screen.safeArea.</summary>
        public static Rect SafeArea => Inset(Screen.safeArea, Screen.height, TopInset);

        public static Rect Inset(Rect area, float screenHeight, float topInset)
        {
            if (topInset <= 0)
                return area;
            float top = Mathf.Min(area.yMax, screenHeight - topInset);
            return new Rect(area.x, area.y, area.width, Mathf.Max(0, top - area.y));
        }

        /// <summary>Monitor that holds the game window, in physical pixels.</summary>
        public static Vector2Int MonitorSize
        {
            get
            {
                var display = Screen.mainWindowDisplayInfo;
                if (display.width > 0 && display.height > 0)
                    return new Vector2Int(display.width, display.height);
                var current = Screen.currentResolution;
                return new Vector2Int(Mathf.Max(current.width, WindowSizes.MinimumWidth), Mathf.Max(current.height, WindowSizes.MinimumHeight));
            }
        }

        /// <summary>Requests the stored display mode; repeated or unchanged requests cost nothing.</summary>
        public static void Apply(SettingsData data)
        {
            if (data == null || !(Supported || PreviewFrame))
                return;
            var monitor = MonitorSize;
            var size = WindowSizes.Resolve(data.windowWidth, data.windowHeight, monitor.x, monitor.y);
            DesktopWindowFrame.Ensure().Request(data.Display, size);
        }
    }
}
