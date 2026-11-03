#if !UNITY_SERVER
using System;
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
    /// <summary>Scene-local authentication view. Credentials remain in AccountRuntime only.</summary>
    public sealed class AccountScreen : MonoBehaviour
    {
        enum Page { Login, Register, Verify, Forgot, ResetPassword, Sync }
        AccountService account;
        CancellationTokenSource lifetime;
        RectTransform root, card, brand;
        TMP_Text title, subtitle, feedback, submitLabel;
        TMP_InputField email, password, confirmPassword, otp;
        UnityEngine.UI.Button loginTab, registerTab, submit, verifyLink, resend, forgot, back;
        UnityEngine.CanvasGroup formGroup;
        readonly List<UnityEngine.UI.Selectable> navigation = new List<UnityEngine.UI.Selectable>();
        Page page;
        bool busy, leaving;
        double resendAt;
        static readonly Color Ink = new Color32(20, 30, 47, 255), Surface = new Color32(15, 40, 51, 255),
            Muted = new Color32(164, 181, 207, 255), Mint = new Color32(143, 233, 216, 255), Text = new Color32(241, 245, 255, 255);
        TMP_FontAsset font;

        void Awake()
        {
            account = AccountRuntime.Ensure().Account;
            lifetime = new CancellationTokenSource();
            font = ScreenUiTheme.Current ? ScreenUiTheme.Current.font : TMP_Settings.defaultFontAsset;
            Build();
            if (!FindAnyObjectByType<EventSystem>())
                new GameObject("Authentication EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule))
                    .GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            account.Changed += OnAccountChanged;
            ProgressionRuntime.Changed += OnGameChanged;
            Show(account.IsSignedIn ? Page.Sync : Page.Login);
            OnGameChanged();
            Debug.Log("[Authentication] Scene ready.");
        }
        void OnAccountChanged()
        {
            if (!this) return;
            password.text = confirmPassword.text = otp.text = "";
            Show(account.IsSignedIn ? Page.Sync : Page.Login);
        }
        async void OnGameChanged()
        {
            if (!this || leaving || !account.IsSignedIn) return;
            var runtime = ProgressionRuntime.Instance;
            if (runtime.Ready)
            {
                leaving = true;
                Debug.Log("[Authentication] Account and game data ready; entering menu.");
                SceneManager.LoadSceneAsync("Menu");
            }
            else
            {
                Show(Page.Sync, false);
                if (!string.IsNullOrEmpty(runtime.Error)) feedback.text = runtime.Error;
                await Task.CompletedTask;
            }
        }
        void Update()
        {
            if (!root) return;
            if (resend) resend.interactable = !busy && Time.realtimeSinceStartupAsDouble >= resendAt;
            bool compact = Screen.width / (float)Mathf.Max(1, Screen.height) < 1.4f;
            brand.gameObject.SetActive(!compact);
            card.anchorMin = card.anchorMax = new Vector2(compact ? .5f : .74f, .5f);
            float height = page == Page.ResetPassword ? 690 : page == Page.Register ? 610 : 570;
            card.sizeDelta = new Vector2(Mathf.Min(500, root.rect.width - 32), Mathf.Min(height, root.rect.height - 20));
            if (Keyboard.current?.tabKey.wasPressedThisFrame == true && EventSystem.current)
            {
                var active = navigation.FindAll(s => s && s.gameObject.activeInHierarchy && s.IsInteractable());
                if (active.Count == 0) return;
                int index = active.FindIndex(s => s.gameObject == EventSystem.current.currentSelectedGameObject);
                int direction = Keyboard.current.shiftKey.isPressed ? -1 : 1;
                active[(index + direction + active.Count) % active.Count].Select();
            }
        }
        void Show(Page next, bool clear = true)
        {
            page = next;
            bool sync = page == Page.Sync, reset = page == Page.ResetPassword;
            title.text = sync ? "TẢI DỮ LIỆU TRÒ CHƠI" : reset ? "ĐẶT LẠI MẬT KHẨU" : page == Page.Forgot ? "QUÊN MẬT KHẨU" :
                page == Page.Verify ? "XÁC THỰC EMAIL" : page == Page.Register ? "TẠO TÀI KHOẢN" : "CHÀO MỪNG TRỞ LẠI";
            subtitle.text = sync ? "Đang tải hồ sơ và nội dung trò chơi. Bạn có thể thử lại khi mất kết nối." :
                reset || page == Page.Verify ? "Nhập mã 6 chữ số trong email. Thời hạn của mã được ghi trong email." :
                page == Page.Forgot ? "Nhập email đã xác thực để nhận mã đặt lại mật khẩu." : "Đăng nhập để tiếp tục hành trình PickleVerse.";
            loginTab.transform.parent.gameObject.SetActive(page == Page.Login || page == Page.Register);
            email.transform.parent.gameObject.SetActive(!sync);
            password.transform.parent.gameObject.SetActive(page == Page.Login || page == Page.Register || reset);
            confirmPassword.transform.parent.gameObject.SetActive(page == Page.Register || reset);
            otp.transform.parent.gameObject.SetActive(page == Page.Verify || reset);
            verifyLink.gameObject.SetActive(page == Page.Login || page == Page.Register);
            forgot.gameObject.SetActive(page == Page.Login);
            resend.gameObject.SetActive(page == Page.Verify || reset);
            back.gameObject.SetActive(page != Page.Login && page != Page.Register);
            back.GetComponentInChildren<TMP_Text>().text = sync ? "ĐỔI TÀI KHOẢN" : "VỀ ĐĂNG NHẬP";
            submitLabel.text = sync ? "THỬ TẢI LẠI" : reset ? "LƯU MẬT KHẨU MỚI" : page == Page.Forgot ? "GỬI MÃ" :
                page == Page.Verify ? "XÁC THỰC" : page == Page.Register ? "TẠO TÀI KHOẢN" : "ĐĂNG NHẬP";
            if (clear) feedback.text = "";
        }
        async void Submit() => await Run(async () =>
        {
            if (page == Page.Sync) { await ProgressionRuntime.Instance.EnsureReadyAsync(); OnGameChanged(); return; }
            string mail = email.text.Trim();
            if (mail.Length == 0 || !mail.Contains("@")) throw new ArgumentException("Vui lòng nhập địa chỉ email hợp lệ.");
            if (page == Page.Forgot) { await SendRecovery(); return; }
            if (page == Page.Verify)
            {
                if (otp.text.Length != 6) throw new ArgumentException("Mã xác thực gồm 6 chữ số.");
                var result = await account.VerifyEmailAsync(mail, otp.text, lifetime.Token);
                otp.text = ""; Show(Page.Login); feedback.text = result.Message; return;
            }
            if (page == Page.Register || page == Page.ResetPassword)
            {
                if (password.text.Length < 6) throw new ArgumentException("Mật khẩu cần ít nhất 6 ký tự.");
                if (password.text != confirmPassword.text) throw new ArgumentException("Mật khẩu xác nhận chưa khớp.");
                if (page == Page.ResetPassword)
                {
                    var result = await account.ResetPasswordAsync(new ResetPasswordRequest { Email = mail, Token = otp.text,
                        Password = password.text, ConfirmPassword = confirmPassword.text }, lifetime.Token);
                    password.text = confirmPassword.text = otp.text = "";
                    Show(Page.Login); feedback.text = result.Message; return;
                }
                await account.RegisterAsync(new RegisterRequest { Email = mail, Password = password.text,
                    ConfirmPassword = confirmPassword.text }, lifetime.Token);
                password.text = confirmPassword.text = "";
                resendAt = Time.realtimeSinceStartupAsDouble + account.OtpResendCooldownSeconds;
                Show(Page.Verify); feedback.text = "Mã xác thực đã được gửi. Hãy kiểm tra cả thư rác."; return;
            }
            if (password.text.Length == 0) throw new ArgumentException("Vui lòng nhập mật khẩu.");
            await account.LoginAsync(mail, password.text, lifetime.Token);
            await ProgressionRuntime.Instance.EnsureReadyAsync();
            OnGameChanged();
        });
        async Task SendRecovery()
        {
            var result = await account.ForgotPasswordAsync(email.text.Trim(), lifetime.Token);
            resendAt = Time.realtimeSinceStartupAsDouble + account.OtpResendCooldownSeconds;
            Show(Page.ResetPassword); feedback.text = result.Message;
        }
        async void Resend() => await Run(async () =>
        {
            if (page == Page.ResetPassword) { await SendRecovery(); return; }
            var result = await account.ResendOtpAsync(email.text.Trim(), lifetime.Token);
            if (result.Code == "email_already_verified") Show(Page.Login);
            else resendAt = Time.realtimeSinceStartupAsDouble + account.OtpResendCooldownSeconds;
            feedback.text = result.Message;
        });
        async Task Run(Func<Task> action)
        {
            if (busy) return;
            busy = true; formGroup.interactable = false; feedback.text = "Đang xử lý…";
            try { await action(); }
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
        void OnDestroy()
        {
            lifetime?.Cancel(); lifetime?.Dispose();
            if (account != null) account.Changed -= OnAccountChanged;
            ProgressionRuntime.Changed -= OnGameChanged;
        }
        void Build()
        {
            var backgroundCamera = new GameObject("Authentication Camera", typeof(Camera)).GetComponent<Camera>();
            backgroundCamera.transform.SetParent(transform, false);
            backgroundCamera.tag = "MainCamera";
            backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
            backgroundCamera.backgroundColor = Ink; backgroundCamera.cullingMask = 0;
            var canvasObject = new GameObject("Authentication Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            root = (RectTransform)canvasObject.transform;
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 1;
            var cover = Box(root, "Authentication Background", Ink); Stretch(cover);
            brand = Rect(cover, "Brand"); brand.anchorMin = new Vector2(.06f, .15f); brand.anchorMax = new Vector2(.48f, .88f); brand.offsetMin = brand.offsetMax = Vector2.zero;
            Place(Label(brand, "Wordmark", "PICKLEVERSE", 42, Mint).rectTransform, new Vector2(.5f, .87f), new Vector2(480, 70));
            Place(Label(brand, "Tagline", "LUYỆN KỸ THUẬT.\nVÀO SÂN THI ĐẤU.", 34, Text).rectTransform, new Vector2(.5f, .62f), new Vector2(480, 130));
            Place(Label(brand, "Note", "Mỗi buổi tập là một bước tiến.\nLưu hành trình của bạn với PickleVerse.", 18, Muted).rectTransform, new Vector2(.5f, .32f), new Vector2(460, 100));
            card = Box(cover, "Authentication Card", Surface); Place(card, new Vector2(.74f, .5f), new Vector2(500, 610));
            formGroup = card.gameObject.AddComponent<CanvasGroup>();
            var layout = card.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 16, 16); layout.spacing = 6;
            layout.childControlHeight = layout.childControlWidth = true; layout.childForceExpandHeight = false;
            var tabs = Row(card, "Authentication Tabs", 36);
            loginTab = Button(tabs, "Login Tab", "ĐĂNG NHẬP", () => Show(Page.Login));
            registerTab = Button(tabs, "Register Tab", "ĐĂNG KÝ", () => Show(Page.Register));
            title = Label(card, "Title", "", 25, Text); Height(title.gameObject, 38);
            subtitle = Label(card, "Subtitle", "", 14, Muted); Height(subtitle.gameObject, 36);
            email = Field(card, "Email", "EMAIL", "ban@example.com", 254);
            password = Field(card, "Password", "MẬT KHẨU", "Nhập mật khẩu", 128, true);
            confirmPassword = Field(card, "Confirm Password", "NHẬP LẠI MẬT KHẨU", "Xác nhận mật khẩu", 128, true);
            otp = Field(card, "OTP", "MÃ XÁC THỰC", "000000", 6); otp.characterValidation = TMP_InputField.CharacterValidation.Digit;
            submit = Button(card, "Submit", "ĐĂNG NHẬP", Submit, true); Height(submit.gameObject, 44);
            submitLabel = submit.GetComponentInChildren<TMP_Text>();
            verifyLink = Button(card, "Verify Link", "Cần xác thực email", () => Show(Page.Verify));
            forgot = Button(card, "Forgot Password", "QUÊN MẬT KHẨU", () => Show(Page.Forgot));
            resend = Button(card, "Resend OTP", "GỬI LẠI MÃ", Resend);
            back = Button(card, "Back To Login", "VỀ ĐĂNG NHẬP", async () =>
            {
                if (account.IsSignedIn) await Run(async () => await account.LogoutAsync(lifetime.Token));
                else { password.text = confirmPassword.text = otp.text = ""; Show(Page.Login); }
            });
            feedback = Label(card, "Feedback", "", 14, Mint); feedback.richText = false; Height(feedback.gameObject, 48);
            password.onSubmit.AddListener(_ => { if (page == Page.Login) Submit(); });
            otp.onSubmit.AddListener(_ => { if (page == Page.Verify) Submit(); });
        }
        RectTransform Rect(Transform parent, string objectName)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
        RectTransform Box(Transform parent, string objectName, Color color)
        {
            var rect = Rect(parent, objectName);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            return rect;
        }
        TMP_Text Label(Transform parent, string objectName, string text, float size, Color color)
        {
            var rect = Rect(parent, objectName);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return label;
        }
        UnityEngine.UI.Button Button(Transform parent, string objectName, string text, UnityEngine.Events.UnityAction action, bool primary = false)
        {
            var rect = Box(parent, objectName, primary ? Mint : new Color(.08f, .19f, .24f));
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
            button.onClick.AddListener(action);
            var label = Label(rect, "Label", text, 14, primary ? Ink : Text);
            label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform);
            Height(rect.gameObject, 36);
            navigation.Add(button);
            return button;
        }
        RectTransform Row(Transform parent, string objectName, float height)
        {
            var row = Rect(parent, objectName);
            Height(row.gameObject, height);
            var layout = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = layout.childForceExpandWidth = true;
            return row;
        }
        TMP_InputField Field(Transform parent, string objectName, string caption, string placeholder, int limit, bool secret = false)
        {
            var group = Rect(parent, objectName + " Field");
            Height(group.gameObject, 60);
            var label = Label(group, "Caption", caption, 11, Muted);
            label.rectTransform.anchorMin = new Vector2(0, .72f);
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            var fieldRect = Box(group, objectName, new Color(.035f, .09f, .13f));
            fieldRect.anchorMin = Vector2.zero;
            fieldRect.anchorMax = new Vector2(1, .70f);
            fieldRect.offsetMin = fieldRect.offsetMax = Vector2.zero;
            var field = fieldRect.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = fieldRect.GetComponent<UnityEngine.UI.Image>();
            var viewport = Rect(fieldRect, "Viewport");
            Stretch(viewport);
            viewport.offsetMin = new Vector2(12, 4);
            viewport.offsetMax = new Vector2(-12, -4);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var value = Label(viewport, "Text", "", 17, Text);
            Stretch(value.rectTransform);
            value.richText = false;
            var hint = Label(viewport, "Placeholder", placeholder, 16, new Color(Muted.r, Muted.g, Muted.b, .65f));
            Stretch(hint.rectTransform);
            field.textViewport = viewport;
            field.textComponent = value;
            field.placeholder = hint;
            field.characterLimit = limit;
            field.contentType = secret ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            field.lineType = TMP_InputField.LineType.SingleLine;
            navigation.Add(field);
            return field;
        }
        static void Height(GameObject go, float height)
        {
            var layout = go.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = layout.preferredHeight = height;
            layout.flexibleHeight = 0;
        }
        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        static void Place(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }
    }
}

#endif
