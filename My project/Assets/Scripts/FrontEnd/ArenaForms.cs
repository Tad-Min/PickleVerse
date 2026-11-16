#if !UNITY_SERVER
using PickleVerse.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PickleVerse.FrontEnd
{
    /// <summary>
    /// Code-built UI in the game's "Arena Glass" language (bevelled glass panels, champagne primary action,
    /// quiet glass secondary actions, gold headings, line icons, engraved ornament) for overlays that are
    /// created at runtime. Reference layout 1920x1080, same as the authored screens.
    /// </summary>
    public static class ArenaForms
    {
        public const float TitleSize = 34, HeadingSize = 24, BodySize = 22, CaptionSize = 18, HintSize = 16;
        public static readonly Color Error = new Color32(255, 150, 140, 255), Success = ScreenUiTheme.Mint, Warning = new Color32(255, 200, 120, 255);
        public static readonly Color Dim = new Color(.01f, .02f, .05f, .78f);

        public static TMP_FontAsset Font => ScreenUiTheme.Current && ScreenUiTheme.Current.font ? ScreenUiTheme.Current.font : TMP_Settings.defaultFontAsset;

        public static GameObject Canvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            return go;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Anchor to a fixed point with a fixed size (pixels in the 1920x1080 reference).</summary>
        public static RectTransform Pin(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = pivot;
            r.anchoredPosition = position;
            r.sizeDelta = size;
            return r;
        }

        /// <summary>Stretch between anchors with pixel insets (left, bottom, right, top).</summary>
        public static RectTransform Fill(RectTransform r, Vector2 min, Vector2 max, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            r.anchorMin = min;
            r.anchorMax = max;
            r.offsetMin = new Vector2(left, bottom);
            r.offsetMax = new Vector2(-right, -top);
            return r;
        }

        /// <summary>Full-screen dimmer that fades the dialog in each time it is activated.</summary>
        public static RectTransform Cover(string name, Transform parent)
        {
            var rect = Rect(name, parent);
            Fill(rect, Vector2.zero, Vector2.one);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Dim;
            rect.gameObject.AddComponent<CanvasGroup>();
            rect.gameObject.AddComponent<UiPanelReveal>();
            return rect;
        }

        /// <summary>Obsidian glass surface with the engraved court ornament.</summary>
        public static RectTransform Surface(string name, Transform parent, bool ornament = true, bool quiet = false)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = LobbyPanelStyle.Surface;
            LobbyPanelStyle.SurfaceStyle(rect, false, quiet);
            if (ornament)
                LobbyEtching.AddTo(rect, quiet);
            return rect;
        }

        /// <summary>Recessed quiet-glass well for long text and inputs.</summary>
        public static RectTransform Inset(string name, Transform parent)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = LobbyPanelStyle.Background;
            LobbyPanelStyle.SurfaceStyle(rect, false, true);
            return rect;
        }

        public static TMP_Text Label(string name, Transform parent, string value, float size, Color colour,
            TextAlignmentOptions align = TextAlignmentOptions.Left, bool wrap = false)
        {
            var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Font;
            text.text = value;
            text.richText = false;
            text.fontSize = size;
            text.color = colour;
            text.alignment = align;
            text.raycastTarget = false;
            text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            text.lineSpacing = wrap ? 6 : 0;
            return text;
        }

        public static UiLineIcon Icon(string name, Transform parent, ArenaGlyph glyph, Color colour, float stroke = .08f)
        {
            var icon = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UiLineIcon)).GetComponent<UiLineIcon>();
            icon.transform.SetParent(parent, false);
            icon.glyph = glyph;
            icon.stroke = stroke;
            icon.color = colour;
            icon.raycastTarget = false;
            return icon;
        }

        /// <summary>A thin gold rule (used under headings and on top of banners).</summary>
        public static Image Rule(string name, Transform parent, Color colour)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Champagne (primary) or glass (secondary) action with an optional leading icon.</summary>
        public static Button Action(string name, Transform parent, string label, bool primary, UnityAction onClick, ArenaGlyph? glyph = null, float size = HeadingSize)
        {
            var rect = Rect(name, parent);
            rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            var text = Label("Label", rect, label, size, primary ? ScreenUiTheme.Ink : ScreenUiTheme.Text, TextAlignmentOptions.Center);
            if (glyph.HasValue)
            {
                var icon = Icon("Icon", rect, glyph.Value, primary ? ScreenUiTheme.Ink : ScreenUiTheme.Gold);
                Pin(icon.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(18, 0), new Vector2(22, 22));
                Fill(text.rectTransform, Vector2.zero, Vector2.one, 44, 0, 14, 0);
            }
            else
                Fill(text.rectTransform, Vector2.zero, Vector2.one, 12, 0, 12, 0);
            LobbyPanelStyle.Button(button, primary);
            if (!ScreenUiTheme.Current)
                rect.GetComponent<Image>().color = primary ? ScreenUiTheme.Gold : LobbyPanelStyle.Card;
            if (onClick != null)
                button.onClick.AddListener(onClick);
            return button;
        }

        /// <summary>Square glass button that carries only an icon (close, previous, next).</summary>
        public static Button IconButton(string name, Transform parent, ArenaGlyph glyph, UnityAction onClick)
        {
            var rect = Rect(name, parent);
            rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            var icon = Icon("Icon", rect, glyph, ScreenUiTheme.Text, .09f);
            Fill(icon.rectTransform, Vector2.zero, Vector2.one, 14, 14, 14, 14);
            LobbyPanelStyle.Button(button, false);
            if (!ScreenUiTheme.Current)
                rect.GetComponent<Image>().color = LobbyPanelStyle.Card;
            if (onClick != null)
                button.onClick.AddListener(onClick);
            return button;
        }

        /// <summary>
        /// Large two-line choice: icon, title and a one-line explanation. Primary cards use the champagne
        /// action surface so the recommended choice is obvious at a glance.
        /// </summary>
        public static Button Choice(string name, Transform parent, ArenaGlyph glyph, string title, string caption, bool primary, UnityAction onClick)
        {
            var rect = Rect(name, parent);
            rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            Color ink = primary ? ScreenUiTheme.Ink : ScreenUiTheme.Text;
            var icon = Icon("Icon", rect, glyph, primary ? ScreenUiTheme.Ink : ScreenUiTheme.Gold, .085f);
            Pin(icon.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(26, 0), new Vector2(44, 44));
            var head = Label("Title", rect, title, 27, ink);
            Fill(head.rectTransform, new Vector2(0, .5f), new Vector2(1, 1), 88, 0, 18, 14);
            head.alignment = TextAlignmentOptions.BottomLeft;
            var body = Label("Caption", rect, caption, CaptionSize, primary ? new Color(ink.r, ink.g, ink.b, .86f) : ScreenUiTheme.Muted, TextAlignmentOptions.TopLeft, true);
            Fill(body.rectTransform, new Vector2(0, 0), new Vector2(1, .5f), 88, 12, 18, 6);
            LobbyPanelStyle.Button(button, primary);
            if (!ScreenUiTheme.Current)
                rect.GetComponent<Image>().color = primary ? ScreenUiTheme.Gold : LobbyPanelStyle.Card;
            // Keep the label colours authored above (ButtonStyle tints every label with the same ink).
            head.color = ink;
            body.color = primary ? new Color(ink.r, ink.g, ink.b, .86f) : ScreenUiTheme.Muted;
            if (onClick != null)
                button.onClick.AddListener(onClick);
            return button;
        }

        /// <summary>Multi-line input in a recessed well with a gold caret and readable placeholder.</summary>
        public static TMP_InputField Input(string name, Transform parent, string placeholder, int limit)
        {
            var well = Inset(name, parent);
            var viewport = Rect("Viewport", well);
            Fill(viewport, Vector2.zero, Vector2.one, 20, 14, 20, 14);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = Label("Text", viewport, "", BodySize, ScreenUiTheme.Text, TextAlignmentOptions.TopLeft, true);
            Fill(text.rectTransform, Vector2.zero, Vector2.one);
            var hint = Label("Placeholder", viewport, placeholder, BodySize, new Color(ScreenUiTheme.Muted.r, ScreenUiTheme.Muted.g, ScreenUiTheme.Muted.b, .8f), TextAlignmentOptions.TopLeft, true);
            hint.fontStyle = FontStyles.Italic;
            Fill(hint.rectTransform, Vector2.zero, Vector2.one);
            var input = well.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = viewport;
            input.textComponent = (TextMeshProUGUI)text;
            input.placeholder = hint;
            input.lineType = TMP_InputField.LineType.MultiLineNewline;
            input.characterLimit = limit;
            input.richText = false;
            input.caretColor = ScreenUiTheme.Gold;
            input.customCaretColor = true;
            input.caretWidth = 2;
            input.selectionColor = new Color(ScreenUiTheme.Gold.r, ScreenUiTheme.Gold.g, ScreenUiTheme.Gold.b, .35f);
            input.targetGraphic = well.GetComponent<Image>();
            return input;
        }

        /// <summary>Scrollable read-only text in a recessed well (mouse wheel and drag).</summary>
        public static TMP_Text ScrollText(string name, Transform parent, out ScrollRect scroll)
        {
            var well = Inset(name, parent);
            var viewport = Rect("Viewport", well);
            Fill(viewport, Vector2.zero, Vector2.one, 24, 18, 24, 18);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = Label("Details", viewport, "", BodySize, ScreenUiTheme.Text, TextAlignmentOptions.TopLeft, true);
            var content = text.rectTransform;
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = well.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32;
            return text;
        }

        /// <summary>Assign text only when it changed, so per-frame refresh paths never allocate or re-mesh.</summary>
        public static void Set(TMP_Text text, string value)
        {
            if (text && !string.Equals(text.text, value))
                text.text = value;
        }

        public static void Show(Component target, bool visible)
        {
            if (target && target.gameObject.activeSelf != visible)
                target.gameObject.SetActive(visible);
        }
    }
}
#endif
