using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PickleVerse.Settings;

namespace PickleVerse.LocalMatch
{
    public sealed class MatchHud : MonoBehaviour
    {
        public TMP_Text scoreboard, status, timing, selection, modalTitle, modalBody, serveClock, difficultyLabel;
        public GameObject modal, help, touchControls, shotPanel;
        public Button continueButton, restartButton, modeButton;
        public MatchAnnouncements announcements;
        public GameObject resultActions;
        public TMP_Text resultSummary;
        public MatchScoreboard broadcast;
        public MatchHudLayout layout;
        BotMatch match;
        bool helpPaused;
        int shownDifficulty=-1;
        int resultGamesA=-1,resultGamesB=-1;
        MatchPhase modalPhase=(MatchPhase)(-1);
        bool modalPaused;
        int modalPointsA=-1,modalPointsB=-1,modalGamesA=-1,modalGamesB=-1,modalTimeouts=-1;
        float nextHint;
        string rallyHint="",driveKey,shownKeys,shownSelection;
        public void Bind(BotMatch owner)
        {
            match = owner;
            if(broadcast)broadcast.SetTeams("BẠN","BOT","");
            GameSettings.Changed-=RefreshBindings;GameSettings.Changed+=RefreshBindings;RefreshBindings();
            if (announcements) { announcements.match = owner; announcements.Clear(); }
            if(!shotPanel && selection)shotPanel=selection.transform.parent.gameObject;
            if (touchControls) { MobileTouchPad.Install(touchControls,owner); touchControls.SetActive(Application.isMobilePlatform); }
        }
        void Update()
        {
            if (!match || match.Rules == null) return;
            if(match.Phase==MatchPhase.Cinematic)return;
            var rules = match.Rules;
            if(broadcast)
            {
                if(shownDifficulty!=match.botDifficulty){shownDifficulty=match.botDifficulty;broadcast.SetTeams("BẠN","BOT","ĐẤU BOT · "+new[]{"DỄ","VỪA","KHÓ","THÍCH ỨNG"}[shownDifficulty]);}
                broadcast.SetScores(rules.Points[0],rules.Points[1],rules.Games[0],rules.Games[1],Mathf.Min(3,rules.GameNumber),rules.Server);
                if(match.Phase==MatchPhase.Rally&&rules.ExpectedHitter==0)
                {
                    if(Time.unscaledTime>=nextHint){nextHint=Time.unscaledTime+.1f;rallyHint=match.PredictContact(match.human.end,true,out _,out var predictedEta,match.human.ContactOffset(new ShotIntent(ShotKind.Drive)).y)?"Thả "+driveKey+" sau "+Mathf.Max(0,predictedEta-.433f).ToString("0.0")+"s":"";}
                }
                else rallyHint="";
                var phase=match.Phase;
                broadcast.SetClock(phase==MatchPhase.PreServe?CourtClockMode.Serve:phase==MatchPhase.Rally?CourtClockMode.Rally:phase==MatchPhase.Timeout?CourtClockMode.Timeout:phase==MatchPhase.MatchOver?CourtClockMode.Finished:phase==MatchPhase.BetweenGames?CourtClockMode.Break:CourtClockMode.Waiting,phase==MatchPhase.Timeout?match.TimeoutRemaining:10-match.ServeElapsed,rules.Bounces>0,phase==MatchPhase.PreServe?(rules.Server==0?"BẠN ĐANG GIAO":"BOT ĐANG GIAO"):rallyHint);
            }
            else scoreboard.text = "<color=#83E6CD>BẠN</color>  " + rules.Points[0] + "   :   " + rules.Points[1] + "  <color=#F9BC7A>BOT</color>\n<size=17>GAME " + Mathf.Min(3, rules.GameNumber) + "    •    Số game thắng " + rules.Games[0] + "–" + rules.Games[1] + "    •    Đấu đơn 1v1</size>";
            status.text = match.Message;
            bool refereeBanner = match.Phase == MatchPhase.Settling || match.Phase == MatchPhase.BetweenPoints || match.Phase == MatchPhase.BetweenGames || match.Phase == MatchPhase.MatchOver;
            status.transform.parent.gameObject.SetActive(!refereeBanner);
            bool results = match.Phase == MatchPhase.MatchOver && !match.Paused && (!announcements || !announcements.Busy) && (!match.victory || match.victory.Ready);
            if (resultActions)
            {
                bool newlyVisible = results && !resultActions.activeSelf;
                resultActions.SetActive(results);
                if (newlyVisible && UnityEngine.EventSystems.EventSystem.current)
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(resultActions.GetComponentInChildren<UnityEngine.UI.Button>().gameObject);
            }
            if (results && resultSummary && (resultGamesA!=rules.Games[0]||resultGamesB!=rules.Games[1])){resultGamesA=rules.Games[0];resultGamesB=rules.Games[1];resultSummary.text = "BẠN  ·  " + rules.Games[0] + " – " + rules.Games[1];}
            bool choosing=!string.IsNullOrEmpty(match.SelectionKeys);
            bool swinging=match.human.Swinging && !string.IsNullOrEmpty(match.ActiveShotKeys);
            string keys=choosing?match.SelectionKeys:swinging?match.ActiveShotKeys:"",description=choosing?match.Selection:swinging?match.human.Intent.ToString():"";
            if(keys!=shownKeys||description!=shownSelection){shownKeys=keys;shownSelection=description;selection.text=keys.Length>0?"<b>"+keys+"</b>\n<size=14>"+description+"</size>":"";}
            if(shotPanel)shotPanel.SetActive(!(touchControls&&touchControls.activeInHierarchy) && !match.Paused && (choosing || swinging));
            if(timing)timing.gameObject.SetActive(false);
            if(!broadcast)serveClock.text = match.Phase == MatchPhase.PreServe ? "GIAO BÓNG  " + Mathf.Max(0, 10f - match.ServeElapsed).ToString("0.0") + "s" : match.Phase == MatchPhase.Rally ? (rules.Bounces == 0 ? "BÓNG CHƯA NẢY" : "BÓNG ĐÃ NẢY") : "";
            if(!broadcast && match.Phase==MatchPhase.Rally && rules.ExpectedHitter==0 && match.PredictContact(match.human.end,true,out _,out var eta,match.human.ContactOffset(new ShotIntent(ShotKind.Drive)).y))
                serveClock.text+="\n<size=16>Thả "+GameSettings.KeyName(GameSettings.Current.KeyFor(GameAction.Drive))+" sau "+Mathf.Max(0,eta-.433f).ToString("0.0")+"s</size>";
            if(!broadcast)difficultyLabel.text = "Bot: " + match.BotLabel;
            bool show = !SettingsOverlay.IsOpen && (match.Paused || match.Phase == MatchPhase.Intro || match.Phase == MatchPhase.Timeout);
            modal.SetActive(show);
            if (!show){modalPhase=(MatchPhase)(-1);return;}
            if(modalPhase==match.Phase&&modalPaused==match.Paused&&modalPointsA==rules.Points[0]&&modalPointsB==rules.Points[1]&&modalGamesA==rules.Games[0]&&modalGamesB==rules.Games[1]&&modalTimeouts==rules.Timeouts[0])return;
            modalPhase=match.Phase;modalPaused=match.Paused;modalPointsA=rules.Points[0];modalPointsB=rules.Points[1];modalGamesA=rules.Games[0];modalGamesB=rules.Games[1];modalTimeouts=rules.Timeouts[0];
            bool intro = match.Phase == MatchPhase.Intro;
            ((RectTransform)modal.transform).sizeDelta=new Vector2(760,370);
            modalBody.rectTransform.anchoredPosition=new Vector2(0,6);
            modalBody.rectTransform.sizeDelta=new Vector2(710,235);
            modalBody.fontSize=19;
            modalTitle.text = match.Paused ? "TẠM DỪNG" : intro ? "PICKLEVERSE / ĐẤU VỚI BOT" : match.Phase == MatchPhase.MatchOver ? match.Message : match.Phase == MatchPhase.Timeout ? "TIME-OUT" : "KẾT THÚC GAME";
            modalBody.text = intro ? "WASD di chuyển • Shift chạy\nGiữ J / K / L để chọn; thả để đánh. Ghép 2 phím để dùng combo.\n\nGiao chéo bằng J • Hai cú đầu phải nảy\nVolley ngoài Kitchen • 11 điểm, cách 2 • Thắng 2/3 game\n\nNhấn Vào sân / Enter để bắt đầu." : match.Paused ? "Esc / Enter để tiếp tục\nF1 hướng dẫn • T gọi time-out trước giao bóng\nĐã dùng " + (2 - rules.Timeouts[0]) + "/2 time-out trong game này." : match.Phase == MatchPhase.MatchOver ? "Game thắng: " + rules.Games[0] + "–" + rules.Games[1] + "\nSố rally: " + match.TotalRallies + " • Rally dài nhất: " + match.LongestRally + " cú\nChơi lại cùng độ khó và bối cảnh đã chọn." : "Điểm: " + rules.Points[0] + "–" + rules.Points[1] + "\nEnter để tiếp tục sớm; bot đồng ý.\nĐổi sân giữa các game và tại 6 điểm trong game quyết định.";
            continueButton.GetComponentInChildren<TMP_Text>().text=intro?"Vào sân / Enter":"Enter / Tiếp tục";
            ((RectTransform)continueButton.transform).anchoredPosition=new Vector2(-245,20);
            ((RectTransform)restartButton.transform).anchoredPosition=new Vector2(0,20);
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
        public void ToggleTouch() { touchControls.SetActive(!touchControls.activeSelf);RefreshBindings(); }
        public void ChooseMode() { UnityEngine.SceneManagement.SceneManager.LoadScene("ModeSelection"); }
        public void OpenSettings() { if(match)SettingsOverlay.Open(match); }
        void RefreshBindings()
        {
            string K(GameAction action)=>GameSettings.KeyName(GameSettings.Current.KeyFor(action));
            bool mobile=Application.isMobilePlatform||touchControls&&touchControls.activeSelf;
            driveKey=mobile?"DRIVE":K(GameAction.Drive);
            var text=help?help.transform.Find("HelpText")?.GetComponent<TMP_Text>():null;
            if(text)text.text="<size=28><color=#83E6CD>ĐIỀU KHIỂN VÀ LUẬT</color></size>\n\n"+
                K(GameAction.Forward)+" / "+K(GameAction.Backward)+" / "+K(GameAction.Left)+" / "+K(GameAction.Right)+" di chuyển • "+K(GameAction.Run)+" chạy\n"+
                K(GameAction.Drive)+" Drive/giao bóng | "+K(GameAction.Dink)+" Dink | "+K(GameAction.Volley)+" Volley\n"+
                K(GameAction.Drive)+"+"+K(GameAction.Dink)+" Drop | "+K(GameAction.Drive)+"+"+K(GameAction.Volley)+" Smash | "+K(GameAction.Dink)+"+"+K(GameAction.Volley)+" Reset\n"+
                K(GameAction.Power)+" tăng lực • "+K(GameAction.Cross)+" đánh chéo • Giữ chọn, thả để đánh\n\nHai cú đầu phải nảy. Volley và đà volley vào Kitchen là lỗi.\n"+
                K(GameAction.Timeout)+" time-out • Esc cài đặt • "+K(GameAction.Help)+" hướng dẫn\nCảm ứng: joystick và các nút kỹ thuật; chỉnh bố trí trong Cài đặt.";
            if(text&&mobile)text.text=TouchShotGesture.HelpText;
            if(layout)layout.RefreshKeys();
            else{var helpButton=transform.Find("Help")?.GetComponentInChildren<TMP_Text>();if(helpButton)helpButton.text=K(GameAction.Help)+" / Hướng dẫn";var timeoutButton=transform.Find("Timeout")?.GetComponentInChildren<TMP_Text>();if(timeoutButton)timeoutButton.text=K(GameAction.Timeout)+" / Time-out";}
        }
        void OnDestroy(){GameSettings.Changed-=RefreshBindings;}
    }
}

