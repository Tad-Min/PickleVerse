using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.Settings
{
    public static class LobbyPanelStyle
    {
        public static readonly Color Background = new Color32(10, 17, 32, 255), Surface = new Color32(22, 35, 56, 255), Card = new Color32(26, 41, 64, 255);
        public static void SurfaceStyle(RectTransform rect, bool ornament = false, bool quiet = false)
        {
            if (!rect)
                return;
            var image = rect.GetComponent<Image>();
            if (image)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = Color.white;
                if (ScreenUiTheme.Current)
                    image.material = quiet ? ScreenUiTheme.Current.quiet : ScreenUiTheme.Current.panel;
            }
            var outline = rect.GetComponent<Outline>();
            if (outline)
                outline.enabled = false;
            if (ornament)
                LobbyEtching.AddTo(rect, quiet);
        }
        public static void Button(Button button, bool primary = false)
        {
            ScreenUiTheme.ButtonStyle(button, primary);
            if (button)
            {
                var c = button.colors;
                c.disabledColor = new Color(.65f, .69f, .75f, 1);
                button.colors = c;
            }
        }
        public static void Tab(Button button, bool selected)
        {
            if (!button)
                return;
            ScreenUiTheme.Selection(button, selected);
            var rect = (RectTransform)button.transform;
            var marker = rect.Find("Selection Mark") as RectTransform;
            if (!marker)
            {
                marker = new GameObject("Selection Mark", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                marker.SetParent(rect, false);
                marker.GetComponent<Image>().color = ScreenUiTheme.Gold;
                marker.GetComponent<Image>().raycastTarget = false;
            }
            marker.anchorMin = new Vector2(.08f, 0);
            marker.anchorMax = new Vector2(.92f, 0);
            marker.pivot = new Vector2(.5f, 0);
            marker.offsetMin = new Vector2(0, 1);
            marker.offsetMax = new Vector2(0, 3);
            marker.gameObject.SetActive(selected);
        }
        public static void Text(TMP_Text text, float size, Color tint, bool wrap = false)
        {
            if (!text)
                return;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.color = tint;
            text.raycastTarget = false;
            text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }
        public static void Place(RectTransform r, float x, float y, float right, float top)
        {
            r.anchorMin = new Vector2(x, y);
            r.anchorMax = new Vector2(right, top);
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
    }
}
