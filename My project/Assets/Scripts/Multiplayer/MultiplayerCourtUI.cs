using TMPro;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using PickleVerse.LocalMatch;
using PickleVerse.Settings;
namespace PickleVerse.Multiplayer
{
    public sealed class MultiplayerCourtUI : MonoBehaviour, ISettingsHost, ISettingsLeaveConfirmation, IMatchTouchShotReceiver
    {
        public static MultiplayerCourtUI Instance { get; private set; }
        public TMP_Text score, status, server;
        public TMP_Text selection, timing, modeLabel, modalTitle, modalBody, resultSummary, introCaption;
        public GameObject shotPanel, help, touchControls, modal, resultActions, intro;
        public MatchAnnouncements announcements;
        public MultiplayerCinematic cinematic;
        public bool EndAnnouncementReceived { get; private set; }
        public bool EndAnnouncementFinished { get; private set; }
        public UnityEngine.UI.Button continueButton, restartButton, modeButton;
        public UnityEngine.UI.Button leave, returnToRoom;
        public MultiplayerResultScreen resultScreen;
        public MatchScoreboard broadcast;
        public MatchHudLayout layout;
        TMP_Text leaveLabel, returnLabel;
        NetworkPlayer localOwner;
        NetworkMatch displayedMatch;
        int displayedTeam = -1, serviceTeam = -1, serviceIndex = -1, serviceNumber = -1, resultA = -1, resultB = -1;
        string serviceText = "", matchType = "Multiplayer", shownKeys, shownSelection, matchStatus = "";
        Unity.Collections.FixedString512Bytes statusValue;
        bool modalVisible, modalTimeout;
        int modalSecond = -1;
        string modalReason;
        struct Notice { public string text; public float duration; }
        readonly System.Collections.Generic.Queue<Notice> notices = new System.Collections.Generic.Queue<Notice>();
        string announcement;
        float announcementUntil;
        bool localInputPaused;
        public int TouchShotMask { get; set; }
        public bool TouchShotsEnabled => !InputBlocked && localOwner && localOwner.TouchShotsEnabled;
        public bool TouchServing => localOwner && localOwner.TouchServing;
        public void CommitTouchShot(int mask)
        {
            if (TouchShotsEnabled)
                localOwner.CommitTouchShot(mask);
        }
        public Vector2 TouchMovement { get; set; }
        public bool TouchRun { get; set; }
        public bool InputBlocked => PickleVerse.Settings.SceneLoading.IsBlocking || cinematic && cinematic.OwnsPose || localInputPaused || help && help.activeSelf || resultScreen && resultScreen.IsVisible;
        public bool IsGameplay => true;
        public bool IsPaused => localInputPaused;
        public bool CanSignOut => false;
        public string LeaveConfirmation => "Rời trận multiplayer và về đại sảnh? Nếu trận chưa kết thúc, đội của bạn sẽ bị xử thua; host rời sẽ kết thúc phiên.";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Instance = null;
        }
        void Awake()
        {
            Instance = this;
            resultScreen = GetComponent<MultiplayerResultScreen>();
            if (!resultScreen)
                resultScreen = gameObject.AddComponent<MultiplayerResultScreen>();
            resultScreen.Configure(this);
        }
        public void ShowAnnouncement(string title, string detail, float duration, AnnouncementTone tone = AnnouncementTone.Neutral)
        {
            bool ending = title == "KẾT THÚC TRẬN ĐẤU" || title == "GAME, MATCH!";
            if (ending && EndAnnouncementReceived)
                return;
            if (ending)
                EndAnnouncementReceived = true;
            if (announcements)
                announcements.Enqueue(title, detail, tone, duration, finished: ending ? () => EndAnnouncementFinished = true : (Action)null);
            else notices.Enqueue(new Notice { text = title + "\n" + detail, duration = duration });
        }
        public void EnsureEndAnnouncement(string detail)
        {
            if (!EndAnnouncementReceived)
                ShowAnnouncement("KẾT THÚC TRẬN ĐẤU", detail, 2.1f, AnnouncementTone.Success);
        }
        void Start()
        {
            GameSettings.Changed += RefreshBindings;
            RefreshBindings();
            foreach (var control in GetComponentsInChildren<TouchControl>(true))
            {
                control.match = null;
                control.Input = this;
            }
            if (touchControls)
            {
                MobileTouchPad.Install(touchControls, this);
                touchControls.SetActive(Application.isMobilePlatform);
            }
            if (help)
                help.SetActive(false);
            if (modal)
                modal.SetActive(false);
            if (resultActions)
                resultActions.SetActive(false);
            if (leave)
            {
                leaveLabel = leave.GetComponentInChildren<TMP_Text>(true);
                leave.onClick.AddListener(LeaveClicked);
            }
            if (returnToRoom)
            {
                returnLabel = returnToRoom.GetComponentInChildren<TMP_Text>(true);
                returnToRoom.onClick.AddListener(ReturnClicked);
            }
        }
        void LeaveClicked()
        {
            var app = MultiplayerApp.Instance;
            if (!app)
            {
                SceneManager.LoadScene("PlayMultiplayer");
                return;
            }
            _ = app.ExecuteAsync(() => app.Searching ? app.CancelSearchAsync() : app.LeaveGameAsync());
        }
        void ReturnClicked()
        {
            var app = MultiplayerApp.Instance;
            if (!app)
            {
                SceneManager.LoadScene("PlayMultiplayer");
                return;
            }
            var match = NetworkMatch.Instance;
            if (match && match.IsSpawned && match.Finished && resultScreen && !resultScreen.IsVisible)
            {
                resultScreen.Open();
                return;
            }
            if (match && match.IsSpawned && match.BetweenGames && !match.Finished)
            {
                if (match.IsServer && !app.Busy)
                    match.RequestNextGame();
                return;
            }
            _ = app.ExecuteAsync(() => app.ReturnToRoomAsync("Trận đã kết thúc."));
        }
        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            GameSettings.Changed -= RefreshBindings;
            ClearTouch();
            if (leave)
                leave.onClick.RemoveListener(LeaveClicked);
            if (returnToRoom)
                returnToRoom.onClick.RemoveListener(ReturnClicked);
        }
        void Update()
        {
            bool readKeys = !PickleVerse.Settings.SceneLoading.IsBlocking && Keyboard.current != null && (Application.isFocused || InputSystem.settings.backgroundBehavior == InputSettings.BackgroundBehavior.IgnoreFocus);
            if (readKeys && (!resultScreen || !resultScreen.IsVisible) && !SettingsOverlay.IsOpen && !SettingsOverlay.Loading)
            {
                if (GameControls.Pressed(GameAction.Settings))
                    OpenSettings();
                else if (GameControls.Pressed(GameAction.Help))
                    ToggleHelp();
            }
            if (Time.unscaledTime >= announcementUntil && notices.Count > 0)
            {
                var notice = notices.Dequeue();
                announcement = notice.text;
                announcementUntil = Time.unscaledTime + notice.duration;
            }
            var app = MultiplayerApp.Instance;
            var match = NetworkMatch.Instance;
            bool hasMatch = match && match.IsSpawned;
            if (hasMatch && match.Finished && !EndAnnouncementReceived)
                EnsureEndAnnouncement(match.Status.ToString());
            if (!announcements && EndAnnouncementReceived && Time.unscaledTime >= announcementUntil && notices.Count == 0)
                EndAnnouncementFinished = true;
            if (cinematic && cinematic.OwnsPose)
            {
                if (intro)
                    intro.SetActive(false);
                if (modal)
                    modal.SetActive(false);
                if (shotPanel)
                    shotPanel.SetActive(false);
                return;
            }

            bool nextGame = hasMatch && match.BetweenGames && !match.Finished && match.IsServer;
            if (!localOwner || !localOwner.IsSpawned)
            {
                localOwner = null;
                foreach (var player in NetworkPlayer.All)
                    if (player.IsLocalHuman)
                    {
                        localOwner = player;
                        break;
                    }
            }
            if (hasMatch && (displayedMatch != match || displayedTeam != (localOwner ? localOwner.Team : 0)))
            {
                displayedMatch = match;
                displayedTeam = localOwner ? localOwner.Team : 0;
                matchType = MatchType(match);
                serviceTeam = serviceIndex = serviceNumber = -1;
                statusValue = default;
                matchStatus = "";
                if (broadcast)
                    broadcast.SetTeams(BroadcastName(match, displayedTeam, localOwner), BroadcastName(match, 1 - displayedTeam, null), matchType);
            }
            if (hasMatch && !statusValue.Equals(match.Status))
            {
                statusValue = match.Status;
                matchStatus = statusValue.ToString();
            }
            // NGO can despawn the match before the court UI is unloaded.
            bool ended = hasMatch ? match.Finished : !app || (!app.Searching && app.State != FlowState.Reconnecting);
            if (broadcast && hasMatch)
            {
                int game = Mathf.Clamp(match.GamesA + match.GamesB + (match.BetweenGames || match.Finished ? 0 : 1), 1, 3);
                bool first = displayedTeam == 0;
                broadcast.SetScores(first ? match.ScoreA : match.ScoreB, first ? match.ScoreB : match.ScoreA, first ? match.GamesA : match.GamesB, first ? match.GamesB : match.GamesA, game, match.ServerTeam == displayedTeam ? 0 : 1);
                if (serviceTeam != match.ServerTeam || serviceIndex != match.ServerIndex || serviceNumber != match.ServerNumber)
                {
                    serviceTeam = match.ServerTeam;
                    serviceIndex = match.ServerIndex;
                    serviceNumber = match.ServerNumber;
                    serviceText = (serviceTeam == displayedTeam ? "ĐỘI BẠN" : "ĐỐI THỦ") + " · NGƯỜI " + (serviceIndex + 1) + " · LƯỢT " + serviceNumber;
                }
                broadcast.SetClock(match.TimedOut ? CourtClockMode.Timeout : match.Finished ? CourtClockMode.Finished : match.BetweenGames ? CourtClockMode.Break : match.WaitingServe ? CourtClockMode.Serve : match.Ball.live ? CourtClockMode.Rally : CourtClockMode.Waiting, match.TimedOut ? match.TimeoutRemaining : 10 - match.ServeElapsed, match.Bounces > 0, match.WaitingServe ? serviceText : "");
            }
            else if (score && hasMatch)
            {
                int game = Mathf.Clamp(match.GamesA + match.GamesB + (match.BetweenGames || match.Finished ? 0 : 1), 1, 3);
                score.text = $"<color=#83E6CD>{TeamName(match, 0)}</color>  {match.ScoreA}   :   {match.ScoreB}  <color=#F9BC7A>{TeamName(match, 1)}</color>\n<size=17>GAME {game}    •    Số game thắng {match.GamesA}–{match.GamesB}    •    {MatchType(match)}</size>";
            }
            if (status)
                status.text = !app ? "Chưa có phiên thi đấu." : app.Searching ? app.Notice + "  " + app.Elapsed :
                                !hasMatch || app.State == FlowState.Reconnecting ? app.Notice : Time.unscaledTime < announcementUntil ? announcement : matchStatus;
            if (status && announcements)
                status.transform.parent.gameObject.SetActive(!hasMatch || match.Paused || !(announcements.Busy || match.BetweenPoints || match.BetweenGames || match.Finished));
            if (leaveLabel)
                leaveLabel.text = app && app.Searching ? "Hủy" : "Rời trận";
            if (server && !broadcast)
                server.text = !hasMatch ? "" : match.TimedOut ? $"TIME-OUT  {match.TimeoutRemaining:0}s" : match.WaitingServe ? $"GIAO BÓNG  {Mathf.Max(0, 10 - match.ServeElapsed):0.0}s\n<size=16>Đội {(match.ServerTeam == 0 ? "A" : "B")} • Người {match.ServerIndex + 1} • Lượt {match.ServerNumber}</size>" : match.Ball.live ? match.Bounces == 0 ? "BÓNG CHƯA NẢY" : "BÓNG ĐÃ NẢY" : "";
            if (returnLabel)
                returnLabel.text = nextGame ? "Ván tiếp" : "Về phòng";
            bool results = hasMatch && match.Finished && (!announcements || !announcements.Busy) && !SettingsOverlay.IsOpen;
            if (resultActions)
                resultActions.SetActive(results && !resultScreen);
            if (resultSummary && results && (resultA != match.GamesA || resultB != match.GamesB))
            {
                resultA = match.GamesA;
                resultB = match.GamesB;
                resultSummary.text = $"SỐ GAME THẮNG  {resultA} - {resultB}";
            }
            if (returnToRoom)
            {
                returnToRoom.gameObject.SetActive(nextGame || ended && !resultActions);
                returnToRoom.interactable = !app || !app.Busy;
            }
            if (leave)
                leave.gameObject.SetActive(!ended && !nextGame);
            var owner = localOwner;
            bool choosing = owner && !string.IsNullOrEmpty(owner.SelectionKeys), swinging = owner && owner.Actor && owner.Actor.Swinging && !string.IsNullOrEmpty(owner.ActiveShotKeys);
            string keys = choosing ? owner.SelectionKeys : swinging ? owner.ActiveShotKeys : "", description = choosing ? owner.Selection : swinging ? owner.Actor.Intent.ToString() : "";
            if (selection && (keys != shownKeys || description != shownSelection))
            {
                shownKeys = keys;
                shownSelection = description;
                selection.text = keys.Length > 0 ? "<b>" + keys + "</b>\n<size=14>" + description + "</size>" : "";
            }
            if (shotPanel)
                shotPanel.SetActive(!(touchControls && touchControls.activeInHierarchy) && !InputBlocked && !SettingsOverlay.IsOpen && hasMatch && !match.Paused && !match.Finished && (choosing || swinging));
            if (timing)
                timing.gameObject.SetActive(false);
            if (modeLabel && !broadcast)
                modeLabel.text = hasMatch ? matchType : "Multiplayer";
            bool showModal = hasMatch && !SettingsOverlay.IsOpen && (match.TimedOut || match.Paused);
            if (modal)
                modal.SetActive(showModal);
            int second = hasMatch ? Mathf.CeilToInt(match.TimeoutRemaining) : 0;
            bool refreshModal = showModal && (!modalVisible || modalTimeout != match.TimedOut || modalSecond != second || modalReason != matchStatus);
            modalVisible = showModal;
            if (refreshModal)
            {
                modalTimeout = match.TimedOut;
                modalSecond = second;
                modalReason = matchStatus;
                ((RectTransform)modal.transform).sizeDelta = new Vector2(760, 370);
                if (modalBody)
                {
                    modalBody.rectTransform.anchoredPosition = new Vector2(0, 6);
                    modalBody.rectTransform.sizeDelta = new Vector2(710, 235);
                    modalBody.fontSize = 19;
                }
                if (continueButton)
                    ((RectTransform)continueButton.transform).anchoredPosition = new Vector2(-245, 20);
                if (restartButton)
                    ((RectTransform)restartButton.transform).anchoredPosition = new Vector2(0, 20);
                if (modalTitle)
                    modalTitle.text = match.TimedOut ? "TIME-OUT" : "CHỜ KẾT NỐI LẠI";
                if (modalBody)
                    modalBody.text = match.TimedOut ? $"Đội {(match.TimeoutTeam == 0 ? "A" : "B")} • Còn {match.TimeoutRemaining:0} giây\nĐiểm {match.ScoreA}–{match.ScoreB}\nĐội xin hội ý bấm Enter / Tiếp tục để kết thúc sớm." : match.Status.ToString();
                if (continueButton)
                {
                    continueButton.gameObject.SetActive(match.TimedOut);
                    continueButton.interactable = owner && owner.Team == match.TimeoutTeam;
                }
                if (restartButton)
                    restartButton.gameObject.SetActive(true);
            }
            if (intro)
                intro.SetActive(!hasMatch || !match.Started);
            if (introCaption)
                introCaption.text = hasMatch ? "PICKLEVERSE / " + MatchType(match) : "ĐANG KẾT NỐI VÀO SÂN";
        }
        static string BroadcastName(NetworkMatch match, int team, NetworkPlayer local)
        {
            var members = match.Roster?.members.Where(m => m.team == team).ToArray();
            if (members == null || members.Length == 0)
                return team == 0 ? "ĐỘI A" : "ĐỘI B";
            return string.Join("\n", members.Select(m => (local && m.id == local.Identity.ToString() ? "BẠN · " : "") + (m.name ?? "Người chơi")));
        }
        static string TeamName(NetworkMatch match, int team)
        {
            var members = match.Roster?.members.Where(m => m.team == team).ToArray();
            if (members == null || members.Length != 1)
                return team == 0 ? "ĐỘI A" : "ĐỘI B";
            string name = members[0].name ?? "";
            if (name.Length > 14)
                name = name.Substring(0, 11) + "...";
            return name.Replace("<", "‹").Replace(">", "›");
        }
        static string MatchType(NetworkMatch match)
        {
            var roster = match.Roster;
            if (roster == null)
                return "Multiplayer";
            int a = roster.members.Count(m => m.team == 0), b = roster.members.Count(m => m.team == 1);
            return roster.custom ? $"PHÒNG RIÊNG · {a}V{b}" : roster.mode == PlayMode.Doubles ? "TRỰC TUYẾN · 2V2" : "TRỰC TUYẾN · 1V1";
        }
        public void ClearTouch()
        {
            TouchShotMask = 0;
            TouchMovement = Vector2.zero;
            TouchRun = false;
        }
        public void SetPaused(bool paused)
        {
            localInputPaused = paused;
            ClearTouch();
        }
        public void OpenSettings()
        {
            ClearTouch();
            SettingsOverlay.Open(this);
        }
        public void ToggleHelp()
        {
            if (!help)
                return;
            ClearTouch();
            help.SetActive(!help.activeSelf);
            if (help.activeSelf)
                help.transform.SetAsLastSibling();
        }
        public void Continue()
        {
            NetworkPlayer.All.FirstOrDefault(p => p.IsLocalHuman)?.ResumeTimeout();
        }
        public void ReturnToRoom()
        {
            ReturnClicked();
        }
        public void SkipIntro()
        {
            if (intro)
                intro.SetActive(false);
        }
        public void ChooseMode()
        {
            var app = MultiplayerApp.Instance;
            if (!app)
            {
                SceneManager.LoadScene("PlayMultiplayer");
                return;
            }
            _ = app.ExecuteAsync(async () =>
            {
                await app.LeaveGameAsync();
                await app.LeaveRoomAsync();
                await app.SelectModeAsync(PlayMode.Singles);
            });
        }
        public void LeaveToMenu()
        {
            var app = MultiplayerApp.Instance;
            if (!app)
                return;
            _ = app.ExecuteAsync(async () =>
            {
                if (SettingsOverlay.IsOpen)
                {
                    SettingsOverlay.Close(true);
                    while (SettingsOverlay.Loading)
                        await Task.Yield();
                }
                await app.LeaveGameAsync();
                await app.LeaveRoomAsync();
                SceneManager.LoadScene("Menu");
            });
        }
        public Task SignOutAsync() => Task.CompletedTask;
        void RefreshBindings()
        {
            string K(GameAction action) => GameSettings.KeyName(GameSettings.Current.KeyFor(action));
            var text = help ? help.transform.Find("HelpText")?.GetComponent<TMP_Text>() : null;
            if (text)
                text.text = "<size=28><color=#83E6CD>ĐIỀU KHIỂN VÀ LUẬT</color></size>\n\n" +
                                K(GameAction.Forward) + " / " + K(GameAction.Backward) + " / " + K(GameAction.Left) + " / " + K(GameAction.Right) + " di chuyển • " + K(GameAction.Run) + " chạy\n" +
                                K(GameAction.Drive) + " Đánh bạt/giao bóng | " + K(GameAction.Dink) + " Dink | " + K(GameAction.Volley) + " Volley\n" +
                                K(GameAction.Drive) + "+" + K(GameAction.Dink) + " Drop | " + K(GameAction.Drive) + "+" + K(GameAction.Volley) + " Smash | " + K(GameAction.Dink) + "+" + K(GameAction.Volley) + " Reset\n" +
                                K(GameAction.Power) + " tăng lực • " + K(GameAction.Cross) + " đánh chéo • Giữ chọn, thả để đánh\n\nHai cú đầu phải nảy. Vô lê và đà vô lê vào vùng cấm vô lê là lỗi.\n11 điểm, cách biệt 2 • Chỉ đội giao ghi điểm • Thắng 2/3 ván\n" +
"Esc cài đặt • " + K(GameAction.Help) + " hướng dẫn\nMở cài đặt/hướng dẫn chỉ khóa thao tác của bạn; trận trực tuyến tiếp tục.";
            if (text && Application.isMobilePlatform)
                text.text = TouchShotGesture.HelpText + "\nMở cài đặt/hướng dẫn chỉ khóa thao tác của bạn; trận trực tuyến tiếp tục.";
            if (layout)
                layout.RefreshKeys();
            else
            {
                var helpButton = transform.Find("Help")?.GetComponentInChildren<TMP_Text>();
                if (helpButton)
                    helpButton.text = K(GameAction.Help) + " / Hướng dẫn";
            }
        }
    }
}
