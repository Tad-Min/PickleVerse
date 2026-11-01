using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using PickleVerse.Services;
using PickleVerse.Services.Api;
using PickleVerse.Progression;
using PickleVerse.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace PickleVerse.FrontEnd
{
    /// <summary>Account/session coordinator; views belong to Login or Menu, never DontDestroyOnLoad.</summary>
    public sealed class AccountScreen : MonoBehaviour
    {
        enum Page { Login, Register, Verify, Profile }
        public const string LoginScene = "Login";
        static AccountScreen instance;
        AccountService account;
        AccountPanelView view;
        MenuAccountView menu;
        CancellationTokenSource sceneLifetime;
        Page page;
        bool busy, navigating, sceneBound;
        int operationVersion, lastResendSeconds = -1;
        double resendAt;
        TMP_InputField focusAfterRun;
        bool loginRetryPending;
        string sessionEmail, attemptedEmail;
        Exception pendingLoginFailure;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => instance = null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { if (!instance) AccountRuntime.Ensure().gameObject.AddComponent<AccountScreen>(); }
        void Awake()
        {
            if (instance && instance != this) { Destroy(this); return; }
            instance = this;
            account = AccountRuntime.Ensure().Account;
            account.Changed += OnAccountChanged;
            ProgressionRuntime.Changed += OnGameChanged;
            SceneManager.sceneLoaded += SceneLoaded;
            SceneManager.activeSceneChanged += ActiveSceneChanged;
        }
        void ActiveSceneChanged(Scene previous, Scene current)
        {
            // An additive Menu is bound only after its data preparation has succeeded.
            if (current.name == "Menu" && SceneLoading.IsPreparedTransition && SceneLoading.Destination == "Menu"
                && (!menu || menu.gameObject.scene != current))
                SceneLoaded(current, LoadSceneMode.Single);
        }
        void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive && scene != SceneManager.GetActiveScene()) return;
            sceneBound = true;
            navigating = false;
            operationVersion++;
            sceneLifetime?.Cancel(); sceneLifetime?.Dispose();
            sceneLifetime = new CancellationTokenSource();
            busy = false;
            FrontEndModalInput.Release(this);
            view = null; menu = null;
            if (scene.name == "Loading" && SceneLoading.IsPreparedTransition) return;
            EnsureEventSystem();
            if (scene.name == LoginScene)
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    view = root.GetComponentInChildren<AccountPanelView>(true);
                    if (view) break;
                }
                if (!view) { Debug.LogError("Login scene is missing its AccountPanelView."); return; }
                BindView();
                loginRetryPending = pendingLoginFailure != null;
                Show(pendingLoginFailure is ApiException api && (api.Code == "email_not_verified" || api.Code == "email_exists") ? Page.Verify : Page.Login);
                if (pendingLoginFailure != null)
                {
                    view.email.SetTextWithoutNotify(attemptedEmail ?? "");
                    Message(pendingLoginFailure is ApiException failure ? failure.Message : "Chưa thể vào sảnh. Vui lòng thử lại.", true);
                    pendingLoginFailure = null;
                }
                RefreshVisibility();
            }
            else if (!account.IsSignedIn) Navigate(LoginScene);
            else if (scene.name == "Menu")
            {
                menu = Instantiate(Resources.Load<MenuAccountView>("Auth/MenuAccount"));
                SceneManager.MoveGameObjectToScene(menu.gameObject, scene);
                view = menu.panel;
                BindView(); menu.open.onClick.AddListener(OpenAccount);
                view.gameObject.SetActive(false); RefreshVisibility();
            }
            StartCoroutine(ApplyScopeAfterSceneReady());
        }
        void BindView()
        {
            view.submit.onClick.AddListener(Submit);
            if (view.switchPage) view.switchPage.onClick.AddListener(() => Show(page == Page.Login ? Page.Register : Page.Login));
            if (view.verifyLink) view.verifyLink.onClick.AddListener(() => Show(Page.Verify));
            if (view.resend) view.resend.onClick.AddListener(Resend);
            if (view.logout) view.logout.onClick.AddListener(Logout);
            if (view.close) view.close.onClick.AddListener(CloseAccount);
            if (view.email) view.email.onSubmit.AddListener(_ => Focus(page == Page.Verify ? view.otp : view.password));
            if (view.password) view.password.onSubmit.AddListener(_ => { if (page == Page.Register) Focus(view.confirmPassword); else Submit(); });
            if (view.confirmPassword) view.confirmPassword.onSubmit.AddListener(_ => Submit());
            if (view.otp) view.otp.onSubmit.AddListener(_ => Submit());
            if (view.playerName) view.playerName.onSubmit.AddListener(_ => Submit());
            view.form.interactable = true; lastResendSeconds = -1;
        }
        IEnumerator ApplyScopeAfterSceneReady() { yield return null; ApplyOnlineScope(); }
        void ApplyOnlineScope()
        {
            var runtime = ProgressionRuntime.Instance;
            if (!runtime) return;
            bool available = account.IsSignedIn && runtime.Ready;
            foreach (var home in FindObjectsByType<HomeMenuShell>(FindObjectsInactive.Include))
            {
                if (home.playButton) home.playButton.interactable = available;
                if (home.wallet) home.wallet.gameObject.SetActive(available);
                if (home.nav)
                    foreach (var button in home.nav.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                        if (!button.name.Contains("Setting") && !button.name.Contains("Music")) button.interactable = available;
            }
            foreach (var progression in FindObjectsByType<PlayerProgressionMenu>(FindObjectsInactive.Include))
            {
                if (!available) progression.Close();
                progression.enabled = available;
                foreach (var button in new[] { progression.profileButton, progression.missionsButton, progression.careerButton, progression.progressionButton })
                    if (button) button.interactable = available;
                if (progression.editButton) progression.editButton.gameObject.SetActive(false);
            }
            foreach (var social in FindObjectsByType<OfflineSocialMenu>(FindObjectsInactive.Include))
            {
                if (!available) social.Close();
                social.enabled = available;
                if (social.friendsButton) social.friendsButton.interactable = available;
                if (social.leaderboardButton) social.leaderboardButton.interactable = available;
            }
            foreach (var home in FindObjectsByType<HomeLobbyPresentation>(FindObjectsInactive.Include))
            {
                if (!home.CharacterChangePending) home.ApplyAccountCharacter(account.Profile?.IsMale == true);
                if (home.maleButton) home.maleButton.interactable = available && !home.CharacterChangePending;
                if (home.femaleButton) home.femaleButton.interactable = available && !home.CharacterChangePending;
            }
        }
        void OnGameChanged()
        {
            if (!sceneBound) return;
            RefreshVisibility(); ApplyOnlineScope();
        }
        void OnAccountChanged()
        {
            if (!sceneBound) return;
            if (view)
            {
                if (view.playerName && account.Profile != null) view.playerName.SetTextWithoutNotify(account.Profile.PlayerName);
                if (!account.IsSignedIn)
                {
                    if (view.password) view.password.text = "";
                    if (view.confirmPassword) view.confirmPassword.text = "";
                }
            }
            RefreshVisibility(); ApplyOnlineScope();
        }
        void RefreshVisibility()
        {
            if (menu) menu.open.gameObject.SetActive(account.IsSignedIn && ProgressionRuntime.Instance.Ready && !view.gameObject.activeSelf);
            if (SceneLoading.IsPreparedTransition) return;
            if (!sceneBound || navigating || !SceneManager.GetActiveScene().isLoaded) return;
            bool login = SceneManager.GetActiveScene().name == LoginScene;
            if (!account.IsSignedIn)
            {
                if (!login) Navigate(LoginScene);
                else if (view && page == Page.Profile) Show(Page.Login);
                return;
            }
            if (login && view)
            {
                if (!busy && !loginRetryPending) BeginMenuLoading(sessionEmail, null, true);
            }
        }
        public void OpenAccount()
        {
            if (!menu || !account.IsSignedIn || !FrontEndModalInput.TryAcquire(this)) return;
            view.gameObject.SetActive(true); menu.open.gameObject.SetActive(false);
            view.playerName.SetTextWithoutNotify(account.Profile.PlayerName); Show(Page.Profile);
        }
        void CloseAccount()
        {
            if (busy || !menu) return;
            view.gameObject.SetActive(false); menu.open.gameObject.SetActive(true); FrontEndModalInput.Release(this);
        }
        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>()) return;
            new GameObject("Account EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule))
                .GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        void Update()
        {
            if (!view || !view.gameObject.activeInHierarchy) return;
            if (!busy && Keyboard.current?.tabKey.wasPressedThisFrame == true && EventSystem.current)
            {
                var choices = view.navigation;
                int current = Array.FindIndex(choices, s => s && s.gameObject == EventSystem.current.currentSelectedGameObject);
                int direction = Keyboard.current.shiftKey.isPressed ? -1 : 1;
                if (current < 0) current = direction > 0 ? -1 : 0;
                for (int step = 1; step <= choices.Length; step++)
                {
                    int index = (current + direction * step + choices.Length * 2) % choices.Length;
                    var choice = choices[index];
                    if (choice && choice.gameObject.activeInHierarchy && choice.IsInteractable()) { choice.Select(); break; }
                }
            }
            if (menu && Keyboard.current?.escapeKey.wasPressedThisFrame == true) CloseAccount();
            if (page == Page.Verify && view.resend)
            {
                int seconds = Mathf.Max(0, (int)Math.Ceiling(resendAt - Time.realtimeSinceStartupAsDouble));
                if (seconds != lastResendSeconds)
                {
                    lastResendSeconds = seconds;
                    view.resendLabel.text = seconds > 0 ? $"Gửi lại mã ({seconds}s)" : "Gửi lại mã xác thực";
                    view.resend.interactable = seconds == 0;
                }
            }
        }
        static void Visible(Component c, bool visible) { if (c) c.gameObject.SetActive(visible); }
        static void FieldVisible(TMP_InputField f, bool visible) { if (f) f.transform.parent.gameObject.SetActive(visible); }
        void Show(Page next)
        {
            if (!view) return;
            page = next;
            bool profile = next == Page.Profile;
            view.ConcealPasswords();
            view.title.text = next == Page.Login ? "Chào mừng trở lại" : next == Page.Register ? "Khởi đầu hành trình" : next == Page.Verify ? "Xác thực email" : "Tài khoản của bạn";
            view.subtitle.text = next == Page.Verify ? $"Nhập mã 6 chữ số được gửi đến email của bạn.{(account.OtpLifetimeMinutes > 0 ? $" Mã có hiệu lực {account.OtpLifetimeMinutes} phút." : "")}"
                : profile ? $"LV {account.Profile.Level}  ·  {account.Profile.Elo} ELO  ·  {account.Profile.WinCount} thắng"
                : next == Page.Register ? "Tạo tài khoản để lưu tiến trình và cùng bạn bè thi đấu." : "Đăng nhập để tiếp tục hành trình PickleVerse.";
            FieldVisible(view.email, !profile);
            FieldVisible(view.password, next == Page.Login || next == Page.Register);
            FieldVisible(view.confirmPassword, next == Page.Register);
            FieldVisible(view.playerName, profile); FieldVisible(view.otp, next == Page.Verify);
            Visible(view.verifyLink, next == Page.Login); Visible(view.resend, next == Page.Verify);
            Visible(view.logout, profile); Visible(view.close, profile);
            if (view.switchPage) view.switchPage.transform.parent.gameObject.SetActive(!profile);
            if (view.switchHint) view.switchHint.text = next == Page.Login ? "Chưa có tài khoản?" : next == Page.Register ? "Đã có tài khoản?" : "Đã xác thực email?";
            if (view.switchLabel) view.switchLabel.text = next == Page.Login ? "Đăng ký" : "Đăng nhập";
            view.submitLabel.text = next == Page.Login ? "ĐĂNG NHẬP" : next == Page.Register ? "TẠO TÀI KHOẢN" : next == Page.Verify ? "XÁC THỰC" : "LƯU THAY ĐỔI";
            view.feedback.text = "";
            view.FitContent();
        }
        void Message(string text, bool error = false)
        {
            if (!view) return;
            view.feedback.text = text ?? "";
            view.feedback.color = error ? new Color32(255, 179, 167, 255) : ScreenUiTheme.Mint;
        }
        static void Focus(TMP_InputField field) { if (field) { field.Select(); field.ActivateInputField(); } }
        void Validate(bool valid, string message, TMP_InputField field)
        {
            if (valid) return;
            focusAfterRun = field; throw new ArgumentException(message);
        }
        async void Submit() => await SubmitAsync();
        async Task SubmitAsync() => await Run(async cancellation =>
        {
            string mail = view.email ? view.email.text.Trim() : "";
            if (page != Page.Profile) Validate(mail.Length > 0 && mail.Contains("@") && mail.LastIndexOf('@') < mail.Length - 1, "Vui lòng nhập địa chỉ email hợp lệ.", view.email);
            if (page == Page.Login)
            {
                bool resume = account.IsSignedIn && string.Equals(mail, sessionEmail, StringComparison.OrdinalIgnoreCase) && view.password.text.Length == 0;
                Validate(resume || view.password.text.Length > 0, "Vui lòng nhập mật khẩu.", view.password);
                if (!BeginMenuLoading(mail, view.password.text, resume))
                    throw new InvalidOperationException("Loading is unavailable.");
                view.password.text = "";
            }
            else if (page == Page.Register)
            {
                Validate(view.password.text.Length >= 6, "Mật khẩu cần ít nhất 6 ký tự.", view.password);
                Validate(view.password.text == view.confirmPassword.text, "Mật khẩu xác nhận chưa khớp.", view.confirmPassword);
                await account.RegisterAsync(new RegisterRequest { Email = mail, Password = view.password.text, ConfirmPassword = view.confirmPassword.text }, cancellation);
                view.password.text = view.confirmPassword.text = "";
                resendAt = Time.realtimeSinceStartupAsDouble + account.OtpResendCooldownSeconds; lastResendSeconds = -1;
                Show(Page.Verify); Message("Mã xác thực đã được gửi. Hãy kiểm tra cả thư rác.");
            }
            else if (page == Page.Verify)
            {
                Validate(view.otp.text.Length == 6, "Mã xác thực gồm 6 chữ số.", view.otp);
                var result = await account.VerifyEmailAsync(mail, view.otp.text, cancellation);
                view.otp.text = ""; Show(Page.Login); Message(result.Message);
            }
            else
            {
                string name = view.playerName.text.Trim();
                Validate(name.Length >= 3 && name.Length <= 18, "Tên người chơi cần từ 3 đến 18 ký tự.", view.playerName);
                await account.UpdateProfileAsync(new UpdateProfileRequest { PlayerName = name }, cancellation); Message("Đã lưu hồ sơ.");
            }
        });
        bool BeginMenuLoading(string mail, string password, bool resume)
        {
            attemptedEmail = mail ?? sessionEmail;
            bool started = SceneLoading.LoadPrepared("Menu", async (token, report) =>
            {
                if (!resume)
                {
                    report(.05f, "Đang đăng nhập tài khoản");
                    await account.LoginAsync(mail, password, token);
                    sessionEmail = mail;
                    password = null;
                }
                token.ThrowIfCancellationRequested();
                report(.65f, "Đang tải hồ sơ và hành trình");
                await ProgressionRuntime.Instance.EnsureReadyAsync();
                token.ThrowIfCancellationRequested();
                if (!account.IsSignedIn || !ProgressionRuntime.Instance.Ready)
                    throw new InvalidOperationException("Game data is not ready.");
                report(1, "Hồ sơ đã sẵn sàng");
            }, error =>
            {
                pendingLoginFailure = error;
                loginRetryPending = true;
            }, LoginScene, () => account.IsSignedIn && ProgressionRuntime.Instance.Ready);
            if (started) { navigating = true; loginRetryPending = false; }
            return started;
        }
        void Navigate(string scene) { if (!navigating) _ = NavigateAsync(scene); }
        async Task NavigateAsync(string scene)
        {
            if (navigating) return;
            navigating = true;
            var operation = SceneManager.LoadSceneAsync(scene);
            while (operation != null && !operation.isDone) await Task.Yield();
        }
        async Task Run(Func<CancellationToken, Task> action)
        {
            if (busy || navigating || SceneLoading.IsBlocking || !view || sceneLifetime == null) return;
            var panel = view; int version = operationVersion;
            busy = true; focusAfterRun = null; panel.form.interactable = false; Message("Đang xử lý…");
            try { await action(sceneLifetime.Token); }
            catch (OperationCanceledException) { }
            catch (ApiException error)
            {
                if (version != operationVersion) return;
                if (error.Code == "email_not_verified" || error.Code == "email_exists") Show(Page.Verify);
                Message(error.Message, true);
            }
            catch (ArgumentException error) { if (version == operationVersion) Message(error.Message, true); }
            catch (Exception) { if (version == operationVersion) Message("Chưa hoàn tất thao tác. Vui lòng thử lại.", true); }
            finally
            {
                if (version == operationVersion)
                {
                    busy = false;
                    if (panel) panel.form.interactable = true;
                    Focus(focusAfterRun); focusAfterRun = null;
                    RefreshVisibility();
                }
            }
        }
        async void Resend() => await Run(async cancellation =>
        {
            Validate(!string.IsNullOrWhiteSpace(view.email.text), "Vui lòng nhập email.", view.email);
            var result = await account.ResendOtpAsync(view.email.text.Trim(), cancellation);
            if (result.Code == "email_already_verified") { view.otp.text = ""; Show(Page.Login); }
            else resendAt = Time.realtimeSinceStartupAsDouble + account.OtpResendCooldownSeconds;
            lastResendSeconds = -1; Message(result.Message);
        });
        async void Logout() => await Run(async cancellation =>
        {
            sessionEmail = attemptedEmail = null;
            loginRetryPending = false;
            await PickleVerse.Multiplayer.PlayerSignOut.FromLobbyAsync();
            await account.LogoutAsync(cancellation); Navigate(LoginScene);
        });
        void OnDestroy()
        {
            if (instance != this) return;
            sceneLifetime?.Cancel(); sceneLifetime?.Dispose();
            account.Changed -= OnAccountChanged; ProgressionRuntime.Changed -= OnGameChanged;
            SceneManager.sceneLoaded -= SceneLoaded; FrontEndModalInput.Release(this); instance = null;
            SceneManager.activeSceneChanged -= ActiveSceneChanged;
        }
    }
}
