using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PickleVerse.Settings
{
    /// <summary>
    /// Applies display-mode requests once per frame and, in windowed mode on the Windows player, replaces the
    /// system caption with the PickleVerse frame: a draggable title bar with minimise and close buttons inside
    /// a rounded blue border. The bar reports its height through DesktopWindow.SafeArea, so UI moves below it.
    /// </summary>
    public sealed class DesktopWindowFrame : MonoBehaviour
    {
        enum Pressed { None, Minimize, Close }
        static DesktopWindowFrame instance;
        Canvas canvas;
        CanvasScaler scaler;
        UiWindowFrame frame;
        RectTransform minimizeRect, closeRect;
        Image minimizeImage, closeImage;
        UiLineIcon minimizeIcon, closeIcon;
        Material normal, hover, press, danger, dangerPress;
        Pressed pressed;
        bool built, visible, dragging, throttled, hasRequest, skipStartupRequest, centreNextFrame, switching, synced;
        bool hoverMinimize, hoverClose;
        DisplayMode requestMode;
        Vector2Int requestSize, dragCursor, dragWindow, dragSize, windowedSize;
        FullScreenMode lastMode;
        int lastWidth, lastHeight, syncAtFrame = -1;
        float scale = 1, nextCheck, switchStarted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            DesktopWindow.TopInset = 0;
        }
        public static DesktopWindowFrame Ensure()
        {
            if (instance)
                return instance;
            var go = new GameObject("PickleVerse Window Frame");
            DontDestroyOnLoad(go);
            return go.AddComponent<DesktopWindowFrame>();
        }
        void Awake()
        {
            if (instance && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            // A command line that sets the window (-screen-fullscreen, -window-mode, -popupwindow) wins at start-up.
            foreach (var arg in Environment.GetCommandLineArgs())
                if (arg == "-screen-fullscreen" || arg == "-window-mode" || arg == "-popupwindow")
                    skipStartupRequest = true;
            lastMode = Screen.fullScreenMode;
            lastWidth = Screen.width;
            lastHeight = Screen.height;
        }

        /// <summary>Queues a display mode; the last request of a frame is the one applied.</summary>
        public void Request(DisplayMode mode, Vector2Int windowSize)
        {
            if (skipStartupRequest)
            {
                skipStartupRequest = false;
                return;
            }
            requestMode = mode;
            requestSize = windowSize;
            hasRequest = true;
        }

        void LateUpdate()
        {
            if (hasRequest)
            {
                hasRequest = false;
                ApplyRequest();
            }
        }
        void ApplyRequest()
        {
            if (!DesktopWindow.Supported)
            {
                SyncChrome();
                return;
            }
            var target = requestMode == DisplayMode.Windowed ? FullScreenMode.Windowed : requestMode == DisplayMode.Fullscreen ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.FullScreenWindow;
            var size = requestMode == DisplayMode.Windowed ? requestSize : DesktopWindow.MonitorSize;
            // The chosen client size; Unity briefly sizes the window with its own caption, which the frame corrects.
            windowedSize = target == FullScreenMode.Windowed ? size : default;
            if (Screen.fullScreenMode == target && Screen.width == size.x && Screen.height == size.y)
            {
                // Settings previews re-send the current mode on every change; only an unframed window needs work.
                if (visible != (target == FullScreenMode.Windowed) || visible && NativeWindow.HasSystemCaption)
                    SyncChrome();
                return;
            }
            centreNextFrame = target == FullScreenMode.Windowed;
            switching = true;
            switchStarted = Time.realtimeSinceStartup;
            if (target == FullScreenMode.ExclusiveFullScreen)
                Screen.SetResolution(size.x, size.y, target, Screen.currentResolution.refreshRateRatio);
            else
                Screen.SetResolution(size.x, size.y, target);
        }

        void Update()
        {
            if (!synced)
            {
                // First frame: a window opened in windowed mode (command line, saved Unity state) gets its frame now.
                synced = true;
                SyncChrome();
            }
            if (Screen.fullScreenMode != lastMode || Screen.width != lastWidth || Screen.height != lastHeight)
            {
                lastMode = Screen.fullScreenMode;
                lastWidth = Screen.width;
                lastHeight = Screen.height;
                if (switching)
                {
                    switching = false;
                    Debug.Log("[Display] " + lastMode + " " + lastWidth + "x" + lastHeight + " in " + ((Time.realtimeSinceStartup - switchStarted) * 1000).ToString("0") + " ms");
                }
                // Leaving windowed mode hides the frame at once; entering it waits two frames for Unity's own
                // window style and size to settle so the frame is applied once, on the final window.
                if (lastMode == FullScreenMode.Windowed)
                    syncAtFrame = Time.frameCount + 2;
                else
                    SyncChrome();
            }
            else if (syncAtFrame >= 0 && Time.frameCount >= syncAtFrame)
            {
                syncAtFrame = -1;
                SyncChrome();
            }
            else if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + .25f;
                // Unity restores its own caption after Alt+Enter or a resolution change; put the frame back.
                if (visible && DesktopWindow.Supported && !dragging && (NativeWindow.HasSystemCaption || WrongWindowedSize()))
                    SyncChrome();
                else if (visible && !Mathf.Approximately(scale, DpiScale()))
                    SyncChrome();
                ThrottleWhileMinimized();
            }
            if (visible)
                HandlePointer();
        }

        bool WrongWindowedSize() => windowedSize.x > 0 && Screen.fullScreenMode == FullScreenMode.Windowed && (Screen.width != windowedSize.x || Screen.height != windowedSize.y);
        /// <summary>Re-evaluates the frame now (used after toggling the Editor preview).</summary>
        public void Refresh() => SyncChrome();
        void SyncChrome()
        {
            bool custom = DesktopWindow.Supported ? Screen.fullScreenMode == FullScreenMode.Windowed : DesktopWindow.PreviewFrame;
            if (DesktopWindow.Supported && (custom || visible))
                NativeWindow.SetCustomFrame(custom, custom && centreNextFrame, DesktopWindow.Accent, windowedSize);
            centreNextFrame = false;
            Show(custom);
        }
        void Show(bool show)
        {
            if (show && !built)
                Build();
            visible = show;
            if (canvas)
                canvas.enabled = show;
            if (show)
            {
                scale = DpiScale();
                scaler.scaleFactor = scale;
            }
            else
            {
                dragging = false;
                pressed = Pressed.None;
            }
            DesktopWindow.TopInset = show ? DesktopWindow.TitleBarHeight * scale : 0;
        }
        static float DpiScale() => Screen.dpi > 0 ? Mathf.Clamp(Mathf.Round(Screen.dpi / 96f * 4) / 4, 1, 3) : 1;

        void ThrottleWhileMinimized()
        {
            if (!DesktopWindow.Supported)
                return;
            bool minimized = NativeWindow.Minimized;
            if (minimized == throttled)
                return;
            throttled = minimized;
            SettingsRuntime.SetBackgroundThrottle(minimized);
        }

        void HandlePointer()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;
            var position = mouse.position.ReadValue();
            bool overMinimize = RectTransformUtility.RectangleContainsScreenPoint(minimizeRect, position, null);
            bool overClose = RectTransformUtility.RectangleContainsScreenPoint(closeRect, position, null);
            bool overBar = position.y >= Screen.height - DesktopWindow.TopInset && position.y <= Screen.height && position.x >= 0 && position.x <= Screen.width;
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (overMinimize)
                    pressed = Pressed.Minimize;
                else if (overClose)
                    pressed = Pressed.Close;
                else if (overBar)
                    BeginDrag();
            }
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (pressed == Pressed.Minimize && overMinimize)
                    NativeWindow.Minimize();
                else if (pressed == Pressed.Close && overClose)
                    GameExit.Request();
                pressed = Pressed.None;
            }
            if (dragging)
                Drag();
            if (overMinimize != hoverMinimize || overClose != hoverClose || pressed != Pressed.None)
            {
                hoverMinimize = overMinimize;
                hoverClose = overClose;
                Paint();
            }
        }
        void BeginDrag()
        {
            dragging = NativeWindow.TryCursor(out dragCursor) && NativeWindow.TryWindowPosition(out dragWindow, out dragSize);
        }
        void Drag()
        {
            if (!NativeWindow.PrimaryButtonHeld || !NativeWindow.TryCursor(out var cursor))
            {
                dragging = false;
                return;
            }
            if (cursor == dragCursor)
                return;
            var target = dragWindow + (cursor - dragCursor);
            NativeWindow.MoveTo(target, dragSize, Mathf.RoundToInt(DesktopWindow.TopInset));
        }

        void Paint()
        {
            minimizeImage.material = pressed == Pressed.Minimize && hoverMinimize ? press : hoverMinimize ? hover : normal;
            closeImage.material = pressed == Pressed.Close && hoverClose ? dangerPress : hoverClose ? danger : normal;
            minimizeIcon.color = hoverMinimize ? Color.white : new Color(.82f, .88f, .98f);
            closeIcon.color = hoverClose ? Color.white : new Color(.82f, .88f, .98f);
        }

        void Build()
        {
            built = true;
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32700;
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            gameObject.AddComponent<GraphicRaycaster>();
            var theme = ScreenUiTheme.Current;

            frame = Child<UiWindowFrame>("Frame", transform);
            Stretch(frame.rectTransform);

            var emblem = Child<UiLineIcon>("Emblem", frame.transform);
            emblem.glyph = ArenaGlyph.Star;
            emblem.stroke = .09f;
            emblem.color = ScreenUiTheme.Gold;
            emblem.raycastTarget = false;
            Place(emblem.rectTransform, new Vector2(0, 1), new Vector2(0, .5f), new Vector2(14, -DesktopWindow.TitleBarHeight * .5f), new Vector2(15, 15));

            var wordmark = Child<TextMeshProUGUI>("Wordmark", frame.transform);
            if (theme && theme.font)
                wordmark.font = theme.font;
            wordmark.text = "PICKLEVERSE";
            wordmark.fontSize = 14;
            wordmark.characterSpacing = 4;
            wordmark.fontStyle = FontStyles.Bold;
            wordmark.color = ScreenUiTheme.Gold;
            wordmark.alignment = TextAlignmentOptions.MidlineLeft;
            wordmark.textWrappingMode = TextWrappingModes.NoWrap;
            wordmark.raycastTarget = false;
            Place(wordmark.rectTransform, new Vector2(0, 1), new Vector2(0, .5f), new Vector2(38, -DesktopWindow.TitleBarHeight * .5f), new Vector2(220, 24));

            normal = Variant(theme ? theme.quiet : null, new Color(.13f, .2f, .31f), new Color(.07f, .11f, .19f), new Color(.51f, .62f, .73f, .55f));
            hover = Variant(theme ? theme.panel : null, new Color(.21f, .43f, .82f), new Color(.11f, .25f, .55f), new Color(.66f, .82f, 1, .95f));
            press = Variant(theme ? theme.panel : null, new Color(.12f, .28f, .58f), new Color(.07f, .16f, .38f), new Color(.66f, .82f, 1, .95f));
            danger = Variant(theme ? theme.panel : null, new Color(.84f, .27f, .31f), new Color(.56f, .13f, .18f), new Color(1, .66f, .66f, .95f));
            dangerPress = Variant(theme ? theme.panel : null, new Color(.6f, .16f, .2f), new Color(.38f, .08f, .12f), new Color(1, .66f, .66f, .95f));

            closeRect = CaptionButton("Close", ArenaGlyph.Close, -8, out closeImage, out closeIcon);
            minimizeRect = CaptionButton("Minimize", ArenaGlyph.Minimize, -8 - 46 - 6, out minimizeImage, out minimizeIcon);
            Paint();
        }
        RectTransform CaptionButton(string name, ArenaGlyph glyph, float right, out Image image, out UiLineIcon icon)
        {
            image = Child<Image>(name, frame.transform);
            image.raycastTarget = true;
            image.color = Color.white;
            Place(image.rectTransform, Vector2.one, Vector2.one, new Vector2(right, -5), new Vector2(46, DesktopWindow.TitleBarHeight - 10));
            icon = Child<UiLineIcon>("Icon", image.transform);
            icon.glyph = glyph;
            icon.stroke = .1f;
            icon.raycastTarget = false;
            Place(icon.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(13, 13));
            return image.rectTransform;
        }
        Material Variant(Material source, Color top, Color bottom, Color edge)
        {
            if (!source)
                return null;
            var m = new Material(source) { name = "Window Caption " + top };
            m.hideFlags = HideFlags.DontSave;
            m.SetColor("_Top", top);
            m.SetColor("_Bottom", bottom);
            m.SetColor("_Border", edge);
            m.SetFloat("_Cut", .16f);
            return m;
        }
        static T Child<T>(string name, Transform parent) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            return go.AddComponent<T>();
        }
        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
        static void Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = pivot;
            r.anchoredPosition = position;
            r.sizeDelta = size;
        }
        void OnDestroy()
        {
            if (instance != this)
                return;
            instance = null;
            DesktopWindow.TopInset = 0;
            foreach (var m in new[] { normal, hover, press, danger, dangerPress })
                if (m)
                    Destroy(m);
        }
    }
}
