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
        public GameObject modePage, botPage;
        public TMP_Text breadcrumb, title, subtitle, summary, difficultyDescription, loadingLabel;
        public GameObject loadingOverlay;
        public UnityEngine.UI.Button botButton, startButton;
        public UnityEngine.UI.Button[] difficultyButtons, environmentButtons;
        public TMP_Text[] difficultySelected, environmentSelected;
        public UnityEngine.UI.RawImage courtPreview;
        public Texture2D dayPreview, nightPreview;
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
            ApplySafeArea();
            ShowModes();
        }

        void Update()
        {
            ApplySafeArea();
            if (!PickleVerse.Settings.FrontEndModalInput.IsOpen && !Loading && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Back();
        }

        void ApplySafeArea()
        {
            if (!safeArea) return;
            Rect area = Screen.safeArea;
            Vector2 screen = new Vector2(Screen.width, Screen.height);
            if (area == previousSafeArea && screen == previousScreen) return;
            previousSafeArea = area; previousScreen = screen;
            safeArea.anchorMin = new Vector2(area.xMin / screen.x, area.yMin / screen.y);
            safeArea.anchorMax = new Vector2(area.xMax / screen.x, area.yMax / screen.y);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }

        public void ShowModes()
        {
            if (Loading) return;
            IsBotSetup = false;
            modePage.SetActive(true); botPage.SetActive(false);
            breadcrumb.text = "CHƠI  /  CHỌN CHẾ ĐỘ";
            title.text = "Chọn cách bạn vào sân";
            subtitle.text = "Một trận đấu mới bắt đầu từ đây.";
            Reveal(modePage);
            Focus(botButton);
        }

        public void OpenBot()
        {
            if (Loading) return;
            IsBotSetup = true;
            modePage.SetActive(false); botPage.SetActive(true);
            breadcrumb.text = "CHƠI  /  ĐẤU VỚI BOT";
            title.text = "Chuẩn bị trận đấu";
            subtitle.text = "Chọn đối thủ và bối cảnh phù hợp với bạn.";
            RefreshChoices();
            Reveal(botPage);
            Focus(startButton);
        }

        public void OpenMultiplayer()
        {
            if (Loading) return;
            if (!Application.CanStreamedLevelBeLoaded("PlayMultiplayer"))
            {
                subtitle.text = "Chưa tìm thấy phòng PlayMultiplayer trong bản game.";
                return;
            }
            Loading = true;
            PickleVerse.Multiplayer.MultiplayerApp.OpenDefaultRoomOnLoad = true;
            StartCoroutine(LoadScene("PlayMultiplayer", "Đang mở phòng multiplayer…"));
        }

        void Reveal(GameObject page)
        {
            if (reveal != null) StopCoroutine(reveal);
            reveal = StartCoroutine(RevealPage(page.GetComponent<CanvasGroup>()));
        }

        IEnumerator RevealPage(CanvasGroup group)
        {
            if (!group) yield break;
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
            if (Loading) return;
            SelectedDifficulty = Mathf.Clamp(level, 0, 2);
            RefreshChoices();
        }

        public void SelectDay() { SetEnvironment(false); }
        public void SelectNight() { SetEnvironment(true); }
        void SetEnvironment(bool night)
        {
            if (Loading) return;
            SelectedNight = night;
            RefreshChoices();
        }

        void RefreshChoices()
        {
            for (int i = 0; i < difficultyButtons.Length; i++) MarkChoice(difficultyButtons[i], difficultySelected[i], i == SelectedDifficulty);
            for (int i = 0; i < environmentButtons.Length; i++) MarkChoice(environmentButtons[i], environmentSelected[i], i == (SelectedNight ? 1 : 0));
            difficultyDescription.text = new[]
            {
                "Dễ  ·  Bot phản ứng chậm hơn, phù hợp để làm quen.",
                "Vừa  ·  Nhịp đấu cân bằng, phù hợp để luyện combo.",
                "Khó  ·  Bot phản ứng nhanh và đánh chính xác hơn."
            }[SelectedDifficulty];
            summary.text = "BOT " + new[] { "DỄ", "VỪA", "KHÓ" }[SelectedDifficulty] + "   /   " + (SelectedNight ? "BAN ĐÊM" : "BAN NGÀY");
            courtPreview.texture = SelectedNight ? nightPreview : dayPreview;
        }

        static void MarkChoice(UnityEngine.UI.Button button, TMP_Text marker, bool selected)
        {
            var outline = button.GetComponent<UnityEngine.UI.Outline>();
            if (outline) outline.effectColor = selected ? Accent : Border;
            marker.gameObject.SetActive(selected);
        }

        static void Focus(UnityEngine.UI.Button button)
        {
            if (EventSystem.current && button) EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        public void Back()
        {
            if (Loading) return;
            if (IsBotSetup) ShowModes();
            else SceneManager.LoadScene("Menu");
        }

        public void StartBot()
        {
            if (Loading || !IsBotSetup) return;
            if (!Application.CanStreamedLevelBeLoaded("BotMatch"))
            {
                subtitle.text = "Chưa tìm thấy sân BotMatch trong bản game.";
                return;
            }
            Loading = true;
            BotMatchLaunchSettings.Prepare(SelectedDifficulty, SelectedNight);
            StartCoroutine(LoadScene("BotMatch", "Đang chuẩn bị sân…"));
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
