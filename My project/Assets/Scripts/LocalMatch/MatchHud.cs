using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.LocalMatch
{
    public sealed class MatchHud : MonoBehaviour
    {
        public TMP_Text scoreboard, status, timing, selection, modalTitle, modalBody, serveClock, difficultyLabel;
        public GameObject modal, help, touchControls, shotPanel;
        public Button continueButton, restartButton;
        public GameObject environmentChoice;
        public UnityEngine.UI.Button dayButton, nightButton;
        BotMatch match;
        bool helpPaused;
        public void Bind(BotMatch owner)
        {
            match = owner;
            if(!shotPanel && selection)shotPanel=selection.transform.parent.gameObject;
            if (touchControls) touchControls.SetActive(Application.isMobilePlatform);
        }
        void Update()
        {
            if (!match || match.Rules == null) return;
            if(match.Phase==MatchPhase.Cinematic)return;
            var rules = match.Rules;
            scoreboard.text = "<color=#83E6CD>BẠN</color>  " + rules.Points[0] + "   :   " + rules.Points[1] + "  <color=#F9BC7A>BOT</color>\n<size=17>GAME " + Mathf.Min(3, rules.GameNumber) + "    •    Số game thắng " + rules.Games[0] + "–" + rules.Games[1] + "    •    Đấu đơn 1v1</size>";
            status.text = match.Message;
            bool choosing=!string.IsNullOrEmpty(match.SelectionKeys);
            bool swinging=match.human.Swinging && !string.IsNullOrEmpty(match.ActiveShotKeys);
            selection.text=choosing?"<b>"+match.SelectionKeys+"</b>\n<size=14>"+match.Selection+"</size>":swinging?"<b>"+match.ActiveShotKeys+"</b>\n<size=14>"+match.human.Intent+"</size>":"";
            if(shotPanel)shotPanel.SetActive(!match.Paused && (choosing || swinging));
            if(timing)timing.gameObject.SetActive(false);
            serveClock.text = match.Phase == MatchPhase.PreServe ? "GIAO BÓNG  " + Mathf.Max(0, 10f - match.ServeElapsed).ToString("0.0") + "s" : match.Phase == MatchPhase.Rally ? (rules.Bounces == 0 ? "BÓNG CHƯA NẢY" : "BÓNG ĐÃ NẢY") : "";
            if(match.Phase==MatchPhase.Rally && rules.ExpectedHitter==0 && match.PredictContact(match.human.end,true,out _,out var eta,match.human.ContactOffset(new ShotIntent(ShotKind.Drive)).y))
                serveClock.text+="\n<size=16>Thả J sau "+Mathf.Max(0,eta-.433f).ToString("0.0")+"s</size>";
            difficultyLabel.text = "Bot: " + new[] { "Dễ", "Vừa", "Khó" }[match.botDifficulty];
            bool show = match.Paused || match.Phase == MatchPhase.Intro || match.Phase == MatchPhase.MatchOver || match.Phase == MatchPhase.BetweenGames || match.Phase == MatchPhase.Timeout;
            modal.SetActive(show);
            if (!show) return;
            bool intro = match.Phase == MatchPhase.Intro;
            ((RectTransform)modal.transform).sizeDelta=new Vector2(760,intro?430:370);
            modalBody.rectTransform.anchoredPosition=new Vector2(0,intro?60:6);
            modalBody.rectTransform.sizeDelta=new Vector2(710,intro?174:235);
            modalBody.fontSize=intro?18:19;
            if(environmentChoice)environmentChoice.SetActive(intro && !match.Paused);
            if(intro && dayButton && nightButton)
            {
                var environment=match.GetComponent<MatchEnvironmentLighting>();
                bool night=environment && environment.selectedNight;
                dayButton.interactable=night;nightButton.interactable=!night;
                dayButton.GetComponentInChildren<TMP_Text>().text=night?"Ban ngày":"Ban ngày (đã chọn)";
                nightButton.GetComponentInChildren<TMP_Text>().text=night?"Ban đêm (đã chọn)":"Ban đêm";
            }
            modalTitle.text = match.Paused ? "TẠM DỪNG" : intro ? "PICKLEVERSE / ĐẤU VỚI BOT" : match.Phase == MatchPhase.MatchOver ? match.Message : match.Phase == MatchPhase.Timeout ? "TIME-OUT" : "KẾT THÚC GAME";
            modalBody.text = intro ? "WASD di chuyển • Shift chạy\nGiữ J / K / L để chọn; thả để đánh. Ghép 2 phím để dùng combo.\n\nGiao chéo bằng J • Hai cú đầu phải nảy\nVolley ngoài Kitchen • 11 điểm, cách 2 • Thắng 2/3 game\n\nChọn bối cảnh bên dưới, rồi nhấn Vào sân / Enter." : match.Paused ? "Esc / Enter để tiếp tục\nF1 hướng dẫn • T gọi time-out trước giao bóng\nĐã dùng " + (2 - rules.Timeouts[0]) + "/2 time-out trong game này." : match.Phase == MatchPhase.MatchOver ? "Game thắng: " + rules.Games[0] + "–" + rules.Games[1] + "\nSố rally: " + match.TotalRallies + " • Rally dài nhất: " + match.LongestRally + " cú\nChơi lại để luyện timing, combo và vị trí chân." : "Điểm: " + rules.Points[0] + "–" + rules.Points[1] + "\nEnter để tiếp tục sớm; bot đồng ý.\nĐổi sân giữa các game và tại 6 điểm trong game quyết định.";
            continueButton.GetComponentInChildren<TMP_Text>().text=intro?"Vào sân / Enter":"Enter / Tiếp tục";
            ((RectTransform)continueButton.transform).anchoredPosition=new Vector2(intro?0:-120,20);
            continueButton.gameObject.SetActive(match.Phase != MatchPhase.MatchOver);
            restartButton.gameObject.SetActive(!intro);
        }
        public void Continue() { if (match) match.Continue(); }
        public void Restart() { if (match) match.RestartWithIntro(); }
        public void Pause() { if (match) match.TogglePause(); }
        public void Timeout() { if (match) match.Timeout(); }
        public void ToggleHelp()
        {
            bool show=!help.activeSelf;help.SetActive(show);
            if(show) {help.transform.SetAsLastSibling();helpPaused=match&&!match.Paused;if(helpPaused)match.TogglePause();}
            else if(helpPaused && match && match.Paused){match.TogglePause();helpPaused=false;}
        }
        public void ToggleTouch() { touchControls.SetActive(!touchControls.activeSelf); }
        public void Difficulty(int level) { if (match) match.SetDifficulty(level); }
        public void SelectDay() { SelectEnvironment(false); }
        public void SelectNight() { SelectEnvironment(true); }
        void SelectEnvironment(bool night)
        {
            if(!match || match.Phase!=MatchPhase.Intro || match.Paused)return;
            match.GetComponent<MatchEnvironmentLighting>()?.SelectForMatch(night);
        }
    }
}
