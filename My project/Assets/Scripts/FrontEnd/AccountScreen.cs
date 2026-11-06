#if !UNITY_SERVER
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
    /// <summary>Account/session coordinator; authentication views belong to their scene, never DontDestroyOnLoad.</summary>
    public sealed class AccountScreen : MonoBehaviour
    {
        enum Page { Login, Register, Verify, Forgot, ResetPassword }
        public const string LoginScene = AuthenticationNavigation.SceneName;
        static AccountScreen instance;
        AccountService account;
        AccountPanelView view;
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
            view = null;
            if (scene.name == "Loading" && SceneLoading.IsPreparedTransition) return;
            EnsureEventSystem();
            if (scene.name == LoginScene)
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    view = root.GetComponentInChildren<AccountPanelView>(true);
                    if (view) break;
                }
                if (!view) { Debug.LogError("Authentication scene is missing its AccountPanelView."); return; }
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
            StartCoroutine(ApplyScopeAfterSceneReady());
        }
        void BindView()
        {
            view.submit.onClick.AddListener(Submit);
            if (view.switchPage) view.switchPage.onClick.AddListener(() => Show(page == Page.Login ? Page.Register : Page.Login));
            if (view.verifyLink) view.verifyLink.onClick.AddListener(() => Show(Page.Verify));
            if (view.resend) view.resend.onClick.AddListener(Resend);
            if (view.logout) view.logout.onClick.AddListener(Logout);
            if (view.forgot) view.forgot.onClick.AddListener(() => Show(Page.Forgot));
            if (view.back) view.back.onClick.AddListener(() => { ClearSecrets(); Show(Page.Login); });
            if (view.email) view.email.onSubmit.AddListener(_ => { if (page == Page.Forgot) Submit(); else Focus(page == Page.Verify || page == Page.ResetPassword ? view.otp : view.password); });
            if (view.password) view.password.onSubmit.AddListener(_ => { if (page == Page.Register || page == Page.ResetPassword) Focus(view.confirmPassword); else Submit(); });
            if (view.confirmPassword) view.confirmPassword.onSubmit.AddListener(_ => Submit());
            if (view.otp) view.otp.onSubmit.AddListener(_ => { if (page == Page.ResetPassword) Focus(view.password); else Submit(); });
            view.form.interactable = true; lastResendSeconds = -1;
        }
        IEnumerator ApplyScopeAfterSceneReady() { yield return null; ApplyOnlineScope(); }
        void ApplyOnlineScope()
        {
            var runtime = ProgressionRuntime.Instance;
            if (!runtime) return;
            bool available = runtime.Ready && !account.IsRestricted;
            foreach (var home in FindObjectsByType<HomeMenuShell>(FindObjectsInactive.Include))
            {
                if (home.playButton) home.playButton.interactable = available && runtime.IsAuthenticated;
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
            }
            foreach (var social in FindObjectsByType<OfflineSocialMenu>(FindObjectsInactive.Include))
            {
                if (!runtime.IsAuthenticated) social.Close();
                social.enabled = runtime.IsAuthenticated;
                if (social.friendsButton) social.friendsButton.interactable = runtime.IsAuthenticated;
                if (social.leaderboardButton) social.leaderboardButton.interactable = runtime.IsAuthenticated;
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
            if (!account.IsSignedIn) ClearSecrets();
            RefreshVisibility(); ApplyOnlineScope();
        }
        void RefreshVisibility()
        {
            if (SceneLoading.IsPreparedTransition) return;
            if (!sceneBound || navigating || !SceneManager.GetActiveScene().isLoaded) return;
            if (account.IsSignedIn && !account.IsRestricted && SceneManager.GetActiveScene().name == LoginScene && view && !busy && !loginRetryPending)
                BeginMenuLoading(sessionEmail, null, true);
        }
        void ClearSecrets()
        {
            if (!view) return;
            view.password.text = view.confirmPassword.text = view.otp.text = "";
            view.ConcealPasswords();
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
            if ((page == Page.Verify || page == Page.ResetPassword) && view.resend)
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
            bool reset = next == Page.ResetPassword;
            view.ConcealPasswords();
            view.title.text = reset ? "Đặt lại mật khẩu" : next == Page.Forgot ? "Quên mật khẩu" : next == Page.Verify ? "Xác thực email" : next == Page.Register ? "Khởi đầu hành trình" : "Chào mừng trở lại";
            view.subtitle.text = reset ? "Nhập mã 6 chữ số trong email và mật khẩu mới. Mã có hiệu lực 10 phút."
                : next == Page.Forgot ? "Nhập email đã xác thực để nhận mã đặt lại mật khẩu."
                : next == Page.Verify ? $"Nhập mã 6 chữ số được gửi đến email của bạn.{(account.OtpLifetimeMinutes > 0 ? $" Mã có hiệu lực {account.OtpLifetimeMinutes} phút." : "")}"
                : next == Page.Register ? "Tạo tài khoản để lưu tiến trình và cùng bạn bè thi đấu." : "Đăng nhập để tiếp tục hành trình PickleVerse.";
            FieldVisible(view.email, true);
            FieldVisible(view.password, next == Page.Login || next == Page.Register || reset);
            FieldVisible(view.confirmPassword, next == Page.Register || reset);
            FieldVisible(view.playerName, false); FieldVisible(view.otp, next == Page.Verify || reset);
            Visible(view.verifyLink, next == Page.Login || next == Page.Register);
            Visible(view.forgot, next == Page.Login); Visible(view.resend, next == Page.Verify || reset);
            Visible(view.back, next != Page.Login && next != Page.Register);
            Visible(view.logout, account.IsSignedIn); Visible(view.close, false);
            if (view.switchPage) view.switchPage.transform.parent.gameObject.SetActive(next == Page.Login || next == Page.Register);
            if (view.switchHint) view.switchHint.text = next == Page.Login ? "Chưa có tài khoản?" : "Đã có tài khoản?";
            if (view.switchLabel) view.switchLabel.text = next == Page.Login ? "Đăng ký" : "Đăng nhập";
            view.submitLabel.text = reset ? "LƯU MẬT KHẨU MỚI" : next == Page.Forgot ? "GỬI MÃ" : next == Page.Verify ? "XÁC THỰC" : next == Page.Register ? "TẠO TÀI KHOẢN" : "ĐĂNG NHẬP";
            lastResendSeconds = -1;
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
            Validate(mail.Length > 0 && mail.Contains("@") && mail.LastIndexOf('@') < mail.Length - 1, "Vui lòng nhập địa chỉ email hợp lệ.", view.email);
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
            else if (page == Page.Forgot) await SendRecovery(cancellation);
            else if (page == Page.ResetPassword)
            {
                Validate(view.otp.text.Length == 6, "Mã xác thực gồm 6 chữ số.", view.otp);
                Validate(view.password.text.Length >= 6, "Mật khẩu cần ít nhất 6 ký tự.", view.password);
                Validate(view.password.text == view.confirmPassword.text, "Mật khẩu xác nhận chưa khớp.", view.confirmPassword);
                var result = await account.ResetPasswordAsync(new ResetPasswordRequest { Email = mail, Token = view.otp.text,
                    Password = view.password.text, ConfirmPassword = view.confirmPassword.text }, cancellation);
                if (account.IsSignedIn) account.Clear();
                ClearSecrets(); sessionEmail = null; loginRetryPending = false;
                Show(Page.Login); Message(result.Message);
            }
        });
        async Task SendRecovery(CancellationToken cancellation)
        {
            var result = await account.ForgotPasswordAsync(view.email.text.Trim(), cancellation);
            ClearSecrets();
            resendAt = Time.realtimeSinceStartupAsDouble + account.OtpResendCooldownSeconds;
            Show(Page.ResetPassword); Message(result.Message);
        }
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
                if (account.IsRestricted) throw new ApiException(403, "Tài khoản đang bị hạn chế. Bạn có thể xem quyết định và gửi khiếu nại.", "account_restricted");
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
            if (page == Page.ResetPassword) { await SendRecovery(cancellation); return; }
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
            ClearSecrets(); Show(Page.Login);
        });
        void OnDestroy()
        {
            if (instance != this) return;
            sceneLifetime?.Cancel(); sceneLifetime?.Dispose();
            account.Changed -= OnAccountChanged; ProgressionRuntime.Changed -= OnGameChanged;
            SceneManager.sceneLoaded -= SceneLoaded; FrontEndModalInput.Release(this); instance = null;
        }
    }
}

#endif
