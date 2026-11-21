using System;
using System.Linq;
using System.Threading.Tasks;
using PickleVerse.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using static PickleVerse.Multiplayer.MatchPresentationUi;

namespace PickleVerse.Multiplayer.UI
{
    /// <summary>
    /// Party-lobby layout: back and queue tabs on top, the party stage in the centre, queue controls along the
    /// bottom and a collapsible friends column on the right that lists real friends and sends room invitations.
    /// </summary>
    public sealed class LobbyFriendsSidebar : MonoBehaviour
    {
        const string OpenPreference = "PickleVerse.Lobby.FriendsOpen";
        const float Left = .025f, Right = .975f, Column = .755f, Gap = .015f, StageBottom = .205f, StageTop = .862f;
        static Sprite dot;
        PlayMultiplayerPresenter presenter;
        RectTransform panel, content, stage;
        CanvasGroup panelGroup;
        UnityEngine.UI.ScrollRect scroll;
        UiRowPool rows;
        TMP_Text status, onlineCount, toggleLabel, toggleChevron;
        UnityEngine.UI.Button toggle;
        TMP_FontAsset font;
        int generation;
        bool loading, sending, inviteReady;
        float searchAt = -1, nextRefresh, openAmount;
        public TMP_InputField Search { get; private set; }
        public InvitableFriendsPage Page { get; private set; } = new InvitableFriendsPage();
        public bool IsOpen { get; private set; }

        public static LobbyFriendsSidebar Attach(PlayMultiplayerPresenter presenter)
        {
            var sidebar = presenter.GetComponent<LobbyFriendsSidebar>();
            if (!sidebar)
            {
                sidebar = presenter.gameObject.AddComponent<LobbyFriendsSidebar>();
                sidebar.Build(presenter);
            }
            return sidebar;
        }

        void Build(PlayMultiplayerPresenter owner)
        {
            presenter = owner;
            font = ScreenUiTheme.Current ? ScreenUiTheme.Current.font : TMP_Settings.defaultFontAsset;
            var safe = (RectTransform)owner.back.transform.parent;
            ArrangeRoom(safe);
            BuildPanel(safe);
            BuildToggle(safe);
            IsOpen = PlayerPrefs.GetInt(OpenPreference, 1) == 1;
            openAmount = IsOpen ? 1 : 0;
            Apply();
            if (IsOpen)
                _ = Fetch(false);
        }

        // Reflow the authored room controls into top / stage / bottom bands.
        void ArrangeRoom(RectTransform safe)
        {
            Place(presenter.back, Left, .885f, .165f, .962f);
            if (presenter.roomType)
            {
                Place(presenter.roomType, .18f, .885f, .44f, .962f);
                presenter.roomType.alignment = TextAlignmentOptions.MidlineLeft;
                presenter.roomType.enableAutoSizing = true;
                presenter.roomType.fontSizeMin = 18;
                presenter.roomType.fontSizeMax = 34;
            }
            var tabs = Rect("Queue Tabs", safe, new Vector2(.445f, .892f), new Vector2(.715f, .956f));
            Reparent(presenter.singles, tabs, 0, 0, .49f, 1);
            Reparent(presenter.doubles, tabs, .51f, 0, 1, 1);
            stage = presenter.singlesPanel ? (RectTransform)presenter.singlesPanel.transform.parent : null;
            if (stage)
                Place(stage, Left, StageBottom, Right, StageTop);
            if (presenter.memberCount)
            {
                Place(presenter.memberCount, Left, .158f, .5f, .2f);
                presenter.memberCount.alignment = TextAlignmentOptions.MidlineLeft;
            }
            if (presenter.roomHint)
            {
                Place(presenter.roomHint, Left, .004f, .73f, .042f);
                presenter.roomHint.alignment = TextAlignmentOptions.MidlineLeft;
                presenter.roomHint.enableAutoSizing = true;
                presenter.roomHint.fontSizeMin = 11;
                presenter.roomHint.fontSizeMax = 15;
            }
            // Join-by-code and the room code chip sit together on the bottom-left.
            Reparent(presenter.joinCode, safe, Left, .052f, .185f, .128f);
            Reparent(presenter.join, safe, .19f, .052f, .3f, .128f);
            SetLabel(presenter.join, "THAM GIA");
            Reparent(presenter.roomCode, safe, .32f, .052f, .455f, .128f);
            if (presenter.roomCode)
            {
                presenter.roomCode.alignment = TextAlignmentOptions.MidlineLeft;
                presenter.roomCode.enableAutoSizing = true;
                presenter.roomCode.fontSizeMin = 11;
                presenter.roomCode.fontSizeMax = 18;
            }
            Reparent(presenter.copy, safe, .46f, .062f, .545f, .118f);
            SetLabel(presenter.copy, "SAO CHÉP");
            Place(presenter.invite, .56f, .052f, .735f, .128f);
            Place(presenter.retry, Column, .14f, Right, .195f);
            Place(presenter.play, Column, .045f, Right, .13f);
            if (presenter.matchTime)
            {
                Place(presenter.matchTime, Column, .138f, Right, .195f);
                presenter.matchTime.alignment = TextAlignmentOptions.Center;
            }
            // The left column only held controls that now live in the bands above.
            Transform nav = null;
            foreach (Transform child in safe)
                if (child.name == "NavPanel")
                    nav = child;
            if (nav)
                nav.gameObject.SetActive(false);
        }

        void BuildPanel(RectTransform safe)
        {
            panel = Image("Friends Panel", safe, new Vector2(Column, StageBottom), new Vector2(Right, StageTop), LobbyPanelStyle.Surface, true).rectTransform;
            LobbyPanelStyle.SurfaceStyle(panel, true);
            panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            var title = Label("Friends Title", panel, font, "BẠN BÈ", 21, new Vector2(.07f, .9f), new Vector2(.6f, .975f), ScreenUiTheme.Gold);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            onlineCount = Label("Friends Online", panel, font, "", 13, new Vector2(.45f, .9f), new Vector2(.93f, .975f), ScreenUiTheme.Mint);
            onlineCount.alignment = TextAlignmentOptions.MidlineRight;
            var field = Image("Friend Search", panel, new Vector2(.06f, .8f), new Vector2(.94f, .885f), LobbyPanelStyle.Background, true).rectTransform;
            Search = field.gameObject.AddComponent<TMP_InputField>();
            Search.characterLimit = 64;
            var input = Label("Text", field, font, "", 15, new Vector2(.05f, .08f), new Vector2(.95f, .92f), ScreenUiTheme.Text);
            input.alignment = TextAlignmentOptions.MidlineLeft;
            input.richText = false;
            input.fontStyle = FontStyles.Normal;
            var hint = Label("Placeholder", field, font, "Tìm theo tên hoặc ID…", 15, new Vector2(.05f, .08f), new Vector2(.95f, .92f), ScreenUiTheme.Muted);
            hint.alignment = TextAlignmentOptions.MidlineLeft;
            hint.fontStyle = FontStyles.Italic;
            Search.textViewport = field;
            Search.textComponent = input;
            Search.placeholder = hint;
            Search.targetGraphic = field.GetComponent<UnityEngine.UI.Image>();
            Search.onValueChanged.AddListener(_ => searchAt = Time.unscaledTime + .3f);
            var area = Rect("Friend List", panel, new Vector2(.035f, .1f), new Vector2(.965f, .785f));
            scroll = area.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            var viewport = Image("Viewport", area, Vector2.zero, Vector2.one, Color.clear, true).rectTransform;
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            content = Rect("Content", viewport, new Vector2(0, 1), Vector2.one);
            content.pivot = new Vector2(.5f, 1);
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.padding = new RectOffset(2, 2, 2, 6);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            rows = content.gameObject.AddComponent<UiRowPool>();
            status = Label("Friends Status", panel, font, "", 12, new Vector2(.06f, .015f), new Vector2(.94f, .09f), ScreenUiTheme.Muted);
            status.fontStyle = FontStyles.Normal;
            status.richText = false;
        }

        void BuildToggle(RectTransform safe)
        {
            toggle = Button("Friends Toggle", safe, font, "", new Vector2(.835f, .892f), new Vector2(Right, .956f), false);
            LobbyPanelStyle.Button(toggle);
            toggleLabel = toggle.GetComponentInChildren<TMP_Text>();
            toggleLabel.fontSize = 16;
            toggleLabel.alignment = TextAlignmentOptions.MidlineLeft;
            toggleLabel.margin = new Vector4(14, 0, 30, 0);
            toggleChevron = Label("Chevron", toggle.transform, font, "", 24, new Vector2(.78f, 0), new Vector2(.96f, 1), ScreenUiTheme.Gold);
            toggle.onClick.AddListener(() => SetOpen(!IsOpen, false));
        }

        public void Open(bool focusSearch)
        {
            SetOpen(true, focusSearch);
        }

        void SetOpen(bool open, bool focusSearch)
        {
            if (IsOpen != open)
            {
                IsOpen = open;
                PlayerPrefs.SetInt(OpenPreference, open ? 1 : 0);
                if (open)
                    _ = Fetch(false);
            }
            if (open && focusSearch && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(Search.gameObject);
            Apply();
        }

        void Update()
        {
            float target = IsOpen ? 1 : 0;
            if (!Mathf.Approximately(openAmount, target))
            {
                openAmount = Mathf.MoveTowards(openAmount, target, Time.unscaledDeltaTime / .18f);
                Apply();
            }
            bool ready = presenter && presenter.CanInviteFriends;
            if (ready != inviteReady)
            {
                inviteReady = ready;
                Render();
            }
            if (!IsOpen || !isActiveAndEnabled)
                return;
            if (searchAt >= 0 && Time.unscaledTime >= searchAt)
            {
                searchAt = -1;
                _ = Fetch(false);
            }
            else if (!loading && !sending && Page.Offset == 0 && Time.unscaledTime >= nextRefresh)
                _ = Fetch(false);
        }

        void Apply()
        {
            float t = Mathf.SmoothStep(0, 1, openAmount);
            if (stage)
            {
                stage.anchorMax = new Vector2(Mathf.Lerp(Right, Column - Gap, t), stage.anchorMax.y);
                stage.offsetMax = new Vector2(0, stage.offsetMax.y);
            }
            if (panel)
            {
                panel.anchoredPosition = new Vector2((1 - t) * 48, 0);
                panelGroup.alpha = t;
                panelGroup.interactable = panelGroup.blocksRaycasts = t > .6f;
                panel.gameObject.SetActive(t > .001f);
            }
            if (presenter.invite)
                presenter.invite.transform.localScale = IsOpen ? Vector3.zero : Vector3.one;
            RefreshToggle();
        }

        void RefreshToggle()
        {
            if (!toggle)
                return;
            int online = Page.Players.Count(p => p.Status != "offline");
            toggleLabel.text = "BẠN BÈ" + (online > 0 ? "  <color=#8FE9D8>" + online + "</color>" : "");
            // Points the way the column will move: › hides it to the right, ‹ brings it back.
            toggleChevron.text = IsOpen ? "›" : "‹";
        }

        public async Task Fetch(bool append)
        {
            var runtime = RoomInvitationsRuntime.Instance;
            if (runtime == null || runtime.Client == null)
            {
                Page = new InvitableFriendsPage();
                Render();
                status.text = "Đăng nhập để xem bạn bè và gửi lời mời.";
                return;
            }
            int run = ++generation;
            loading = true;
            nextRefresh = Time.unscaledTime + 5;
            if (Page.Players.Length == 0)
                status.text = "Đang tải danh sách bạn bè…";
            try
            {
                await runtime.PublishNowAsync();
                var page = await runtime.Client.FriendsAsync(Search.text, append ? Page.Offset + 50 : 0);
                if (!this || run != generation)
                    return;
                if (append)
                    page.Players = Page.Players.Concat(page.Players).GroupBy(p => p.Id).Select(g => g.Last()).ToArray();
                Page = page;
                Render();
                status.text = page.Players.Length == 0
                    ? string.IsNullOrWhiteSpace(Search.text) ? "Chưa có bạn bè. Kết bạn từ mục Bạn bè ở sảnh chính." : "Không tìm thấy bạn phù hợp."
                    : inviteReady ? "Chỉ bạn đang ở sảnh mới nhận được lời mời." : "Chọn Đấu đôi để mời bạn vào phòng.";
            }
            catch (Exception error)
            {
                if (this && run == generation)
                    status.text = error.Message;
            }
            finally
            {
                if (run == generation)
                    loading = false;
            }
        }

        void Render()
        {
            if (!rows)
                return;
            // A fresh list starts at the top; later refreshes keep the reader's scroll position.
            float position = rows.ActiveCount == 0 ? 1 : scroll.verticalNormalizedPosition;
            rows.ReleaseAll();
            var ordered = Page.Players.OrderBy(p => Rank(p.Status)).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
            int online = ordered.Count(p => p.Status != "offline");
            if (online > 0)
                Section("TRỰC TUYẾN — " + online);
            for (int i = 0; i < ordered.Length; i++)
            {
                if (ordered[i].Status == "offline" && (i == 0 || ordered[i - 1].Status != "offline"))
                    Section("NGOẠI TUYẾN — " + (ordered.Length - online));
                Row(ordered[i]);
            }
            if (Page.HasMore)
            {
                var row = rows.Take("more", "More friends", 36);
                var more = row.Find("More")?.GetComponent<UnityEngine.UI.Button>() ?? Button("More", row, font, "XEM THÊM", Vector2.zero, Vector2.one, false);
                LobbyPanelStyle.Button(more);
                more.GetComponentInChildren<TMP_Text>().fontSize = 14;
                more.onClick.AddListener(() => _ = Fetch(true));
            }
            onlineCount.text = Page.Players.Length == 0 ? "" : online + " trực tuyến";
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.verticalNormalizedPosition = position;
            RefreshToggle();
        }

        void Section(string text)
        {
            var row = rows.Take("section", "Section", 24);
            var image = row.GetComponent<UnityEngine.UI.Image>();
            image.material = null;
            image.color = Color.clear;
            var label = row.Find("Heading")?.GetComponent<TMP_Text>() ?? Label("Heading", row, font, "", 11, new Vector2(.03f, 0), new Vector2(.97f, 1), ScreenUiTheme.Muted);
            label.alignment = TextAlignmentOptions.BottomLeft;
            label.characterSpacing = 4;
            label.text = text;
        }

        void Row(InvitableFriend friend)
        {
            var row = rows.Take("friend", friend.Id, 58);
            var avatar = row.Find("Avatar") as RectTransform;
            if (!avatar)
            {
                avatar = Image("Avatar", row, new Vector2(0, .5f), new Vector2(0, .5f), LobbyPanelStyle.Background).rectTransform;
                avatar.sizeDelta = new Vector2(40, 40);
                avatar.anchoredPosition = new Vector2(28, 0);
                LobbyPanelStyle.SurfaceStyle(avatar, false, true);
                Label("Initial", avatar, font, "", 18, Vector2.zero, Vector2.one, ScreenUiTheme.Gold);
                var presence = Image("Presence", avatar, new Vector2(1, 0), new Vector2(1, 0), Color.white).rectTransform;
                presence.sizeDelta = new Vector2(13, 13);
                presence.anchoredPosition = new Vector2(-2, 2);
                presence.GetComponent<UnityEngine.UI.Image>().sprite = Dot();
            }
            avatar.Find("Initial").GetComponent<TMP_Text>().text = Initial(friend.Name);
            avatar.Find("Presence").GetComponent<UnityEngine.UI.Image>().color = PresenceColor(friend.Status);
            var name = row.Find("Friend Name")?.GetComponent<TMP_Text>() ?? Label("Friend Name", row, font, "", 16, new Vector2(0, .5f), new Vector2(1, .94f), ScreenUiTheme.Text);
            bool showButton = friend.Status != "offline" || friend.Pending;
            float inset = showButton ? 70 : 10;
            name.rectTransform.offsetMin = new Vector2(58, 0);
            name.rectTransform.offsetMax = new Vector2(-inset, 0);
            name.alignment = TextAlignmentOptions.BottomLeft;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.richText = false;
            name.text = friend.Name;
            var state = row.Find("Friend State")?.GetComponent<TMP_Text>() ?? Label("Friend State", row, font, "", 12, new Vector2(0, .06f), new Vector2(1, .5f), ScreenUiTheme.Muted);
            state.rectTransform.offsetMin = new Vector2(58, 0);
            state.rectTransform.offsetMax = new Vector2(-inset, 0);
            state.alignment = TextAlignmentOptions.TopLeft;
            state.fontStyle = FontStyles.Normal;
            state.textWrappingMode = TextWrappingModes.NoWrap;
            state.overflowMode = TextOverflowModes.Ellipsis;
            state.text = PresenceText(friend.Status) + " · Elo " + friend.Elo;
            var button = row.Find("Invite Friend")?.GetComponent<UnityEngine.UI.Button>();
            if (!button)
            {
                button = Button("Invite Friend", row, font, "MỜI", new Vector2(1, .5f), new Vector2(1, .5f), true);
                var rect = (RectTransform)button.transform;
                rect.sizeDelta = new Vector2(58, 30);
                rect.anchoredPosition = new Vector2(-36, 0);
            }
            bool canInvite = friend.CanInvite && !friend.Pending && inviteReady && !sending;
            LobbyPanelStyle.Button(button, canInvite);
            var label = button.GetComponentInChildren<TMP_Text>();
            label.fontSize = 12;
            label.text = friend.Pending ? "ĐÃ MỜI" : "MỜI";
            if (friend.Pending)
                label.color = ScreenUiTheme.Mint;
            button.interactable = canInvite;
            button.gameObject.SetActive(showButton);
            button.onClick.AddListener(() => _ = SendAsync(friend));
        }

        public async Task SendAsync(InvitableFriend friend)
        {
            if (sending || !friend.CanInvite || !inviteReady)
                return;
            sending = true;
            Render();
            try
            {
                await RoomInvitationsRuntime.Instance.PublishNowAsync();
                await RoomInvitationsRuntime.Instance.Client.SendAsync(friend.Id);
                if (!this)
                    return;
                friend.Pending = true;
                friend.CanInvite = false;
                status.text = "Đã gửi lời mời cho " + friend.Name + ".";
            }
            catch (Exception error)
            {
                if (this)
                    status.text = error.Message;
            }
            finally
            {
                sending = false;
                if (this)
                    Render();
            }
        }

        static int Rank(string status) => status == "lobby" ? 0 : status == "in_room" ? 1 : status == "busy" ? 2 : 3;
        static string PresenceText(string status) => status == "lobby" ? "Ở sảnh" : status == "in_room" ? "Trong phòng" : status == "busy" ? "Đang bận" : "Ngoại tuyến";
        static Color PresenceColor(string status) => status == "lobby" ? ScreenUiTheme.Mint : status == "in_room" ? ScreenUiTheme.Gold : status == "busy" ? new Color32(240, 140, 90, 255) : new Color32(96, 108, 128, 255);
        static string Initial(string name)
        {
            var trimmed = (name ?? "").Trim();
            return trimmed.Length == 0 ? "?" : char.ToUpperInvariant(trimmed[0]).ToString();
        }
        static Sprite Dot()
        {
            if (dot)
                return dot;
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Presence Dot", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            float radius = size * .5f - 1;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float distance = new Vector2(x + .5f - size * .5f, y + .5f - size * .5f).magnitude;
                    float rim = Mathf.Clamp01(radius - distance + .5f), core = Mathf.Clamp01(radius - 4 - distance + .5f);
                    byte shade = (byte)(Mathf.Lerp(10, 255, core));
                    pixels[y * size + x] = new Color32(shade, shade, shade, (byte)(rim * 255));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            dot = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100);
            return dot;
        }

        static void Place(Component component, float xMin, float yMin, float xMax, float yMax)
        {
            if (!component)
                return;
            var rect = (RectTransform)component.transform;
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        static void Reparent(Component component, Transform parent, float xMin, float yMin, float xMax, float yMax)
        {
            if (!component)
                return;
            component.transform.SetParent(parent, false);
            Place(component, xMin, yMin, xMax, yMax);
        }
        static void SetLabel(UnityEngine.UI.Button button, string text)
        {
            var label = button ? button.GetComponentInChildren<TMP_Text>(true) : null;
            if (label)
                label.text = text;
        }
    }
}
