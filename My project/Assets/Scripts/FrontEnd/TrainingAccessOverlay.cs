#if !UNITY_SERVER
using System;
using PickleVerse.Progression;
using PickleVerse.Services;
using PickleVerse.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PickleVerse.FrontEnd
{
    /// <summary>Account choice, cached training entry and durable-sync feedback.</summary>
    public sealed class TrainingAccessOverlay : MonoBehaviour
    {
        /// <summary>Wording and symbol of the sync/offline banner for one state (pure, unit-tested).</summary>
        public readonly struct BannerText
        {
            public readonly string title, detail, action;
            public readonly ArenaGlyph glyph;
            public readonly bool attention;
            public BannerText(string title, string detail, string action, ArenaGlyph glyph, bool attention)
            {
                this.title = title; this.detail = detail; this.action = action; this.glyph = glyph; this.attention = attention;
            }
        }

        public static BannerText Describe(bool offline, int pending, string message, bool signedIn)
        {
            string action = signedIn ? "Đồng bộ" : "Đăng nhập";
            if (offline)
                return new BannerText("Đang luyện tập ngoại tuyến",
                    pending > 0 ? pending + " mục chờ đồng bộ · tự gửi khi bạn đăng nhập" : "Tiến trình được lưu trên máy · đăng nhập để đồng bộ",
                    action, ArenaGlyph.CloudOff, true);
            if (pending > 0)
                return new BannerText("Đang đồng bộ tiến trình", pending + " mục chờ xác nhận · XP và sao đang tạm tính", action, ArenaGlyph.Sync, true);
            return new BannerText("Đồng bộ tiến trình", string.IsNullOrEmpty(message) ? "Tiến trình đã đồng bộ." : message, action, ArenaGlyph.Sync, false);
        }

        const float BannerTop = 134, DockBottom = 8;
        bool docked;
        GameObject viewRoot;
        Canvas viewCanvas;
        RectTransform loginEntry, banner, modal, cover, choices, pager;
        TMP_Text bannerTitle, bannerDetail, syncLabel, title, detail, hint;
        UiLineIcon bannerIcon, modalIcon;
        Button newcomer, experienced, previous, next, close, sync, failures, offlineEntry;
        bool busy, showingFailures, modalShown;
        int shownMode;    // 0 hidden, 1 onboarding choice, 2 sync details
        int shownPaging = -1;     // -1 not laid out yet
        int failureIndex, shownFailureIndex = -1, shownFailureCount = -1, shownPending = -1, shownFailures = -1;
        string tutorialPlayer, lastMessage, shownMessage, sceneName = "";
        int tutorialSession = -1;
        Scene nameScene;
        bool shownOffline, shownSignedIn;
        float messageUntil;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() => new GameObject("Training Access").AddComponent<TrainingAccessOverlay>();
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += Loaded;
            SceneManager.activeSceneChanged += ActiveSceneChanged;
        }
        void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive && scene != SceneManager.GetActiveScene()) return;
            BindScene(scene);
        }
        void ActiveSceneChanged(Scene previous, Scene current)
        {
            // Sign-in activates the already-loaded Menu after preparing the account.
            if (current.isLoaded) BindScene(current);
        }
        void BindScene(Scene scene)
        {
            if (viewRoot && viewRoot.scene == scene) return;

            FrontEndModalInput.Release(this);
            if (viewRoot) Destroy(viewRoot);
            viewRoot = null;
            if (scene.name != "Login" && scene.name != "Menu" && scene.name != "Training" && scene.name != "Tutorial") return;

            docked = scene.name != "Menu";
            viewRoot = ArenaForms.Canvas("Training Access View", 1400);
            viewCanvas = viewRoot.GetComponent<Canvas>();
            SceneManager.MoveGameObjectToScene(viewRoot, scene);
            Build();
        }

        void Build()
        {
            shownPending = shownFailureIndex = shownFailureCount = shownFailures = shownPaging = -1;
            shownMessage = null;
            modalShown = false; shownMode = 0;

            // Login: practise offline. A glass card in the bottom-right corner, clear of the sign-in form.
            loginEntry = ArenaForms.Rect("Offline Entry", viewRoot.transform);
            ArenaForms.Pin(loginEntry, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-34, 32), new Vector2(512, 96));
            offlineEntry = ArenaForms.Choice("Open Offline", loginEntry, ArenaGlyph.Run, "Luyện tập ngoại tuyến", "Học viện, hướng dẫn · không cần mạng", false, () =>
            {
                var runtime = ProgressionRuntime.Instance;
                if (runtime.TryEnterOffline()) SceneLoading.Load("Menu");
            });
            ArenaForms.Fill((RectTransform)offlineEntry.transform, Vector2.zero, Vector2.one);
            var arrow = ArenaForms.Icon("Go", offlineEntry.transform, ArenaGlyph.Arrow, ScreenUiTheme.Gold, .09f);
            ArenaForms.Pin(arrow.rectTransform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-22, 0), new Vector2(22, 22));
            LobbyEtching.AddTo((RectTransform)offlineEntry.transform, true);
            foreach (var label in offlineEntry.GetComponentsInChildren<TMP_Text>(true))
            {
                label.rectTransform.offsetMax = new Vector2(-54, label.rectTransform.offsetMax.y);    // clear of the arrow
                label.textWrappingMode = TextWrappingModes.NoWrap;                                   // one line each, never spill out
                label.overflowMode = TextOverflowModes.Ellipsis;
            }

            // Menu: slim status bar below the top HUD (profile and currency plates end at ~118 px).
            // Academy / tutorial: their own headers fill the top, so a compact bar docks at the bottom centre,
            // under the lesson cards and input cues and between the touch controls.
            float height = docked ? 60 : 78, badgeSize = docked ? 44 : 54, textLeft = docked ? 64 : 84, textRight = docked ? 270 : 300;
            Vector2 actionSize = docked ? new Vector2(124, 42) : new Vector2(146, 50), detailSize = docked ? new Vector2(112, 42) : new Vector2(124, 50);
            banner = ArenaForms.Surface("Training Sync", viewRoot.transform, !docked, false);
            ArenaForms.Pin(banner, new Vector2(.5f, docked ? 0 : 1), new Vector2(.5f, docked ? 0 : 1), BannerPosition(1), new Vector2(docked ? 780 : 940, height));
            var badge = ArenaForms.Inset("Badge", banner);
            ArenaForms.Pin(badge, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(docked ? 8 : 14, 0), new Vector2(badgeSize, badgeSize));
            bannerIcon = ArenaForms.Icon("State", badge, ArenaGlyph.CloudOff, ArenaForms.Warning, .1f);
            float pad = docked ? 8 : 10;
            ArenaForms.Fill(bannerIcon.rectTransform, Vector2.zero, Vector2.one, pad, pad, pad, pad);
            bannerTitle = ArenaForms.Label("Title", banner, "", docked ? 19 : 21, ScreenUiTheme.Gold);
            ArenaForms.Fill(bannerTitle.rectTransform, new Vector2(0, .5f), new Vector2(1, 1), textLeft, 0, textRight, docked ? 6 : 10);
            bannerTitle.alignment = TextAlignmentOptions.BottomLeft;
            bannerDetail = ArenaForms.Label("Status", banner, "", docked ? 16 : 17, new Color(ScreenUiTheme.Text.r, ScreenUiTheme.Text.g, ScreenUiTheme.Text.b, .86f));
            ArenaForms.Fill(bannerDetail.rectTransform, new Vector2(0, 0), new Vector2(1, .5f), textLeft, docked ? 6 : 10, textRight, 2);
            bannerDetail.alignment = TextAlignmentOptions.TopLeft;
            sync = ArenaForms.Action("Sync", banner, "Đăng nhập", true, Sync, null, docked ? 18 : 20);
            ArenaForms.Pin((RectTransform)sync.transform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-(detailSize.x + 24), 0), actionSize);
            syncLabel = sync.GetComponentInChildren<TMP_Text>(true);
            failures = ArenaForms.Action("Failures", banner, "Chi tiết", false, () => { showingFailures = true; failureIndex = 0; }, null, docked ? 18 : 20);
            ArenaForms.Pin((RectTransform)failures.transform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-12, 0), detailSize);

            // Onboarding and sync details share one centred dialog.
            cover = ArenaForms.Cover("Cover", viewRoot.transform);
            modal = ArenaForms.Surface("Onboarding", cover);
            ArenaForms.Pin(modal, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(800, 470));
            modalIcon = ArenaForms.Icon("Icon", modal, ArenaGlyph.Book, ScreenUiTheme.Gold);
            ArenaForms.Pin(modalIcon.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, .5f), new Vector2(0, -54), new Vector2(40, 40));
            title = ArenaForms.Label("Title", modal, "Làm quen với PickleVerse", ArenaForms.TitleSize, ScreenUiTheme.Gold, TextAlignmentOptions.Center);
            ArenaForms.Pin(title.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, .5f), new Vector2(0, -108), new Vector2(720, 48));
            var rule = ArenaForms.Rule("Rule", modal, new Color(ScreenUiTheme.Gold.r, ScreenUiTheme.Gold.g, ScreenUiTheme.Gold.b, .45f));
            ArenaForms.Pin(rule.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, .5f), new Vector2(0, -140), new Vector2(120, 2));
            detail = ArenaForms.Label("Detail", modal, "", ArenaForms.BodySize, ScreenUiTheme.Text, TextAlignmentOptions.Center, true);
            ArenaForms.Pin(detail.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -158), new Vector2(700, 116));
            detail.alignment = TextAlignmentOptions.Top;

            choices = ArenaForms.Rect("Choices", modal);
            ArenaForms.Pin(choices, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 62), new Vector2(720, 120));
            newcomer = ArenaForms.Choice("Newcomer", choices, ArenaGlyph.Book, "Người mới", "Hướng dẫn luật, điều khiển và cú đánh", true, () => Choose(true));
            ArenaForms.Fill((RectTransform)newcomer.transform, new Vector2(0, 0), new Vector2(.5f, 1), 0, 0, 10, 0);
            experienced = ArenaForms.Choice("Experienced", choices, ArenaGlyph.Play, "Đã thành thạo", "Vào sảnh ngay · xem lại trong Học viện", false, () => Choose(false));
            ArenaForms.Fill((RectTransform)experienced.transform, new Vector2(.5f, 0), new Vector2(1, 1), 10, 0, 0, 0);
            newcomer.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = experienced };
            experienced.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = newcomer };
            hint = ArenaForms.Label("Hint", modal, "← → để chọn · Enter để xác nhận", ArenaForms.HintSize, ScreenUiTheme.Muted, TextAlignmentOptions.Center);
            ArenaForms.Pin(hint.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, .5f), new Vector2(0, 30), new Vector2(700, 22));

            pager = ArenaForms.Rect("Pager", modal);
            ArenaForms.Pin(pager, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 40), new Vector2(720, 60));
            previous = ArenaForms.Action("Previous", pager, "Trước", false, () => failureIndex--, ArenaGlyph.Previous, 21);
            ArenaForms.Fill((RectTransform)previous.transform, new Vector2(0, 0), new Vector2(.3f, 1), 0, 0, 8, 0);
            next = ArenaForms.Action("Next", pager, "Sau", false, () => failureIndex++, ArenaGlyph.Next, 21);
            ArenaForms.Fill((RectTransform)next.transform, new Vector2(.3f, 0), new Vector2(.6f, 1), 8, 0, 8, 0);
            close = ArenaForms.Action("Close", pager, "Đóng", true, () => showingFailures = false, null, 21);
            ArenaForms.Fill((RectTransform)close.transform, new Vector2(.64f, 0), new Vector2(1, 1));

            cover.gameObject.SetActive(false); loginEntry.gameObject.SetActive(false); banner.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!viewRoot) return;
            var runtime = ProgressionRuntime.Instance; if (!runtime) return;
            // Scene.name allocates on every call; read it once per active scene.
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene != nameScene) { nameScene = activeScene; sceneName = activeScene.name; }
            string scene = sceneName;
            ArenaForms.Show(loginEntry, scene == "Login" && runtime.HasOfflineProfile && !SceneLoading.IsBlocking);
            bool playable = scene == "Menu" || scene == "Training" || scene == "Tutorial";
            bool needsChoice = scene == "Menu" && runtime.Ready && runtime.Snapshot.catalog.contentSchemaVersion >= 2 && string.IsNullOrEmpty(runtime.Snapshot.experienceChoice);
            bool showModal = playable && !SceneLoading.IsBlocking && (needsChoice || showingFailures);
            if (showModal && !FrontEndModalInput.IsOwnedBy(this)) showModal = FrontEndModalInput.TryAcquire(this);
            ArenaForms.Show(cover, showModal);
            if (!showModal) FrontEndModalInput.Release(this);
            if (showModal) RefreshModal(runtime, needsChoice);
            modalShown = showModal;
            if (!showModal) shownMode = 0;

            if (runtime.SyncMessage != lastMessage) { lastMessage = runtime.SyncMessage; messageUntil = Time.unscaledTime + 10; }
            bool visible = playable && runtime.Ready && (runtime.IsOffline || runtime.PendingOperations > 0 || runtime.SyncFailures.Count > 0 || !string.IsNullOrEmpty(lastMessage) && Time.unscaledTime < messageUntil);
            bool anotherModalIsOpen = FrontEndModalInput.IsOpen && !FrontEndModalInput.IsOwnedBy(this);
            // The dialog already explains the state; keep the banner out from behind its dimmer.
            bool bannerVisible = visible && !showModal && !SceneLoading.IsBlocking && !anotherModalIsOpen;
            ArenaForms.Show(banner, bannerVisible);
            if (bannerVisible)
            {
                var position = BannerPosition(viewCanvas.scaleFactor);
                if (banner.anchoredPosition != position) banner.anchoredPosition = position;
                RefreshBanner(runtime);
            }

            if (scene == "Menu" && runtime.Ready && !showModal && !SceneLoading.IsBlocking && runtime.Snapshot.experienceChoice == "NEW" && !runtime.Snapshot.tutorial.completed && !runtime.Snapshot.tutorial.skipped)
            {
                string player = runtime.Player.id; int session = AccountRuntime.Ensure().Account.SessionVersion;
                if (tutorialPlayer != player || tutorialSession != session) { tutorialPlayer = player; tutorialSession = session; SceneLoading.Load("Tutorial"); }
            }
        }

        /// <summary>Top bar stays below the desktop title bar when the PickleVerse window frame is showing.</summary>
        Vector2 BannerPosition(float scale) => docked ? new Vector2(0, DockBottom) : new Vector2(0, -BannerTop - DesktopWindow.TopInset / Mathf.Max(.01f, scale));

        /// <summary>Text is rebuilt only when the underlying state changes (no per-frame string work).</summary>
        void RefreshBanner(ProgressionRuntime runtime)
        {
            bool offline = runtime.IsOffline, signedIn = AccountRuntime.Ensure().Account.IsSignedIn;
            int pending = runtime.PendingOperations, failed = runtime.SyncFailures.Count;
            if (offline == shownOffline && pending == shownPending && failed == shownFailures && signedIn == shownSignedIn && ReferenceEquals(runtime.SyncMessage, shownMessage))
                return;
            shownOffline = offline; shownPending = pending; shownFailures = failed; shownSignedIn = signedIn; shownMessage = runtime.SyncMessage;
            var text = Describe(offline, pending, runtime.SyncMessage, signedIn);
            ArenaForms.Set(bannerTitle, text.title);
            ArenaForms.Set(bannerDetail, text.detail);
            ArenaForms.Set(syncLabel, text.action);
            bannerIcon.SetGlyph(text.glyph);
            bannerIcon.color = text.attention ? ArenaForms.Warning : ScreenUiTheme.Mint;
            bool details = failed > 0 || !string.IsNullOrEmpty(runtime.SyncMessage);
            ArenaForms.Show(failures, details);
            // The primary action takes the edge when there is nothing to inspect.
            var at = new Vector2(details ? -(((RectTransform)failures.transform).sizeDelta.x + 24) : -12, 0);
            var syncRect = (RectTransform)sync.transform;
            if (syncRect.anchoredPosition != at) syncRect.anchoredPosition = at;
        }

        void RefreshModal(ProgressionRuntime runtime, bool needsChoice)
        {
            int mode = needsChoice ? 1 : 2;
            bool opening = !modalShown || shownMode != mode;    // also when the dialog switches content while open
            shownMode = mode;
            if (needsChoice)
            {
                if (opening)
                {
                    modalIcon.SetGlyph(ArenaGlyph.Book);
                    ArenaForms.Set(title, "Bạn đã quen với pickleball chưa?");
                    ShowDetail("Người mới sẽ được hướng dẫn luật, cách di chuyển và các cú đánh cơ bản. Nếu đã thành thạo, bạn có thể vào sảnh ngay.", false);
                    ArenaForms.Show(choices, true); ArenaForms.Show(hint, !Application.isMobilePlatform); ArenaForms.Show(pager, false);
                    if (EventSystem.current) EventSystem.current.SetSelectedGameObject(newcomer.gameObject);
                }
                newcomer.interactable = experienced.interactable = !busy;
                return;
            }
            if (opening)
            {
                modalIcon.SetGlyph(ArenaGlyph.Sync);
                ArenaForms.Show(choices, false); ArenaForms.Show(hint, false); ArenaForms.Show(pager, true);
                shownFailureIndex = shownFailureCount = -1;
                shownPaging = -1;
                if (EventSystem.current) EventSystem.current.SetSelectedGameObject(close.gameObject);
            }
            int count = runtime.SyncFailures.Count;
            // Previous/next only when there is something to page through; otherwise one centred Close.
            bool paging = count > 1;
            if ((paging ? 1 : 0) != shownPaging)
            {
                shownPaging = paging ? 1 : 0;
                ArenaForms.Show(previous, paging); ArenaForms.Show(next, paging);
                if (paging) ArenaForms.Fill((RectTransform)close.transform, new Vector2(.64f, 0), Vector2.one);
                else ArenaForms.Fill((RectTransform)close.transform, new Vector2(.32f, 0), new Vector2(.68f, 1));
            }
            if (count > 0)
            {
                failureIndex = Mathf.Clamp(failureIndex, 0, count - 1);
                if (failureIndex != shownFailureIndex || count != shownFailureCount)
                {
                    shownFailureIndex = failureIndex; shownFailureCount = count;
                    var failure = runtime.SyncFailures[failureIndex];
                    ArenaForms.Set(title, "Chi tiết đồng bộ · " + (failureIndex + 1) + "/" + count);
                    var lesson = failure.attempt?.definition ?? TrainingCurriculum.Find(failure.attempt?.lessonId);
                    ShowDetail((failure.attempt != null ? (lesson?.title ?? "Lượt luyện tập") + "\n" : "") + failure.message, true);
                }
                previous.interactable = failureIndex > 0; next.interactable = failureIndex + 1 < count;
            }
            else if (shownFailureCount != 0)
            {
                shownFailureCount = 0;
                ArenaForms.Set(title, "Đồng bộ tiến trình");
                ShowDetail(runtime.SyncMessage ?? "Không có lượt bị từ chối.", false);
                previous.interactable = next.interactable = false;
            }
        }

        void ShowDetail(string value, bool problem)
        {
            ArenaForms.Set(detail, value);
            detail.color = problem ? ArenaForms.Error : ScreenUiTheme.Text;
        }

        async void Choose(bool isNew)
        {
            if (busy) return; busy = true; ShowDetail("Đang lưu lựa chọn…", false);
            try
            {
                await ProgressionRuntime.Instance.SetExperienceAsync(isNew);
                if (!this) return;
                if (isNew) { tutorialPlayer = ProgressionRuntime.Instance.Player.id; tutorialSession = AccountRuntime.Ensure().Account.SessionVersion; FrontEndModalInput.Release(this); SceneLoading.Load("Tutorial"); }
            }
            catch (Exception error) { if (this && detail) ShowDetail(error.Message, true); }
            finally { busy = false; }
        }
        async void Sync()
        {
            if (!AccountRuntime.Ensure().Account.IsSignedIn) { ProgressionRuntime.Instance.ExitOffline(); SceneLoading.Load("Login"); return; }
            try { await ProgressionRuntime.Instance.SyncOfflineAsync(); }
            catch (Exception error) { Debug.LogWarning("[Training] Sync pending (" + error.GetType().Name + ")."); }
        }
        void OnDestroy()
        {
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.activeSceneChanged -= ActiveSceneChanged;
            FrontEndModalInput.Release(this);
            if (viewRoot) Destroy(viewRoot);
        }
    }
}
#endif
