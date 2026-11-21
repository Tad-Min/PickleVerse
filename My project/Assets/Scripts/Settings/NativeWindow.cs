using System;
using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Runtime.InteropServices;
using System.Text;
#endif

namespace PickleVerse.Settings
{
    /// <summary>Win32 calls for the custom window frame. Every member is a no-op outside the Windows player.</summary>
    internal static class NativeWindow
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        const int GWL_STYLE = -16, SW_MINIMIZE = 6, SM_SWAPBUTTON = 23, VK_LBUTTON = 1, VK_RBUTTON = 2;
        const long WS_POPUP = 0x80000000L, WS_CAPTION = 0x00C00000L, WS_THICKFRAME = 0x00040000L, WS_MAXIMIZEBOX = 0x00010000L,
            WS_MINIMIZEBOX = 0x00020000L, WS_SYSMENU = 0x00080000L;
        const uint SWP_NOSIZE = 0x1, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_FRAMECHANGED = 0x20, SWP_NOOWNERZORDER = 0x200;
        const int DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWA_BORDER_COLOR = 34, DWMWCP_DEFAULT = 0, DWMWCP_ROUND = 2;
        const uint DWMWA_COLOR_DEFAULT = 0xFFFFFFFF, MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)] struct RECT { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] struct MONITORINFO { public int size; public RECT monitor, work; public uint flags; }
        delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out RECT rect);
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr window, out RECT rect);
        [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr window, ref POINT point);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
        [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SetWindowText(IntPtr window, string text);
        [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint thread, EnumWindowsProc callback, IntPtr parameter);
        [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        static IntPtr handle, found;
        static readonly StringBuilder className = new StringBuilder(32);
        static readonly EnumWindowsProc finder = Find;
        static bool titled;
        static int dwmState = -1;

        [AOT.MonoPInvokeCallback(typeof(EnumWindowsProc))]
        static bool Find(IntPtr window, IntPtr parameter)
        {
            className.Length = 0;
            GetClassName(window, className, className.Capacity);
            if (className.ToString() != "UnityWndClass")
                return true;
            found = window;
            return false;
        }
        static IntPtr Handle
        {
            get
            {
                if (handle != IntPtr.Zero)
                    return handle;
                found = IntPtr.Zero;
                EnumThreadWindows(GetCurrentThreadId(), finder, IntPtr.Zero);
                handle = found != IntPtr.Zero ? found : GetActiveWindow();
                if (handle != IntPtr.Zero && !titled)
                {
                    titled = true;
                    SetWindowText(handle, "PickleVerse");
                }
                return handle;
            }
        }
        public static bool Available => Handle != IntPtr.Zero;

        /// <summary>
        /// Removes the system caption (custom) or leaves the style to Unity; true when the window changed.
        /// A non-zero size is the client size to keep: without a caption the window is exactly that size.
        /// </summary>
        public static bool SetCustomFrame(bool custom, bool centre, Color border, Vector2Int size)
        {
            var window = Handle;
            if (window == IntPtr.Zero)
                return false;
            bool changed = false;
            if (custom)
            {
                long style = GetWindowLongPtr(window, GWL_STYLE).ToInt64();
                long desired = (style & ~(WS_CAPTION | WS_THICKFRAME | WS_MAXIMIZEBOX)) | WS_POPUP | WS_MINIMIZEBOX | WS_SYSMENU;
                GetClientRect(window, out var client);
                int width = size.x > 0 ? size.x : client.right - client.left, height = size.y > 0 ? size.y : client.bottom - client.top;
                bool resize = width != client.right - client.left || height != client.bottom - client.top;
                if (desired != style || centre || resize)
                {
                    // Keep the client area where it is (or centre it); only a wrong size makes Unity resize.
                    var origin = new POINT();
                    ClientToScreen(window, ref origin);
                    if (centre && TryWorkArea(window, out var work))
                    {
                        origin.x = work.left + Mathf.Max(0, (work.right - work.left - width) / 2);
                        origin.y = work.top + Mathf.Max(0, (work.bottom - work.top - height) / 2);
                    }
                    SetWindowLongPtr(window, GWL_STYLE, new IntPtr(desired));
                    SetWindowPos(window, IntPtr.Zero, origin.x, origin.y, width, height, SWP_FRAMECHANGED | SWP_NOZORDER | SWP_NOOWNERZORDER | SWP_NOACTIVATE);
                    changed = true;
                }
            }
            SetDwm(window, custom, border);
            return changed;
        }
        static void SetDwm(IntPtr window, bool custom, Color border)
        {
            if (dwmState == (custom ? 1 : 0))
                return;
            dwmState = custom ? 1 : 0;
            try
            {
                // Windows 11 rounds the corners and tints the 1px edge; older systems reject the attributes harmlessly.
                int corner = custom ? DWMWCP_ROUND : DWMWCP_DEFAULT;
                DwmSetWindowAttribute(window, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, 4);
                Color32 c = border;
                int colour = custom ? c.r | c.g << 8 | c.b << 16 : unchecked((int)DWMWA_COLOR_DEFAULT);
                DwmSetWindowAttribute(window, DWMWA_BORDER_COLOR, ref colour, 4);
            }
            catch (Exception)
            {
            }
        }
        static bool TryWorkArea(IntPtr window, out RECT work)
        {
            var info = new MONITORINFO { size = Marshal.SizeOf(typeof(MONITORINFO)) };
            bool ok = GetMonitorInfo(MonitorFromWindow(window, MONITOR_DEFAULTTONEAREST), ref info);
            work = info.work;
            return ok;
        }
        public static bool HasSystemCaption => Handle != IntPtr.Zero && (GetWindowLongPtr(Handle, GWL_STYLE).ToInt64() & WS_CAPTION) != 0;
        public static bool Minimized => Handle != IntPtr.Zero && IsIconic(Handle);
        public static void Minimize()
        {
            if (Handle != IntPtr.Zero)
                ShowWindow(Handle, SW_MINIMIZE);
        }
        public static bool PrimaryButtonHeld => (GetAsyncKeyState(GetSystemMetrics(SM_SWAPBUTTON) != 0 ? VK_RBUTTON : VK_LBUTTON) & 0x8000) != 0;
        public static bool TryCursor(out Vector2Int cursor)
        {
            bool ok = GetCursorPos(out var p);
            cursor = new Vector2Int(p.x, p.y);
            return ok;
        }
        public static bool TryWindowPosition(out Vector2Int position, out Vector2Int size)
        {
            RECT r = default;
            bool ok = Handle != IntPtr.Zero && GetWindowRect(Handle, out r);
            position = ok ? new Vector2Int(r.left, r.top) : default;
            size = ok ? new Vector2Int(r.right - r.left, r.bottom - r.top) : default;
            return ok;
        }
        /// <summary>Moves the window while keeping its title bar reachable on the monitor work area.</summary>
        public static void MoveTo(Vector2Int position, Vector2Int size, int titleBar)
        {
            var window = Handle;
            if (window == IntPtr.Zero)
                return;
            if (TryWorkArea(window, out var work))
            {
                const int keep = 120;
                position.x = Mathf.Clamp(position.x, work.left - size.x + keep, work.right - keep);
                position.y = Mathf.Clamp(position.y, work.top, work.bottom - Mathf.Max(titleBar, 24));
            }
            SetWindowPos(window, IntPtr.Zero, position.x, position.y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOOWNERZORDER | SWP_NOACTIVATE);
        }
#else
        public static bool Available => false;
        public static bool SetCustomFrame(bool custom, bool centre, Color border, Vector2Int size) => false;
        public static bool HasSystemCaption => false;
        public static bool Minimized => false;
        public static void Minimize() { }
        public static bool PrimaryButtonHeld => false;
        public static bool TryCursor(out Vector2Int cursor) { cursor = default; return false; }
        public static bool TryWindowPosition(out Vector2Int position, out Vector2Int size) { position = size = default; return false; }
        public static void MoveTo(Vector2Int position, Vector2Int size, int titleBar) { }
#endif
    }
}
