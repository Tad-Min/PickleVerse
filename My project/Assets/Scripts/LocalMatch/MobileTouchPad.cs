using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using PickleVerse.Settings;

namespace PickleVerse.LocalMatch
{
    public struct MobileTouchLayout
    {
        public float unit;
        public Vector2 joystick, drive, dink, volley, run;
        public static MobileTouchLayout Calculate(Rect safe, float scale, bool leftHanded)
        {
            float u = Mathf.Min(safe.height / 390, safe.width / 700) * Mathf.Clamp(scale, .75f, 1.4f);
            // Even at maximum size, the entire cancel wave stays above the safe bottom edge.
            u = Mathf.Min(u, safe.height / 440, safe.width / 760);
            var r = new MobileTouchLayout { unit = u };
            r.joystick = new Vector2(safe.xMin + 98 * u, safe.yMin + 108 * u);
            r.drive = new Vector2(safe.xMax - 230 * u, safe.yMin + 126 * u);
            r.dink = new Vector2(safe.xMax - 242 * u, safe.yMin + 244 * u);
            r.volley = new Vector2(safe.xMax - 100 * u, safe.yMin + 320 * u);
            r.run = new Vector2(safe.xMax - 96 * u, safe.yMin + 108 * u);
            if (leftHanded)
            {
                r.joystick.x = safe.center.x * 2 - r.joystick.x;
                r.drive.x = safe.center.x * 2 - r.drive.x;
                r.dink.x = safe.center.x * 2 - r.dink.x;
                r.volley.x = safe.center.x * 2 - r.volley.x;
                r.run.x = safe.center.x * 2 - r.run.x;
            }
            return r;
        }
    }

    [DisallowMultipleComponent]
    public sealed class MobileTouchPad : MonoBehaviour
    {
        public readonly TouchShotGesture Gesture = new TouchShotGesture();
        public IMatchTouchShotReceiver Receiver { get; private set; }
        public TouchControl Joystick { get; private set; }
        public TouchControl Drive { get; private set; }
        public TouchControl Dink { get; private set; }
        public TouchControl Volley { get; private set; }
        public TouchControl Run { get; private set; }
        public Rect SafeRect { get; private set; }
        public float Unit { get; private set; } = 1;
        RectTransform root, waveRoot;
        TouchWaveGraphic wave;
        readonly TMP_Text[] waveLabels = new TMP_Text[4];
        readonly UnityEngine.UI.Image[] labelBackings = new UnityEngine.UI.Image[4];
        readonly TMP_Text[] comboHints = new TMP_Text[5];
        readonly CanvasGroup[] groups = new CanvasGroup[5];
        Canvas canvas;
        TMP_FontAsset font;
        TouchControl activeControl;
        Vector2 pressPoint;
        int joyPointer = TouchShotGesture.NoPointer;
        int runPointer = TouchShotGesture.NoPointer;
        int screenWidth, screenHeight;
        Rect previousSafe, previousRoot;
        bool configured, serving, dirty = true;
        bool previousReady, previousServe;
        float previousAlpha = -1;
        MobileTouchHud presentation;
        static Texture2D atlas;
        static Sprite[] sprites;

        public static MobileTouchPad Install(GameObject touchRoot, IMatchTouchShotReceiver receiver)
        {
            if (!touchRoot)
                return null;
            var pad = touchRoot.GetComponent<MobileTouchPad>();
            if (!pad)
                pad = touchRoot.AddComponent<MobileTouchPad>();
            if (!pad.configured)
                pad.Configure(receiver);
            return pad;
        }
        void Configure(IMatchTouchShotReceiver receiver)
        {
            Receiver = receiver;
            root = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>(true);
            if (canvas && MobileRenderPolicy.Enabled)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
            }
            var controls = GetComponentsInChildren<TouchControl>(true);
            foreach (var c in controls)
            {
                c.Input = receiver;
                if (c.joystick)
                    Joystick = c;
                else if (c.sprint)
                    Run = c;
                else if (c.shotMask == 1)
                    Drive = c;
                else if (c.shotMask == 2)
                    Dink = c;
                else if (c.shotMask == 4)
                    Volley = c;
                else c.gameObject.SetActive(false);
            }
            if (!Joystick || !Drive || !Dink || !Volley)
            {
                Debug.LogError("Touch pad needs a joystick and three technique controls.", this);
                return;
            }
            if (!Run)
            {
                var runRect = Rect("Sprint", root, new Vector2(94, 94));
                runRect.gameObject.SetActive(false);
                runRect.gameObject.AddComponent<UnityEngine.UI.Image>();
                Run = runRect.gameObject.AddComponent<TouchControl>();
                Run.sprint = true;
                Run.Input = receiver;
            }
            var old = Drive.GetComponentInChildren<TMP_Text>(true);
            font = old ? old.font : TMP_Settings.defaultFontAsset;
            if (!font && canvas)
                foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true))
                    if (text.font)
                    {
                        font = text.font;
                        break;
                    }
            LoadArt();
            Prepare(Joystick, 0, 0);
            Prepare(Drive, 1, 2);
            Prepare(Dink, 2, 3);
            Prepare(Volley, 3, 4);
            Prepare(Run, 4, 1);
            var runIcon = Rect("Run icon", Run.transform, new Vector2(48, 48));
            var glyph = runIcon.gameObject.AddComponent<UiLineIcon>();
            glyph.glyph = ArenaGlyph.Run;
            glyph.color = Color.white;
            glyph.raycastTarget = false;
            if (Joystick.knob)
            {
                var image = Joystick.knob.GetComponent<UnityEngine.UI.Image>();
                if (image)
                {
                    image.sprite = sprites?[1];
                    image.material = null;
                    image.color = Color.white;
                    image.raycastTarget = false;
                }
                Joystick.knob.anchorMin = Joystick.knob.anchorMax = Joystick.knob.pivot = new Vector2(.5f, .5f);
                Joystick.knob.sizeDelta = new Vector2(48, 48);
            }
            var hint = root.Find("MoveLabel");
            if (hint)
                hint.gameObject.SetActive(false);
            waveRoot = Rect("Swipe waves", root, new Vector2(200, 200));
            wave = waveRoot.gameObject.AddComponent<TouchWaveGraphic>();
            wave.raycastTarget = false;
            for (int i = 0; i < 4; i++)
            {
                var backing = Rect("Direction " + i, waveRoot, new Vector2(i == 0 ? 94 : 76, 28));
                labelBackings[i] = backing.gameObject.AddComponent<UnityEngine.UI.Image>();
                labelBackings[i].color = new Color(.025f, .06f, .10f, .96f);
                labelBackings[i].raycastTarget = false;
                waveLabels[i] = Label("Label", backing, 15);
                var textRect = waveLabels[i].rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            }
            waveLabels[1].text = "CHÉO";
            waveLabels[2].text = "LỰC";
            waveLabels[3].text = "HỦY";
            waveRoot.gameObject.SetActive(false);
            presentation = new MobileTouchHud(receiver);
            configured = true;
            dirty = true;
            Layout();
        }
        static void LoadArt()
        {
            if (atlas && sprites != null && sprites[0])
                return;
            atlas = Resources.Load<Texture2D>("Touch/ControlsAtlas");
            if (!atlas)
                return;
            sprites = new Sprite[8];
            for (int i = 0; i < 8; i++)
                sprites[i] = Sprite.Create(atlas, new Rect((i % 4) * 256, 256 - (i / 4) * 256, 256, 256), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
        }
        void Prepare(TouchControl c, int index, int art)
        {
            c.Pad = this;
            c.modifier = false;
            c.gameObject.SetActive(true);
            foreach (var text in c.GetComponentsInChildren<TMP_Text>(true))
                text.gameObject.SetActive(false);
            var rect = (RectTransform)c.transform;
            rect.localScale = Vector3.one;
            var image = c.GetComponent<UnityEngine.UI.Image>();
            if (image)
            {
                image.sprite = sprites?[art];
                image.material = null;
                image.color = Color.white;
                image.raycastTarget = true;
                image.preserveAspect = true;
                image.raycastPadding = Vector4.one * -4;
            }
            groups[index] = c.GetComponent<CanvasGroup>();
            if (!groups[index])
                groups[index] = c.gameObject.AddComponent<CanvasGroup>();
            if (index == 0)
                return;
            var caption = Label("Technique caption", rect, 12);
            caption.text = index == 1 ? "DRIVE" : index == 2 ? "DINK" : index == 3 ? "VOLLEY" : "CHẠY";
            caption.rectTransform.anchoredPosition = new Vector2(0, -45);
            if (index == 4)
            {
                caption.rectTransform.anchoredPosition = new Vector2(0, -59);
                return;
            }
            var combo = Label("Combo hint", rect, 9);
            comboHints[index] = combo;
            combo.text = index == 1 ? "↑ SMASH" : index == 2 ? "↑ DROP" : "↑ RESET";
            combo.color = new Color(.72f, .86f, 1, .85f);
            combo.rectTransform.anchoredPosition = new Vector2(0, -59);
        }
        static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            r.sizeDelta = size;
            return r;
        }
        TMP_Text Label(string name, Transform parent, float size)
        {
            var r = Rect(name, parent, new Vector2(108, 22));
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            t.raycastTarget = false;
            if (font)
                t.fontSharedMaterial = font.material;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }
        void OnEnable()
        {
            GameSettings.Changed += SettingsChanged;
            dirty = true;
        }
        void OnDisable()
        {
            GameSettings.Changed -= SettingsChanged;
            CancelAll();
            presentation?.Restore();
        }
        void LateUpdate()
        {
            if (configured)
                presentation?.Poll();
        }
        void OnApplicationFocus(bool focused)
        {
            if (!focused)
                CancelAll();
        }
        void OnApplicationPause(bool paused)
        {
            if (paused)
                CancelAll();
        }
        void SettingsChanged()
        {
            CancelAll();
            dirty = true;
        }
        bool Blocked => SettingsOverlay.IsOpen || SettingsOverlay.Loading || SceneLoading.IsBlocking;
        void Update()
        {
            if (!configured)
                return;
            if (screenWidth != Screen.width || screenHeight != Screen.height || previousSafe != Screen.safeArea || previousRoot != root.rect)
                dirty = true;
            if (dirty)
                Layout();
            if (Blocked)
            {
                CancelAll();
                return;
            }
            if (Gesture.Active && (!Receiver.TouchShotsEnabled || serving != Receiver.TouchServing))
                CancelShot();
            float alpha = GameSettings.Current.touchOpacity;
            bool ready = Receiver.TouchShotsEnabled, serve = Receiver.TouchServing;
            if (alpha != previousAlpha || ready != previousReady || serve != previousServe)
            {
                previousAlpha = alpha;
                previousReady = ready;
                previousServe = serve;
                RefreshOpacity();
            }
        }
        public void Layout(Rect? safeOverride = null)
        {
            if (!configured || root.rect.width <= 0 || Screen.width <= 0)
                return;
            CancelAll();
            dirty = false;
            screenWidth = Screen.width;
            screenHeight = Screen.height;
            previousSafe = Screen.safeArea;
            previousRoot = root.rect;
            Camera cam = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, Screen.safeArea.min, cam, out var min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, Screen.safeArea.max, cam, out var max);
            SafeRect = safeOverride ?? UnityEngine.Rect.MinMaxRect(Mathf.Max(root.rect.xMin, min.x), Mathf.Max(root.rect.yMin, min.y), Mathf.Min(root.rect.xMax, max.x), Mathf.Min(root.rect.yMax, max.y));
            var settings = GameSettings.Current;
            var layout = MobileTouchLayout.Calculate(SafeRect, settings.touchScale, settings.leftHanded);
            Unit = layout.unit;
            var defaults = SettingsData.Defaults().touchPositions;
            layout.joystick = Customized(layout.joystick, 0, 78, settings, defaults);
            layout.drive = Customized(layout.drive, 1, 99, settings, defaults);
            layout.dink = Customized(layout.dink, 2, 99, settings, defaults);
            layout.volley = Customized(layout.volley, 3, 99, settings, defaults);
            layout.run = Customized(layout.run, 4, 56, settings, defaults);
            // Reject overlapping custom targets while retaining safe, responsive defaults.
            if (Vector2.Distance(layout.drive, layout.dink) < 100 * Unit || Vector2.Distance(layout.drive, layout.volley) < 100 * Unit || Vector2.Distance(layout.dink, layout.volley) < 94 * Unit ||
                Vector2.Distance(layout.joystick, layout.drive) < 160 * Unit || Vector2.Distance(layout.joystick, layout.dink) < 155 * Unit || Vector2.Distance(layout.joystick, layout.volley) < 155 * Unit ||
                Vector2.Distance(layout.run, layout.drive) < 110 * Unit || Vector2.Distance(layout.run, layout.dink) < 110 * Unit || Vector2.Distance(layout.run, layout.volley) < 110 * Unit || Vector2.Distance(layout.run, layout.joystick) < 160 * Unit)
                layout = MobileTouchLayout.Calculate(SafeRect, settings.touchScale, settings.leftHanded);
            Position(Joystick, layout.joystick, 146);
            Position(Drive, layout.drive, 84);
            Position(Dink, layout.dink, 84);
            Position(Volley, layout.volley, 84);
            Position(Run, layout.run, 112);
            if (isActiveAndEnabled)
                presentation?.Apply();
            RefreshOpacity();
        }
        Vector2 Customized(Vector2 position, int index, float radius, SettingsData settings, Vector2[] defaults)
        {
            var offset = Vector2.Scale(settings.touchPositions[index] - defaults[index], SafeRect.size);
            if (settings.leftHanded)
                offset.x = -offset.x;
            position += offset;
            float r = radius * Unit;
            return new Vector2(Mathf.Clamp(position.x, SafeRect.xMin + r, SafeRect.xMax - r), Mathf.Clamp(position.y, SafeRect.yMin + r, SafeRect.yMax - r));
        }
        void Position(TouchControl control, Vector2 position, float size)
        {
            var r = (RectTransform)control.transform;
            r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
            r.pivot = new Vector2(.5f, .5f);
            // Coordinates are in the root rect, whose pivot need not be centered.
            r.anchoredPosition = position - root.rect.center;
            r.sizeDelta = new Vector2(size, size);
            r.localScale = Vector3.one * Unit;
        }
        Vector2 Point(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, e.position, e.pressEventCamera, out var p);
            return p;
        }
        public void Down(TouchControl control, PointerEventData e)
        {
            if (!configured || Blocked)
                return;
            if (control.sprint)
            {
                if (runPointer != TouchShotGesture.NoPointer)
                    return;
                runPointer = e.pointerId;
                Receiver.TouchRun = true;
                RefreshOpacity();
                return;
            }
            if (control.joystick)
            {
                if (joyPointer != TouchShotGesture.NoPointer)
                    return;
                joyPointer = e.pointerId;
                Drag(control, e);
                return;
            }
            if (!Receiver.TouchShotsEnabled || (Receiver.TouchServing && control != Drive))
                return;
            if (!Gesture.Begin(e.pointerId, control.shotMask))
                return;
            activeControl = control;
            serving = Receiver.TouchServing;
            pressPoint = Point(e);
            ShowWave();
            RefreshOpacity();
        }
        public void Drag(TouchControl control, PointerEventData e)
        {
            if (Blocked)
            {
                CancelAll();
                return;
            }
            if (control.sprint)
                return;
            if (control.joystick)
            {
                if (joyPointer != e.pointerId)
                    return;
                var r = (RectTransform)control.transform;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(r, e.position, e.pressEventCamera, out var p);
                Vector2 value = (p - r.rect.center) / (r.rect.width * .32f);
                var s = GameSettings.Current;
                float length = value.magnitude;
                value = length <= s.deadzone ? Vector2.zero : value.normalized * Mathf.InverseLerp(s.deadzone, 1, length);
                Receiver.TouchMovement = Vector2.ClampMagnitude(value, 1);
                if (control.knob)
                    control.knob.anchoredPosition = Receiver.TouchMovement * (r.rect.width * .27f);
                return;
            }
            if (activeControl != control || Gesture.Pointer != e.pointerId)
                return;
            var before = Gesture.Direction;
            float progress = Gesture.Progress;
            Gesture.Move(e.pointerId, (Point(e) - pressPoint) / Unit, serving);
            if (before != Gesture.Direction || Mathf.Abs(progress - Gesture.Progress) > .015f)
                ShowWave();
        }
        public void Up(TouchControl control, PointerEventData e, bool cancelled = false)
        {
            if (control.sprint)
            {
                if (runPointer != e.pointerId)
                    return;
                runPointer = TouchShotGesture.NoPointer;
                Receiver.TouchRun = false;
                RefreshOpacity();
                return;
            }
            if (control.joystick)
            {
                if (joyPointer != e.pointerId)
                    return;
                joyPointer = TouchShotGesture.NoPointer;
                Receiver.TouchMovement = Vector2.zero;
                if (control.knob)
                    control.knob.anchoredPosition = Vector2.zero;
                return;
            }
            if (activeControl != control || Gesture.Pointer != e.pointerId)
                return;
            // Apply the final position even if the input module coalesced the last drag event.
            Drag(control, e);
            bool commit = Gesture.Release(e.pointerId, out int mask);
            bool valid = !cancelled && !Blocked && Receiver.TouchShotsEnabled && serving == Receiver.TouchServing;
            CancelShot();
            if (commit && valid)
                Receiver.CommitTouchShot(mask);
        }
        void ShowWave()
        {
            if (!activeControl)
                return;
            waveRoot.gameObject.SetActive(true);
            waveRoot.SetAsLastSibling();
            Vector2 center = ((RectTransform)activeControl.transform).anchoredPosition + root.rect.center;
            center.x = Mathf.Clamp(center.x, SafeRect.xMin + 145 * Unit, SafeRect.xMax - 145 * Unit);
            center.y = Mathf.Clamp(center.y, SafeRect.yMin + 118 * Unit, SafeRect.yMax - 118 * Unit);
            waveRoot.anchoredPosition = center - root.rect.center;
            waveRoot.localScale = Vector3.one * Unit;
            wave.direction = Gesture.Direction;
            wave.progress = Gesture.Progress;
            wave.serving = serving;
            wave.SetVerticesDirty();
            waveLabels[0].text = Gesture.BaseMask == 1 ? "SMASH" : Gesture.BaseMask == 2 ? "DROP" : "RESET";
            for (int i = 0; i < 4; i++)
            {
                bool selected = (int)Gesture.Direction == i + 1;
                float r = selected ? 66 + Gesture.Progress * 20 : i == 3 ? 78 : 64;
                Vector2 d = i == 0 ? Vector2.up : i == 1 ? Vector2.left : i == 2 ? Vector2.right : Vector2.down;
                labelBackings[i].rectTransform.anchoredPosition = d * (r + 18);
                labelBackings[i].color = selected ? new Color(.94f, .82f, .51f, 1) : new Color(.025f, .06f, .10f, .96f);
                waveLabels[i].fontSize = selected ? 17 : 15;
                waveLabels[i].color = selected ? new Color(.04f, .07f, .10f, 1) : Color.white;
                labelBackings[i].gameObject.SetActive(!(serving && i == 0));
            }
        }
        void RefreshOpacity()
        {
            if (!configured)
                return;
            float alpha = GameSettings.Current.touchOpacity;
            bool ready = Receiver != null && Receiver.TouchShotsEnabled;
            bool serve = Receiver != null && Receiver.TouchServing;
            if (comboHints[1])
                comboHints[1].text = serve ? "GIAO BÓNG" : "↑ SMASH";
            for (int i = 1; i < 4; i++)
                if (comboHints[i])
                    comboHints[i].gameObject.SetActive(!Gesture.Active || activeControl != (i == 1 ? Drive : i == 2 ? Dink : Volley));
            groups[0].alpha = alpha * .48f;
            groups[1].alpha = alpha * (!ready ? .45f : Gesture.Active && activeControl != Drive ? .24f : 1);
            groups[2].alpha = alpha * (!ready || serve ? .3f : Gesture.Active && activeControl != Dink ? .24f : 1);
            groups[3].alpha = alpha * (!ready || serve ? .3f : Gesture.Active && activeControl != Volley ? .24f : 1);
            groups[4].alpha = runPointer != TouchShotGesture.NoPointer ? 1 : alpha;
        }
        void CancelShot()
        {
            Gesture.Cancel();
            activeControl = null;
            if (waveRoot)
                waveRoot.gameObject.SetActive(false);
            RefreshOpacity();
        }
        public void CancelAll()
        {
            CancelShot();
            joyPointer = runPointer = TouchShotGesture.NoPointer;
            if (Receiver != null)
            {
                Receiver.TouchMovement = Vector2.zero;
                Receiver.TouchRun = false;
                Receiver.TouchShotMask = 0;
            }
            if (Joystick && Joystick.knob)
                Joystick.knob.anchoredPosition = Vector2.zero;
            RefreshOpacity();
        }
        public bool Hit(TouchControl control, Vector2 screenPoint, Camera eventCamera)
        {
            var r = (RectTransform)control.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(r, screenPoint, eventCamera, out var p);
            // Circular, padded targets avoid the transparent square corners stealing other touches.
            return (p - r.rect.center).sqrMagnitude <= Mathf.Pow(r.rect.width * .54f, 2);
        }
    }
}
