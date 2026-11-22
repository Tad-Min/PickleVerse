#if !UNITY_SERVER
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using PickleVerse.Replays;
using PickleVerse.Services;
using PickleVerse.Services.Api;
using PickleVerse.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PickleVerse.FrontEnd
{
    public sealed class WebReplayLauncher : MonoBehaviour
    {
        static WebReplayLauncher instance;
        readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
        ReplayWebLink pending;
        string openingCode, openedCode;
        bool opening, stopped, notified;
        GameObject notice;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        WindowsReplayBridge bridge;
#endif
        public static bool OwnsNavigation => instance && instance.opening || ReplayLaunch.IsExternal &&
            (SceneManager.GetActiveScene().name == "Replay" || SceneLoading.IsPreparedTransition && SceneLoading.Destination == "Replay");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => instance = null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            var launcher = new GameObject("Web Replay Launcher").AddComponent<WebReplayLauncher>();
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                launcher.bridge = new WindowsReplayBridge(AccountRuntime.Ensure().Settings.baseUrl, launcher.Enqueue);
                try { WindowsReplayBridge.Register(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName); }
                catch (Exception error) { Debug.LogWarning("[ReplayLaunch] Cannot register Windows protocol: " + error.GetType().Name); }
                string uri = Environment.GetCommandLineArgs().FirstOrDefault(value => ReplayWebLink.TryParse(value, out _));
                if (uri == null) return;
                if (!launcher.bridge.IsPrimary)
                {
                    bool delivered = launcher.bridge.Forward(uri);
                    if (delivered || !launcher.bridge.IsPrimary)
                    {
                        if (!delivered) Debug.LogWarning("[ReplayLaunch] Chưa chuyển được yêu cầu tới game đang mở. Hãy tạo vé mới và thử lại.");
                        launcher.stopped = true;
                        Application.Quit();
                        return;
                    }
                }
                launcher.Enqueue(uri);
            }
            catch (Exception error)
            {
                Debug.LogWarning("[ReplayLaunch] Windows integration: " + error.GetType().Name);
            }
#endif
        }
        void Awake()
        {
            if (instance && instance != this) { Destroy(gameObject); return; }
            instance = this; DontDestroyOnLoad(gameObject);
        }
        public void Enqueue(string uri)
        {
            if (incoming.Count < 8 && ReplayWebLink.TryParse(uri, out _)) incoming.Enqueue(uri);
        }
        void Update()
        {
            if (stopped) return;
            while (incoming.TryDequeue(out string uri))
                if (ReplayWebLink.TryParse(uri, out var link) && link.Code != openedCode && link.Code != openingCode)
                {
                    pending = link;
                    notified = false;
                }
            if (pending == null || opening || SceneLoading.IsBlocking || FrontEndModalInput.IsOpen) return;
            var app = MultiplayerApp.Instance;
            bool busy = app && (app.Busy || !app.CanChangeLocalProfile);
            string scene = SceneManager.GetActiveScene().name;
            if (busy || scene == "MultiplayerCourt" || scene == "BotMatch" || scene == "Training" || scene == "Tutorial")
            {
                if (!notified)
                {
                    notified = true;
                    const string text = "Có yêu cầu xem replay từ trang Admin. Kết thúc hoặc rời trận/phòng hiện tại để mở replay.";
                    if (app) app.SetNotice(text);
                    if (MultiplayerCourtUI.Instance) MultiplayerCourtUI.Instance.ShowAnnouncement("YÊU CẦU XEM REPLAY", text, 5);
                    Debug.Log("[ReplayLaunch] Waiting for the current game to finish.");
                }
                return;
            }
            var next = pending; pending = null;
            Open(next);
        }
        void Open(ReplayWebLink link)
        {
            if (notice) Destroy(notice);
            opening = true;
            openingCode = link.Code;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            WindowsReplayBridge.Foreground();
#endif
            var account = AccountRuntime.Ensure();
            var api = new ApiClient(account.GetComponent<UnityApiTransport>());
            var repository = new TaskCompletionSource<IOnlineReplayRepository>(TaskCreationOptions.RunContinuationsAsynchronously);
            string BackScene() => ProgressionRuntime.Instance.IsAuthenticated ? "Menu" : "Login";
            string back = BackScene();
            ReplayLaunch.SelectExternal(link.MatchId, repository.Task, () => SceneLoading.Load(BackScene()));
            var presentation = ReplayLaunch.PresentationReady;
            bool failed = false;
            bool started = SceneLoading.LoadPrepared("Replay", async (token, report) =>
            {
                try
                {
                    report(.1f, "Đang xác nhận vé xem replay");
                    var access = await api.SendAsync<WebReplayAccess>("POST", "api/replay-launch/redeem", new { code = link.Code }, cancellation: token);
                    var scoped = new WebReplayRepository(api, access, link.MatchId);
                    repository.TrySetResult(scoped);
                    openedCode = link.Code;
                    report(.65f, "Đang tải replay và nhân vật");
                    while (!presentation.Task.IsCompleted) { token.ThrowIfCancellationRequested(); await Task.Yield(); }
                    await presentation.Task;
                    report(1, "Replay đã sẵn sàng");
                }
                catch (Exception error)
                {
                    repository.TrySetException(error);
                    if (repository.Task.IsFaulted) { var ignored = repository.Task.Exception; }
                    throw;
                }
            }, error =>
            {
                failed = true;
                // The replay scene may still activate under the cover before rollback.
                repository.TrySetCanceled();
                ShowError(error is ApiException || error is System.IO.InvalidDataException ? error.Message : "Chưa mở được replay. Hãy bấm Xem replay lại trên trang Admin.");
            }, back);
            if (!started)
            {
                opening = false; openingCode = null;
                repository.TrySetCanceled(); ReplayLaunch.ClearExternal(); pending = link;
                return;
            }
            _ = FinishOpening();

            async Task FinishOpening()
            {
                while (this && SceneLoading.IsBlocking) await Task.Yield();
                if (!this) return;
                opening = false;
                openingCode = null;
                if (ReplayLaunch.ExternalRepository != repository.Task) return;
                if (failed) ReplayLaunch.ClearExternal();
                else if (SceneManager.GetActiveScene().name == "Replay")
                    Debug.Log("[ReplayLaunch] Opened the requested replay without changing the game login.");
            }
        }
        void ShowError(string message)
        {
            if (notice) Destroy(notice);
            notice = ArenaForms.Canvas("Replay launch message", 2200); DontDestroyOnLoad(notice);
            var cover = ArenaForms.Cover("Cover", notice.transform);
            var card = ArenaForms.Surface("Message", cover, true);
            ArenaForms.Pin(card, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(760, 330));
            var title = ArenaForms.Label("Title", card, "XEM REPLAY", 30, ScreenUiTheme.Gold);
            ArenaForms.Pin(title.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -28), new Vector2(680, 44));
            var text = ArenaForms.Label("Message", card, message, 23, ScreenUiTheme.Text);
            ArenaForms.Pin(text.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(680, 140));
            text.richText = false;
            var close = ArenaForms.Action("Close", card, "Đóng", true, () => { if (notice) Destroy(notice); });
            ArenaForms.Pin((RectTransform)close.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 24), new Vector2(220, 56));
        }
        void OnApplicationQuit() { stopped = true; }
        void OnDestroy()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            bridge?.Dispose();
#endif
            if (notice) Destroy(notice);
            if (instance == this) instance = null;
        }
    }
}
#endif
