using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PickleVerse.Settings
{
    /// <summary>"Quit the game?" confirmation shared by the login screen and the window close button.</summary>
    public sealed class GameExitPrompt : MonoBehaviour
    {
        static GameExitPrompt instance;
        public static bool IsOpen => instance && instance.root && instance.root.activeSelf;
        GameObject root, events;
        TMP_Text message;
        Button stay, leave;
        bool quitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
        }
        public static void Show()
        {
            if (!instance)
            {
                var go = new GameObject("Game Exit Prompt");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<GameExitPrompt>();
                instance.Build();
            }
            instance.Open();
        }
        public static void Hide()
        {
            if (instance)
                instance.Close();
        }

        void Open()
        {
            if (IsOpen)
                return;
            quitting = false;
            message.text = "Bạn có chắc muốn thoát game không?";
            stay.interactable = leave.interactable = true;
            root.SetActive(true);
            FrontEndModalInput.TryAcquire(this);
            if (!EventSystem.current)
            {
                // Some scenes (loading screens) have no EventSystem; borrow one only while the prompt is open.
                events = new GameObject("Exit Prompt EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(stay.gameObject);
        }
        void Close()
        {
            if (!root || quitting)
                return;
            root.SetActive(false);
            FrontEndModalInput.Release(this);
            if (events)
                Destroy(events);
            events = null;
        }
        void Confirm()
        {
            if (quitting)
                return;
            quitting = true;
            message.text = "Đang thoát…";
            stay.interactable = leave.interactable = false;
            GameExit.Quit();
        }
        void Update()
        {
            if (!IsOpen || quitting)
                return;
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            // Enter/Submit goes through the selected button; Esc always means "stay".
            if (keyboard.escapeKey.wasPressedThisFrame)
                Close();
        }

        void Build()
        {
            var theme = ScreenUiTheme.Current;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32600;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();

            root = new GameObject("Cover", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(UiPanelReveal));
            root.transform.SetParent(transform, false);
            var cover = (RectTransform)root.transform;
            cover.anchorMin = Vector2.zero;
            cover.anchorMax = Vector2.one;
            cover.offsetMin = cover.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(.01f, .02f, .05f, .74f);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            panel.SetParent(cover, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.sizeDelta = new Vector2(700, 330);
            LobbyPanelStyle.SurfaceStyle(panel);
            if (!theme)
                panel.GetComponent<Image>().color = LobbyPanelStyle.Surface;
            LobbyEtching.AddTo(panel, false);

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(UiLineIcon)).GetComponent<UiLineIcon>();
            icon.transform.SetParent(panel, false);
            icon.glyph = ArenaGlyph.Power;
            icon.stroke = .08f;
            icon.color = ScreenUiTheme.Gold;
            icon.raycastTarget = false;
            Place(icon.rectTransform, new Vector2(.5f, 1), new Vector2(0, -58), new Vector2(40, 40));

            var title = Text("Title", panel, "Thoát PickleVerse?", 36, ScreenUiTheme.Gold, theme);
            Place(title.rectTransform, new Vector2(.5f, 1), new Vector2(0, -116), new Vector2(620, 50));
            message = Text("Message", panel, "", 23, ScreenUiTheme.Text, theme);
            Place(message.rectTransform, new Vector2(.5f, 1), new Vector2(0, -166), new Vector2(620, 40));

            stay = PromptButton("Stay", panel, "Ở lại", false, new Vector2(-150, 62), theme);
            leave = PromptButton("Quit", panel, "Thoát game", true, new Vector2(150, 62), theme);
            stay.onClick.AddListener(Close);
            leave.onClick.AddListener(Confirm);
            var nav = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = leave };
            stay.navigation = nav;
            leave.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = stay };
            root.SetActive(false);
        }
        static TMP_Text Text(string name, Transform parent, string value, float size, Color colour, ScreenUiTheme theme)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            if (theme && theme.font)
                text.font = theme.font;
            text.text = value;
            text.fontSize = size;
            text.color = colour;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }
        static Button PromptButton(string name, Transform parent, string label, bool primary, Vector2 position, ScreenUiTheme theme)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Place(rect, new Vector2(.5f, 0), position, new Vector2(270, 64));
            var text = Text("Label", rect, label, 24, primary ? ScreenUiTheme.Ink : ScreenUiTheme.Text, theme);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            var button = rect.GetComponent<Button>();
            LobbyPanelStyle.Button(button, primary);
            if (!theme)
                rect.GetComponent<Image>().color = primary ? ScreenUiTheme.Gold : LobbyPanelStyle.Card;
            return button;
        }
        static void Place(RectTransform r, Vector2 anchor, Vector2 position, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = new Vector2(.5f, .5f);
            r.anchoredPosition = position;
            r.sizeDelta = size;
        }
        void OnDestroy()
        {
            if (instance == this)
            {
                FrontEndModalInput.Release(this);
                instance = null;
            }
        }
    }
}
