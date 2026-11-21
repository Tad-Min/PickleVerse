#if !UNITY_SERVER
using PickleVerse.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.FrontEnd
{
    /// <summary>"Thoát game" in the top-right corner of the login screen, beside the edition label.</summary>
    internal static class LoginExitButton
    {
        const string Name = "Quit Game";
        static readonly Vector2 Size = new Vector2(190, 46);

        public static void Attach(AccountPanelView view)
        {
            if (!GameExit.Supported || !view || !view.safeArea || view.safeArea.Find(Name))
                return;
            var safe = view.safeArea;
            var theme = ScreenUiTheme.Current;
            var rect = new GameObject(Name, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            rect.SetParent(safe, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-34, -26);
            rect.sizeDelta = Size;

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(UiLineIcon)).GetComponent<UiLineIcon>();
            icon.transform.SetParent(rect, false);
            icon.glyph = ArenaGlyph.Power;
            icon.stroke = .1f;
            icon.color = ScreenUiTheme.Gold;
            icon.raycastTarget = false;
            var iconRect = icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0, .5f);
            iconRect.anchoredPosition = new Vector2(30, 0);
            iconRect.sizeDelta = new Vector2(20, 20);

            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(rect, false);
            if (theme && theme.font)
                label.font = theme.font;
            label.text = "THOÁT GAME";
            label.fontSize = 16;
            label.characterSpacing = 2;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(44, 0);
            labelRect.offsetMax = new Vector2(-12, 0);

            var button = rect.GetComponent<Button>();
            LobbyPanelStyle.Button(button);
            if (!theme)
                rect.GetComponent<Image>().color = LobbyPanelStyle.Card;
            button.onClick.AddListener(GameExit.Request);
            // Keyboard focus starts in the form; the quit button is reached with the mouse or by navigating up.
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            // The edition label shared this corner; move it left of the button.
            if (safe.Find("Welcome") is RectTransform welcome && welcome.anchorMin == Vector2.one)
                welcome.anchoredPosition = new Vector2(-34 - Size.x - 22, welcome.anchoredPosition.y);
        }
    }
}
#endif
