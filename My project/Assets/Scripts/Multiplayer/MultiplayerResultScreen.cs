using System.Linq;
using PickleVerse.Progression;
using PickleVerse.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PickleVerse.Multiplayer
{
    // The summary reads the saved, idempotently applied result, including the actual Elo floor.
    // Arena Glass layout: outcome banner, players, Elo / rank / games tiles, level progress, coach and the two exits.
    public sealed class MultiplayerResultScreen : MonoBehaviour
    {
        /// <summary>Bumped when the built layout changes; older templates saved in scenes are rebuilt.</summary>
        public const int LayoutVersion = 2;
        static readonly Color Loss = new Color32(255, 160, 148, 255);
        static readonly Color[] RankColours =
        {
            new Color32(214, 150, 104, 255), new Color32(206, 216, 232, 255), new Color32(240, 204, 110, 255), new Color32(150, 228, 220, 255),
            new Color32(146, 196, 255, 255), new Color32(206, 160, 255, 255), new Color32(255, 150, 116, 255),
        };

        public RectTransform panel, safeArea;
        public TMP_Text heading, opponent, delta, elo, rank, score, experience;
        public Button roomButton, menuButton;
        public bool IsVisible => panel && panel.gameObject.activeSelf;
        public string RenderedMatchId { get; private set; }
        internal string MatchId { get; private set; }

        [SerializeField, HideInInspector] int layout;
        [SerializeField] TMP_Text context, self, kind, rankNote, rankShift, level, gamesNote;
        [SerializeField] RectTransform card, hero, chips, rankFill, xpFill, players, levelRow, kindPill, rankBadge;
        [SerializeField] CanvasGroup heroGroup, statsGroup;
        [SerializeField] CanvasGroup[] tiles;
        [SerializeField] UiLineIcon spinner, emblem;
        [SerializeField] RawImage glow;
        [SerializeField] Image rankFillImage;
        MultiplayerApp app;
        MultiplayerCourtUI court;
        GameObject resultCanvas;
        EventSystem focusedEventSystem;
        bool connectionClosed;
        float presentedAt, rankTarget, xpTarget;
        Color tone = ScreenUiTheme.Text;
        static Texture2D glowTexture;

        public static MultiplayerResultScreen CreateTemplate(Transform parent, MultiplayerCourtUI court)
        {
            var panel = Rect("MultiplayerSummary", parent, Vector2.zero, Vector2.one);
            var screen = panel.gameObject.AddComponent<MultiplayerResultScreen>();
            screen.panel = panel;
            screen.Configure(court);
            return screen;
        }

        internal static MultiplayerResultScreen Create(MultiplayerApp app, string matchId, MultiplayerCourtUI court)
        {
            var root = new GameObject("Online Match Results", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            // The app survives the court scene that Fusion unloads on shutdown.
            root.transform.SetParent(app.transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 600;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            var screen = court && court.resultScreen && court.resultScreen.layout == LayoutVersion ? court.resultScreen : null;
            if (!screen)
            {
                if (court && court.resultScreen)
                    Destroy(court.resultScreen.gameObject); // a template from an older layout saved in the court scene
                screen = CreateTemplate(root.transform, court);
            }
            screen.transform.SetParent(root.transform, false);
            screen.app = app;
            screen.court = court;
            screen.resultCanvas = root;
            screen.MatchId = matchId;
            if (court)
                court.resultScreen = screen;
            if (!screen.safeArea.GetComponent<UiSafeArea>())
                screen.safeArea.gameObject.AddComponent<UiSafeArea>();
            screen.roomButton.onClick.AddListener(() => _ = app.ExecuteAsync(() => app.ReturnToRoomAsync("Trận đã kết thúc.")));
            screen.menuButton.onClick.AddListener(() => _ = app.ExecuteAsync(app.ReturnFromResultToMenuAsync));
            return screen;
        }

        public void Open()
        {
            // An older template saved in the court scene is replaced by Create once the result is known; never show it.
            if (IsVisible || !panel || layout != LayoutVersion)
                return;
            if (court)
            {
                court.ClearTouch();
                if (court.help)
                    court.help.SetActive(false);
            }
            ShowPending();
            panel.gameObject.SetActive(true);
            if (app)
                app.CloseOutcome();
            Update();
        }

        internal void MarkDisconnected() => connectionClosed = true;
        internal void Close()
        {
            resultCanvas.SetActive(false);
            Destroy(resultCanvas);
        }
        void Update()
        {
            if (!IsVisible)
                return;
            var events = MatchPresentationUi.EnsureEventSystem(transform);
            roomButton.interactable = menuButton.interactable = app && !app.Busy && (connectionClosed || RenderedMatchId != null);
            if (events != focusedEventSystem)
            {
                focusedEventSystem = events;
                focusedEventSystem.SetSelectedGameObject(roomButton.gameObject);
            }
            if (ProgressionRuntime.Exists && RenderedMatchId != MatchId)
            {
                var player = ProgressionRuntime.Instance.Player;
                var result = player.history.Find(r => r.id == MatchId);
                if (result != null)
                    Present(player, result);
            }
            Animate();
        }

        /// <summary>Waiting for the server to confirm the stored result: spinner, quiet placeholders, no exits yet.</summary>
        void ShowPending()
        {
            if (RenderedMatchId != null)
                return;
            tone = ScreenUiTheme.Text;
            heading.text = "ĐANG XÁC NHẬN KẾT QUẢ";
            heading.color = ScreenUiTheme.Text;
            heading.fontSizeMax = 38;
            context.text = "Máy chủ đang lưu kết quả, Elo và phần thưởng…";
            self.text = opponent.text = kind.text = elo.text = rankNote.text = rankShift.text = experience.text = level.text = gamesNote.text = "";
            delta.text = rank.text = score.text = "—";
            delta.color = rank.color = score.color = ScreenUiTheme.Muted;
            SetFill(rankFill, 0);
            SetFill(xpFill, 0);
            ClearChips();
            players.gameObject.SetActive(false);
            levelRow.gameObject.SetActive(false);
            kindPill.gameObject.SetActive(false);
            rankBadge.gameObject.SetActive(false);
            spinner.gameObject.SetActive(true);
            statsGroup.alpha = .4f;
            heroGroup.alpha = 1;
            hero.localScale = Vector3.one;
            foreach (var tile in tiles)
            {
                tile.alpha = 1;
                ((RectTransform)tile.transform).anchoredPosition = Vector2.zero;
            }
        }

        internal void ShowSyncFailure()
        {
            if (RenderedMatchId == null)
            {
                spinner.gameObject.SetActive(false);
                heading.text = "CHƯA TẢI ĐƯỢC KẾT QUẢ";
                heading.color = ScreenUiTheme.Text;
                context.text = "Bạn có thể xem lại kết quả trong Lịch sử thi đấu sau khi đồng bộ.";
            }
        }

        public void Present(PlayerProfile player, MatchResult result)
        {
            tone = result.outcome == MatchOutcome.Won ? ScreenUiTheme.Gold : result.outcome == MatchOutcome.Lost ? Loss : ScreenUiTheme.Muted;
            heading.text = MatchSummaryFormat.Title(result);
            heading.color = tone;
            heading.fontSizeMax = 64;
            context.text = MatchSummaryFormat.Context(result);
            kind.text = result.KindLabel.ToUpperInvariant();
            self.text = player.playerName;
            opponent.text = string.Join(" / ", result.opponents.Select(p => p.name));

            delta.text = MatchSummaryFormat.Signed(result.eloDelta);
            delta.color = !result.Ranked ? ScreenUiTheme.Muted : result.eloDelta > 0 ? ScreenUiTheme.Mint : result.eloDelta < 0 ? Loss : ScreenUiTheme.Text;
            elo.text = MatchSummaryFormat.EloLine(result, player.elo);

            int tier = MatchSummaryFormat.RankTier(player.elo);
            var rankColour = RankColours[Mathf.Clamp(tier, 0, RankColours.Length - 1)];
            rank.text = EloPolicy.Rank(player.elo).ToUpperInvariant();
            rank.color = emblem.color = rankFillImage.color = rankColour;
            rankNote.text = MatchSummaryFormat.RankNote(player.elo);
            int shift = MatchSummaryFormat.RankShift(result);
            rankShift.text = shift > 0 ? "THĂNG HẠNG" : shift < 0 ? "XUỐNG HẠNG" : "";
            rankBadge.GetComponent<Image>().color = shift > 0 ? ScreenUiTheme.Gold : Loss;
            rankBadge.gameObject.SetActive(shift != 0);
            rankTarget = MatchSummaryFormat.RankProgress(player.elo);

            score.text = result.gamesWon + " – " + result.gamesLost;
            score.color = ScreenUiTheme.Text;
            ClearChips();
            if (MatchSummaryFormat.CompletedGames(result) == 0)
                gamesNote.text = "Trận kết thúc trước khi xong ván 1";
            else
            {
                gamesNote.text = "";
                foreach (var game in result.scores.Where(g => g != null))
                    Chip(game);
            }

            level.text = "CẤP " + player.level;
            experience.text = $"{player.levelXp:N0} / {player.NextLevelXp:N0} XP";
            xpTarget = MatchSummaryFormat.Ratio(player.levelXp, player.NextLevelXp);

            RenderedMatchId = result.id;
            presentedAt = Time.unscaledTime;
            players.gameObject.SetActive(true);
            levelRow.gameObject.SetActive(true);
            kindPill.gameObject.SetActive(true);
            spinner.gameObject.SetActive(false);
            statsGroup.alpha = 1;
            var coach = PickleVerse.LocalMatch.CoachingSessionView.Attach(card, result.id, "match", new Vector2(.665f, .118f), new Vector2(.955f, .182f),
                () => MultiplayerApp.Instance.ReturnToModeSelectionAsync());
            StyleCoach(coach);
            Animate();
        }

        /// <summary>Banner pop, tiles rising in turn, bars filling, a slow glow on a win (unscaled time).</summary>
        void Animate()
        {
            if (RenderedMatchId == null)
            {
                if (spinner && spinner.gameObject.activeSelf)
                    spinner.rectTransform.localRotation = Quaternion.Euler(0, 0, -Time.unscaledTime * 220);
                SetGlow(.12f);
                return;
            }
            float t = Time.unscaledTime - presentedAt;
            float h = Ease(t / .45f);
            hero.localScale = Vector3.one * Mathf.Lerp(1.14f, 1, h);
            heroGroup.alpha = h;
            for (int i = 0; i < tiles.Length; i++)
            {
                float k = Ease((t - .18f - i * .09f) / .38f);
                tiles[i].alpha = k;
                ((RectTransform)tiles[i].transform).anchoredPosition = new Vector2(0, (1 - k) * -16);
            }
            float b = Ease((t - .5f) / .9f);
            SetFill(rankFill, rankTarget * b);
            SetFill(xpFill, xpTarget * b);
            SetGlow(tone == ScreenUiTheme.Gold ? .34f + .07f * Mathf.Sin(t * 1.7f) : .22f);
        }

        static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return 1 - (1 - t) * (1 - t) * (1 - t);
        }

        void SetGlow(float alpha)
        {
            if (glow)
                glow.color = new Color(tone.r, tone.g, tone.b, alpha);
        }

        static void SetFill(RectTransform fill, float value)
        {
            if (fill)
                fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1);
        }

        void ClearChips()
        {
            for (int i = chips.childCount - 1; i >= 0; i--)
            {
                var child = chips.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        void Chip(GameScore game)
        {
            var chip = Rect("Game", chips, Vector2.zero, Vector2.one);
            var image = chip.gameObject.AddComponent<Image>();
            var colour = !game.completed ? ScreenUiTheme.Muted : game.own > game.opponent ? ScreenUiTheme.Mint : Loss;
            image.color = new Color(colour.r, colour.g, colour.b, .14f);
            image.raycastTarget = false;
            var label = Label("Score", chip, heading.font, 15, Vector2.zero, Vector2.one);
            label.text = MatchSummaryFormat.GameChip(game);
            label.fontStyle = FontStyles.Bold;
            label.color = colour;
        }

        static void StyleCoach(Component coach)
        {
            if (!coach)
                return;
            var caption = coach.transform.Find("Caption") as RectTransform;
            if (caption && caption.TryGetComponent<TMP_Text>(out var text))
            {
                text.fontSize = 15;
                text.fontStyle = FontStyles.Bold;
                caption.offsetMin = new Vector2(34, 0);
                caption.offsetMax = new Vector2(-10, 0);
            }
            if (!coach.transform.Find("Icon"))
            {
                var icon = Icon("Icon", coach.transform, ArenaGlyph.Book, ScreenUiTheme.Ink, .09f);
                Pin(icon.rectTransform, new Vector2(0, .5f), new Vector2(14, 0), new Vector2(20, 20));
            }
        }

        void Configure(MultiplayerCourtUI ui)
        {
            layout = LayoutVersion;
            var font = ui && ui.modalTitle ? ui.modalTitle.font : ScreenUiTheme.Current ? ScreenUiTheme.Current.font : TMP_Settings.defaultFontAsset;
            var backdrop = panel.gameObject.AddComponent<Image>();
            backdrop.color = LobbyPanelStyle.Background;
            backdrop.raycastTarget = true;
            panel.gameObject.AddComponent<CanvasGroup>();
            panel.gameObject.AddComponent<UiPanelReveal>();

            glow = new GameObject("Glow", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            glow.transform.SetParent(panel, false);
            glow.texture = GlowTexture();
            glow.raycastTarget = false;
            Pin(glow.rectTransform, new Vector2(.5f, 1), new Vector2(0, -190), new Vector2(1400, 560));

            safeArea = Rect("SafeArea", panel, Vector2.zero, Vector2.one);
            safeArea.gameObject.AddComponent<UiSafeArea>();
            card = Rect("Card", safeArea, new Vector2(.07f, .05f), new Vector2(.93f, .95f));
            Glass(card, false);
            LobbyEtching.AddTo(card, true);

            // Header: title of the page, match type and a thin gold rule.
            var caption = Label("Caption", card, font, 15, new Vector2(.045f, .915f), new Vector2(.6f, .975f), TextAlignmentOptions.Left);
            caption.text = "PICKLEVERSE  ·  TỔNG KẾT TRẬN ĐẤU";
            caption.color = ScreenUiTheme.Gold;
            caption.characterSpacing = 6;
            caption.fontStyle = FontStyles.Bold;
            kindPill = Rect("MatchType", card, new Vector2(.72f, .92f), new Vector2(.955f, .972f));
            Glass(kindPill, true);
            kind = Label("Label", kindPill, font, 13, Vector2.zero, Vector2.one);
            kind.color = ScreenUiTheme.Muted;
            kind.characterSpacing = 4;
            var rule = Rect("Rule", card, new Vector2(.045f, .904f), new Vector2(.955f, .904f));
            rule.offsetMin = new Vector2(0, -.75f);
            rule.offsetMax = new Vector2(0, .75f);
            var ruleImage = rule.gameObject.AddComponent<Image>();
            ruleImage.color = new Color(ScreenUiTheme.Gold.r, ScreenUiTheme.Gold.g, ScreenUiTheme.Gold.b, .45f);
            ruleImage.raycastTarget = false;

            // Outcome banner.
            hero = Rect("Hero", card, new Vector2(.08f, .705f), new Vector2(.92f, .89f));
            heroGroup = hero.gameObject.AddComponent<CanvasGroup>();
            heading = Label("Outcome", hero, font, 64, Vector2.zero, Vector2.one);
            heading.fontStyle = FontStyles.Bold;
            heading.characterSpacing = 6;
            heading.fontSizeMin = 30;
            context = Label("Context", card, font, 18, new Vector2(.06f, .648f), new Vector2(.94f, .705f));
            context.color = ScreenUiTheme.Muted;
            context.richText = false;

            // Players.
            var strip = players = Rect("Players", card, new Vector2(.045f, .55f), new Vector2(.955f, .628f));
            var stripImage = strip.gameObject.AddComponent<Image>();
            stripImage.color = new Color(0, 0, 0, .22f);
            stripImage.raycastTarget = false;
            self = Label("Self", strip, font, 21, new Vector2(.03f, 0), new Vector2(.45f, 1), TextAlignmentOptions.Right);
            self.fontStyle = FontStyles.Bold;
            self.richText = false;
            var medal = Rect("Versus", strip, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            medal.sizeDelta = new Vector2(30, 30);
            medal.localRotation = Quaternion.Euler(0, 0, 45);
            var medalImage = medal.gameObject.AddComponent<Image>();
            medalImage.color = ScreenUiTheme.Gold;
            medalImage.raycastTarget = false;
            var vs = Label("VS", strip, font, 13, new Vector2(.47f, 0), new Vector2(.53f, 1));
            vs.text = "VS";
            vs.color = ScreenUiTheme.Ink;
            vs.fontStyle = FontStyles.Bold;
            opponent = Label("Players", strip, font, 21, new Vector2(.55f, 0), new Vector2(.97f, 1), TextAlignmentOptions.Left);
            opponent.fontStyle = FontStyles.Bold;
            opponent.richText = false;
            spinner = Icon("Waiting", card, ArenaGlyph.Sync, ScreenUiTheme.Gold, .1f);
            Pin(spinner.rectTransform, new Vector2(.5f, .589f), Vector2.zero, new Vector2(30, 30));

            // Elo, rank and games.
            var stats = Rect("Stats", card, new Vector2(.045f, .215f), new Vector2(.955f, .52f));
            statsGroup = stats.gameObject.AddComponent<CanvasGroup>();
            var eloTile = Tile("EloTile", stats, font, 0, "ELO TRẬN NÀY", out var eloGroup);
            delta = Label("EloChange", eloTile, font, 54, new Vector2(.05f, .3f), new Vector2(.95f, .8f));
            delta.fontStyle = FontStyles.Bold;
            elo = Label("EloBeforeAfter", eloTile, font, 15, new Vector2(.05f, .07f), new Vector2(.95f, .3f));
            elo.color = ScreenUiTheme.Muted;

            var rankTile = Tile("RankTile", stats, font, 1, "HẠNG HIỆN TẠI", out var rankGroup);
            emblem = Icon("Emblem", rankTile, ArenaGlyph.Shield, ScreenUiTheme.Gold, .09f);
            Pin(emblem.rectTransform, new Vector2(.2f, .6f), Vector2.zero, new Vector2(44, 44));
            rank = Label("CurrentRank", rankTile, font, 32, new Vector2(.32f, .42f), new Vector2(.95f, .79f), TextAlignmentOptions.Left);
            rank.fontStyle = FontStyles.Bold;
            rankBadge = Rect("RankShift", rankTile, new Vector2(.5f, 1), new Vector2(.5f, 1));
            Pin(rankBadge, new Vector2(.5f, 1), new Vector2(0, 5), new Vector2(132, 22));
            var badgeImage = rankBadge.gameObject.AddComponent<Image>();
            badgeImage.color = ScreenUiTheme.Gold;
            badgeImage.raycastTarget = false;
            rankShift = Label("Label", rankBadge, font, 12, Vector2.zero, Vector2.one);
            rankShift.color = ScreenUiTheme.Ink;
            rankShift.fontStyle = FontStyles.Bold;
            rankShift.characterSpacing = 4;
            rankBadge.gameObject.SetActive(false);
            rankFill = Bar("RankProgress", rankTile, new Vector2(.08f, .32f), new Vector2(.92f, .32f), 5, out rankFillImage);
            rankNote = Label("RankNote", rankTile, font, 13, new Vector2(.05f, .06f), new Vector2(.95f, .26f));
            rankNote.color = ScreenUiTheme.Muted;

            var gamesTile = Tile("GamesTile", stats, font, 2, "VÁN ĐẤU", out var gamesGroup);
            score = Label("Games", gamesTile, font, 44, new Vector2(.05f, .4f), new Vector2(.95f, .8f));
            score.fontStyle = FontStyles.Bold;
            chips = Rect("GameScores", gamesTile, new Vector2(.07f, .09f), new Vector2(.93f, .33f));
            var row = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 8;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            gamesNote = Label("GamesNote", gamesTile, font, 13, new Vector2(.05f, .07f), new Vector2(.95f, .33f));
            gamesNote.color = ScreenUiTheme.Muted;
            tiles = new[] { eloGroup, rankGroup, gamesGroup };

            // Level progress (the coach pill is attached beside it once the result is known).
            levelRow = Rect("LevelRow", card, new Vector2(.045f, .118f), new Vector2(.65f, .182f));
            level = Label("Level", levelRow, font, 18, Vector2.zero, new Vector2(.17f, 1), TextAlignmentOptions.Left);
            level.color = ScreenUiTheme.Gold;
            level.fontStyle = FontStyles.Bold;
            xpFill = Bar("LevelProgress", levelRow, new Vector2(.17f, .5f), new Vector2(.75f, .5f), 8, out var xpImage);
            xpImage.color = ScreenUiTheme.Mint;
            experience = Label("Experience", levelRow, font, 14, new Vector2(.775f, 0), Vector2.one, TextAlignmentOptions.Left);
            experience.color = ScreenUiTheme.Muted;

            roomButton = Action("ReturnToRoom", card, font, "VỀ PHÒNG CHỜ", true, ArenaGlyph.Previous, new Vector2(.045f, .03f), new Vector2(.495f, .098f));
            menuButton = Action("ReturnToMenu", card, font, "VỀ TRANG CHỦ", false, ArenaGlyph.Home, new Vector2(.505f, .03f), new Vector2(.955f, .098f));
            roomButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = menuButton, selectOnUp = menuButton, selectOnDown = menuButton };
            menuButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = roomButton, selectOnUp = roomButton, selectOnDown = roomButton };
            panel.gameObject.SetActive(false);
        }

        static void Glass(RectTransform rect, bool quiet)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = !quiet;
            LobbyPanelStyle.SurfaceStyle(rect, false, quiet);
            if (!ScreenUiTheme.Current || !(quiet ? ScreenUiTheme.Current.quiet : ScreenUiTheme.Current.panel))
                image.color = quiet ? LobbyPanelStyle.Card : LobbyPanelStyle.Surface;
        }

        static RectTransform Tile(string name, RectTransform parent, TMP_FontAsset font, int index, string caption, out CanvasGroup group)
        {
            const float gap = .025f, width = (1 - 2 * gap) / 3;
            var tile = Rect(name, parent, new Vector2(index * (width + gap), 0), new Vector2(index * (width + gap) + width, 1));
            Glass(tile, true);
            group = tile.gameObject.AddComponent<CanvasGroup>();
            var label = Label("Caption", tile, font, 12, new Vector2(.06f, .82f), new Vector2(.94f, .95f));
            label.text = caption;
            label.color = ScreenUiTheme.Muted;
            label.characterSpacing = 5;
            return tile;
        }

        /// <summary>A thin track with a fill whose right anchor is the value.</summary>
        static RectTransform Bar(string name, RectTransform parent, Vector2 min, Vector2 max, float height, out Image fillImage)
        {
            var track = Rect(name, parent, min, max);
            track.offsetMin = new Vector2(0, -height / 2);
            track.offsetMax = new Vector2(0, height / 2);
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(0, 0, 0, .38f);
            trackImage.raycastTarget = false;
            var fill = Rect("Fill", track, Vector2.zero, new Vector2(0, 1));
            fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = ScreenUiTheme.Gold;
            fillImage.raycastTarget = false;
            return fill;
        }

        static Texture2D GlowTexture()
        {
            if (glowTexture)
                return glowTexture;
            const int n = 64;
            glowTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Summary Glow", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + .5f) / n * 2 - 1, dy = (y + .5f) / n * 2 - 1;
                    float a = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy));
                    a = a * a * (3 - 2 * a);
                    pixels[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            glowTexture.SetPixels32(pixels);
            glowTexture.Apply(false, true);
            return glowTexture;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = min;
            r.anchorMax = max;
            r.offsetMin = r.offsetMax = Vector2.zero;
            return r;
        }

        static void Pin(RectTransform r, Vector2 anchor, Vector2 position, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = new Vector2(.5f, .5f);
            r.anchoredPosition = position;
            r.sizeDelta = size;
        }

        static UiLineIcon Icon(string name, Transform parent, ArenaGlyph glyph, Color colour, float stroke)
        {
            var icon = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UiLineIcon)).GetComponent<UiLineIcon>();
            icon.transform.SetParent(parent, false);
            icon.glyph = glyph;
            icon.stroke = stroke;
            icon.color = colour;
            icon.raycastTarget = false;
            return icon;
        }

        static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, float size, Vector2 min, Vector2 max,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var t = Rect(name, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.fontSize = size;
            t.color = ScreenUiTheme.Text;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.enableAutoSizing = true;
            t.fontSizeMin = size * .7f;
            t.fontSizeMax = size;
            return t;
        }

        static Button Action(string name, Transform parent, TMP_FontAsset font, string caption, bool primary, ArenaGlyph glyph, Vector2 min, Vector2 max)
        {
            var r = Rect(name, parent, min, max);
            var image = r.gameObject.AddComponent<Image>();
            image.raycastTarget = true;
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = image;
            var ink = primary ? ScreenUiTheme.Ink : ScreenUiTheme.Text;
            var t = Label("Label", r, font, 19, Vector2.zero, Vector2.one);
            t.text = caption;
            t.fontStyle = FontStyles.Bold;
            t.characterSpacing = 4;
            t.rectTransform.offsetMin = new Vector2(40, 0);
            t.rectTransform.offsetMax = new Vector2(-40, 0);
            var icon = Icon("Icon", r, glyph, primary ? ScreenUiTheme.Ink : ScreenUiTheme.Gold, .09f);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, .5f);
            icon.rectTransform.pivot = new Vector2(0, .5f);
            icon.rectTransform.anchoredPosition = new Vector2(18, 0);
            icon.rectTransform.sizeDelta = new Vector2(20, 20);
            LobbyPanelStyle.Button(b, primary);
            if (!ScreenUiTheme.Current)
                image.color = primary ? ScreenUiTheme.Gold : LobbyPanelStyle.Card;
            t.color = ink;
            return b;
        }
    }
}
