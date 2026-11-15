#if !UNITY_SERVER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PickleVerse.Replays;
using PickleVerse.Services;
using PickleVerse.Services.Api;
using PickleVerse.Settings;
using PickleVerse.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace PickleVerse.FrontEnd
{
    internal sealed partial class SocialLobbyMenu : MonoBehaviour
    {
        const string Base = "api/Game/social/";
        static readonly Color Ink = LobbyPanelStyle.Background, Surface = LobbyPanelStyle.Surface, Card = LobbyPanelStyle.Card,
            Mint = ScreenUiTheme.Mint, Gold = ScreenUiTheme.Gold, White = ScreenUiTheme.Text, Muted = ScreenUiTheme.Muted;
        OfflineSocialMenu friends;
        PlayerProgressionMenu ui;
        AccountService Account => AccountRuntime.Ensure().Account;
        Guid Self => Account.Profile?.UserId ?? Guid.Empty;
        RectTransform root, window, rows, editor, mediaPanel;
        GameObject safe;
        CanvasGroup home;
        ScrollRect scroll;
        TMP_Text header, feedback;
        TMP_InputField entry;
        VideoPlayer player;
        RenderTexture videoSurface;
        SocialLobbyConnection live;
        int liveSession = -1;
        CancellationTokenSource request = new CancellationTokenSource();
        bool opened, originalHome, submitting, renderingMessages;
        long? next;
        int offset;
        enum ScreenView { Feed, Compose, Comments, Inbox, Whispers, Direct }
        ScreenView view;
        Post selected;
        Guid recipient, conversation, messageId;
        string recipientName, messageBody;
        bool canSend, whispers;
        readonly List<Whisper> whispersReceived = new List<Whisper>();
        readonly List<Message> messages = new List<Message>();

        [Serializable] sealed class Page<T> { public T[] items; public long? nextBefore; }
        [Serializable] sealed class Post
        {
            public long postId;
            public Guid authorId;
            public Guid? mediaId, replayMatchId;
            public Guid[] exclude;
            public string author, body, audience;
            public DateTime createdAt;
            public bool hasVideo, liked;
            public int likes, comments;
        }
        [Serializable] sealed class Comment { public long commentId; public Guid authorId; public string author, body; }
        [Serializable] sealed class InboxItem
        {
            public Guid conversationId, playerId;
            public string playerName, lastMessage;
            public int unread;
            public bool canSend;
        }
        [Serializable] sealed class Message { public long messageId; public Guid conversationId, senderId; public string body; }
        [Serializable] sealed class Whisper { public Guid senderId, recipientId; public string sender, body; }
        [Serializable] sealed class ConversationLookup { public Guid? conversationId; }
        [Serializable] sealed class FriendPage { public Friend[] players; public bool hasMore; public int offset; }
        [Serializable] sealed class Friend { public Guid id; public string name, relationship; }
        [Serializable] sealed class Playback { public string path; }

        public void Bind(OfflineSocialMenu source)
        {
            friends = source;
            ui = GetComponent<PlayerProgressionMenu>();
            home = transform.Find("HomeSafeArea")?.GetComponent<CanvasGroup>();
            Account.Changed += AccountChanged;
        }

        void Update()
        {
            if (!friends) return;
            bool lobby = SceneManager.GetActiveScene().name == "Menu" && Account.IsSignedIn && !Account.IsRestricted;
            if (lobby && live == null)
            {
                liveSession = Account.SessionVersion;
                live = new SocialLobbyConnection();
                live.Received += OnLiveMessage;
                live.Connected += RefreshDirect;
                _ = live.Run();
            }
            else if (!lobby && live != null) StopLive();
            if (!lobby && opened) Close();
            if (opened && !ModerationOverlay.IsOpen && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                Close();
            if (opened) PickleVerse.Multiplayer.MatchPresentationUi.SafeArea((RectTransform)safe.transform);
        }

        void StopLive()
        {
            if (live == null) return;
            live.Received -= OnLiveMessage;
            live.Connected -= RefreshDirect;
            live.Dispose();
            live = null;
            whispersReceived.Clear();
        }
        void AccountChanged()
        {
            if (liveSession != Account.SessionVersion || !Account.IsSignedIn || Account.IsRestricted)
            {
                StopLive();
                Close();
            }
        }
        void OnDestroy()
        {
            if (AccountRuntime.Instance) Account.Changed -= AccountChanged;
            StopLive();
            StopVideo();
            request.Cancel();
            request.Dispose();
            FrontEndModalInput.Release(this);
            if (root) Destroy(root.gameObject);
        }
        void OnDisable()
        {
            StopLive();
            Close();
        }

        void OnLiveMessage(string method, JArray args)
        {
            if (args == null || args.Count != 1) return;
            if (method == "MessageChanged")
            {
                if (opened && (view == ScreenView.Inbox || view == ScreenView.Direct && !whispers))
                    RefreshDirect();
                return;
            }
            if (method != "WhisperReceived") return;
            var message = args[0].ToObject<Whisper>();
            if (message == null) return;
            AddWhisper(message);
            if (!opened)
            {
                if (FrontEndModalInput.IsOpen) return;
                OpenDirect(message.senderId, message.sender, false);
                return;
            }
            if (view == ScreenView.Direct && whispers && recipient == message.senderId) RenderWhispers();
            else if (view == ScreenView.Whispers) OpenWhispers();
            else feedback.text = "Thì thầm mới từ " + message.sender;
        }
        void AddWhisper(Whisper message)
        {
            if (whispersReceived.Count == 50) whispersReceived.RemoveAt(0);
            whispersReceived.Add(message);
        }
        void RefreshDirect()
        {
            if (!opened) return;
            if (view == ScreenView.Inbox) _ = LoadInbox(reset: true);
            else if (view == ScreenView.Direct && !whispers) _ = LoadMessages(reset: true);
        }

        void Build()
        {
            root = ui.Panel("LobbySocial", friends.modal.parent, new Color(0,0,0,.83f));
            root.GetComponent<Image>().raycastTarget = true;
            safe = ui.Rect("SafeArea", root, Vector2.zero, Vector2.one).gameObject;
            window = ui.Panel("Window", safe.transform, Ink, new Vector2(.065f,.065f), new Vector2(.935f,.935f));
            header = Label("Title", window, "", 34, White, .035f,.87f,.47f,.98f);
            Action("Feed", window, "Bảng tin", OpenFeed, .48f,.9f,.60f,.97f);
            Action("Inbox", window, "Hộp thư", OpenInbox, .61f,.9f,.73f,.97f);
            Action("Friends", window, "Bạn bè", () => { Close(); friends.OpenFriends(); }, .74f,.9f,.86f,.97f);
            Action("Close", window, "×", Close, .90f,.9f,.965f,.97f);
            feedback = Label("Feedback", window, "", 19, Muted, .035f,.02f,.97f,.08f);
            var holder = ui.Panel("Scroll", window, Surface, new Vector2(.035f,.19f), new Vector2(.965f,.86f));
            var viewport = ui.Panel("Viewport", holder, Ink, new Vector2(.007f,.012f), new Vector2(.993f,.988f));
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            rows = ui.Rect("Content", viewport, new Vector2(0,1), Vector2.one);
            rows.pivot = new Vector2(.5f,1);
            var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 9;
            layout.padding = new RectOffset(10,10,10,10);
            layout.childControlWidth = layout.childControlHeight = layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = holder.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = rows;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 42;
            editor = ui.Rect("Composer", window, new Vector2(.035f,.083f), new Vector2(.965f,.18f));
        }

        bool Open(ScreenView screen, string title)
        {
            if (!Account.IsSignedIn) return false;
            if (!opened)
            {
                friends.Close();
                if (!FrontEndModalInput.TryAcquire(this)) return false;
                if (!root) Build();
                if (!home) home = transform.Find("HomeSafeArea")?.GetComponent<CanvasGroup>();
                originalHome = home && home.interactable;
                if (home) home.interactable = false;
            }
            request.Cancel();
            request.Dispose();
            request = new CancellationTokenSource();
            StopVideo();
            opened = true;
            submitting = renderingMessages = false;
            pendingDirectRefresh = false;
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            view = screen;
            header.text = title;
            feedback.text = "";
            next = null;
            offset = 0;
            Clear(rows);
            Clear(editor);
            var picker = window.Find("Picker");
            if (picker)
            {
                picker.gameObject.SetActive(false);
                picker.SetParent(null, false);
                Destroy(picker.gameObject);
            }
            return true;
        }
        void Close()
        {
            if (!opened) return;
            opened = false;
            request.Cancel();
            StopVideo();
            root.gameObject.SetActive(false);
            if (home) home.interactable = originalHome;
            FrontEndModalInput.Release(this);
        }
        static void Clear(Transform container)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                child.gameObject.SetActive(false);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }
        TMP_Text Label(string name, Transform parent, string value, int size, Color color,
            float left, float bottom, float right, float top)
        {
            var label = ui.Text(name, parent, value, size, color, new Vector2(left,bottom), new Vector2(right,top), Vector2.zero, Vector2.zero);
            label.richText = false;
            return label;
        }
        Button Action(string name, Transform parent, string text, UnityEngine.Events.UnityAction action,
            float left, float bottom, float right, float top, bool primary = false)
            => ui.MakeButton(name, parent, text, action, new Vector2(left,bottom), new Vector2(right,top),
                primary ? Mint : Card, primary ? Ink : White, 18);
        RectTransform Row(string name, float height) => ui.AddListPanel(name, rows, Card, height);
        void Info(string text) => Label("Info", Row("InfoRow", 50), text, 19, Muted, .02f,0,.98f,1);
        void More(UnityEngine.Events.UnityAction load)
        {
            var row = Row("MoreRow", 55);
            Action("More", row, "Xem thêm", () => { row.gameObject.SetActive(false); Destroy(row.gameObject); load(); }, 0,0,1,1);
        }
        void Top()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rows);
            scroll.verticalNormalizedPosition = 1;
        }
        static string Cursor(long? before) => before.HasValue ? "?before=" + before.Value.ToString(CultureInfo.InvariantCulture) : "";
        Task<T> Get<T>(string path, CancellationToken token) => Account.SendAuthorizedAsync<T>("GET", Base + path, cancellation: token);
        Task<T> Send<T>(string method, string path, object body, CancellationToken token)
            => Account.SendAuthorizedAsync<T>(method, Base + path, body, token);
        async Task Run(Func<CancellationToken, Task> action)
        {
            if (submitting) return;
            submitting = true;
            var token = request.Token;
            try { await action(token); }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (this && !token.IsCancellationRequested) feedback.text = error.Message; }
            finally { if (this && !token.IsCancellationRequested) submitting = false; }
        }
    }
}
#endif
