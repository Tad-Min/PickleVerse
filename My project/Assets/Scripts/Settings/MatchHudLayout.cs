using UnityEngine;

namespace PickleVerse.Settings
{
    // Retain the original HUD root paths and cinematic visibility lists.
    // Only corner offsets change when the device safe area changes.
    public sealed class MatchHudLayout : MonoBehaviour
    {
        public RectTransform scoreboard, notice, settings, help, touch, timeout, leave, returnToRoom, technique;
        public TMPro.TMP_Text settingsKey, helpKey, timeoutKey;
        public void RefreshKeys()
        {
            string Name(GameAction action)
            {
                string name = GameSettings.KeyName(GameSettings.Current.KeyFor(action));
                return name == "Escape" ? "Esc" : name;
            }
            settingsKey.text = Name(GameAction.Settings);
            helpKey.text = Name(GameAction.Help);
            timeoutKey.text = Name(GameAction.Timeout);
        }
        Canvas canvas; Rect last; Vector2 size; float lastScale; bool refreshText;
        TMPro.TMP_Text[] labels;
        void OnEnable()
        {
            canvas = GetComponent<Canvas>();
            size = Vector2.zero;
            Apply();
        }
        void Update()
        {
            Apply();
        }
        void LateUpdate()
        {
            if (!refreshText || !scoreboard)
                return;
            refreshText = false;
            // Fixed-size HUD labels can keep their rect while their screen scale
            // changes. Rebind SDF geometry once after CanvasScaler has updated.
            Canvas.ForceUpdateCanvases();
            if (labels == null)
                labels = scoreboard.GetComponentsInChildren<TMPro.TMP_Text>(true);
            foreach (var text in labels)
                if (text && text.isActiveAndEnabled)
                {
                    text.SetVerticesDirty();
                    text.SetMaterialDirty();
                    text.ForceMeshUpdate();
                }
        }
        void Apply()
        {
            var screen = new Vector2(Screen.width, Screen.height);
            var area = Screen.safeArea;
            if (!canvas)
                canvas = GetComponent<Canvas>();
            float scale = Mathf.Max(.01f, canvas ? canvas.scaleFactor : 1);
            if (screen == size && area == last && Mathf.Approximately(scale, lastScale))
                return;
            size = screen;
            last = area;
            lastScale = scale;
            refreshText = true;
            ApplyInsets(area.xMin / scale, (screen.x - area.xMax) / scale, (screen.y - area.yMax) / scale, area.yMin / scale);
        }
        public void ApplyInsets(float left, float right, float top, float bottom)
        {
            Place(scoreboard, left + 18, -top - 18);
            Place(timeout, left + 18, -top - 194);
            Place(notice, left + 18, -top - 238);
            Place(settings, -right - 18, -top - 18);
            Place(help, -right - 96, -top - 70);
            Place(touch, -right - 18, -top - 70);
            Place(leave, -right - 18, -top - 116);
            Place(returnToRoom, -right - 18, -top - 116);
            Place(technique, 0, bottom + 18);
        }
        static void Place(RectTransform rect, float x, float y)
        {
            if (rect)
                rect.anchoredPosition = new Vector2(x, y);
        }
    }
}
