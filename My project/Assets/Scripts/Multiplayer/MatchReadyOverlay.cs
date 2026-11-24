using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PickleVerse.Settings;
using C = PickleVerse.Multiplayer.MatchFoundChoreography;

namespace PickleVerse.Multiplayer
{
    public sealed class ReadyCheckSeat
    {
        public string id = "", name = "";
        public int team;
        public bool ready, bot;
    }

    /// <summary>Everything the match-found screen shows, refreshed in place every frame (no allocations).</summary>
    public sealed class ReadyCheckSnapshot
    {
        public bool accepted, busy, canAccept;
        public PlayMode mode = PlayMode.Singles;
        public float seconds, totalSeconds;
        public string localId = "";
        public readonly List<ReadyCheckSeat> seats = new List<ReadyCheckSeat>();
        public int ReadyCount { get { int n = 0; foreach (var s in seats) if (s.ready) n++; return n; } }
        public bool AllReady => seats.Count > 0 && ReadyCount == seats.Count;
    }

    /// <summary>
    /// Match found: a cinematic intro (paddles rally, cross and hold the VS ball) under the ready check. Players are
    /// shown as default avatars per team (1v1 or 2v2, the local team on the left) and light up the moment they accept;
    /// Accept/Cancel and the confirmation countdown appear once the intro has played; the server adds the same grace
    /// (Matchmaking:ReadyIntroSeconds) in front of the confirmation window, so the intro never costs decision time.
    /// </summary>
    public sealed class MatchReadyOverlay : MonoBehaviour
    {
        /// <summary>Play Mode previews and tests: when set it replaces MultiplayerApp as the data source.</summary>
        public static Func<ReadyCheckSnapshot, bool> PreviewSource;
        public static Action PreviewAccept, PreviewDecline;

        static readonly Color Gold = new Color(.94f, .82f, .57f), Ink = new Color(.035f, .07f, .12f), Mint = new Color(.56f, .91f, .85f),
            Muted = new Color(.64f, .71f, .81f), Urgent = new Color(1f, .47f, .38f), Lit = new Color(.55f, .78f, 1f);

        readonly ReadyCheckSnapshot snapshot = new ReadyCheckSnapshot();
        readonly List<ReadyCheckSeat> pool = new List<ReadyCheckSeat>();
        readonly List<ReadyCheckSeat> left = new List<ReadyCheckSeat>(), right = new List<ReadyCheckSeat>();
        readonly List<AvatarView> avatars = new List<AvatarView>();
        RectTransform root, safeArea, sides, buttonRow;
        CanvasGroup group;
        RawImage stageImage;
        Image fallback, timerFill, confirmedPill;
        TMP_Text title, readyTitle, mode, timerText, hint, acceptLabel;
        Button accept, cancel;
        TMP_FontAsset font;
        MatchFoundStage stage;
        float shownAt, allReadyAt = -1, controlsAt = -1;
        CanvasGroup rowGroup;
        bool controlsShown;
        bool visible;
        int shownSecond = -1, shownMode = -1, shownState = -1, seatKey;

        // ------------------------------------------------------------------------------------------ data
        public static bool Read(MultiplayerApp app, ReadyCheckSnapshot s, List<ReadyCheckSeat> pool)
        {
            if (!app || !app.ReadyCheckActive || app.Roster == null)
                return false;
            s.mode = app.Mode;
            s.seconds = app.ReadySeconds;
            s.totalSeconds = Mathf.Max(1, app.ReadyConfirmationSeconds);
            s.accepted = app.ReadyCheckAccepted;
            s.busy = app.ReadyCheckBusy;
            s.canAccept = app.CanAcceptMatch;
            s.localId = app.PlayerId ?? "";
            s.seats.Clear();
            var members = app.Roster.members;
            for (int i = 0; i < members.Count; i++)
            {
                while (pool.Count <= i) pool.Add(new ReadyCheckSeat());
                var seat = pool[i];
                var m = members[i];
                seat.id = m.id ?? "";
                seat.name = string.IsNullOrEmpty(m.name) ? "Người chơi" : m.name;
                seat.team = m.team;
                seat.bot = m.bot;
                seat.ready = m.ready || m.bot || (seat.id == s.localId && s.accepted);
                s.seats.Add(seat);
            }
            return true;
        }

        /// <summary>Local team on the left (local player first); everyone else on the right.</summary>
        public static void Arrange(IReadOnlyList<ReadyCheckSeat> seats, string localId, List<ReadyCheckSeat> leftSide, List<ReadyCheckSeat> rightSide)
        {
            leftSide.Clear();
            rightSide.Clear();
            int localTeam = 0;
            foreach (var s in seats)
                if (s.id == localId) localTeam = s.team;
            foreach (var s in seats)
                (s.team == localTeam ? leftSide : rightSide).Add(s);
            for (int i = 1; i < leftSide.Count; i++)
                if (leftSide[i].id == localId)
                {
                    var me = leftSide[i];
                    leftSide.RemoveAt(i);
                    leftSide.Insert(0, me);
                }
        }

        /// <summary>The cinematic (rally, cross, VS lock) plus the avatars settling in.</summary>
        public static float IntroSeconds => C.LockSeconds + .6f;

        /// <summary>
        /// When Accept/Cancel and the countdown appear: after the intro, but never later than the grace the server
        /// added in front of the visible confirmation (so the delay can never eat into a player's decision time).
        /// </summary>
        public static float ControlsDelay(float remaining, float confirmation, float intro, bool accepted)
            => accepted ? 0 : Mathf.Clamp(remaining - confirmation, 0, intro);

        /// <summary>Seconds shown to the player: the confirmation window only, never the intro grace.</summary>
        public static float VisibleSeconds(float remaining, float confirmation) => Mathf.Clamp(remaining, 0, Mathf.Max(0, confirmation));

        public static string TimerText(float seconds, bool accepted)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return accepted ? $"CHỜ MỌI NGƯỜI XÁC NHẬN · {s:00} GIÂY" : $"TỰ HỦY SAU {s:00} GIÂY";
        }

        public static string Initial(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var t = name.Trim();
            return char.ToUpperInvariant(t[0]).ToString();
        }

        // ------------------------------------------------------------------------------------------ lifecycle
        void Update()
        {
            bool show = PreviewSource != null ? PreviewSource(snapshot) : Read(MultiplayerApp.Instance, snapshot, pool);
            if (!show)
            {
                if (visible) Hide();
                return;
            }
            if (!root) Build();
            if (!visible) Show();
            float t = Time.unscaledTime - shownAt;
            Shader.SetGlobalFloat("_MFTime", t);
            if (stage) stage.Tick(t);
            MatchPresentationUi.SafeArea(safeArea);
            Refresh(t);
        }

        void Show()
        {
            visible = true;
            shownAt = Time.unscaledTime;
            allReadyAt = -1;
            controlsAt = -1;
            controlsShown = false;
            shownSecond = shownMode = shownState = -1;
            root.gameObject.SetActive(true);
            bool mobile = MobileRenderPolicy.Enabled;
            float limit = mobile ? 1280f : 1920f;
            float scale = Mathf.Min(1, limit / Mathf.Max(1, Mathf.Max(Screen.width, Screen.height)));
            stage = MatchFoundStage.Create(Mathf.Max(64, Mathf.RoundToInt(Screen.width * scale)), Mathf.Max(64, Mathf.RoundToInt(Screen.height * scale)), mobile);
            stageImage.texture = stage ? stage.Texture : null;
            stageImage.enabled = stage;
            seatKey = 0;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }

        void Hide()
        {
            visible = false;
            if (stage) Destroy(stage.gameObject);
            stage = null;
            if (stageImage) stageImage.texture = null;
            if (root) root.gameObject.SetActive(false);
            ClearAvatars();
        }

        void OnDestroy()
        {
            if (stage) Destroy(stage.gameObject);
            ClearAvatars();
        }

        // ------------------------------------------------------------------------------------------ per frame
        void Refresh(float t)
        {
            var s = snapshot;
            if ((int)s.mode != shownMode)
            {
                shownMode = (int)s.mode;
                mode.text = s.mode == PlayMode.Doubles ? "ĐẤU ĐÔI  ·  2v2" : "ĐẤU ĐƠN  ·  1v1";
            }
            int key = SeatKey(s);
            if (key != seatKey)
            {
                seatKey = key;
                RebuildAvatars(s);
            }

            // Intro: fade in from black, the UI settles while the rally plays.
            float fade = Mathf.Clamp01(t / .2f);
            if (group.alpha != fade) group.alpha = fade;
            float rise = C.Smooth((t - .15f) / .35f);
            stageImage.color = Color.Lerp(Color.black, Color.white, Mathf.Clamp01(t / .25f));
            title.alpha = rise * (allReadyAt < 0 ? 1 : 1 - Mathf.Clamp01((t - allReadyAt) / .15f));
            mode.alpha = rise;
            // Accept/Cancel and the countdown wait for the intro to finish (see ControlsDelay).
            if (controlsAt < 0) controlsAt = ControlsDelay(s.seconds, s.totalSeconds, IntroSeconds, s.accepted);
            float reveal = t < controlsAt ? 0 : C.Smooth((t - controlsAt) / .35f);
            if (rowGroup.alpha != reveal) rowGroup.alpha = reveal;
            bool controls = t >= controlsAt;
            if (rowGroup.interactable != controls) { rowGroup.interactable = controls; rowGroup.blocksRaycasts = controls; }
            if (controls && !controlsShown)
            {
                controlsShown = true;
                if (EventSystem.current && !s.accepted) EventSystem.current.SetSelectedGameObject(accept.gameObject);
            }
            var row = new Vector2(0, Mathf.Lerp(-40, 0, reveal));
            if (buttonRow.anchoredPosition != row) buttonRow.anchoredPosition = row;

            if (s.AllReady && allReadyAt < 0)
            {
                allReadyAt = t;
                if (stage) stage.TriggerReadyWave(t);
            }
            readyTitle.alpha = allReadyAt < 0 ? 0 : Mathf.Clamp01((t - allReadyAt - .12f) / .2f);

            // Accept / cancel / confirmed.
            int state = s.accepted ? 2 : s.busy ? 1 : 0;
            if (state != shownState)
            {
                shownState = state;
                accept.gameObject.SetActive(!s.accepted);
                cancel.gameObject.SetActive(!s.accepted);
                confirmedPill.gameObject.SetActive(s.accepted);
                acceptLabel.text = s.busy ? "ĐANG GỬI…" : "CHẤP NHẬN";
            }
            if (accept.interactable != s.canAccept) accept.interactable = s.canAccept;
            bool canCancel = !s.busy && !s.accepted;
            if (cancel.interactable != canCancel) cancel.interactable = canCancel;

            // Timeout.
            float visibleSeconds = VisibleSeconds(s.seconds, s.totalSeconds);
            float fraction = Mathf.Clamp01(visibleSeconds / Mathf.Max(1, s.totalSeconds));
            var fill = new Vector2(fraction, 1);
            if (timerFill.rectTransform.anchorMax != fill) timerFill.rectTransform.anchorMax = fill;
            bool urgent = !s.accepted && !s.AllReady && visibleSeconds <= 3.0f;
            float pulse = urgent ? .65f + .35f * Mathf.Abs(Mathf.Sin(t * 6)) : 1;
            var bar = urgent ? Urgent : Gold;
            timerFill.color = new Color(bar.r, bar.g, bar.b, pulse);
            timerText.color = urgent ? Urgent : Muted;
            bool allReady = s.AllReady;
            int second = Mathf.Max(0, Mathf.CeilToInt(visibleSeconds)) * 4 + (s.accepted ? 1 : 0) + (allReady ? 2 : 0);
            if (second != shownSecond)
            {
                shownSecond = second;
                timerText.text = allReady ? "ĐANG VÀO TRẬN…" : TimerText(visibleSeconds, s.accepted);
                hint.text = allReady ? "Mọi người đã xác nhận. Trận đấu sắp bắt đầu."
                    : s.accepted ? "Nếu trận bị hủy, bạn sẽ tự động tìm trận tiếp."
                    : s.canAccept || s.busy ? "Nhấn HỦY để dừng tìm trận. Hết giờ mà chưa xác nhận sẽ tự hủy."
                    : "Đang kết nối những người chơi đã tìm thấy…";
            }

            float appear = C.LockSeconds + .15f;
            foreach (var a in avatars)
                a.Tick(t, appear, Find(s, a.id));
        }

        static ReadyCheckSeat Find(ReadyCheckSnapshot s, string id)
        {
            foreach (var seat in s.seats) if (seat.id == id) return seat;
            return null;
        }

        /// <summary>Changes only when the line-up changes (no per-frame allocation).</summary>
        static int SeatKey(ReadyCheckSnapshot s)
        {
            if (s.seats.Count == 0) return 0;
            unchecked
            {
                int h = 17 * 31 + (int)s.mode;
                h = h * 31 + s.localId.GetHashCode();
                foreach (var seat in s.seats) h = (h * 31 + seat.id.GetHashCode()) * 31 + seat.team;
                return h == 0 ? 1 : h;
            }
        }

        // ------------------------------------------------------------------------------------------ avatars
        void RebuildAvatars(ReadyCheckSnapshot s)
        {
            ClearAvatars();
            Arrange(s.seats, s.localId, left, right);
            bool doubles = s.mode == PlayMode.Doubles || left.Count > 1 || right.Count > 1;
            Place(left, -1, doubles, s.localId);
            Place(right, 1, doubles, s.localId);
        }

        void Place(List<ReadyCheckSeat> seats, int side, bool doubles, string localId)
        {
            float d = doubles ? 176 : 250;
            for (int i = 0; i < seats.Count; i++)
            {
                float y = doubles ? (seats.Count == 1 ? 10 : (i == 0 ? 165 : -150)) : 10;
                var view = new AvatarView(sides, font, seats[i], seats[i].id == localId, d, new Vector2(side * 640, y), side);
                avatars.Add(view);
            }
        }

        void ClearAvatars()
        {
            foreach (var a in avatars) a.Dispose();
            avatars.Clear();
        }

        sealed class AvatarView
        {
            public readonly string id;
            readonly RectTransform root;
            readonly CanvasGroup fader;
            readonly Image halo, waves, pulse, disc, ring, badge;
            readonly TMP_Text initial, name, status;
            readonly RectTransform check;
            readonly Material wavesMaterial;
            readonly Vector2 home;
            readonly int side;
            readonly bool local;
            float readyAt = -1;
            bool shownReady;

            public AvatarView(RectTransform parent, TMP_FontAsset font, ReadyCheckSeat seat, bool local, float d, Vector2 position, int side)
            {
                id = seat.id;
                this.local = local;
                this.side = side;
                home = position;
                root = Box("Avatar " + seat.name, parent, position, new Vector2(d, d));
                // Stacked 2v2 avatars keep their aura tighter so neighbours and names stay readable.
                float aura = d >= 200 ? 2.15f : 1.85f;
                halo = Img("Halo", root, MatchFoundArt.Halo, new Vector2(d * aura, d * aura), new Color(.28f, .58f, 1f, 0));
                waves = Img("Waves", root, null, new Vector2(d * aura, d * aura), new Color(1, 1, 1, 0));
                var wavesTemplate = Resources.Load<Material>("MatchFound/Materials/UI Waves");
                if (wavesTemplate)
                {
                    wavesMaterial = new Material(wavesTemplate);
                    waves.material = wavesMaterial;
                }
                else waves.enabled = false;
                pulse = Img("Pulse", root, MatchFoundArt.Ring, new Vector2(d, d), new Color(.75f, .9f, 1f, 0));
                disc = Img("Disc", root, MatchFoundArt.Circle, new Vector2(d * .92f, d * .92f), new Color(.025f, .04f, .07f, 1));
                ring = Img("Ring", root, MatchFoundArt.Ring, new Vector2(d, d), new Color(Gold.r, Gold.g, Gold.b, .85f));
                initial = Text("Initial", root, font, Initial(seat.name), d * .42f, Muted, new Vector2(0, -d * .02f), new Vector2(d, d * .6f));
                check = Box("Check", root, new Vector2(d * .36f, -d * .36f), new Vector2(d * .27f, d * .27f));
                badge = Img("Badge", check, MatchFoundArt.Circle, check.sizeDelta, Mint);
                var icon = new GameObject("Tick", typeof(RectTransform), typeof(CanvasRenderer), typeof(UiLineIcon)).GetComponent<UiLineIcon>();
                icon.transform.SetParent(check, false);
                icon.rectTransform.sizeDelta = check.sizeDelta * .62f;
                icon.glyph = ArenaGlyph.Check;
                icon.stroke = .16f;
                icon.color = Ink;
                icon.raycastTarget = false;
                check.localScale = Vector3.zero;
                float labelSize = d >= 200 ? 32 : 26;
                // Soft dark plate so the name stays legible over the energy waves.
                Img("Name Plate", root, MatchFoundArt.Halo, new Vector2(d * 1.9f, labelSize * 3.4f), new Color(0, .01f, .03f, .78f)).rectTransform.anchoredPosition = new Vector2(0, -d * .5f - labelSize * 1.5f);
                string label = local ? seat.name + "  (Bạn)" : seat.bot ? seat.name + "  · MÁY" : seat.name;
                name = Text("Name", root, font, label, labelSize, local ? Gold : Color.white, new Vector2(0, -d * .5f - labelSize * .95f), new Vector2(460, labelSize * 1.4f));
                status = Text("Status", root, font, "ĐANG CHỜ…", labelSize * .7f, Muted, new Vector2(0, -d * .5f - labelSize * 2.05f), new Vector2(420, labelSize));
                fader = root.gameObject.AddComponent<CanvasGroup>();
                fader.alpha = 0;
            }

            public void Tick(float t, float appearAt, ReadyCheckSeat seat)
            {
                bool ready = seat != null && seat.ready;
                if (ready && readyAt < 0) readyAt = Mathf.Max(t, appearAt);
                if (!ready) readyAt = -1;
                // slide in from the screen edge as the ball locks
                float k = C.Smooth((t - appearAt) / .45f);
                var at = home + new Vector2(side * 220 * (1 - k), 0);
                if (root.anchoredPosition != at) root.anchoredPosition = at;
                if (fader.alpha != k) fader.alpha = k;
                float since = readyAt < 0 ? -1 : t - readyAt;
                bool lit = since >= 0;
                float flash = lit ? Mathf.Clamp01(since / .16f) : 0;
                float settle = lit ? Mathf.Clamp01((since - .16f) / .5f) : 0;
                // waiting: the gold ring breathes; ready: blue light, waves, pulse and a tick
                if (!lit)
                {
                    float breathe = .7f + .3f * (.5f + .5f * Mathf.Sin(t * 2.4f + side));
                    ring.color = new Color(Gold.r, Gold.g, Gold.b, .85f * breathe);
                    disc.color = new Color(.025f, .04f, .07f, 1);
                    initial.color = Muted;
                    halo.color = new Color(.28f, .58f, 1f, 0);
                    waves.color = new Color(1, 1, 1, 0);
                    pulse.color = new Color(.75f, .9f, 1f, 0);
                    check.localScale = Vector3.zero;
                }
                else
                {
                    var peak = Color.Lerp(Gold, new Color(.9f, .97f, 1f), flash);
                    ring.color = Color.Lerp(peak, Lit, settle);
                    disc.color = Color.Lerp(new Color(.025f, .04f, .07f, 1), new Color(.02f, .07f, .2f, 1), flash);
                    initial.color = Color.Lerp(Muted, Color.white, flash);
                    halo.color = new Color(.28f, .58f, 1f, .5f * flash + .08f * Mathf.Sin(t * 2.2f) * settle);
                    float w = since < .13f ? since / .13f : Mathf.Lerp(1, .78f, Mathf.Clamp01((since - .13f) / .6f));
                    waves.color = new Color(1, 1, 1, w);
                    float p = Mathf.Clamp01(since / .55f);
                    pulse.rectTransform.localScale = Vector3.one * Mathf.Lerp(1, 1.9f, p);
                    pulse.color = new Color(.75f, .9f, 1f, 1 - p);
                    float pop = since < .13f ? 0 : since < .27f ? Mathf.Lerp(0, 1.25f, (since - .13f) / .14f) : Mathf.Lerp(1.25f, 1, Mathf.Clamp01((since - .27f) / .13f));
                    check.localScale = Vector3.one * pop;
                }
                if (lit != shownReady)
                {
                    shownReady = lit;
                    status.text = lit ? "ĐÃ SẴN SÀNG" : "ĐANG CHỜ…";
                    status.color = lit ? Mint : Muted;
                    if (lit && wavesMaterial) wavesMaterial.SetFloat("_Start", t);
                }
                if (lit && since < .4f)
                    root.localScale = Vector3.one * (1 + .08f * Mathf.Sin(Mathf.Clamp01(since / .4f) * Mathf.PI));
                else if (root.localScale != Vector3.one)
                    root.localScale = Vector3.one;
            }

            public void Dispose()
            {
                if (wavesMaterial) Destroy(wavesMaterial);
                if (root) Destroy(root.gameObject);
            }
        }

        // ------------------------------------------------------------------------------------------ build
        void Build()
        {
            var theme = ScreenUiTheme.Current;
            font = theme ? theme.font : TMP_Settings.defaultFontAsset;
            root = MatchPresentationUi.Rect("MatchFound", transform, Vector2.zero, Vector2.one);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1900;   // above room invites, training and moderation banners; below the exit prompt
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            group = root.gameObject.AddComponent<CanvasGroup>();

            fallback = MatchPresentationUi.Image("Backdrop", root, Vector2.zero, Vector2.one, new Color(.004f, .008f, .02f, 1), true);
            stageImage = MatchPresentationUi.Rect("Stage", root, Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
            stageImage.raycastTarget = true;
            safeArea = MatchPresentationUi.Rect("SafeArea", root, Vector2.zero, Vector2.one);

            title = Text("Title", safeArea, font, "ĐÃ TÌM THẤY TRẬN", 60, Gold, new Vector2(0, -78), new Vector2(1400, 80), new Vector2(.5f, 1));
            readyTitle = Text("All Ready", safeArea, font, "TẤT CẢ ĐÃ SẴN SÀNG", 60, Mint, new Vector2(0, -78), new Vector2(1400, 80), new Vector2(.5f, 1));
            readyTitle.alpha = 0;
            mode = Text("Mode", safeArea, font, "", 26, Color.white, new Vector2(0, -134), new Vector2(800, 40), new Vector2(.5f, 1));
            sides = MatchPresentationUi.Rect("Players", safeArea, new Vector2(.5f, .5f), new Vector2(.5f, .5f));

            // Bottom: [HỦY] [CHẤP NHẬN], the confirmation timeout under them and a one-line hint.
            var bottom = MatchPresentationUi.Rect("Bottom", safeArea, new Vector2(.5f, 0), new Vector2(.5f, 0));
            buttonRow = Box("Buttons", bottom, Vector2.zero, new Vector2(784, 84));
            buttonRow.anchorMin = buttonRow.anchorMax = new Vector2(.5f, 0);
            buttonRow.pivot = new Vector2(.5f, 0);
            rowGroup = buttonRow.gameObject.AddComponent<CanvasGroup>();
            rowGroup.alpha = 0;
            rowGroup.interactable = rowGroup.blocksRaycasts = false;
            var rowHome = Box("Row", buttonRow, new Vector2(0, 116), new Vector2(784, 84));
            cancel = MakeButton("Cancel", rowHome, "HỦY", false, new Vector2(-242, 0), new Vector2(300, 84), () =>
            {
                if (PreviewDecline != null) { PreviewDecline(); return; }
                var app = MultiplayerApp.Instance;
                if (app) _ = app.DeclineMatchAsync();
            });
            accept = MakeButton("Accept", rowHome, "CHẤP NHẬN", true, new Vector2(162, 0), new Vector2(460, 84), () =>
            {
                if (PreviewAccept != null) { PreviewAccept(); return; }
                var app = MultiplayerApp.Instance;
                if (app) _ = app.AcceptMatchAsync();
            });
            acceptLabel = accept.GetComponentInChildren<TMP_Text>(true);
            var nav = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = cancel };
            accept.navigation = nav;
            cancel.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = accept };
            confirmedPill = MatchPresentationUi.Image("Confirmed", rowHome, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Color.white);
            confirmedPill.rectTransform.sizeDelta = new Vector2(560, 84);
            if (theme) confirmedPill.material = theme.panel;
            else confirmedPill.color = Ink;
            Text("Label", confirmedPill.rectTransform, font, "ĐÃ XÁC NHẬN", 30, Gold, Vector2.zero, new Vector2(560, 84));
            confirmedPill.gameObject.SetActive(false);
            var track = MatchPresentationUi.Image("Timeout", rowHome, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Color(.12f, .19f, .27f, .9f));
            track.rectTransform.sizeDelta = new Vector2(784, 8);
            track.rectTransform.anchoredPosition = new Vector2(0, -64);
            timerFill = MatchPresentationUi.Image("Remaining", track.transform, Vector2.zero, Vector2.one, Gold);
            timerText = Text("Timeout Label", rowHome, font, "", 24, Muted, new Vector2(0, -92), new Vector2(900, 34));
            hint = Text("Hint", rowHome, font, "", 20, new Color(.58f, .65f, .75f), new Vector2(0, -124), new Vector2(1200, 30));
            root.gameObject.SetActive(false);
        }

        Button MakeButton(string name, RectTransform parent, string label, bool primary, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var image = MatchPresentationUi.Image(name, parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f), primary ? Gold : new Color(.09f, .16f, .23f), true);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
            var b = image.gameObject.AddComponent<Button>();
            b.targetGraphic = image;
            Text("Label", image.rectTransform, font, label, 30, primary ? Ink : Color.white, Vector2.zero, size);
            ScreenUiTheme.ButtonStyle(b, primary);
            b.onClick.AddListener(onClick);
            return b;
        }

        static RectTransform Box(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
            r.anchoredPosition = position;
            r.sizeDelta = size;
            return r;
        }

        static Image Img(string name, RectTransform parent, Sprite sprite, Vector2 size, Color color)
        {
            var i = Box(name, parent, Vector2.zero, size).gameObject.AddComponent<Image>();
            i.sprite = sprite;
            i.color = color;
            i.raycastTarget = false;
            return i;
        }

        static TMP_Text Text(string name, RectTransform parent, TMP_FontAsset font, string value, float size, Color color, Vector2 position, Vector2 box, Vector2? anchor = null)
        {
            var r = Box(name, parent, position, box);
            if (anchor.HasValue) r.anchorMin = r.anchorMax = anchor.Value;
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = value;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }
    }

    /// <summary>Anti-aliased circle, ring and soft halo sprites generated once.</summary>
    static class MatchFoundArt
    {
        static Sprite circle, ring, halo;
        public static Sprite Circle => circle ? circle : (circle = Make("Circle", (r, px) => Mathf.Clamp01((1 - r) / px)));
        public static Sprite Ring => ring ? ring : (ring = Make("Ring", (r, px) => Mathf.Clamp01((1 - r) / px) * Mathf.Clamp01((r - .9f) / px)));
        public static Sprite Halo => halo ? halo : (halo = Make("Halo", (r, px) => Mathf.Pow(Mathf.Clamp01(1 - r), 2.2f)));

        static Sprite Make(string name, Func<float, float, float> alpha)
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "MatchFound " + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color32[size * size];
            float aa = 2f / size;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + .5f) / size * 2 - 1, v = (y + .5f) / size * 2 - 1;
                    float r = Mathf.Sqrt(u * u + v * v);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * alpha(r, aa)));
                }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100);
        }
    }
}
