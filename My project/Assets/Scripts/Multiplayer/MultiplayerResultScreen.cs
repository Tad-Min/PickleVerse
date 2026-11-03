using System.Linq;
using PickleVerse.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PickleVerse.Multiplayer
{
    // The summary reads the saved, idempotently applied result, including the actual Elo floor.
    public sealed class MultiplayerResultScreen : MonoBehaviour
    {
        public RectTransform panel, safeArea;
        public TMP_Text heading, opponent, delta, elo, rank, score, experience;
        public UnityEngine.UI.Button roomButton, menuButton;
        public bool IsVisible => panel && panel.gameObject.activeSelf;
        public string RenderedMatchId { get; private set; }
        MultiplayerCourtUI court;
        bool requested;
        public void Open()
        {
            if (court && court.cinematic && court.cinematic.OutcomeReady)
                requested = true;
        }
        void Start()
        {
            court = GetComponent<MultiplayerCourtUI>();
            Configure(court);
            roomButton.onClick.AddListener(court.ReturnToRoom);
            menuButton.onClick.AddListener(court.LeaveToMenu);
        }
        void Update()
        {
            var match = NetworkMatch.Instance;
            if (!requested || !court || !match || !match.IsSpawned || !match.Finished)
            {
                if (panel)
                    panel.gameObject.SetActive(false);
                RenderedMatchId = null;
                return;
            }
            if (!IsVisible)
            {
                panel.gameObject.SetActive(true);
                panel.SetAsLastSibling();
                court.ClearTouch();
                if (court.help)
                    court.help.SetActive(false);
                EventSystem.current?.SetSelectedGameObject(roomButton.gameObject);
                heading.text = "ĐANG XÁC NHẬN KẾT QUẢ";
                delta.text = elo.text = rank.text = score.text = experience.text = opponent.text = "";
                roomButton.interactable = menuButton.interactable = false;
            }
            ApplySafeArea();
            if (ProgressionRuntime.Exists && RenderedMatchId != match.Roster.matchId)
            {
                var player = ProgressionRuntime.Instance.Player;
                var result = player.history.Find(r => r.id == match.Roster.matchId);
                if (result != null)
                    Present(player, result);
            }
            bool busy = MultiplayerApp.Instance && MultiplayerApp.Instance.Busy;
            roomButton.interactable = menuButton.interactable = RenderedMatchId != null && !busy;
        }
        void ApplySafeArea()
        {
            if (Screen.width <= 0 || Screen.height <= 0)
                return;
            var area = Screen.safeArea;
            safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }
        public void Present(PlayerProfile player, MatchResult result)
        {
            heading.text = result.outcome == MatchOutcome.Won ? "THẮNG" : result.outcome == MatchOutcome.Lost ? "THUA" : "TRẬN ĐẤU GIÁN ĐOẠN";
            heading.color = result.outcome == MatchOutcome.Won ? new Color(.79f, .96f, .45f) : Color.white;
            opponent.text = player.playerName + "  đấu với  " + string.Join(" / ", result.opponents.Select(p => p.name));
            delta.text = (result.eloDelta >= 0 ? "+" : "") + result.eloDelta + " ELO";
            delta.color = result.eloDelta < 0 ? new Color(.98f, .66f, .48f) : new Color(.45f, .86f, .77f);
            elo.text = result.Ranked ? $"{result.ownElo:N0} → {result.ownElo + result.eloDelta:N0} ELO" : $"{player.elo:N0} ELO • Chế độ này không đổi Elo";
            rank.text = EloPolicy.Rank(player.elo).ToUpperInvariant() + $"\n<size=22>Hạng hiện tại • {player.elo:N0} Elo</size>";
            score.text = $"SỐ VÁN THẮNG  {result.gamesWon} – {result.gamesLost}\n<size=20>" + string.Join("    •    ", result.scores.Select((g, i) => $"Ván {i + 1}: {g.own}–{g.opponent}" + (g.completed ? "" : " (chưa xong)"))) + "</size>";
            experience.text = $"CẤP {player.level}    •    {player.levelXp:N0} / {player.NextLevelXp:N0} XP";
            RenderedMatchId = result.id;
            score.rectTransform.anchorMax = new Vector2(.49f, .34f);
            score.enableAutoSizing = true; score.fontSizeMin = 14;
            PickleVerse.LocalMatch.CoachingSessionView.Attach((RectTransform)experience.transform.parent, result.id, "match", new Vector2(.52f, .24f), new Vector2(.94f, .33f),
                () => MultiplayerApp.Instance.ReturnToModeSelectionAsync());
        }
        public void Configure(MultiplayerCourtUI ui)
        {
            if (panel || !ui || !ui.modalTitle || !ui.modal)
                return;
            var font = ui.modalTitle.font;
            panel = Rect("MultiplayerSummary", transform, Vector2.zero, Vector2.one);
            var backdrop = panel.gameObject.AddComponent<UnityEngine.UI.Image>();
            backdrop.color = new Color(.015f, .04f, .055f, 1);
            backdrop.raycastTarget = true;
            safeArea = Rect("SafeArea", panel, Vector2.zero, Vector2.one);
            var card = Rect("Card", safeArea, new Vector2(.08f, .07f), new Vector2(.92f, .93f));
            var surface = card.gameObject.AddComponent<UnityEngine.UI.Image>();
            surface.color = new Color(.045f, .115f, .15f);
            surface.raycastTarget = true;
            surface.sprite = ui.modal.GetComponent<UnityEngine.UI.Image>()?.sprite;
            surface.type = UnityEngine.UI.Image.Type.Sliced;
            Label("Caption", card, font, 20, new Vector2(.06f, .88f), new Vector2(.94f, .95f)).text = "PICKLEVERSE / TỔNG KẾT TRẬN ĐẤU";
            heading = Label("Outcome", card, font, 46, new Vector2(.06f, .73f), new Vector2(.94f, .88f));
            heading.fontStyle = FontStyles.Bold;
            opponent = Label("Players", card, font, 23, new Vector2(.06f, .65f), new Vector2(.94f, .73f));
            opponent.richText = false;
            delta = Label("EloChange", card, font, 50, new Vector2(.06f, .43f), new Vector2(.48f, .62f));
            delta.fontStyle = FontStyles.Bold;
            elo = Label("EloBeforeAfter", card, font, 20, new Vector2(.06f, .36f), new Vector2(.48f, .44f));
            rank = Label("CurrentRank", card, font, 34, new Vector2(.52f, .36f), new Vector2(.94f, .62f));
            rank.fontStyle = FontStyles.Bold;
            score = Label("Games", card, font, 24, new Vector2(.06f, .21f), new Vector2(.94f, .34f));
            experience = Label("Experience", card, font, 20, new Vector2(.06f, .15f), new Vector2(.94f, .22f));
            roomButton = Button("ReturnToRoom", card, font, "VỀ PHÒNG CHỜ", new Vector2(.06f, .035f), new Vector2(.48f, .13f), new Color(.45f, .86f, .77f), new Color(.025f, .065f, .095f));
            menuButton = Button("ReturnToMenu", card, font, "VỀ TRANG CHỦ", new Vector2(.52f, .035f), new Vector2(.94f, .13f), new Color(.075f, .20f, .25f), Color.white);
            roomButton.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnRight = menuButton, selectOnUp = menuButton, selectOnDown = menuButton };
            menuButton.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnLeft = roomButton, selectOnUp = roomButton, selectOnDown = roomButton };
            panel.gameObject.SetActive(false);
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
        static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, float size, Vector2 min, Vector2 max)
        {
            var t = Rect(name, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.fontSize = size;
            t.color = new Color(.94f, .97f, .96f);
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.enableAutoSizing = true;
            t.fontSizeMin = size * .75f;
            t.fontSizeMax = size;
            return t;
        }
        static UnityEngine.UI.Button Button(string name, Transform parent, TMP_FontAsset font, string caption, Vector2 min, Vector2 max, Color background, Color foreground)
        {
            var r = Rect(name, parent, min, max);
            var image = r.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = background;
            image.raycastTarget = true;
            var b = r.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.targetGraphic = image;
            var t = Label("Label", r, font, 22, new Vector2(.03f, .08f), new Vector2(.97f, .92f));
            t.text = caption;
            t.color = foreground;
            t.fontStyle = FontStyles.Bold;
            return b;
        }
    }
}
