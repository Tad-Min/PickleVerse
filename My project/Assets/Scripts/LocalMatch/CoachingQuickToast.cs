using PickleVerse.Settings;
using TMPro;
using UnityEngine;

namespace PickleVerse.LocalMatch
{
    public sealed class CoachingQuickToast : MonoBehaviour
    {
        TMP_Text label;
        float until;
        public static void Show(GameObject owner, string tip)
        {
            var view = owner.GetComponent<CoachingQuickToast>() ?? owner.AddComponent<CoachingQuickToast>();
            if (!view.label)
            {
                var canvas = new GameObject("Quick Coaching", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
                canvas.transform.SetParent(owner.transform, false);
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.GetComponent<Canvas>().sortingOrder = 50;
                var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720);
                var text = new GameObject("Tip", typeof(RectTransform), typeof(TextMeshProUGUI)); text.transform.SetParent(canvas.transform, false);
                var r = (RectTransform)text.transform; r.anchorMin = new Vector2(.22f, .15f); r.anchorMax = new Vector2(.78f, .24f); r.offsetMin = r.offsetMax = Vector2.zero;
                view.label = text.GetComponent<TMP_Text>(); view.label.font = ScreenUiTheme.Current?.font; view.label.fontSize = 19;
                view.label.color = ScreenUiTheme.Mint; view.label.alignment = TextAlignmentOptions.Center; view.label.raycastTarget = false; view.label.richText = false;
            }
            view.label.text = string.IsNullOrEmpty(tip) ? "" : "HLV • " + tip; view.until = Time.unscaledTime + 3;
        }
        void Update() { if (label && Time.unscaledTime >= until) label.text = ""; }
    }
}
