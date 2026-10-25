using TMPro;
using UnityEngine;

namespace PickleVerse.LocalMatch
{
    /// <summary>One small, reused overlay for the local player's confirmed hits.</summary>
    public sealed class HitGradeFeedback : MonoBehaviour
    {
        const float Lifetime = .82f;
        GameObject root;
        RectTransform panel;
        CanvasGroup opacity;
        TMP_Text label;
        Material textMaterial;
        MatchAnnouncements[] announcements;
        float age;
        public int ShowCount { get; private set; }
        public HitGrade LastGrade { get; private set; }
        public bool Visible => root && root.activeSelf;
        public string Caption => label ? label.text : "";

        public void Prepare()
        {
            if (root)
                return;
            Build();
            label.text = "Hoàn hảo";
            label.fontSize = 42;
            label.ForceMeshUpdate();
            label.text = "Tốt";
            label.fontSize = 34;
            label.ForceMeshUpdate();
            Clear();
        }

        void Build()
        {
            TMP_FontAsset font = null;
            foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (text.font && text.font.HasCharacters("Hoàn hảoTốt"))
                {
                    font = text.font;
                    break;
                }
            root = new GameObject("Local hit rating", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(CanvasGroup));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 15;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            opacity = root.GetComponent<CanvasGroup>();
            opacity.blocksRaycasts = opacity.interactable = false;
            var go = new GameObject("Contact grade", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(root.transform, false);
            label = go.GetComponent<TMP_Text>();
            panel = (RectTransform)go.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, .63f);
            panel.pivot = new Vector2(.5f, .5f);
            panel.sizeDelta = new Vector2(380, 80);
            label.font = font ? font : TMP_Settings.defaultFontAsset;
            label.fontStyle = FontStyles.Bold | FontStyles.Italic;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            textMaterial = new Material(label.fontSharedMaterial);
            textMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, .16f);
            textMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(.055f, .075f, .11f, .95f));
            label.fontSharedMaterial = textMaterial;
            announcements = FindObjectsByType<MatchAnnouncements>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }
        public void Show(HitGrade grade)
        {
            if (grade == HitGrade.None)
                return;
            if (!root)
                Prepare();
            enabled = true;
            LastGrade = grade;
            ShowCount++;
            age = 0;
            label.text = grade == HitGrade.Perfect ? "Hoàn hảo" : "Tốt";
            label.fontSize = grade == HitGrade.Perfect ? 42 : 34;
            root.SetActive(true);
            label.ForceMeshUpdate();
            Render();
        }
        void Update()
        {
            Advance(Time.deltaTime);
        }
        public void Advance(float dt)
        {
            if (!Visible)
                return;
            age += Mathf.Max(0, dt);
            if (age >= Lifetime)
            {
                Clear();
                return;
            }
            Render();
        }
        void Render()
        {
            // Referee/lesson banners take priority over this short contact cue.
            bool covered = false;
            foreach (var notice in announcements)
                if (notice && notice.Busy)
                {
                    covered = true;
                    break;
                }
            float entry = Mathf.Clamp01(age / .09f), exit = Mathf.Clamp01((Lifetime - age) / .2f);
            opacity.alpha = covered ? 0 : entry * exit;
            panel.localScale = Vector3.one * Mathf.Lerp(1.16f, 1, Mathf.SmoothStep(0, 1, entry));
            panel.anchoredPosition = new Vector2(0, Mathf.Lerp(-8, 10, age / Lifetime));
            bool perfect = LastGrade == HitGrade.Perfect;
            Color bottom = perfect ? new Color(1, .43f, .075f) : new Color(.55f, .91f, .83f);
            Color top = perfect ? new Color(1, .94f, .63f) : new Color(.93f, 1, .98f);
            var info = label.textInfo;
            var bounds = label.textBounds;
            for (int i = 0; i < info.meshInfo.Length; i++)
            {
                var mesh = info.meshInfo[i];
                for (int v = 0; v < mesh.vertexCount; v++)
                {
                    Vector3 p = mesh.vertices[v];
                    float y = Mathf.InverseLerp(bounds.min.y, bounds.max.y, p.y);
                    Color color = Color.Lerp(bottom, top, y);
                    if (perfect)
                    {
                        float x = Mathf.InverseLerp(bounds.min.x, bounds.max.x, p.x);
                        float shine = Mathf.Clamp01(1 - Mathf.Abs(x - Mathf.Lerp(-.25f, 1.25f, age / .6f)) / .16f);
                        color = Color.Lerp(color, new Color(1, 1, .93f), shine * .9f);
                    }
                    mesh.colors32[v] = color;
                }
            }
            label.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }
        public void Clear()
        {
            if (root)
                root.SetActive(false);
            enabled = false;
        }
        void OnDisable()
        {
            if (root)
                root.SetActive(false);
        }
        void OnDestroy()
        {
            if (root)
                Destroy(root);
            if (textMaterial)
                Destroy(textMaterial);
        }
    }
}
