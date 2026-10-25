using System.Collections;
using PickleVerse.LocalMatch;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace PickleVerse.FrontEnd
{
    public sealed class ModeSelectionMenu : MonoBehaviour
    {
        public RectTransform safeArea;
        public GameObject modePage, botPage, roomPage;
        public TMP_InputField roomJoinCode;
        public bool IsRoomSetup { get; private set; }
        public TMP_Text breadcrumb, title, subtitle, summary, difficultyDescription, loadingLabel;
        public GameObject loadingOverlay;
        public UnityEngine.UI.Button botButton, startButton;
        public UnityEngine.UI.Button tutorialButton;
        public UnityEngine.UI.Button[] difficultyButtons, environmentButtons;
        public TMP_Text[] difficultySelected, environmentSelected;
        public UnityEngine.UI.RawImage courtPreview;
        public Texture2D dayPreview, nightPreview;
        public UnityEngine.UI.Button[] venueButtons;
        public TMP_Text[] venueSelected;
        public Texture2D[] venueDayPreviews, venueNightPreviews;
        public CourtVenue SelectedVenue { get; private set; }
        public bool IsBotSetup { get; private set; }
        public int SelectedDifficulty { get; private set; }
        public bool SelectedNight { get; private set; }
        public bool Loading { get; private set; }
        Rect previousSafeArea;
        Vector2 previousScreen;
        Coroutine reveal;
        static readonly Color Accent = new Color(.79f, .96f, .45f);
        static readonly Color Border = new Color(.22f, .34f, .39f);

        void Start()
        {
            SelectedDifficulty = BotMatchLaunchSettings.Difficulty;
            SelectedNight = BotMatchLaunchSettings.Night;
            SelectedVenue = BotMatchLaunchSettings.Venue;
            if (roomJoinCode)
                roomJoinCode.onSubmit.AddListener(_ => JoinPrivateRoom());
            ApplySafeArea();
            ShowModes();
        }

        void Update()
        {
            ApplySafeArea();
            if (!PickleVerse.Settings.FrontEndModalInput.IsOpen && !Loading && !PickleVerse.Settings.SceneLoading.IsBlocking && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Back();
        }

        void ApplySafeArea()
        {
            if (!safeArea)
                return;
            Rect area = Screen.safeArea;
            Vector2 screen = new Vector2(Screen.width, Screen.height);
            if (area == previousSafeArea && screen == previousScreen)
                return;
            previousSafeArea = area;
            previousScreen = screen;
            safeArea.anchorMin = new Vector2(area.xMin / screen.x, area.yMin / screen.y);
            safeArea.anchorMax = new Vector2(area.xMax / screen.x, area.yMax / screen.y);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }

        public void ShowModes()
        {
            if (Loading)
                return;
            IsBotSetup = false;
            IsRoomSetup = false;
            if (roomPage)
                roomPage.SetActive(false);
            modePage.SetActive(true);
            botPage.SetActive(false);
            if (tutorialButton)
                tutorialButton.gameObject.SetActive(true);
            breadcrumb.text = "CHƠI  /  CHỌN CHẾ ĐỘ";
            title.text = "Chọn cách bạn vào sân";
            subtitle.text = "Một trận đấu mới bắt đầu từ đây.";
            Reveal(modePage);
            Focus(botButton);
        }

        public void OpenBot()
        {
            if (Loading)
                return;
            IsBotSetup = true;
            IsRoomSetup = false;
            if (roomPage)
                roomPage.SetActive(false);
            modePage.SetActive(false);
            botPage.SetActive(true);
            if (tutorialButton)
                tutorialButton.gameObject.SetActive(false);
            breadcrumb.text = "CHƠI  /  ĐẤU VỚI BOT";
            title.text = "Chuẩn bị trận đấu";
            subtitle.text = "Chọn đối thủ và bối cảnh phù hợp với bạn.";
            RefreshChoices();
            Reveal(botPage);
            Focus(startButton);
        }

        public void OpenMultiplayer()
        {
            PickleVerse.Multiplayer.UI.RoomLaunchRequest.Clear();
            OpenRoomScene("Đang mở phòng multiplayer…");
        }

        public void OpenRooms()
        {
            if (Loading || !roomPage)
                return;
            IsBotSetup = false;
            IsRoomSetup = true;
            modePage.SetActive(false);
            botPage.SetActive(false);
            roomPage.SetActive(true);
            if (tutorialButton)
                tutorialButton.gameObject.SetActive(false);
            breadcrumb.text = "CHƠI  /  PHÒNG RIÊNG";
            title.text = "Lập đội. Hẹn đối thủ.";
            subtitle.text = "Tạo phòng riêng hoặc nhập mã để tham gia cùng bạn bè.";
            Reveal(roomPage);
            Focus(roomPage.GetComponentInChildren<UnityEngine.UI.Button>());
        }
        public void CreateSinglesRoom()
        {
            CreateRoom(PickleVerse.Multiplayer.PlayMode.Singles);
        }
        public void CreateDoublesRoom()
        {
            CreateRoom(PickleVerse.Multiplayer.PlayMode.Doubles);
        }
        void CreateRoom(PickleVerse.Multiplayer.PlayMode mode)
        {
            if (Loading)
                return;
            PickleVerse.Multiplayer.UI.RoomLaunchRequest.Create(mode);
            OpenRoomScene("Đang tạo phòng " + (mode == PickleVerse.Multiplayer.PlayMode.Singles ? "1v1" : "2v2") + "…");
        }
        public void JoinPrivateRoom()
        {
            if (Loading)
                return;
            if (!roomJoinCode || string.IsNullOrWhiteSpace(roomJoinCode.text))
            {
                subtitle.text = "Nhập mã phòng mà bạn bè đã chia sẻ.";
                roomJoinCode?.ActivateInputField();
                return;
            }
            PickleVerse.Multiplayer.UI.RoomLaunchRequest.Join(roomJoinCode.text);
            OpenRoomScene("Đang tham gia phòng…");
        }
        void OpenRoomScene(string message)
        {
            if (Loading)
                return;
            if (!Application.CanStreamedLevelBeLoaded("PlayMultiplayer"))
            {
                subtitle.text = "Chưa tìm thấy phòng PlayMultiplayer trong bản game.";
                return;
            }
            Loading = true;
            PickleVerse.Multiplayer.MultiplayerApp.OpenDefaultRoomOnLoad = true;
            StartCoroutine(LoadScene("PlayMultiplayer", message));
        }

        public void OpenTraining()
        {
            if (Loading || PickleVerse.Shop.LobbyNavigation.IsBusy)
                return;
            if (!Application.CanStreamedLevelBeLoaded("Training"))
            {
                subtitle.text = "Chưa tìm thấy sân luyện tập.";
                return;
            }
            Loading = true;
            PickleVerse.Shop.LobbyNavigation.OpenTraining();
        }

        void Reveal(GameObject page)
        {
            if (reveal != null)
                StopCoroutine(reveal);
            reveal = StartCoroutine(RevealPage(page.GetComponent<CanvasGroup>()));
        }

        IEnumerator RevealPage(CanvasGroup group)
        {
            if (!group)
                yield break;
            for (float t = 0; t < .18f; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Clamp01(t / .18f);
                yield return null;
            }
            group.alpha = 1;
            reveal = null;
        }

        public void SetDifficulty(int level)
        {
            if (Loading)
                return;
            SelectedDifficulty = Mathf.Clamp(level, 0, 3);
            RefreshChoices();
        }

        public void SelectDay()
        {
            SetEnvironment(false);
        }
        public void SelectNight()
        {
            SetEnvironment(true);
        }
        public void SelectProfessional()
        {
            SetVenue(CourtVenue.Professional);
        }
        public void SelectLowPoly()
        {
            SetVenue(CourtVenue.LowPoly);
        }
        void SetVenue(CourtVenue venue)
        {
            if (Loading)
                return;
            SelectedVenue = CourtVenueSelection.Normalize(venue);
            RefreshChoices();
        }
        void SetEnvironment(bool night)
        {
            if (Loading)
                return;
            SelectedNight = night;
            RefreshChoices();
        }

        void RefreshChoices()
        {
            for (int i = 0; i < difficultyButtons.Length; i++)
                MarkChoice(difficultyButtons[i], difficultySelected[i], i == SelectedDifficulty);
            for (int i = 0; i < environmentButtons.Length; i++)
                MarkChoice(environmentButtons[i], environmentSelected[i], i == (SelectedNight ? 1 : 0));
            if (venueButtons != null)
                for (int i = 0; i < venueButtons.Length; i++)
                {
                    bool selected = i == (int)SelectedVenue;
                    PickleVerse.Settings.ScreenUiTheme.Selection(venueButtons[i], selected);
                    if (venueSelected != null && i < venueSelected.Length && venueSelected[i])
                        venueSelected[i].gameObject.SetActive(selected);
                }
            difficultyDescription.text = new[]
            {
                "Dễ  ·  Bot phản ứng chậm hơn, phù hợp để làm quen.",
                "Vừa  ·  Nhịp đấu cân bằng, phù hợp để luyện combo.",
                "Khó  ·  Bot phản ứng nhanh và đánh chính xác hơn.",
                "BhTree  ·  Behavior graph thử nghiệm, cấu hình theo Elo local."
            }[SelectedDifficulty];
            summary.text = "BOT " + new[] { "DỄ", "VỪA", "KHÓ", "BHTREE" }[SelectedDifficulty] + "   /   " + (SelectedNight ? "BAN ĐÊM" : "BAN NGÀY");
            summary.text += "   /   " + CourtVenueSelection.DisplayName(SelectedVenue);
            var previews = SelectedNight ? venueNightPreviews : venueDayPreviews;
            courtPreview.texture = previews != null && previews.Length > (int)SelectedVenue ? previews[(int)SelectedVenue] : (SelectedNight ? nightPreview : dayPreview);
        }

        static void MarkChoice(UnityEngine.UI.Button button, TMP_Text marker, bool selected)
        {
            var outline = button.GetComponent<UnityEngine.UI.Outline>();
            if (outline)
                outline.effectColor = selected ? Accent : Border;
            marker.gameObject.SetActive(selected);
        }

        static void Focus(UnityEngine.UI.Button button)
        {
            if (EventSystem.current && button)
                EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        public void Back()
        {
            if (Loading)
                return;
            if (IsBotSetup || IsRoomSetup)
                ShowModes();
            else PickleVerse.Shop.LobbyNavigation.ReturnToLobby();
        }

        public void StartBot()
        {
            if (Loading || !IsBotSetup)
                return;
            if (!Application.CanStreamedLevelBeLoaded("BotMatch"))
            {
                subtitle.text = "Chưa tìm thấy sân BotMatch trong bản game.";
                return;
            }
            Loading = true;
            BotMatchLaunchSettings.Prepare(SelectedDifficulty, SelectedNight, SelectedVenue);
            PickleVerse.Settings.SceneLoading.Load("BotMatch");
        }

        IEnumerator LoadScene(string sceneName, string message)
        {
            loadingOverlay.SetActive(true);
            loadingLabel.text = message;
            EventSystem.current?.SetSelectedGameObject(null);
            yield return null;
            yield return SceneManager.LoadSceneAsync(sceneName);
        }
    }
}

