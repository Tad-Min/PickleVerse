using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    /// <summary>
    /// AI coaching of one result session (bot match, online match or drill): one owner, bounded polling,
    /// never an earlier profile analysis. The panel refreshes itself while the analysis is still running.
    /// </summary>
    public sealed class CoachingSessionView : MonoBehaviour
    {
        const float PollSeconds = 90;
        static readonly Color Panel = new Color32(17, 27, 43, 250), Tile = new Color32(24, 39, 58, 255),
            Fault = new Color32(255, 112, 112, 255), Good = new Color32(120, 224, 160, 255);
        string session, sessionType, owner;
        int generation;
        bool polling, timedOut;
        CancellationTokenSource cancellation;
        TMP_Text caption, loading;
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
                view.caption = Text(rect, "Caption", "Đang phân tích…", Vector2.zero, Vector2.one, 16, ScreenUiTheme.Ink, true);
            }
            if (view.session != sessionId || view.owner != AccountRuntime.Ensure().Account.Profile?.UserId.ToString("N"))
            {
                view.session = sessionId; view.sessionType = type;
                view.owner = AccountRuntime.Ensure().Account.Profile?.UserId.ToString("N");
                view.summary = null; view.Begin();
            }
            return view;
        }

        void OnEnable() { if (!string.IsNullOrEmpty(session) && summary?.status != "ready") Begin(); }
        void OnDisable() { generation++; polling = false; cancellation?.Cancel(); if (details) details.SetActive(false); }
        void OnDestroy() { cancellation?.Cancel(); cancellation?.Dispose(); if (details) Destroy(details); }

        void Update()
        {
            if (loading && details && details.activeSelf)
                loading.text = LoadingText + new string('.', 1 + (int)(Time.unscaledTime * 2.5f) % 3);
        }

        string LoadingText => sessionType == "training" ? "Đang phân tích dữ liệu buổi tập" : "Đang phân tích dữ liệu trận đấu";

        void Begin()
        {
            cancellation?.Cancel(); cancellation?.Dispose(); cancellation = new CancellationTokenSource();
            cancellation.CancelAfter(TimeSpan.FromSeconds(PollSeconds + 15));
            timedOut = false;
            _ = Poll(++generation, cancellation.Token);
        }

        async Task Poll(int revision, CancellationToken token)
        {
            // Offline sessions are analysed only after they are synchronised; never leave a
            // permanent "analysing" caption when no request can be made.
            if (!AccountRuntime.Ensure().Account.IsSignedIn) { summary = null; caption.text = "HLV • Đăng nhập để xem phân tích"; Refresh(); return; }
            if (!Guid.TryParse(session, out var id)) { summary = null; caption.text = "HLV • Xem lại trong Tiến trình"; Refresh(); return; }
            polling = true;
            caption.text = "Đang phân tích…";
            Refresh();
            float started = Time.unscaledTime;
            try
            {
                // A bot match is uploaded only after the API has saved its result, so the analysis can
                // take tens of seconds: poll for up to 90 s, faster at first.
                for (int attempt = 0; ; attempt++)
                {
                    CoachingSummary result = null;
                    try
                    {
                        result = await AccountRuntime.Ensure().Account.SendAuthorizedAsync<CoachingSummary>("GET",
                            "api/AiCoaching/summary?sessionId=" + id.ToString("N") + "&sessionType=" + sessionType, cancellation: token);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception) { /* a dropped request must not end the wait: the next attempt asks again */ }
                    if (!this || revision != generation || owner != AccountRuntime.Ensure().Account.Profile?.UserId.ToString("N")) return;
                    if (result != null)
                    {
                        if (result.status == "ready" && (!Guid.TryParse(result.analysis?.sessionId, out var analyzed) || analyzed != id))
                            throw new InvalidOperationException("Session mismatch.");
                        summary = result;
                        if (result.status == "ready" || result.status == "failed" || result.status == "disabled") break;
                    }
                    if (Time.unscaledTime - started > PollSeconds) { timedOut = true; break; }
                    await Task.Delay(attempt < 20 ? 1500 : 3000, token);
                }
            }
            catch (OperationCanceledException) { if (this && revision == generation) timedOut = true; }
            catch (Exception) { if (this && revision == generation) timedOut = true; }
            finally
            {
                if (this && revision == generation)
                {
                    polling = false;
                    caption.text = summary?.status == "ready" ? "Xem phân tích AI" : summary?.status == "disabled" ? "HLV • AI đang tạm tắt"
                        : summary?.status == "failed" ? "HLV • Chưa phân tích được" : "HLV • Xem lại trong Tiến trình";
                    Refresh();
                }
            }
        }

        void Refresh() { if (details && details.activeSelf) Render(); }

        void Open()
        {
            if (!details)
            {
                details = new GameObject("Coaching Analysis", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(details, gameObject.scene);
                details.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; details.GetComponent<Canvas>().sortingOrder = 31000;
                var scaler = details.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
                var shade = Rect("Shade", details.transform, Vector2.zero, Vector2.one); shade.gameObject.AddComponent<Image>().color = new Color(.01f, .02f, .04f, .82f);
                detailCard = Rect("Card", shade, new Vector2(.1f, .07f), new Vector2(.9f, .93f));
                Surface(detailCard, Panel, true);
            }
            details.SetActive(true);
            // Opening the panel of an unfinished analysis starts waiting again instead of showing a stale message.
            if (!polling && summary?.status != "ready" && AccountRuntime.Ensure().Account.IsSignedIn && Guid.TryParse(session, out _)) Begin();
            Render();
        }

        void Render()
        {
            loading = null;
            foreach (Transform child in detailCard) Destroy(child.gameObject);
            Text(detailCard, "Caption", "PICKLEVERSE / HUẤN LUYỆN VIÊN AI", new Vector2(.04f, .91f), new Vector2(.96f, .97f), 16, ScreenUiTheme.Mint, true, TextAlignmentOptions.Left);
            Text(detailCard, "Title", sessionType == "training" ? "PHÂN TÍCH BUỔI TẬP" : "PHÂN TÍCH TRẬN ĐẤU",
                new Vector2(.04f, .82f), new Vector2(.96f, .91f), 32, ScreenUiTheme.Text, true, TextAlignmentOptions.Left);
            var analysis = summary?.status == "ready" ? summary.analysis : null;
            if (analysis != null) RenderAnalysis(analysis);
            else RenderState();
            var close = Action(detailCard, "Close", "ĐÓNG", new Vector2(.36f, .035f), new Vector2(.64f, .115f), true);
            close.onClick.AddListener(() => details.SetActive(false));
        }

        void RenderState()
        {
            string title, body;
            bool retry = false;
            if (!AccountRuntime.Ensure().Account.IsSignedIn)
            { title = "Cần đăng nhập"; body = "Phân tích của HLV cần kết nối mạng. Kết quả luyện tập ngoại tuyến được phân tích sau khi bạn đăng nhập và đồng bộ."; }
            else if (polling)
            {
                loading = Text(detailCard, "Loading", LoadingText + "…", new Vector2(.08f, .5f), new Vector2(.92f, .62f), 26, ScreenUiTheme.Mint, true);
                Text(detailCard, "Hint", "HLV đang chấm từng cú đánh của bạn: thời điểm tiếp xúc, điểm rơi và lỗi luật.\nKết quả sẽ hiện ngay tại đây khi xong.",
                    new Vector2(.08f, .34f), new Vector2(.92f, .5f), 17, ScreenUiTheme.Muted);
                return;
            }
            else if (summary?.status == "disabled") { title = "Phân tích AI đang tạm tắt"; body = "Máy chủ chưa bật phân tích AI. Kết quả trận vẫn được lưu trong Lịch sử."; }
            else if (summary?.status == "failed") { title = "Chưa phân tích được trận này"; body = "Dịch vụ AI gặp lỗi khi chấm dữ liệu. Bạn có thể thử lại hoặc xem trong Tiến trình luyện tập."; retry = true; }
            else { title = "Phân tích chưa xong"; body = "Dữ liệu trận đang được gửi hoặc xếp hàng chấm. Bạn có thể chờ thêm hoặc xem lại trong Tiến trình luyện tập."; retry = timedOut; }
            Text(detailCard, "StateTitle", title, new Vector2(.08f, .54f), new Vector2(.92f, .64f), 26, ScreenUiTheme.Gold, true);
            Text(detailCard, "StateBody", body, new Vector2(.1f, .36f), new Vector2(.9f, .54f), 17, ScreenUiTheme.Text);
            if (retry)
            {
                var again = Action(detailCard, "Retry", "THỬ LẠI", new Vector2(.4f, .2f), new Vector2(.6f, .28f), false);
                again.onClick.AddListener(() => { Begin(); Render(); });
            }
        }

        void RenderAnalysis(CoachingAnalysis analysis)
        {
            var stats = Stats.Of(analysis);
            string[] labels = { "LƯỢT ĐÁNH", "CÚ CHUẨN", "TỈ LỆ CHUẨN", "LỖI GHI NHẬN" };
            string[] values = { stats.shots.ToString(), stats.clean.ToString(), stats.shots > 0 ? Mathf.RoundToInt(100f * stats.clean / stats.shots) + "%" : "–", stats.faults.ToString() };
            Color[] tints = { ScreenUiTheme.Text, Good, Good, stats.faults > 0 ? Fault : Good };
            for (int i = 0; i < 4; i++)
            {
                float x = .04f + i * .23375f;
                var tile = Rect("Stat" + i, detailCard, new Vector2(x, .665f), new Vector2(x + .21875f, .81f)); Surface(tile, Tile);
                Text(tile, "Value", values[i], new Vector2(0, .38f), new Vector2(1, .95f), 34, tints[i], true);
                Text(tile, "Label", labels[i], new Vector2(0, .06f), new Vector2(1, .38f), 13, ScreenUiTheme.Muted, true);
            }

            // Rule faults outside the measured criteria are not in the analysis, so say what the number means.
            Text(detailCard, "StatNote", "Cú chuẩn là cú đánh không vướng lỗi nào trong các tiêu chí HLV đo được.",
                new Vector2(.04f, .622f), new Vector2(.96f, .657f), 12, ScreenUiTheme.Muted, false, TextAlignmentOptions.Left);

            // Left column: every fault of this session, worst first, each with its own advice.
            var faults = (analysis.metrics ?? new List<TechniqueMetric>()).GroupBy(m => m.code)
                .Select(g => (code: g.Key, errors: g.Sum(m => m.errors), opportunities: g.Sum(m => m.opportunities),
                    shots: string.Join(" · ", g.Where(m => m.errors > 0).OrderByDescending(m => m.errors).Take(3).Select(m => CoachingAnalysisText.Technique(m.technique) + " " + m.errors))))
                .Where(f => f.errors > 0).OrderByDescending(f => f.errors).ToList();
            Text(detailCard, "FaultsTitle", faults.Count > 0 ? "LỖI CẦN SỬA (" + faults.Count + ")" : "LỖI CẦN SỬA",
                new Vector2(.04f, .56f), new Vector2(.5f, .62f), 17, ScreenUiTheme.Gold, true, TextAlignmentOptions.Left);
            if (faults.Count == 0)
            {
                string ruleFaults = (analysis.summary ?? "").Split('\n').FirstOrDefault(line => line.Contains("vi phạm luật ngoài"));
                Text(detailCard, "NoFaults", stats.shots == 0 ? "Chưa ghi nhận được cú đánh nào. Hãy hoàn thành một lượt có giao và trả bóng để HLV thống kê."
                    : ruleFaults ?? "Không phát hiện lỗi trong các tiêu chí đã đo. Giữ nhịp này nhé!",
                    new Vector2(.04f, .4f), new Vector2(.5f, .55f), 16, stats.shots > 0 && ruleFaults == null ? Good : ScreenUiTheme.Gold, false, TextAlignmentOptions.TopLeft);
            }
            else
            {
                var list = FaultList(faults.Count);
                for (int i = 0; i < faults.Count; i++)
                {
                    var fault = faults[i];
                    var row = Rect("Fault" + i, list, new Vector2(0, 1), Vector2.one); Surface(row, Tile);
                    row.offsetMin = new Vector2(0, -i * (RowHeight + RowGap) - RowHeight); row.offsetMax = new Vector2(0, -i * (RowHeight + RowGap));
                    Text(row, "Name", CoachingAnalysisText.Name(fault.code), new Vector2(.04f, .64f), new Vector2(.42f, .97f), 15, ScreenUiTheme.Text, true, TextAlignmentOptions.Left);
                    Text(row, "Shots", fault.shots, new Vector2(.43f, .64f), new Vector2(.76f, .97f), 12, ScreenUiTheme.Muted, false, TextAlignmentOptions.Left);
                    Text(row, "Count", fault.errors + "/" + Math.Max(fault.errors, fault.opportunities) + " lần", new Vector2(.77f, .64f), new Vector2(.96f, .97f), 14, Fault, true, TextAlignmentOptions.Right);
                    var track = Rect("Track", row, new Vector2(.04f, .55f), new Vector2(.96f, .62f)); track.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, .08f);
                    float share = Mathf.Clamp01(fault.errors / (float)Math.Max(1, fault.opportunities));
                    Rect("Fill", track, Vector2.zero, new Vector2(Mathf.Max(.02f, share), 1)).gameObject.AddComponent<Image>().color = Fault;
                    string tip = analysis.findings?.FirstOrDefault(f => f.code == fault.code && !string.IsNullOrWhiteSpace(f.tip))?.tip;
                    if (!string.IsNullOrEmpty(tip))
                        Text(row, "Tip", "HLV: " + tip, new Vector2(.04f, .05f), new Vector2(.96f, .5f), 13, ScreenUiTheme.Mint, false, TextAlignmentOptions.Left);
                }
            }

            // Right column: drills recommended for this session.
            Text(detailCard, "DrillsTitle", "BÀI TẬP ĐỀ XUẤT", new Vector2(.54f, .56f), new Vector2(.96f, .62f), 17, ScreenUiTheme.Gold, true, TextAlignmentOptions.Left);
            var drills = (analysis.recommendations ?? new List<DrillRecommendation>()).Take(3).ToList();
            if (drills.Count == 0)
                Text(detailCard, "NoDrills", "Chưa có bài tập đề xuất cho phiên này.", new Vector2(.54f, .4f), new Vector2(.96f, .55f), 16, ScreenUiTheme.Muted, false, TextAlignmentOptions.TopLeft);
            for (int i = 0; i < drills.Count; i++)
            {
                string drillId = drills[i].id;
                float top = .55f - i * .14f;
                var action = Action(detailCard, "Drill" + i, "", new Vector2(.54f, top - .125f), new Vector2(.96f, top), false);
                Text(action.transform, "Title", drills[i].title, new Vector2(.04f, .52f), new Vector2(.72f, .95f), 16, ScreenUiTheme.Text, true, TextAlignmentOptions.Left);
                Text(action.transform, "Reason", drills[i].reason, new Vector2(.04f, .05f), new Vector2(.72f, .52f), 12, ScreenUiTheme.Muted, false, TextAlignmentOptions.Left);
                Text(action.transform, "Go", "LUYỆN NGAY ›", new Vector2(.72f, .1f), new Vector2(.97f, .9f), 14, ScreenUiTheme.Mint, true, TextAlignmentOptions.Right);
                action.onClick.AddListener(async () =>
                {
                    action.interactable = false;
                    try { if (beforePractice != null) await beforePractice(); if (details) details.SetActive(false); TrainingLaunch.Open(drillId); }
                    catch (Exception) { if (action) action.interactable = true; }
                });
            }
        }

        readonly struct Stats
        {
            public readonly int shots, clean, faults;
            Stats(int shots, int clean, int faults) { this.shots = shots; this.clean = clean; this.faults = faults; }
            static readonly Regex Recorded = new Regex(@"(\d+) lượt đánh");
            public static Stats Of(CoachingAnalysis analysis)
            {
                // The AI summary states the number of recorded shots; older analyses fall back to the metrics.
                var match = Recorded.Match(analysis.summary ?? "");
                int shots = match.Success ? int.Parse(match.Groups[1].Value)
                    : (analysis.metrics ?? new List<TechniqueMetric>()).GroupBy(m => m.technique).Sum(g => g.Max(m => m.opportunities));
                var findings = analysis.findings ?? new List<CoachingFinding>();
                int faultyShots = findings.Select(f => f.shotId).Where(s => !string.IsNullOrEmpty(s)).Distinct().Count();
                int faults = Math.Max(findings.Count, (analysis.metrics ?? new List<TechniqueMetric>()).Sum(m => m.errors));
                return new Stats(shots, Math.Max(0, shots - faultyShots), faults);
            }
        }

        const float RowHeight = 80, RowGap = 8;

        /// <summary>Scrolling column (wheel and drag); the bar shows only when the faults do not fit.</summary>
        RectTransform FaultList(int count)
        {
            var viewport = Rect("Faults", detailCard, new Vector2(.04f, .135f), new Vector2(.5f, .55f));
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            var content = Rect("Content", viewport, new Vector2(0, 1), Vector2.one); content.pivot = new Vector2(.5f, 1);
            content.offsetMin = new Vector2(0, -count * (RowHeight + RowGap) + RowGap); content.offsetMax = new Vector2(-10, 0);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;
            var bar = Rect("Bar", detailCard, new Vector2(.503f, .135f), new Vector2(.508f, .55f));
            bar.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, .08f);
            var handle = Rect("Handle", bar, Vector2.zero, Vector2.one);
            var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = ScreenUiTheme.Mint;
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle; scrollbar.targetGraphic = handleImage; scrollbar.direction = Scrollbar.Direction.BottomToTop; scrollbar.value = 1; // start at the worst fault
            scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        static void Surface(RectTransform rect, Color color, bool themed = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            var theme = ScreenUiTheme.Current;
            if (themed && theme && theme.panel) { image.material = theme.panel; image.color = Color.white; }
            else image.color = color;
            image.raycastTarget = false;
        }
        static Button Action(RectTransform parent, string name, string label, Vector2 min, Vector2 max, bool primary)
        {
            var rect = Rect(name, parent, min, max);
            var image = rect.gameObject.AddComponent<Image>(); image.color = primary ? ScreenUiTheme.Mint : Tile;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            ScreenUiTheme.ButtonStyle(button, primary);
            if (!string.IsNullOrEmpty(label)) Text(rect, "Label", label, Vector2.zero, Vector2.one, 18, primary ? ScreenUiTheme.Ink : ScreenUiTheme.Text, true);
            return button;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform; rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
        }
        static TMP_Text Text(Transform parent, string name, string value, Vector2 min, Vector2 max, int size, Color color,
            bool bold = false, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var text = Rect(name, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = ScreenUiTheme.Current?.font; text.text = value; text.fontSize = size; text.color = color;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal; text.enableAutoSizing = true; text.fontSizeMin = Mathf.Min(10, size); text.fontSizeMax = size;
            text.alignment = alignment; text.richText = false; text.raycastTarget = false; return text;
        }
    }
}
