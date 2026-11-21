using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace PickleVerse.Settings
{
    public sealed class SettingsScreen : MonoBehaviour
    {
        public RectTransform safeArea, touchBoard;
        public GameObject[] pages;
        public UnityEngine.UI.Button[] tabs, presets, fpsChoices, aaChoices, bindingButtons;
        public UnityEngine.UI.Slider[] sliders;
        public TMP_Text[] sliderValues, bindingLabels;
        public UnityEngine.UI.Toggle shadowToggle, vsyncToggle, leftToggle;
        public GameObject keyboardPage, touchPage, captureOverlay, confirmOverlay;
        public TMP_Text contextLabel, status, comboHint, captureText, confirmText;
        public UnityEngine.UI.Button applyButton, resumeButton, leaveButton, signOutButton, confirmButton;
        public TouchLayoutHandle[] touchHandles;
        public Camera previewCamera;
        public SettingsData Draft { get; private set; }
        public bool Dirty { get; private set; }
        public bool Capturing => bindingAction >= 0;
        SettingsData baseline;
        int bindingAction = -1, captureFrame;
        bool refreshing, operation;
        Action confirmed;
        Rect lastSafeArea;
        Vector2 lastResolution;
        UnityEngine.UI.Button quitButton;
        UnityEngine.UI.Button[] displayChoices = Array.Empty<UnityEngine.UI.Button>(), sizeChoices = Array.Empty<UnityEngine.UI.Button>();
        Vector2Int[] windowSizes = Array.Empty<Vector2Int>();

        void Awake()
        {
            SettingsOverlay.AttachStandalone();
            baseline = GameSettings.Current.Copy();
            Draft = baseline.Copy();
            var canvas = GetComponent<Canvas>();
            var camera = Camera.main;
            if (camera && camera != previewCamera)
            {
                if (previewCamera)
                    previewCamera.gameObject.SetActive(false);
                if (MobileRenderPolicy.Enabled)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.worldCamera = null;
                }
                else
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = camera.nearClipPlane + .05f;
                }
            }
            if (!EventSystem.current)
            {
                var events = new GameObject("Settings EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(events, gameObject.scene);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            AddDisplayTab();
            for (int i = 0; i < sliders.Length; i++)
            {
                int index = i;
                sliders[i].onValueChanged.AddListener(v => SetSlider(index, v));
            }
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                tabs[i].onClick.AddListener(() => ShowTab(index));
            }
            for (int i = 0; i < presets.Length; i++)
            {
                int index = i;
                presets[i].onClick.AddListener(() => SetPreset(index));
            }
            for (int i = 0; i < fpsChoices.Length; i++)
            {
                int rate = new[] { 30, 60, 120 }[i];
                fpsChoices[i].onClick.AddListener(() => SetFps(rate));
            }
            for (int i = 0; i < aaChoices.Length; i++)
            {
                int samples = new[] { 1, 2, 4 }[i];
                aaChoices[i].onClick.AddListener(() => SetAa(samples));
            }
            for (int i = 0; i < bindingButtons.Length; i++)
            {
                int index = i;
                bindingButtons[i].onClick.AddListener(() => BeginBinding(index));
            }
            shadowToggle.onValueChanged.AddListener(v =>
            {
                if (!refreshing)
                {
                    Draft.shadows = v;
                    Changed(true);
                }
            });
            vsyncToggle.onValueChanged.AddListener(v =>
            {
                if (!refreshing)
                {
                    Draft.vsync = v;
                    Changed(true);
                }
            });
            leftToggle.onValueChanged.AddListener(v =>
            {
                if (!refreshing)
                {
                    Draft.leftHanded = v;
                    Changed(false);
                }
            });
            bool game = SettingsOverlay.Host?.IsGameplay ?? false;
            resumeButton.gameObject.SetActive(game);
            leaveButton.gameObject.SetActive(game);
            signOutButton.gameObject.SetActive(SettingsOverlay.Host?.CanSignOut ?? false);
            contextLabel.text = game ? "TRẬN ĐẤU MÁY ĐANG TẠM DỪNG" : "THIẾT LẬP TRẢI NGHIỆM CỦA BẠN";
            AddQuitButton();
            var hint = pages[0].transform.Find("PauseHint").GetComponent<TMP_Text>();
            if (!game)
                hint.text = "Tùy chỉnh trước khi vào sân. Nút đăng xuất áp dụng cho phiên hiện tại.";
            captureOverlay.SetActive(false);
            confirmOverlay.SetActive(false);
            ConfigureTouchControls();
            Refresh();
            ShowTab(0);
            ShowKeyboard(!Application.isMobilePlatform);
            ApplySafeArea();
        }

        void ConfigureTouchControls()
        {
            string[] names = { "DI CHUYỂN", "DRIVE", "DINK", "VOLLEY", "CHẠY" };
            for (int i = 0; i < touchHandles.Length; i++)
            {
                touchHandles[i].gameObject.SetActive(i < names.Length);
                if (i < names.Length)
                {
                    var label = touchHandles[i].GetComponentInChildren<TMP_Text>(true);
                    if (label)
                        label.text = names[i];
                }
            }
            // Sprint is a dedicated hold button; its old joystick threshold no longer applies.
            if (sliders.Length > 7)
                sliders[7].gameObject.SetActive(false);
            if (sliderValues.Length > 7)
                sliderValues[7].gameObject.SetActive(false);
            foreach (var label in touchPage.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.text == "Ngưỡng chạy")
                    label.gameObject.SetActive(false);
                if (label.name == "TouchNote")
                    label.text = "Kéo nút để đổi vị trí. Giữ CHẠY để tăng tốc.\nGiữ kỹ thuật rồi kéo: ↑ combo • ← Chéo • → Lực • ↓ Hủy.";
            }
        }

        void Start()
        {
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(tabs[0].gameObject);
        }
        void Update()
        {
            ApplySafeArea();
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            // The quit confirmation above Settings owns Esc while it is open.
            if (keyboard == null || GameExit.PromptOpen)
                return;
            if (Capturing)
            {
                if (Time.frameCount <= captureFrame + 1)
                    return;
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    CancelBinding();
                    return;
                }
                var key = keyboard.allKeys.FirstOrDefault(k => k.wasPressedThisFrame);
                if (key != null)
                    TryAssignKey(key.keyCode);
                return;
            }
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (confirmOverlay.activeSelf)
                    CancelConfirm();
                else RequestClose();
            }
        }

        public void ShowTab(int tab)
        {
            if (operation || Capturing)
                return;
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].SetActive(i == tab);
                Tint(tabs[i], i == tab);
            }
        }
        public void ShowKeyboard(bool keyboard)
        {
            keyboardPage.SetActive(keyboard);
            touchPage.SetActive(!keyboard);
        }
        public void Keyboard()
        {
            ShowKeyboard(true);
        }
        public void Touch()
        {
            ShowKeyboard(false);
        }
        public void SetPreset(int preset)
        {
            Draft.SetPreset(preset);
            Changed(true);
        }
        public void SetFps(int fps)
        {
            Draft.fps = fps;
            Changed(true);
        }
        public void SetAa(int aa)
        {
            Draft.msaa = aa;
            Changed(true);
        }
        public void SetDisplayMode(int mode)
        {
            Draft.displayMode = mode;
            Changed(true);
        }
        public void SetWindowSize(int index)
        {
            if (index < 0 || index >= windowSizes.Length)
                return;
            Draft.windowWidth = windowSizes[index].x;
            Draft.windowHeight = windowSizes[index].y;
            if (Draft.Display != DisplayMode.Windowed)
                Draft.displayMode = (int)DisplayMode.Windowed;
            Changed(true);
        }
        public void QuitGame()
        {
            bool game = SettingsOverlay.Host?.IsGameplay ?? false;
            Confirm("Thoát PickleVerse?" + (game ? " Bạn sẽ rời trận đang chơi." : "") + (Dirty ? " Các thay đổi chưa áp dụng sẽ không được lưu." : ""), GameExit.Quit);
        }
        public void SetSlider(int index, float value)
        {
            if (refreshing)
                return;
            switch (index) { case 0: Draft.master = value; break; case 1: Draft.music = value; break; case 2: Draft.effects = value; break; case 3: Draft.renderScale = value; break; case 4: Draft.touchScale = value; break; case 5: Draft.touchOpacity = value; break; case 6: Draft.deadzone = value; break; case 7: Draft.sprintThreshold = value; break; }
            Changed(index < 4);
        }
        void Changed(bool preview)
        {
            Dirty = true;
            if (preview)
                SettingsRuntime.Apply(Draft);
            status.text = "Chưa lưu thay đổi";
            Refresh();
        }

        public void BeginBinding(int action)
        {
            if (action == (int)GameAction.Settings || operation)
                return;
            bindingAction = action;
            captureFrame = Time.frameCount;
            captureText.text = "Nhấn phím mới cho: " + GameSettings.ActionName((GameAction)action) + "\nEsc để hủy";
            captureOverlay.SetActive(true);
            captureOverlay.transform.SetAsLastSibling();
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(null);
        }
        public bool TryAssignKey(Key key)
        {
            if (bindingAction < 0)
                return false;
            if (!GameSettings.TryBind(Draft, (GameAction)bindingAction, key, out var error))
            {
                captureText.text = error + "\nChọn phím khác hoặc Esc để hủy";
                return false;
            }
            CancelBinding();
            Changed(false);
            return true;
        }
        public void CancelBinding()
        {
            bindingAction = -1;
            captureOverlay.SetActive(false);
        }
        public void MoveTouch(int index, Vector2 position)
        {
            if (Draft.leftHanded)
                position.x = 1 - position.x;
            Draft.touchPositions[index] = new Vector2(Mathf.Clamp(position.x, .05f, .95f), Mathf.Clamp(position.y, .06f, .78f));
            Changed(false);
        }
        public void Apply()
        {
            CancelBinding();
            GameSettings.Commit(Draft);
            baseline = GameSettings.Current.Copy();
            Draft = baseline.Copy();
            Dirty = false;
            status.text = "Đã áp dụng và lưu";
            Refresh();
        }
        public void RestoreDefaults()
        {
            Confirm("Khôi phục toàn bộ cài đặt mặc định? Bạn có thể kiểm tra lại trước khi Áp dụng.", () =>
            {
                Draft = SettingsData.Defaults();
                Changed(true);
            });
        }
        public void RestoreControls()
        {
            var d = SettingsData.Defaults();
            Draft.keys = d.keys;
            Draft.touchPositions = d.touchPositions;
            Draft.touchScale = d.touchScale;
            Draft.touchOpacity = d.touchOpacity;
            Draft.deadzone = d.deadzone;
            Draft.sprintThreshold = d.sprintThreshold;
            Draft.leftHanded = false;
            Changed(false);
        }
        public void Discard()
        {
            Draft = baseline.Copy();
            SettingsRuntime.Apply(baseline);
            Dirty = false;
            Refresh();
        }
        public void RequestClose()
        {
            if (operation)
                return;
            if (Dirty)
                Confirm("Bỏ các thay đổi chưa áp dụng và đóng cài đặt?", () =>
                {
                    Discard();
                    SettingsOverlay.Close();
                });
            else SettingsOverlay.Close();
        }
        public void Resume()
        {
            if (Dirty)
                Confirm("Áp dụng thay đổi và tiếp tục trận?", () =>
                {
                    Apply();
                    SettingsOverlay.Close(true);
                });
            else SettingsOverlay.Close(true);
        }
        public void LeaveMatch()
        {
            var message = SettingsOverlay.Host is ISettingsLeaveConfirmation context ? context.LeaveConfirmation : "Rời trận đấu máy? Trận đang chơi sẽ kết thúc và bạn trở về đại sảnh.";
            Confirm(message, () =>
            {
                Discard();
                SettingsOverlay.Host?.LeaveToMenu();
            });
        }
        public void SignOut()
        {
            Confirm("Đăng xuất tài khoản và trở về màn hình đăng nhập?", SignOutConfirmed);
        }
        async void SignOutConfirmed()
        {
            operation = true;
            status.text = "Đang đăng xuất…";
            try
            {
                await (SettingsOverlay.Host?.SignOutAsync() ?? System.Threading.Tasks.Task.CompletedTask);
                if (status) status.text = "Đã đăng xuất.";
            }
            catch (Exception e)
            {
                status.text = e.Message;
            }
            finally
            {
                operation = false;
            }
        }
        void Confirm(string message, Action action)
        {
            CancelBinding();
            confirmed = action;
            confirmText.text = message;
            confirmOverlay.SetActive(true);
            confirmOverlay.transform.SetAsLastSibling();
        }
        public void AcceptConfirm()
        {
            var action = confirmed;
            confirmed = null;
            confirmOverlay.SetActive(false);
            action?.Invoke();
        }
        public void CancelConfirm()
        {
            confirmed = null;
            confirmOverlay.SetActive(false);
        }

        void Refresh()
        {
            refreshing = true;
            float[] values = { Draft.master, Draft.music, Draft.effects, Draft.renderScale, Draft.touchScale, Draft.touchOpacity, Draft.deadzone, Draft.sprintThreshold };
            for (int i = 0; i < sliders.Length; i++)
            {
                sliders[i].SetValueWithoutNotify(values[i]);
                sliderValues[i].text = (values[i] * 100).ToString("0") + "%";
            }
            shadowToggle.SetIsOnWithoutNotify(Draft.shadows);
            vsyncToggle.SetIsOnWithoutNotify(Draft.vsync);
            leftToggle.SetIsOnWithoutNotify(Draft.leftHanded);
            for (int i = 0; i < presets.Length; i++)
                Tint(presets[i], i == Draft.graphics);
            for (int i = 0; i < fpsChoices.Length; i++)
                Tint(fpsChoices[i], new[] { 30, 60, 120 }[i] == Draft.fps);
            for (int i = 0; i < aaChoices.Length; i++)
                Tint(aaChoices[i], new[] { 1, 2, 4 }[i] == Draft.msaa);
            for (int i = 0; i < displayChoices.Length; i++)
                Tint(displayChoices[i], i == Draft.displayMode);
            if (sizeChoices.Length > 0)
            {
                var monitor = DesktopWindow.MonitorSize;
                var size = WindowSizes.Resolve(Draft.windowWidth, Draft.windowHeight, monitor.x, monitor.y);
                for (int i = 0; i < sizeChoices.Length; i++)
                    Tint(sizeChoices[i], Draft.Display == DisplayMode.Windowed && windowSizes[i] == size);
            }
            for (int i = 0; i < bindingLabels.Length; i++)
                bindingLabels[i].text = GameSettings.KeyName((Key)Draft.keys[i]);
            comboHint.text = "Drop: " + GameSettings.KeyName(Draft.KeyFor(GameAction.Drive)) + " + " + GameSettings.KeyName(Draft.KeyFor(GameAction.Dink)) + "   |   Smash: " + GameSettings.KeyName(Draft.KeyFor(GameAction.Drive)) + " + " + GameSettings.KeyName(Draft.KeyFor(GameAction.Volley));
            foreach (var handle in touchHandles)
                handle.Refresh();
            applyButton.interactable = Dirty;
            refreshing = false;
        }
        // Desktop-only "Khung hình" tab built from the graphics page so it inherits the scene's styling.
        void AddDisplayTab()
        {
            if (!(DesktopWindow.Supported || Application.isEditor) || tabs.Length != 4 || pages.Length != 4)
                return;
            var tab = Instantiate(tabs[2], tabs[2].transform.parent);
            tab.name = "Tab4";
            tab.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            var tabLabel = tab.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (tabLabel)
                tabLabel.text = "Khung hình";
            var tabIcon = tab.GetComponentInChildren<UiLineIcon>(true);
            if (tabIcon)
                tabIcon.SetGlyph(ArenaGlyph.Display);

            var page = Instantiate(pages[2], pages[2].transform.parent);
            page.name = "Page4";
            page.SetActive(false);
            var heading = page.transform.Find("Heading");
            var label = page.transform.Find("FpsLabel");
            var choice = page.transform.Find("Fps0")?.GetComponent<UnityEngine.UI.Button>();
            var hintSource = pages[0].transform.Find("Intro");
            if (!heading || !label || !choice || !hintSource)
            {
                Destroy(page);
                Destroy(tab.gameObject);
                return;
            }
            foreach (Transform child in page.transform)
                if (child != heading && child != label && child != choice.transform)
                    Destroy(child.gameObject);
            tabs = tabs.Append(tab).ToArray();
            for (int i = 0; i < tabs.Length; i++)
            {
                float top = .97f - i * .1925f;
                Place((RectTransform)tabs[i].transform, 0, top - .17f, 1, top);
            }
            heading.GetComponent<TMP_Text>().text = "Khung hình và cửa sổ";
            label.name = "DisplayModeLabel";
            label.GetComponent<TMP_Text>().text = "Chế độ hiển thị";
            Place((RectTransform)label, 0, .74f, 1, .82f);
            string[] modes = { "Cửa sổ", "Toàn màn hình", "Không viền" };
            displayChoices = new UnityEngine.UI.Button[modes.Length];
            for (int i = 0; i < modes.Length; i++)
            {
                int mode = i;
                displayChoices[i] = ChoiceButton(choice, page.transform, "DisplayMode" + i, modes[i], i, .6f, .72f);
                displayChoices[i].onClick.AddListener(() => SetDisplayMode(mode));
            }
            var sizeLabel = Instantiate(label, page.transform);
            sizeLabel.name = "WindowSizeLabel";
            sizeLabel.GetComponent<TMP_Text>().text = "Kích thước cửa sổ";
            Place((RectTransform)sizeLabel, 0, .44f, 1, .52f);
            var monitor = DesktopWindow.MonitorSize;
            windowSizes = WindowSizes.Choices(monitor.x, monitor.y);
            sizeChoices = new UnityEngine.UI.Button[windowSizes.Length];
            for (int i = 0; i < windowSizes.Length; i++)
            {
                int index = i;
                sizeChoices[i] = ChoiceButton(choice, page.transform, "WindowSize" + i, WindowSizes.Label(windowSizes[i]), i, .3f, .42f);
                sizeChoices[i].onClick.AddListener(() => SetWindowSize(index));
            }
            Destroy(choice.gameObject);
            var hint = Instantiate(hintSource, page.transform).GetComponent<TMP_Text>();
            hint.name = "WindowHint";
            hint.text = "Chế độ cửa sổ dùng khung PickleVerse: kéo thanh tiêu đề để di chuyển, nút \u2013 để thu nhỏ, nút \u00D7 để thoát game. Chọn kích thước sẽ chuyển sang chế độ cửa sổ."
                + (DesktopWindow.Supported ? "" : " Trong Unity Editor, lựa chọn chỉ được lưu, cửa sổ không đổi.");
            hint.fontSize *= .82f;
            hint.color = ScreenUiTheme.Muted;
            Place(hint.rectTransform, 0, .02f, 1, .26f);
            pages = pages.Append(page).ToArray();
        }
        static UnityEngine.UI.Button ChoiceButton(UnityEngine.UI.Button prototype, Transform parent, string name, string text, int column, float bottom, float top)
        {
            var button = Instantiate(prototype, parent);
            button.name = name;
            button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label)
                label.text = text;
            Place((RectTransform)button.transform, column * .34f, bottom, column * .34f + .32f, top);
            return button;
        }
        void AddQuitButton()
        {
            if (!GameExit.Supported || !signOutButton)
                return;
            quitButton = Instantiate(signOutButton, signOutButton.transform.parent);
            quitButton.name = "QuitGame";
            quitButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            quitButton.onClick.AddListener(QuitGame);
            quitButton.gameObject.SetActive(true);
            var label = quitButton.GetComponentInChildren<TMP_Text>(true);
            if (label)
                label.text = "Thoát game";
            bool paired = signOutButton.gameObject.activeSelf || leaveButton.gameObject.activeSelf;
            Place((RectTransform)quitButton.transform, paired ? .67f : 0, .1f, paired ? 1 : .64f, .25f);
        }
        static void Place(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        static void Tint(UnityEngine.UI.Button button, bool selected)
        {
            if (ScreenUiTheme.Current)
                ScreenUiTheme.Selection(button, selected);
            else button.GetComponent<UnityEngine.UI.Image>().color = selected ? new Color(.10f, .32f, .33f) : new Color(.06f, .14f, .19f);
        }
        void ApplySafeArea()
        {
            var area = UiSafeArea.Area;
            var size = new Vector2(Screen.width, Screen.height);
            if (area == lastSafeArea && size == lastResolution)
                return;
            lastSafeArea = area;
            lastResolution = size;
            safeArea.anchorMin = new Vector2(area.xMin / size.x, area.yMin / size.y);
            safeArea.anchorMax = new Vector2(area.xMax / size.x, area.yMax / size.y);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }
        void OnDestroy()
        {
            if (Dirty)
                SettingsRuntime.Apply(baseline);
            SettingsOverlay.NotifyDestroyed();
        }
    }

}
