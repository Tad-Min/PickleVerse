using System;
using System.Collections;
using System.Collections.Generic;
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
    /// <summary>Persistent online sign-in gate, using the lobby's Vietnamese font and palette.</summary>
    public sealed class AccountScreen : MonoBehaviour
    {
        enum Page { Login, Register, Verify, Profile, Sync }
        static AccountScreen instance;
        AccountService account;
        CancellationTokenSource lifetime;
        RectTransform root, card, brand;
        GameObject cover;
        TMP_Text title, subtitle, feedback, submitLabel;
        TMP_InputField email, password, confirmPassword, playerName, otp;
        UnityEngine.UI.Button loginTab, registerTab, submit, verifyLink, resend, close, logout, accountButton, maleButton, femaleButton;
        UnityEngine.CanvasGroup formGroup;
        readonly List<UnityEngine.UI.Selectable> navigation = new List<UnityEngine.UI.Selectable>();
        Page page;
        bool busy, male = true;
        double resendAt;
        float priorTimeScale = 1;
        bool pausedByGate;
        static readonly Color Ink = new Color32(20, 30, 47, 255), Surface = new Color32(15, 40, 51, 255),
            Muted = new Color32(164, 181, 207, 255), Mint = new Color32(143, 233, 216, 255), Text = new Color32(241, 245, 255, 255);
        TMP_FontAsset font;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() { instance = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (!instance) AccountRuntime.Ensure().gameObject.AddComponent<AccountScreen>();
        }
        void Awake()
        {
            if (instance && instance != this) { Destroy(this); return; }
            instance = this; account = AccountRuntime.Ensure().Account; lifetime = new CancellationTokenSource();
            font = ScreenUiTheme.Current ? ScreenUiTheme.Current.font : TMP_Settings.defaultFontAsset;
            Build(); account.Changed += OnAccountChanged; ProgressionRuntime.Changed += OnGameChanged; SceneManager.sceneLoaded += SceneLoaded;
            Show(Page.Login); OnAccountChanged();
        }
        void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureEventSystem(); RefreshVisibility();
            if (!account.IsSignedIn && scene.name != "Menu" && scene.name != "Login" && scene.name != "Settings" && scene.name != "PhotonLobby") SceneManager.LoadSceneAsync("Menu");
            else StartCoroutine(ApplyScopeAfterSceneReady());
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
                if (home.nav) foreach (var button in home.nav.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    if (!button.name.Contains("Setting") && !button.name.Contains("Music")) button.interactable = available;
            }
            foreach (var menu in FindObjectsByType<PlayerProgressionMenu>(FindObjectsInactive.Include))
            {
                if (!available) menu.Close(); menu.enabled = available;
                foreach (var button in new[] { menu.profileButton, menu.missionsButton, menu.careerButton, menu.progressionButton }) if (button) button.interactable = available;
                if (menu.editButton) menu.editButton.gameObject.SetActive(false);
            }
            foreach (var social in FindObjectsByType<OfflineSocialMenu>(FindObjectsInactive.Include))
            {
                if(!available)social.Close(); social.enabled = available;
                if (social.friendsButton) social.friendsButton.interactable = available;
                if (social.leaderboardButton) social.leaderboardButton.interactable = available;
            }
            foreach (var home in FindObjectsByType<HomeLobbyPresentation>(FindObjectsInactive.Include))
            {
                home.ApplyAccountCharacter(account.Profile?.IsMale == true);
                if (home.maleButton) home.maleButton.interactable = false;
                if (home.femaleButton) home.femaleButton.interactable = false;
            }
        }
        void OnGameChanged()
        {
            if (!root || !ProgressionRuntime.Exists) return;
            if (ProgressionRuntime.Instance.Ready && page == Page.Sync) page = Page.Login;
            RefreshVisibility(); ApplyOnlineScope();
            if (page == Page.Sync && !string.IsNullOrEmpty(ProgressionRuntime.Instance.Error)) feedback.text = ProgressionRuntime.Instance.Error;
        }
        public void OpenAccount()
        {
            if (!account.IsSignedIn || !FrontEndModalInput.TryAcquire(this)) return;
            Show(Page.Profile); cover.SetActive(true); accountButton.gameObject.SetActive(false);
        }
        void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>()) return;
            var go = new GameObject("Account EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            // The temporary system belongs to this scene and is replaced by authored scene systems.
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        void Update()
        {
            if (cover && cover.activeSelf && Keyboard.current?.tabKey.wasPressedThisFrame == true)
            {
                var active = navigation.FindAll(s => s && s.gameObject.activeInHierarchy && s.IsInteractable());
                if (active.Count == 0 || !EventSystem.current) return;
                int index = active.FindIndex(s => s.gameObject == EventSystem.current.currentSelectedGameObject);
                int direction = Keyboard.current.shiftKey.isPressed ? -1 : 1;
                active[(index + direction + active.Count) % active.Count].Select();
            }
            if (resend) resend.interactable = !busy && Time.realtimeSinceStartupAsDouble >= resendAt;
            if (!root) return;
            bool compact = Screen.width / (float)Mathf.Max(1, Screen.height) < 1.4f;
            brand.gameObject.SetActive(!compact);
            card.anchorMin = card.anchorMax = new Vector2(compact ? .5f : .74f, .5f);
            float height = page == Page.Register ? 660 : page == Page.Profile ? 560 : page == Page.Verify ? 510 : 500;
            card.sizeDelta = new Vector2(Mathf.Min(500, root.rect.width - 40), Mathf.Min(height, root.rect.height - 32));
        }
        void OnAccountChanged()
        {
            if (!root || !ProgressionRuntime.Instance) return;
            if (account.IsSignedIn)
            {
                var p = account.Profile; playerName.SetTextWithoutNotify(p.PlayerName); male = p.IsMale;
                password.text = confirmPassword.text = otp.text = "";
            }
            else { page = Page.Login; password.text = confirmPassword.text = ""; }
            RefreshVisibility();
            ApplyOnlineScope();
        }
        void RefreshVisibility()
        {
            var runtime = ProgressionRuntime.Instance;
            if (!root || !runtime) return;
            bool required = !account.IsSignedIn || !runtime.Ready;
            if (required)
            {
                FrontEndModalInput.TryAcquire(this);
                if (!pausedByGate) { priorTimeScale = Time.timeScale; Time.timeScale = 0; pausedByGate = true; }
                cover.SetActive(true); Show(account.IsSignedIn ? Page.Sync : page == Page.Profile || page == Page.Sync ? Page.Login : page);
            }
            else
            {
                if (pausedByGate) { Time.timeScale = priorTimeScale; pausedByGate = false; }
                cover.SetActive(page == Page.Profile);
                if (page != Page.Profile) FrontEndModalInput.Release(this);
            }
            accountButton.gameObject.SetActive(!required && page != Page.Profile && SceneManager.GetActiveScene().name == "Menu");
        }
        void Show(Page next)
        {
            page = next;
            bool syncing = next == Page.Sync;
            title.text = syncing ? "TẢI DỮ LIỆU TRÒ CHƠI" : next == Page.Login ? "CHÀO MỪNG TRỞ LẠI" : next == Page.Register ? "TẠO TÀI KHOẢN" : next == Page.Verify ? "XÁC THỰC EMAIL" : "TÀI KHOẢN CỦA BẠN";
            subtitle.text = next == Page.Verify ? "Nhập mã 6 chữ số trong email. Mã có hiệu lực 10 phút."
                : syncing ? "Đang tải hồ sơ, cửa hàng và bài học trực tuyến. Nếu mất kết nối, hãy thử lại." : next == Page.Profile ? $"LV {account.Profile.Level}  ·  {account.Profile.Elo} ELO  ·  {account.Profile.WinCount} thắng / {account.Profile.LoseCount} thua" : "Đăng nhập để tiếp tục hành trình PickleVerse.";
            loginTab.gameObject.SetActive(next != Page.Profile && !syncing); registerTab.gameObject.SetActive(next != Page.Profile && !syncing);
            loginTab.transform.parent.gameObject.SetActive(next != Page.Profile && !syncing);
            email.transform.parent.gameObject.SetActive(next != Page.Profile && !syncing);
            password.transform.parent.gameObject.SetActive(next == Page.Login || next == Page.Register);
            confirmPassword.transform.parent.gameObject.SetActive(next == Page.Register);
            playerName.transform.parent.gameObject.SetActive(next == Page.Profile);
            otp.transform.parent.gameObject.SetActive(next == Page.Verify);
            maleButton.transform.parent.gameObject.SetActive(next == Page.Register || next == Page.Profile);
            verifyLink.gameObject.SetActive(next == Page.Login || next == Page.Register);
            resend.gameObject.SetActive(next == Page.Verify);
            logout.gameObject.SetActive(next == Page.Profile || syncing); close.gameObject.SetActive(next == Page.Profile);
            submitLabel.text = syncing ? "THỬ TẢI LẠI" : next == Page.Login ? "ĐĂNG NHẬP" : next == Page.Register ? "TẠO TÀI KHOẢN" : next == Page.Verify ? "XÁC THỰC" : "LƯU HỒ SƠ";
            SetGender(male); feedback.text = "";
        }
        void SetGender(bool value)
        {
            male = value; ScreenUiTheme.Selection(maleButton, value); ScreenUiTheme.Selection(femaleButton, !value);
        }
        async void Submit() => await Run(async () =>
        {
            var cancellation = lifetime.Token;
            if (page == Page.Sync) { await ProgressionRuntime.Instance.ReloadAsync(); RefreshVisibility(); return; }
            string mail = email.text.Trim();
            if (page != Page.Profile && (mail.Length == 0 || !mail.Contains("@"))) throw new ArgumentException("Vui lòng nhập địa chỉ email hợp lệ.");
            if (page == Page.Login)
            {
                if (password.text.Length == 0) throw new ArgumentException("Vui lòng nhập mật khẩu.");
                await account.LoginAsync(mail, password.text, cancellation);
                await ProgressionRuntime.Instance.EnsureReadyAsync();
                if (SceneManager.GetActiveScene().name != "Menu") await LoadMenu();
            }
            else if (page == Page.Register)
            {
                if (password.text.Length < 6) throw new ArgumentException("Mật khẩu cần ít nhất 6 ký tự.");
                if (password.text != confirmPassword.text) throw new ArgumentException("Mật khẩu xác nhận chưa khớp.");
                await account.RegisterAsync(new RegisterRequest { Email = mail, Password = password.text, ConfirmPassword = confirmPassword.text, IsMale = male }, cancellation);
                password.text = confirmPassword.text = ""; resendAt = Time.realtimeSinceStartupAsDouble + 60;
                Show(Page.Verify); feedback.text = "Mã xác thực đã được gửi. Hãy kiểm tra cả thư rác.";
            }
            else if (page == Page.Verify)
            {
                if (otp.text.Length != 6) throw new ArgumentException("Mã xác thực gồm 6 chữ số.");
                var result = await account.VerifyEmailAsync(mail, otp.text, cancellation);
                otp.text = ""; Show(Page.Login); feedback.text = result.Message;
            }
            else
            {
                if (playerName.text.Trim().Length < 3) throw new ArgumentException("Tên người chơi cần từ 3 đến 18 ký tự.");
                await account.UpdateProfileAsync(new UpdateProfileRequest { PlayerName = playerName.text.Trim(), IsMale = male }, cancellation);
                feedback.text = "Đã lưu hồ sơ.";
            }
        });
        async Task LoadMenu()
        {
            var operation = SceneManager.LoadSceneAsync("Menu");
            while (operation != null && !operation.isDone) await Task.Yield();
        }
        async Task Run(Func<Task> action)
        {
            if (busy) return; busy = true; formGroup.interactable = false; feedback.text = "Đang xử lý…";
            try { await action(); RefreshVisibility(); }
            catch (OperationCanceledException) { }
            catch (ApiException error)
            {
                if (error.Code == "email_not_verified" || error.Code == "email_exists") Show(Page.Verify);
                if (feedback) feedback.text = error.Message;
            }
            catch (ArgumentException error) { if (feedback) feedback.text = error.Message; }
            catch (Exception) { if (feedback) feedback.text = "Chưa hoàn tất thao tác. Vui lòng thử lại."; }
            finally { busy = false; if (formGroup) formGroup.interactable = true; }
        }
        async void Resend() => await Run(async () =>
        {
            if (string.IsNullOrWhiteSpace(email.text)) throw new ArgumentException("Vui lòng nhập email.");
            var result = await account.ResendOtpAsync(email.text.Trim(), lifetime.Token);
            resendAt = Time.realtimeSinceStartupAsDouble + 60; feedback.text = result.Message;
        });
        async void Logout() => await Run(async () => { await account.LogoutAsync(lifetime.Token); Show(Page.Login); RefreshVisibility(); });
        void OnDestroy()
        {
            if (instance != this) return;
            lifetime?.Cancel(); lifetime?.Dispose(); account.Changed -= OnAccountChanged; ProgressionRuntime.Changed -= OnGameChanged; SceneManager.sceneLoaded -= SceneLoaded;
            FrontEndModalInput.Release(this);
            if (pausedByGate) Time.timeScale = priorTimeScale;
            if (root) Destroy(root.gameObject); instance = null;
        }

        void Build()
        {
            var canvasObject = new GameObject("Account Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            DontDestroyOnLoad(canvasObject); root = (RectTransform)canvasObject.transform;
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 1;
            cover = Box(root, "Account Gate", Ink).gameObject; Stretch((RectTransform)cover.transform);
            brand = Rect(cover.transform, "Brand"); brand.anchorMin = new Vector2(.06f, .15f); brand.anchorMax = new Vector2(.48f, .88f); brand.offsetMin = brand.offsetMax = Vector2.zero;
            var wordmark = Label(brand, "Wordmark", "PICKLEVERSE", 42, Mint); Place(wordmark.rectTransform, new Vector2(.5f, .87f), new Vector2(480, 70));
            var tagline = Label(brand, "Tagline", "LUYỆN KỸ THUẬT.\nVÀO SÂN THI ĐẤU.", 34, Text); Place(tagline.rectTransform, new Vector2(.5f, .62f), new Vector2(480, 130));
            var note = Label(brand, "Note", "Mỗi buổi tập là một bước tiến.\nLưu hành trình của bạn với tài khoản PickleVerse.", 18, Muted); Place(note.rectTransform, new Vector2(.5f, .32f), new Vector2(460, 100));
            card = Box(cover.transform, "Account Card", Surface); Place(card, new Vector2(.74f, .5f), new Vector2(500, 660));
            var outline = card.gameObject.AddComponent<UnityEngine.UI.Outline>(); outline.effectColor = new Color(Mint.r, Mint.g, Mint.b, .2f); outline.effectDistance = Vector2.one;
            formGroup = card.gameObject.AddComponent<UnityEngine.CanvasGroup>();
            var layout = card.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>(); layout.padding = new RectOffset(32, 32, 24, 24);
            layout.spacing = 7; layout.childControlHeight = layout.childControlWidth = true; layout.childForceExpandHeight = false;
            var tabs = Row(card, "Account Tabs", 36);
            loginTab = Button(tabs, "Login Tab", "ĐĂNG NHẬP", () => Show(Page.Login));
            registerTab = Button(tabs, "Register Tab", "ĐĂNG KÝ", () => Show(Page.Register));
            title = Label(card, "Title", "", 25, Text); Height(title.gameObject, 38);
            subtitle = Label(card, "Subtitle", "", 14, Muted); Height(subtitle.gameObject, 42);
            email = Field(card, "Email", "EMAIL", "ban@example.com", 254);
            password = Field(card, "Password", "MẬT KHẨU", "Nhập mật khẩu", 128, true);
            confirmPassword = Field(card, "Confirm Password", "NHẬP LẠI MẬT KHẨU", "Xác nhận mật khẩu", 128, true);
            playerName = Field(card, "Player Name", "TÊN NGƯỜI CHƠI", "Từ 3 đến 18 ký tự", 18);
            otp = Field(card, "OTP", "MÃ XÁC THỰC", "000000", 6); otp.characterValidation = TMP_InputField.CharacterValidation.Digit;
            var gender = Row(card, "Character", 35);
            maleButton = Button(gender, "Male", "NAM", () => SetGender(true)); femaleButton = Button(gender, "Female", "NỮ", () => SetGender(false));
            submit = Button(card, "Submit", "ĐĂNG NHẬP", Submit, true); Height(submit.gameObject, 46); submitLabel = submit.GetComponentInChildren<TMP_Text>();
            verifyLink = Button(card, "Verify Link", "Đã có tài khoản, cần xác thực email", () => Show(Page.Verify)); Height(verifyLink.gameObject, 30);
            resend = Button(card, "Resend OTP", "GỬI LẠI MÃ", Resend); Height(resend.gameObject, 32);
            logout = Button(card, "Logout", "ĐĂNG XUẤT", Logout); Height(logout.gameObject, 36);
            close = Button(card, "Close Account", "TRỞ VỀ SẢNH", () => { page = Page.Login; RefreshVisibility(); }); Height(close.gameObject, 32);
            feedback = Label(card, "Feedback", "", 14, Mint); feedback.richText = false; Height(feedback.gameObject, 62);
            accountButton = Button(root, "Online Account Button", "TÀI KHOẢN", OpenAccount);
            var buttonRect = (RectTransform)accountButton.transform; buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(.5f, 1); buttonRect.pivot = new Vector2(.5f, 1);
            buttonRect.anchoredPosition = new Vector2(0, -14); buttonRect.sizeDelta = new Vector2(150, 38);
            password.onSubmit.AddListener(_ => { if (page == Page.Login) Submit(); }); otp.onSubmit.AddListener(_ => Submit());
        }
        RectTransform Rect(Transform parent, string objectName)
        {
            var go = new GameObject(objectName, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform;
        }
        RectTransform Box(Transform parent, string objectName, Color color)
        {
            var rect = Rect(parent, objectName); var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; return rect;
        }
        TMP_Text Label(Transform parent, string objectName, string text, float size, Color color)
        {
            var rect = Rect(parent, objectName); var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font;
            label.text = text; label.fontSize = size; label.color = color; label.raycastTarget = false; label.alignment = TextAlignmentOptions.MidlineLeft;
            return label;
        }
        UnityEngine.UI.Button Button(Transform parent, string objectName, string text, UnityEngine.Events.UnityAction action, bool primary = false)
        {
            var rect = Box(parent, objectName, primary ? Mint : new Color(.08f, .19f, .24f)); var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>(); button.onClick.AddListener(action);
            var label = Label(rect, "Label", text, 14, primary ? Ink : Text); label.alignment = TextAlignmentOptions.Center; Stretch(label.rectTransform);
            Height(rect.gameObject, 36); navigation.Add(button); return button;
        }
        RectTransform Row(Transform parent, string objectName, float height)
        {
            var row = Rect(parent, objectName); Height(row.gameObject, height);
            var layout = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>(); layout.spacing = 8;
            layout.childControlHeight = layout.childControlWidth = true; layout.childForceExpandHeight = layout.childForceExpandWidth = true; return row;
        }
        TMP_InputField Field(Transform parent, string objectName, string caption, string placeholder, int limit, bool secret = false)
        {
            var group = Rect(parent, objectName + " Field"); Height(group.gameObject, 60);
            var label = Label(group, "Caption", caption, 11, Muted); label.rectTransform.anchorMin = new Vector2(0, .72f); label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            var fieldRect = Box(group, objectName, new Color(.035f, .09f, .13f)); fieldRect.anchorMin = Vector2.zero; fieldRect.anchorMax = new Vector2(1, .70f); fieldRect.offsetMin = fieldRect.offsetMax = Vector2.zero;
            var field = fieldRect.gameObject.AddComponent<TMP_InputField>(); field.targetGraphic = fieldRect.GetComponent<UnityEngine.UI.Image>();
            var viewport = Rect(fieldRect, "Viewport"); Stretch(viewport); viewport.offsetMin = new Vector2(12, 4); viewport.offsetMax = new Vector2(-12, -4); viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var value = Label(viewport, "Text", "", 17, Text); Stretch(value.rectTransform); value.richText = false;
            var hint = Label(viewport, "Placeholder", placeholder, 16, new Color(Muted.r, Muted.g, Muted.b, .65f)); Stretch(hint.rectTransform);
            field.textViewport = viewport; field.textComponent = value; field.placeholder = hint; field.characterLimit = limit;
            field.contentType = secret ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            field.lineType = TMP_InputField.LineType.SingleLine; navigation.Add(field); return field;
        }
        static void Height(GameObject go, float height)
        { var layout = go.AddComponent<UnityEngine.UI.LayoutElement>(); layout.minHeight = layout.preferredHeight = height; layout.flexibleHeight = 0; }
        static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        static void Place(RectTransform rect, Vector2 anchor, Vector2 size)
        { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = size; }
    }
}
