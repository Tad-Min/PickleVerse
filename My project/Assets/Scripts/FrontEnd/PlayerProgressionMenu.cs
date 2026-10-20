using System;
using System.Globalization;
using System.Linq;
using PickleVerse.Progression;
using PickleVerse.Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PickleVerse.FrontEnd
{
    // Scene bindings and shell are authored by ProgressionMenuSetup; rows are populated from the current player.
    public sealed partial class PlayerProgressionMenu : MonoBehaviour
    {
        public TMP_FontAsset font;
        public Sprite rounded, avatar;
        public Button profileButton, missionsButton, careerButton, editButton, progressionButton;
        public TMP_Text menuName, menuLevel, wallet;
        public RectTransform modal, modalSafe, window, left, right, content, summary;
        public TMP_Text title, subtitle, identityName, identityId, identityRank, levelText, xpText, summaryText, countdown, feedback;
        public Image xpFill, summaryFill;
        public Button statsTab, achievementTab, dailyTab, weeklyTab, bonusButton;
        public TMP_InputField editId, editName, editElo, editTokenpad, editEliteToken;
        public RectTransform editForm;
        public ScrollRect scroll;
        enum View { Statistics, Achievements, Daily, Weekly, Career, Edit, Training }
        View view;
        GameObject priorSelection;
        CanvasGroup menuInteraction;
        bool priorMenuInteraction=true;
        double nextTick;
        bool buildingShell;
        static readonly Color Ink=new Color(.025f,.065f,.095f), Surface=new Color(.045f,.115f,.15f), Card=new Color(.075f,.16f,.20f), Mint=new Color(.45f,.86f,.77f), Lime=new Color(.79f,.96f,.45f), White=new Color(.94f,.97f,.96f), Muted=new Color(.63f,.75f,.79f);

        void Awake()
        {
            if(!modal) BuildShell();
            else ConfigureEditForm();
            ConfigureTrainingUI();
            if(!progressionButton)progressionButton=GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name=="BtnProgression");
            if(progressionButton){progressionButton.interactable=true;progressionButton.onClick.AddListener(OpenTrainingProgress);}
            var menuRoot=transform.Find("HomeSafeArea");
            if(menuRoot)
            {
                menuInteraction=menuRoot.GetComponent<CanvasGroup>();
                if(!menuInteraction) menuInteraction=menuRoot.gameObject.AddComponent<CanvasGroup>();
            }
            modal.Find("SafeArea/Window/Close").GetComponent<Button>().onClick.AddListener(Close);
            statsTab.onClick.AddListener(()=>Open(View.Statistics)); achievementTab.onClick.AddListener(()=>Open(View.Achievements));
            dailyTab.onClick.AddListener(()=>Open(View.Daily)); weeklyTab.onClick.AddListener(()=>Open(View.Weekly));
            editForm.Find("SaveName").GetComponent<Button>().onClick.AddListener(()=>SaveEditor(false));
            editForm.Find("Switch").GetComponent<Button>().onClick.AddListener(()=>SaveEditor(true));
            profileButton.onClick.AddListener(OpenProfile);
            missionsButton.onClick.AddListener(OpenMissions);
            careerButton.onClick.AddListener(OpenCareer);
            editButton.onClick.AddListener(OpenEditor);
            editButton.gameObject.SetActive(ProgressionRuntime.DeveloperControls);
            ProgressionRuntime.Changed+=Refresh;
            Close(); Refresh();
        }
        void OnDestroy() { ProgressionRuntime.Changed-=Refresh;PickleVerse.Settings.FrontEndModalInput.Release(this); }
        void Update()
        {
            if(!modal.gameObject.activeSelf) return;
            ApplySafeArea();
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true) Close();
            if(Time.realtimeSinceStartupAsDouble>=nextTick)
            {
                nextTick=Time.realtimeSinceStartupAsDouble+1;
                UpdateCountdown();
                if(view==View.Statistics) RefreshStatisticsTime();
            }
        }
        public void OpenProfile() { Open(View.Statistics); }
        public void OpenMissions() { Open(View.Daily); }
        public void OpenTrainingProgress() { Open(View.Training); }
        public void OpenCareer() { Open(View.Career); }
        public void OpenEditor() { if(ProgressionRuntime.DeveloperControls) Open(View.Edit); }
        async void Open(View next)
        {
            GetComponent<OfflineSocialMenu>()?.Close();
            if(!PickleVerse.Settings.FrontEndModalInput.TryAcquire(this))return;
            if(!modal.gameObject.activeSelf)
            {
                priorSelection=EventSystem.current?.currentSelectedGameObject;
                if(menuInteraction) { priorMenuInteraction=menuInteraction.interactable; menuInteraction.interactable=false; }
            }
            view=next; modal.gameObject.SetActive(true); modal.SetAsLastSibling(); ApplySafeArea(); Refresh();
            EventSystem.current?.SetSelectedGameObject(modal.Find("SafeArea/Window/Close").gameObject);
            try
            {
                await ProgressionRuntime.Instance.ReloadAsync();
                if(next==View.Career&&PickleVerse.Replays.ReplayStorage.Repository is PickleVerse.Replays.IOnlineReplayRepository replays)await replays.RefreshAsync();
                if(this&&modal.gameObject.activeSelf)Refresh();
            }
            catch(Exception error){if(this&&modal.gameObject.activeSelf)feedback.text="Không tải được dữ liệu mới: "+error.Message;}
        }
        public void Close()
        {
            if(!modal) return;
            modal.gameObject.SetActive(false);
            PickleVerse.Settings.FrontEndModalInput.Release(this);
            if(menuInteraction) menuInteraction.interactable=priorMenuInteraction;
            if(EventSystem.current && priorSelection && priorSelection.activeInHierarchy) EventSystem.current.SetSelectedGameObject(priorSelection);
        }
        void ApplySafeArea()
        {
            var area=Screen.safeArea;
            if(Screen.width<=0 || Screen.height<=0) return;
            modalSafe.anchorMin=new Vector2(area.xMin/Screen.width,area.yMin/Screen.height);
            modalSafe.anchorMax=new Vector2(area.xMax/Screen.width,area.yMax/Screen.height);
            modalSafe.offsetMin=modalSafe.offsetMax=Vector2.zero;
        }
        public void Refresh()
        {
            if(!Application.isPlaying || !ProgressionRuntime.Exists) return;
            var player=ProgressionRuntime.Instance.Player; var engine=ProgressionRuntime.Instance.Engine;
            menuName.text=player.playerName; menuLevel.text=$"LV {player.level}  ·  {EloPolicy.Rank(player.elo)}  {player.elo}";
            wallet.text=$"<color=#C9F573>TOKENPAD</color>  {player.coins:N0}    <color=#73DCC4>ELITE TOKEN</color>  {player.gems:N0}";
            if(!modal.gameObject.activeSelf) return;
            Render(player,engine);
        }
        void Render(PlayerProfile p,ProgressionEngine engine)
        {
            bool profile=view==View.Statistics || view==View.Achievements, missions=view==View.Daily || view==View.Weekly, training=view==View.Training;
            title.text=training?"TIẾN TRÌNH LUYỆN TẬP":profile?"HỒ SƠ":missions?"NHIỆM VỤ":view==View.Career?"LỊCH SỬ THI ĐẤU":"CHỈNH PLAYER";
            subtitle.text=training?"THÀNH THẠO KỸ THUẬT • LỖI THƯỜNG GẶP • TIẾP TỤC LUYỆN":profile?"THỐNG KÊ VÀ TIẾN TRÌNH":missions?"TÍCH LŨY TIẾN TRÌNH • NHẬN PHẦN THƯỞNG":view==View.Career?"20 TRẬN GẦN NHẤT • GIỜ VIỆT NAM":"EDITOR / DEVELOPMENT BUILD";
            identityName.text=p.playerName; identityId.text="ID\n"+p.id;
            identityRank.text=$"<color=#C9F573>{EloPolicy.Rank(p.elo).ToUpperInvariant()}</color>\n{p.elo:N0} ELO";
            statsTab.gameObject.SetActive(profile); achievementTab.gameObject.SetActive(profile);
            dailyTab.gameObject.SetActive(missions||training); weeklyTab.gameObject.SetActive(missions||training);trainingTab.gameObject.SetActive(missions||training);
            Tab(statsTab,view==View.Statistics); Tab(achievementTab,view==View.Achievements); Tab(dailyTab,view==View.Daily); Tab(weeklyTab,view==View.Weekly);
            summary.gameObject.SetActive(view!=View.Edit); editForm.gameObject.SetActive(view==View.Edit);
            scroll.gameObject.SetActive(view!=View.Edit);
            levelText.gameObject.SetActive(profile || view==View.Career); xpText.gameObject.SetActive(profile || view==View.Career);
            xpFill.transform.parent.gameObject.SetActive(profile || view==View.Career);
            countdown.gameObject.SetActive(missions); summaryFill.transform.parent.gameObject.SetActive(missions||training); bonusButton.gameObject.SetActive(missions||training);
            summaryText.gameObject.SetActive(missions||training);
            levelText.text=$"LEVEL {p.level}"; xpText.text=$"{p.levelXp:N0} / {p.NextLevelXp:N0} XP";
            Fill(xpFill,p.levelXp/(float)p.NextLevelXp);
            ClearRows();
            if(training) BuildTrainingProgress(p);
            else if(view==View.Statistics) BuildStatistics(p);
            else if(view==View.Achievements || missions)
            {
                if(view==View.Daily&&p.dailyStreak!=null)
                {
                    var streak=p.dailyStreak;bool played=streak.days.Contains(ProgressionEngine.DayKey(DateTime.UtcNow));
                    AddMessage("DAILY STREAK · "+streak.current+" ngày",(played?"Đã chơi hôm nay":"Chơi ít nhất một trận để giữ chuỗi")+"\nDài nhất: "+streak.best+" ngày · Đổi ngày lúc 07:00 giờ Việt Nam");
                }
                var scope=view==View.Achievements?ProgressScope.Achievement:view==View.Daily?ProgressScope.Daily:ProgressScope.Weekly;
                foreach(var def in engine.Config.definitions.Where(d=>d.scope==scope)) AddProgressRow(p,engine,def);
                if(missions)
                {
                    int count=engine.Count(scope), completed=engine.Completed(p,scope);
                    summaryText.text=$"{(scope==ProgressScope.Daily?"NHIỆM VỤ NGÀY":"NHIỆM VỤ TUẦN")}  ·  {completed}/{count} hoàn thành\n"+(scope==ProgressScope.Daily?engine.Config.dailyBonus:engine.Config.weeklyBonus).ToString();
                    Fill(summaryFill,count==0?0:completed/(float)count);
                    bool claimed=ProgressionEngine.Period(p,scope).bonusClaimed;
                    Label(bonusButton).text=claimed?"ĐÃ NHẬN":completed==count?"NHẬN THƯỞNG TỔNG":"THƯỞNG TỔNG";
                    bonusButton.interactable=!claimed && completed==count;
                    bonusButton.onClick.RemoveAllListeners(); bonusButton.onClick.AddListener(()=>Claim(scope==ProgressScope.Daily?"DailyBonus":"WeeklyBonus"));
                }
            }
            else if(view==View.Career)
            {
                if(p.history.Count==0) AddMessage("Chưa có trận đấu", "Hoàn tất một trận để lưu đối thủ và tỷ số tại đây.");
                foreach(var result in p.history) AddHistory(result);
            }
            else
            {
                editId.SetTextWithoutNotify(p.id); editName.SetTextWithoutNotify(p.playerName);
                editElo.SetTextWithoutNotify(p.elo.ToString(CultureInfo.InvariantCulture));
                editTokenpad.SetTextWithoutNotify(p.coins.ToString(CultureInfo.InvariantCulture));
                editEliteToken.SetTextWithoutNotify(p.gems.ToString(CultureInfo.InvariantCulture));
                feedback.text="Lưu player đang dùng. Chuyển ID để tải hoặc tạo player khác.";
            }
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(content); scroll.verticalNormalizedPosition=1;
            UpdateCountdown();
        }
        TMP_Text activeTime;
        void BuildStatistics(PlayerProfile p)
        {
            activeTime=Stat("Thời gian hoạt động",ProgressionRuntime.FormatTime(p.activeSeconds));
            Stat("Tổng kinh nghiệm",p.totalXp.ToString("N0")+" XP"); Stat("Trận thắng",p.wins.ToString("N0"));
            Stat("Trận thua",p.losses.ToString("N0")); Stat("Tỷ lệ thắng",p.WinRate.ToString("0.0")+"%");
            Stat("Chuỗi thắng dài nhất",p.lifetime.bestStreak+" trận");
            Stat("Chuỗi ngày chơi dài nhất",(p.dailyStreak?.best??0)+" ngày");
        }
        void RefreshStatisticsTime() { if(activeTime && ProgressionRuntime.Exists) activeTime.text=ProgressionRuntime.FormatTime(ProgressionRuntime.Instance.Player.activeSeconds); }
        TMP_Text Stat(string key,string value)
        {
            var row=Row("Stat",62); Text("Label",row,key,19,Muted,new Vector2(0,0),new Vector2(.65f,1),new Vector2(18,0),new Vector2(-4,0));
            return Text("Value",row,value,21,White,new Vector2(.55f,0),Vector2.one,new Vector2(0,0),new Vector2(-18,0),TextAlignmentOptions.MidlineRight);
        }
        void AddProgressRow(PlayerProfile p,ProgressionEngine engine,ProgressDefinition def)
        {
            var row=Row(def.id,130); long progress=engine.Progress(p,def); bool claimed=engine.Claimed(p,def), done=progress>=def.target;
            Text("Title",row,def.title,19,White,new Vector2(0,.57f),new Vector2(.66f,1),new Vector2(18,0),new Vector2(-12,-8));
            var fill=Bar("Progress",row,new Vector2(.02f,.17f),new Vector2(.65f,.42f)); Fill(fill,progress/(float)def.target);
            Text("Count",(RectTransform)fill.transform.parent,$"{Math.Min(progress,def.target):N0} / {def.target:N0}",16,White,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);
            Text("Reward",row,def.reward.ToString(),14,Muted,new Vector2(.68f,.53f),new Vector2(.98f,.98f),Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);
            var claim=MakeButton("Claim",row,claimed?"ĐÃ NHẬN":done?"NHẬN THƯỞNG":"CHƯA ĐẠT",()=>Claim(def.id),new Vector2(.69f,.15f),new Vector2(.97f,.51f),done&&!claimed?Lime:Surface,done&&!claimed?Ink:Muted,15);
            claim.interactable=done&&!claimed;
        }
        bool claiming;
        async void Claim(string id)
        {
            if(claiming)return;
            claiming=true;
            float position=scroll.verticalNormalizedPosition;
            try { await ProgressionRuntime.Instance.ClaimAsync(id); }
            catch(Exception e) { if(feedback)feedback.text="Nhận thưởng: "+e.Message; }
            finally { claiming=false; }
            if(!this)return;
            Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition=position;
        }
        void UpdateCountdown()
        {
            if(view!=View.Daily && view!=View.Weekly || !countdown) return;
            var utc=DateTime.UtcNow; var remain=ProgressionEngine.NextReset(view==View.Daily?ProgressScope.Daily:ProgressScope.Weekly,utc)-utc;
            countdown.text=$"RESET SAU  {(int)remain.TotalHours:00}:{remain.Minutes:00}:{remain.Seconds:00}";
        }
        void AddHistory(MatchResult result)
        {
            var row=Row("Match "+result.id,112);
            string opponents=string.Join(" & ",result.opponents.Select(o=>o.name));
            Text("Opponent",row,opponents,20,White,new Vector2(0,.55f),new Vector2(.68f,1),new Vector2(18,0),new Vector2(-12,-6));
            string time=new DateTime(result.endedUtcTicks,DateTimeKind.Utc).AddHours(7).ToString("dd/MM/yyyy  HH:mm");
            Text("Time",row,result.KindLabel+"  ·  "+time,14,Muted,new Vector2(0,.29f),new Vector2(.73f,.57f),new Vector2(18,0),Vector2.zero);
            Text("Outcome",row,$"{result.OutcomeLabel}\n{result.gamesWon} : {result.gamesLost} game  ·  {(result.eloDelta>=0?"+":"")}{result.eloDelta} Elo",17,result.outcome==MatchOutcome.Won?Lime:Mint,new Vector2(.71f,.26f),new Vector2(.98f,.97f),Vector2.zero,Vector2.zero,TextAlignmentOptions.MidlineRight);
            var detail=Text("Detail",row,"Bấm để xem tỷ số từng game",13,Muted,new Vector2(0,0),new Vector2(.98f,.28f),new Vector2(18,4),new Vector2(-12,0));
            var button=row.gameObject.AddComponent<Button>(); button.targetGraphic=row.GetComponent<Image>(); StyleButton(button,Card); bool expanded=false;
            var replayLabel=PickleVerse.Replays.ReplayStorage.Availability(result.playerId,result.id);
            var replayRect=new GameObject("WatchReplay",typeof(RectTransform),typeof(Image),typeof(Button)).GetComponent<RectTransform>();replayRect.SetParent(row,false);
            replayRect.anchorMin=new Vector2(.53f,0);replayRect.anchorMax=new Vector2(.98f,.27f);replayRect.offsetMin=replayRect.offsetMax=Vector2.zero;
            replayRect.GetComponent<Image>().color=Card;
            var replayButton=replayRect.GetComponent<Button>();StyleButton(replayButton,Card);replayButton.interactable=replayLabel.StartsWith("Xem replay",StringComparison.Ordinal);
            Text("Label",replayRect,replayLabel,13,Mint,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);
            replayButton.onClick.AddListener(()=>{float prior=scroll.verticalNormalizedPosition;Close();PickleVerse.Replays.ReplayLaunch.Select(result.playerId,result.id,()=>{PickleVerse.Shop.LobbyNavigation.ReturnToLobby();StartCoroutine(RestoreHistory(prior));});PickleVerse.Shop.LobbyNavigation.OpenReplay();});
            button.onClick.AddListener(()=> {
                expanded=!expanded; row.GetComponent<LayoutElement>().preferredHeight=expanded?158:112;
                detail.text=expanded?string.Join("   ·   ",result.scores.Select((s,i)=>$"Game {i+1}: {s.own}–{s.opponent}{(s.completed?"":" (chưa xong)")}"))+ (string.IsNullOrEmpty(result.reason)?"":"\n"+result.reason):"Bấm để xem tỷ số từng game";
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            });
        }
        void AddMessage(string heading,string body)
        { var row=Row("Empty",120); Text("Message",row,heading+"\n<size=17><color=#A1BFC9>"+body+"</color></size>",24,White,Vector2.zero,Vector2.one,new Vector2(24,0),new Vector2(-24,0),TextAlignmentOptions.Center); }
        System.Collections.IEnumerator RestoreHistory(float position)
        {
            yield return null;while(PickleVerse.Shop.LobbyNavigation.IsBusy)yield return null;
            OpenCareer();scroll.verticalNormalizedPosition=position;
        }
        void SaveEditor(bool switchId)
        {
            try
            {
                if(!ProgressionRuntime.DeveloperControls) throw new InvalidOperationException("Chỉ chỉnh player trong Editor hoặc Development Build.");
                var app=MultiplayerApp.Instance;
                if(app && !app.CanChangeLocalProfile) throw new InvalidOperationException("Kết thúc phiên mạng và trở về Menu trước khi đổi player.");
                if(switchId)
                {
                    // Switching an existing ID preserves that player's stored name.
                    ProgressionRuntime.Instance.SwitchPlayer(editId.text.Trim(),null);
                }
                else
                {
                    if(!int.TryParse(editElo.text,NumberStyles.None,CultureInfo.InvariantCulture,out int elo) || elo<EloPolicy.Minimum)
                        throw new ArgumentException("Elo phải là số nguyên từ 100 đến 2147483647.");
                    if(!long.TryParse(editTokenpad.text,NumberStyles.None,CultureInfo.InvariantCulture,out long tokenpad) ||
                       !long.TryParse(editEliteToken.text,NumberStyles.None,CultureInfo.InvariantCulture,out long elite))
                        throw new ArgumentException("Số dư tiền phải là số nguyên từ 0 đến 9223372036854775807, không dùng dấu phân cách.");
                    ProgressionRuntime.Instance.EditLocalPlayer(editName.text,elo,tokenpad,elite);
                }
                Refresh(); feedback.text="Đã lưu player.";
            }
            catch(Exception e) { feedback.text=e.Message; }
        }
        public void BuildShell()
        {
            buildingShell=true;
            var canvas=GetComponentInParent<Canvas>();
            modal=Panel("ProgressionModal",canvas.transform,new Color(0,0,0,.75f));
            modalSafe=Rect("SafeArea",modal,Vector2.zero,Vector2.one);
            window=Panel("Window",modalSafe,Ink,new Vector2(.045f,.065f),new Vector2(.955f,.935f));
            var outline=window.gameObject.AddComponent<Outline>(); outline.effectColor=new Color(.45f,.86f,.77f,.3f); outline.effectDistance=new Vector2(1,-1);
            title=Text("Title",window,"HỒ SƠ",29,White,new Vector2(0,.87f),new Vector2(.8f,1),new Vector2(28,8),new Vector2(0,-2));
            subtitle=Text("Subtitle",window,"THỐNG KÊ VÀ TIẾN TRÌNH",12,Mint,new Vector2(0,.845f),new Vector2(.85f,.9f),new Vector2(29,0),Vector2.zero);
            MakeButton("Close",window,"×",Close,new Vector2(.925f,.89f),new Vector2(.98f,.977f),Card,White,28);
            left=Panel("Identity",window,Surface,new Vector2(.018f,.025f),new Vector2(.257f,.827f));
            var face=Rect("Avatar",left,new Vector2(.23f,.71f),new Vector2(.77f,.97f));
            var image=face.gameObject.AddComponent<Image>(); image.sprite=avatar; image.color=avatar?White:Mint; image.preserveAspect=true; image.raycastTarget=false;
            face.anchorMin=face.anchorMax=new Vector2(.5f,.84f); face.sizeDelta=new Vector2(96,96); face.anchoredPosition=Vector2.zero;
            identityName=Text("PlayerName",left,"PLAYER",23,White,new Vector2(.05f,.64f),new Vector2(.95f,.72f),Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);
            identityId=Text("PlayerId",left,"ID",12,Muted,new Vector2(.06f,.51f),new Vector2(.94f,.64f),Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);
            identityRank=Text("Rank",left,"ĐỒNG\n100 ELO",22,White,new Vector2(.05f,.33f),new Vector2(.95f,.49f),Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);
            statsTab=MakeButton("Statistics",left,"THỐNG KÊ",()=>Open(View.Statistics),new Vector2(.08f,.18f),new Vector2(.92f,.29f),Card,White,16);
            achievementTab=MakeButton("Achievements",left,"THÀNH TỰU",()=>Open(View.Achievements),new Vector2(.08f,.04f),new Vector2(.92f,.15f),Card,White,16);
            dailyTab=MakeButton("Daily",left,"NHIỆM VỤ NGÀY",()=>Open(View.Daily),new Vector2(.08f,.18f),new Vector2(.92f,.29f),Card,White,16);
            weeklyTab=MakeButton("Weekly",left,"NHIỆM VỤ TUẦN",()=>Open(View.Weekly),new Vector2(.08f,.04f),new Vector2(.92f,.15f),Card,White,16);
            right=Rect("Body",window,new Vector2(.27f,.025f),new Vector2(.982f,.827f));
            summary=Panel("Summary",right,Surface,new Vector2(0,.765f),Vector2.one);
            levelText=Text("Level",summary,"LEVEL 1",25,Lime,new Vector2(0,.5f),new Vector2(.6f,1),new Vector2(20,0),Vector2.zero);
            xpText=Text("XP",summary,"0 / 10 XP",17,White,new Vector2(.55f,.5f),Vector2.one,Vector2.zero,new Vector2(-20,0),TextAlignmentOptions.MidlineRight);
            xpFill=Bar("XPBar",summary,new Vector2(.025f,.19f),new Vector2(.975f,.39f));
            summaryText=Text("MissionSummary",summary,"NHIỆM VỤ NGÀY",17,White,new Vector2(.025f,.33f),new Vector2(.67f,.95f),Vector2.zero,Vector2.zero);
            countdown=Text("Countdown",summary,"RESET SAU",13,Mint,new Vector2(.64f,.69f),new Vector2(.975f,.98f),Vector2.zero,Vector2.zero,TextAlignmentOptions.MidlineRight);
            summaryFill=Bar("MissionBar",summary,new Vector2(.025f,.14f),new Vector2(.655f,.26f));
            bonusButton=MakeButton("Bonus",summary,"THƯỞNG TỔNG",()=>{},new Vector2(.70f,.15f),new Vector2(.975f,.59f),Lime,Ink,13);
            var scrollRoot=Rect("Scroll",right,Vector2.zero,new Vector2(1,.738f)); scroll=scrollRoot.gameObject.AddComponent<ScrollRect>(); scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=28;
            var viewport=Rect("Viewport",scrollRoot,Vector2.zero,Vector2.one); viewport.offsetMax=new Vector2(-9,0);
            var maskImage=viewport.gameObject.AddComponent<Image>(); maskImage.color=Ink; var mask=viewport.gameObject.AddComponent<Mask>(); mask.showMaskGraphic=false;
            content=Rect("Content",viewport,new Vector2(0,1),Vector2.one); content.pivot=new Vector2(.5f,1);
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing=10; layout.childControlHeight=layout.childControlWidth=true; layout.childForceExpandHeight=false; layout.childForceExpandWidth=true;
            var fitter=content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport; scroll.content=content;
            var track=Panel("Scrollbar",scrollRoot,Surface,new Vector2(.99f,0),Vector2.one); var sb=track.gameObject.AddComponent<Scrollbar>(); sb.direction=Scrollbar.Direction.BottomToTop;
            var sliding=Rect("Sliding",track,Vector2.zero,Vector2.one); var handle=Panel("Handle",sliding,Mint); sb.handleRect=handle; sb.targetGraphic=handle.GetComponent<Image>(); scroll.verticalScrollbar=sb;
            BuildEditForm();
            modal.gameObject.SetActive(false);
            buildingShell=false;
        }
        void BuildEditForm()
        {
            editForm=Panel("EditForm",right,Surface);
            Text("Help",editForm,"PLAYER CỤC BỘ\n<size=16>Đổi ID tại Menu khi đã kết thúc phiên mạng.</size>",24,Mint,new Vector2(.035f,.76f),new Vector2(.96f,.97f),Vector2.zero,Vector2.zero);
            Text("NameLabel",editForm,"Tên (tối đa 18 ký tự)",16,Muted,new Vector2(.04f,.66f),new Vector2(.96f,.75f),Vector2.zero,Vector2.zero);
            editName=Input("NameInput",editForm,new Vector2(.04f,.54f),new Vector2(.70f,.66f),18);
            MakeButton("SaveName",editForm,"LƯU TÊN",()=>SaveEditor(false),new Vector2(.73f,.54f),new Vector2(.96f,.66f),Mint,Ink,15);
            Text("IDLabel",editForm,"ID (1–24 chữ cái tiếng Anh / chữ số)",16,Muted,new Vector2(.04f,.39f),new Vector2(.96f,.48f),Vector2.zero,Vector2.zero);
            editId=Input("IDInput",editForm,new Vector2(.04f,.26f),new Vector2(.70f,.39f),24);
            MakeButton("Switch",editForm,"CHUYỂN ID",()=>SaveEditor(true),new Vector2(.73f,.26f),new Vector2(.96f,.39f),Lime,Ink,15);
            feedback=Text("Feedback",editForm,"",16,White,new Vector2(.04f,.04f),new Vector2(.96f,.22f),Vector2.zero,Vector2.zero);
            ConfigureEditForm();
        }
        // Updates an existing scene shell too, without rebuilding other profile UI.
        public void ConfigureEditForm()
        {
            PlaceEdit("Help",.04f,.89f,.96f,.99f);
            editForm.Find("Help").GetComponent<TMP_Text>().text="PLAYER CỤC BỘ  <size=14>·  CHỈNH DỮ LIỆU TEST</size>";
            editForm.Find("Help").GetComponent<TMP_Text>().fontSize=21;
            PlaceEdit("NameLabel",.04f,.81f,.96f,.88f);
            PlaceEdit("NameInput",.04f,.70f,.96f,.81f);
            PlaceEdit("IDLabel",.04f,.60f,.70f,.68f);
            editForm.Find("IDLabel").GetComponent<TMP_Text>().text="ID (1–24 chữ cái tiếng Anh / chữ số)";
            PlaceEdit("IDInput",.04f,.49f,.68f,.60f);
            PlaceEdit("Switch",.71f,.49f,.96f,.60f);
            editElo=NumberInput("EloInput","EloLabel","ELO (từ 100)",.04f,.29f,10);
            editTokenpad=NumberInput("TokenpadInput","TokenpadLabel","TOKENPAD",.32f,.62f,19);
            editEliteToken=NumberInput("EliteTokenInput","EliteTokenLabel","ELITE TOKEN",.65f,.96f,19);
            PlaceEdit("SaveName",.04f,.15f,.96f,.27f);
            Label(editForm.Find("SaveName").GetComponent<Button>()).text="LƯU PLAYER";
            PlaceEdit("Feedback",.04f,.01f,.96f,.13f);
            feedback.fontSize=14;
        }
        TMP_InputField NumberInput(string name,string label,string caption,float xMin,float xMax,int limit)
        {
            var heading=editForm.Find(label);
            if(!heading) Text(label,editForm,caption,14,Muted,new Vector2(xMin,.41f),new Vector2(xMax,.48f),Vector2.zero,Vector2.zero);
            var found=editForm.Find(name);
            var input=found?found.GetComponent<TMP_InputField>():Input(name,editForm,new Vector2(xMin,.30f),new Vector2(xMax,.41f),limit);
            input.characterLimit=limit; input.contentType=TMP_InputField.ContentType.IntegerNumber;
            input.textComponent.enableAutoSizing=true; input.textComponent.fontSizeMin=10; input.textComponent.fontSizeMax=19;
            return input;
        }
        void PlaceEdit(string name,float xMin,float yMin,float xMax,float yMax)
        {
            var rect=(RectTransform)editForm.Find(name);
            rect.anchorMin=new Vector2(xMin,yMin); rect.anchorMax=new Vector2(xMax,yMax);
            rect.offsetMin=rect.offsetMax=Vector2.zero;
        }
        TMP_InputField Input(string name,Transform parent,Vector2 min,Vector2 max,int limit)
        {
            var root=Panel(name,parent,Card,min,max); var input=root.gameObject.AddComponent<TMP_InputField>();
            var viewport=Rect("TextArea",root,Vector2.zero,Vector2.one); viewport.offsetMin=new Vector2(12,4); viewport.offsetMax=new Vector2(-12,-4); viewport.gameObject.AddComponent<RectMask2D>();
            var label=Text("Text",viewport,"",19,White,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            input.textViewport=viewport; input.textComponent=label; input.targetGraphic=root.GetComponent<Image>(); input.characterLimit=limit; input.caretColor=Mint; input.customCaretColor=true; return input;
        }
        void ClearRows()
        {
            activeTime=null;
            foreach(Transform row in content.Cast<Transform>().ToArray()) { row.SetParent(null); if(Application.isPlaying) Destroy(row.gameObject); else DestroyImmediate(row.gameObject); }
        }
        RectTransform Row(string name,float height)
        { var row=Panel(name,content,Card); var layout=row.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight=height; layout.minHeight=height; return row; }
        public RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        { var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false); rect.anchorMin=min; rect.anchorMax=max; rect.offsetMin=rect.offsetMax=Vector2.zero; return rect; }
        public RectTransform Panel(string name,Transform parent,Color color,Vector2? min=null,Vector2? max=null)
        { var rect=Rect(name,parent,min??Vector2.zero,max??Vector2.one); var image=rect.gameObject.AddComponent<Image>(); image.color=color; image.sprite=rounded; image.type=Image.Type.Sliced; return rect; }
        public TMP_Text Text(string name,Transform parent,string value,int size,Color color,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax,TextAlignmentOptions alignment=TextAlignmentOptions.MidlineLeft)
        {
            var rect=Rect(name,parent,min,max); rect.offsetMin=offsetMin; rect.offsetMax=offsetMax;
            var text=rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font=font; text.text=value; text.fontSize=size; text.color=color; text.alignment=alignment; text.raycastTarget=false; text.textWrappingMode=TextWrappingModes.Normal; text.overflowMode=TextOverflowModes.Ellipsis; return text;
        }
        Image Bar(string name,Transform parent,Vector2 min,Vector2 max)
        { var track=Panel(name,parent,Ink,min,max); return Panel("Fill",track,Mint).GetComponent<Image>(); }
        static void Fill(Image image,float amount) { image.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(amount),1); image.rectTransform.offsetMin=image.rectTransform.offsetMax=Vector2.zero; }
        public Button MakeButton(string name,Transform parent,string caption,UnityEngine.Events.UnityAction action,Vector2 min,Vector2 max,Color color,Color textColor,int fontSize)
        { var rect=Panel(name,parent,color,min,max); var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=rect.GetComponent<Image>(); StyleButton(button,color); Text("Label",rect,caption,fontSize,textColor,Vector2.zero,Vector2.one,new Vector2(8,0),new Vector2(-8,0),TextAlignmentOptions.Center); if(Application.isPlaying && !buildingShell) button.onClick.AddListener(action); return button; }
        static TMP_Text Label(Button button) => button.GetComponentInChildren<TMP_Text>();
        static void StyleButton(Button button,Color color)
        { var colors=button.colors; colors.normalColor=Color.white; colors.highlightedColor=new Color(1.13f,1.13f,1.13f); colors.pressedColor=new Color(.72f,.8f,.8f); colors.selectedColor=new Color(1.1f,1.1f,1.1f); colors.disabledColor=new Color(.65f,.65f,.65f); button.colors=colors; }
        static void Tab(Button button,bool selected)
        { button.GetComponent<Image>().color=selected?Mint:Card; Label(button).color=selected?Ink:White; }
    }
}
