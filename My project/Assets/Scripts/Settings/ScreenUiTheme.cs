using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.Settings
{
    [CreateAssetMenu(menuName = "PickleVerse/UI/Screen theme")]
    public sealed class ScreenUiTheme : ScriptableObject
    {
        public TMP_FontAsset font;
        public Material panel, quiet, action, background;
        public static readonly Color Ink = new Color32(20, 30, 47, 255), Text = new Color32(241, 245, 255, 255), Muted = new Color32(164, 181, 207, 255), Mint = new Color32(143, 233, 216, 255), Gold = new Color32(232, 205, 158, 255);
        static ScreenUiTheme cached;
        public static ScreenUiTheme Current => cached ? cached : (cached = Resources.Load<ScreenUiTheme>("ScreenUiTheme"));
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            cached = null;
        }
        public static void ButtonStyle(Button button, bool primary = false)
        {
            var theme = Current;
            if (!theme || !button)
                return;
            var image = button.GetComponent<Image>();
            if (!image)
                image = button.gameObject.AddComponent<Image>();
            image.sprite = null;
            image.material = primary ? theme.action : theme.panel;
            image.color = Color.white;
            image.raycastTarget = true;
            button.targetGraphic = image;
            var c = button.colors;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
            c.selectedColor = new Color(1.08f, 1.08f, 1.08f);
            c.pressedColor = new Color(.73f, .82f, .90f);
            c.disabledColor = new Color(.4f, .45f, .54f, .7f);
            c.fadeDuration = .12f;
            button.colors = c;
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
            {
                label.color = primary ? Ink : Text;
                label.raycastTarget = false;
            }
            if (!button.GetComponent<UiInteractionVfx>())
                button.gameObject.AddComponent<UiInteractionVfx>();
        }
        public static void Selection(Button button, bool active)
        {
            if (!button)
                return;
            var image = button.GetComponent<Image>();
            if (Current && image)
            {
                image.material = active ? Current.panel : Current.quiet;
                image.color = Color.white;
            }
            foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
                text.color = active ? Gold : Muted;
            var marker = button.transform.Find("Selection Mark");
            if (marker)
                marker.gameObject.SetActive(active);
        }
    }
}
