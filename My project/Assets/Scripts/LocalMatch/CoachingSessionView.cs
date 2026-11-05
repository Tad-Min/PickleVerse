using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PickleVerse.Progression;
using PickleVerse.Services;
using PickleVerse.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.LocalMatch
{
    /// <summary>One result session, one owner, bounded polling. Never substitutes an earlier profile analysis.</summary>
    public sealed class CoachingSessionView : MonoBehaviour
    {
        string session, sessionType, owner;
        int generation;
        CancellationTokenSource cancellation;
        TMP_Text caption;
        GameObject details;
        RectTransform detailCard;
        CoachingSummary summary;
        Func<Task> beforePractice;
        public static CoachingSessionView Attach(RectTransform parent, string sessionId, string type, Vector2 min, Vector2 max, Func<Task> beforePractice = null)
        {
            var existing = parent.Find("SessionCoaching");
            var rect = existing ? (RectTransform)existing : Rect("SessionCoaching", parent, min, max);
            var view = rect.GetComponent<CoachingSessionView>() ?? rect.gameObject.AddComponent<CoachingSessionView>();
            view.beforePractice = beforePractice;
            if (!view.caption)
            {
                var image = rect.gameObject.AddComponent<Image>(); image.color = ScreenUiTheme.Mint;
                var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(view.Open);
                view.caption = Text(rect, "Caption", "Đang phân tích…", Vector2.zero, Vector2.one, 16, ScreenUiTheme.Ink);
            }
            if (view.session != sessionId || view.owner != AccountRuntime.Ensure().Account.Profile?.UserId.ToString("N"))
            {
                view.session = sessionId; view.sessionType = type;
                view.owner = AccountRuntime.Ensure().Account.Profile?.UserId.ToString("N");
                view.summary = null; view.Begin();
            }
            return view;
        }
        void OnEnable() { if (!string.IsNullOrEmpty(session)) Begin(); }
        void OnDisable() { generation++; cancellation?.Cancel(); if (details) details.SetActive(false); }
        void OnDestroy() { cancellation?.Cancel(); cancellation?.Dispose(); if (details) Destroy(details); }
        void Begin()
        {
            cancellation?.Cancel(); cancellation?.Dispose(); cancellation = new CancellationTokenSource();
            cancellation.CancelAfter(TimeSpan.FromSeconds(10));
            _ = Poll(++generation, cancellation.Token);
        }
        async Task Poll(int revision, CancellationToken token)
        {
            if (!Guid.TryParse(session, out var id) || !AccountRuntime.Ensure().Account.IsSignedIn) return;
            caption.text = "Đang phân tích…";
            try
            {
                for (int attempt = 0; attempt <= 10; attempt++)
                {
                    var result = await AccountRuntime.Ensure().Account.SendAuthorizedAsync<CoachingSummary>("GET",
                        "api/AiCoaching/summary?sessionId=" + id.ToString("N") + "&sessionType=" + sessionType, cancellation: token);
                    if (!this || revision != generation || owner != AccountRuntime.Ensure().Account.Profile?.UserId.ToString("N")) return;
                    if (result.status == "ready" && (!Guid.TryParse(result.analysis?.sessionId, out var analyzed) || analyzed != id))
                        throw new InvalidOperationException("Session mismatch.");
                    summary = result;
                    if (result.status == "ready" || result.status == "failed" || result.status == "disabled") break;
                    if (attempt < 10) await Task.Delay(1000, token);
                }
                if (this && revision == generation) caption.text = summary?.status == "ready" ? "Xem phân tích" : "HLV • Xem trạng thái";
            }
            catch (OperationCanceledException) { if (this && revision == generation) caption.text = "HLV • Xem lại trong Tiến trình"; }
            catch (Exception) { if (this && revision == generation) caption.text = "HLV • Xem lại trong Tiến trình"; }
        }
        void Open()
        {
            if (!details)
            {
                details = new GameObject("Coaching Analysis", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(details, gameObject.scene);
                details.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; details.GetComponent<Canvas>().sortingOrder = 31000;
                var scaler = details.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720);
                var shade = Rect("Shade", details.transform, Vector2.zero, Vector2.one); shade.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .85f);
                detailCard = Rect("Card", shade, new Vector2(.15f, .12f), new Vector2(.85f, .88f)); detailCard.gameObject.AddComponent<Image>().color = ScreenUiTheme.Ink;
            }
            details.SetActive(true);
            foreach (Transform child in detailCard) Destroy(child.gameObject);
            Text(detailCard, "Title", "HUẤN LUYỆN VIÊN", new Vector2(.05f, .86f), new Vector2(.95f, .98f), 26, ScreenUiTheme.Mint);
            var analysis = summary?.status == "ready" ? summary.analysis : null;
            string body = CoachingAnalysisText.Describe(analysis);
            Text(detailCard, "Findings", body, new Vector2(.05f, .57f), new Vector2(.95f, .85f), 18, ScreenUiTheme.Text);
            int row = 0;
            if (analysis?.recommendations != null) foreach (var drill in analysis.recommendations.Take(3))
            {
                string drillId = drill.id;
                var button = Rect("Drill" + row, detailCard, new Vector2(.05f, .43f - row * .12f), new Vector2(.95f, .54f - row * .12f));
                button.gameObject.AddComponent<Image>().color = new Color(.08f, .2f, .24f);
                var action = button.gameObject.AddComponent<Button>(); action.onClick.AddListener(async () =>
                {
                    action.interactable = false;
                    try { if (beforePractice != null) await beforePractice(); if (details) details.SetActive(false); TrainingLaunch.Open(drillId); }
                    catch (Exception) { if (action) action.interactable = true; }
                });
                Text(button, "Label", drill.title + " • Luyện ngay\n" + drill.reason, new Vector2(.02f, 0), new Vector2(.98f, 1), 14, ScreenUiTheme.Text);
                row++;
            }
            var close = Rect("Close", detailCard, new Vector2(.32f, .04f), new Vector2(.68f, .14f)); close.gameObject.AddComponent<Image>().color = ScreenUiTheme.Mint;
            close.gameObject.AddComponent<Button>().onClick.AddListener(() => details.SetActive(false));
            Text(close, "Label", "Đóng", Vector2.zero, Vector2.one, 18, ScreenUiTheme.Ink);
        }
        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform; rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
        }
        static TMP_Text Text(Transform parent, string name, string value, Vector2 min, Vector2 max, int size, Color color)
        {
            var text = Rect(name, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = ScreenUiTheme.Current?.font; text.text = value; text.fontSize = size; text.color = color;
            text.alignment = TextAlignmentOptions.Center; text.richText = false; text.raycastTarget = false; return text;
        }
    }
}
