#if !UNITY_SERVER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PickleVerse.Multiplayer;
using PickleVerse.Services;
using PickleVerse.Services.Api;
using PickleVerse.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PickleVerse.FrontEnd
{
    public sealed class ModerationOverlay : MonoBehaviour
    {
        static ModerationOverlay instance;
        GameObject root, cover; TMP_Text heading, details, feedback, selected, statementLabel, counter, entryLabel, submitLabel, closeLabel;
        TMP_InputField statement; Button submit, refresh, close, previous, next, entry, dismiss;
        ScrollRect detailScroll; UiLineIcon selectedIcon; RectTransform panel;
        int shownResult = -1, shownRestricted = -1; bool currentHasAppeal; Scene nameScene; string sceneName = "";
        readonly List<SanctionDecision> decisions=new List<SanctionDecision>();readonly List<PlayerAppeal> appeals=new List<PlayerAppeal>();
        readonly List<ReportTarget> targets=new List<ReportTarget>();
        int index, requestVersion, autoSession=-1; bool opened, reporting, busy; string matchId;
        Guid? targetPlayer;
        public static bool IsOpen => instance && instance.opened;
        AccountService Account => AccountRuntime.Ensure().Account;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { if (!instance) new GameObject("Moderation Coordinator").AddComponent<ModerationOverlay>(); }
        void Awake() { instance=this;DontDestroyOnLoad(gameObject);SceneManager.sceneLoaded+=Loaded; }
        void Loaded(Scene scene,LoadSceneMode mode)
        {
            if (mode==LoadSceneMode.Additive && scene!=SceneManager.GetActiveScene()) return;
            requestVersion++;busy=false;opened=false;FrontEndModalInput.Release(this);if(root)Destroy(root);root=null;autoSession=-1;
        }
        void EnsureView()
        {
            if (root) return;
            root=ArenaForms.Canvas("Quyết định và tố cáo",1600);SceneManager.MoveGameObjectToScene(root,SceneManager.GetActiveScene());
            shownResult=shownRestricted=-1;
            entry=ArenaForms.Action("Moderation",root.transform,"Quyết định & Khiếu nại",false,null,ArenaGlyph.Shield,21);
            ArenaForms.Pin((RectTransform)entry.transform,Vector2.zero,Vector2.zero,new Vector2(28,24),new Vector2(340,60));
            entryLabel=entry.GetComponentInChildren<TMP_Text>(true);

            cover=ArenaForms.Cover("Cover",root.transform).gameObject;
            cover.GetComponent<Image>().color=new Color(.01f,.02f,.05f,.84f);
            panel=ArenaForms.Surface("Panel",cover.transform);
            ArenaForms.Pin(panel,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(1160,860));

            // Header: shield, gold title, close.
            var shield=ArenaForms.Icon("Icon",panel,ArenaGlyph.Shield,ScreenUiTheme.Gold);
            ArenaForms.Pin(shield.rectTransform,new Vector2(0,1),new Vector2(.5f,.5f),new Vector2(66,-58),new Vector2(42,42));
            heading=ArenaForms.Label("Title",panel,"",ArenaForms.TitleSize,ScreenUiTheme.Gold);
            ArenaForms.Pin(heading.rectTransform,new Vector2(0,1),new Vector2(0,.5f),new Vector2(104,-58),new Vector2(860,50));
            dismiss=ArenaForms.IconButton("Dismiss",panel,ArenaGlyph.Close,Close);
            ArenaForms.Pin((RectTransform)dismiss.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(-26,-30),new Vector2(56,56));
            var divider=ArenaForms.Rule("Divider",panel,new Color(1,1,1,.08f));
            divider.rectTransform.anchorMin=new Vector2(0,1);divider.rectTransform.anchorMax=new Vector2(1,1);
            divider.rectTransform.offsetMin=new Vector2(40,-106);divider.rectTransform.offsetMax=new Vector2(-40,-105);
            var accent=ArenaForms.Rule("Accent",panel,new Color(ScreenUiTheme.Gold.r,ScreenUiTheme.Gold.g,ScreenUiTheme.Gold.b,.6f));
            ArenaForms.Pin(accent.rectTransform,new Vector2(0,1),new Vector2(0,.5f),new Vector2(40,-106),new Vector2(120,2));
            accent.rectTransform.pivot=new Vector2(0,.5f);

            // Item line with its own pager.
            selectedIcon=ArenaForms.Icon("State",panel,ArenaGlyph.Shield,ScreenUiTheme.Mint,.08f);
            ArenaForms.Pin(selectedIcon.rectTransform,new Vector2(0,1),new Vector2(.5f,.5f),new Vector2(56,-150),new Vector2(24,24));
            selected=ArenaForms.Label("Selected",panel,"",ArenaForms.HeadingSize,ScreenUiTheme.Text);
            ArenaForms.Pin(selected.rectTransform,new Vector2(0,1),new Vector2(0,.5f),new Vector2(82,-150),new Vector2(880,40));
            previous=ArenaForms.IconButton("Previous",panel,ArenaGlyph.Previous,()=>Select(-1));
            ArenaForms.Pin((RectTransform)previous.transform,new Vector2(1,1),new Vector2(1,.5f),new Vector2(-104,-150),new Vector2(52,52));
            next=ArenaForms.IconButton("Next",panel,ArenaGlyph.Next,()=>Select(1));
            ArenaForms.Pin((RectTransform)next.transform,new Vector2(1,1),new Vector2(1,.5f),new Vector2(-40,-150),new Vector2(52,52));

            // Decision / report text.
            details=ArenaForms.ScrollText("Evidence",panel,out detailScroll);
            var evidence=(RectTransform)detailScroll.transform;
            evidence.anchorMin=new Vector2(0,1);evidence.anchorMax=new Vector2(1,1);
            evidence.offsetMin=new Vector2(40,-488);evidence.offsetMax=new Vector2(-40,-186);

            // Statement.
            statementLabel=ArenaForms.Label("Statement Label",panel,"Nội dung khiếu nại",ArenaForms.CaptionSize,ScreenUiTheme.Muted);
            ArenaForms.Pin(statementLabel.rectTransform,new Vector2(0,1),new Vector2(0,.5f),new Vector2(42,-514),new Vector2(700,28));
            statement=ArenaForms.Input("Statement",panel,"Nhập nội dung khiếu nại…",4000);
            var well=(RectTransform)statement.transform;
            well.anchorMin=new Vector2(0,1);well.anchorMax=new Vector2(1,1);well.offsetMin=new Vector2(40,-694);well.offsetMax=new Vector2(-40,-534);
            // Counter and feedback sit just above the footer, so they follow the panel when it is compact.
            counter=ArenaForms.Label("Counter",panel,"0/4000",ArenaForms.HintSize,ScreenUiTheme.Muted,TextAlignmentOptions.Right);
            ArenaForms.Pin(counter.rectTransform,new Vector2(1,0),new Vector2(1,.5f),new Vector2(-42,146),new Vector2(260,24));
            statement.onValueChanged.AddListener(_=>UpdateCounter());
            feedback=ArenaForms.Label("Feedback",panel,"",19,ScreenUiTheme.Muted,TextAlignmentOptions.Left,true);
            ArenaForms.Pin(feedback.rectTransform,new Vector2(0,0),new Vector2(0,1),new Vector2(42,158),new Vector2(780,40));

            // Footer.
            refresh=ArenaForms.Action("Refresh",panel,"Làm mới",false,()=>Reload(),ArenaGlyph.Sync,22);
            ArenaForms.Pin((RectTransform)refresh.transform,new Vector2(0,0),new Vector2(0,0),new Vector2(40,40),new Vector2(200,62));
            close=ArenaForms.Action("Close",panel,"Đóng",false,Close,null,22);
            ArenaForms.Pin((RectTransform)close.transform,new Vector2(1,0),new Vector2(1,0),new Vector2(-316,40),new Vector2(190,62));
            closeLabel=close.GetComponentInChildren<TMP_Text>(true);
            submit=ArenaForms.Action("Submit",panel,"Gửi",true,Send,null,22);
            ArenaForms.Pin((RectTransform)submit.transform,new Vector2(1,0),new Vector2(1,0),new Vector2(-40,40),new Vector2(262,62));
            submitLabel=submit.GetComponentInChildren<TMP_Text>(true);
            cover.SetActive(false);
        }
        void UpdateCounter()
        {
            if (!statement) return;
            ArenaForms.Set(counter,statement.text.Length+"/"+statement.characterLimit);
        }
        enum Tone { Info, Success, Problem }
        void Feedback(string value, Tone tone=Tone.Info)
        {
            if (!feedback) return;
            ArenaForms.Set(feedback,value);
            feedback.color=tone==Tone.Problem?ArenaForms.Error:tone==Tone.Success?ArenaForms.Success:ScreenUiTheme.Muted;
        }
        void Update()
        {
            // Scene.name allocates a new string on every call; read it once per scene.
            var activeScene=SceneManager.GetActiveScene();
            if (activeScene!=nameScene) { nameScene=activeScene;sceneName=activeScene.name; }
            string scene=sceneName;var match=NetworkMatch.Instance;
            bool result=match && match.Finished && match.Roster!=null;
            bool available=Account.IsSignedIn && (scene=="Login" && Account.IsRestricted || scene=="Menu" || result);
            // In the home menu the entry lives in the bottom navigation as a "!" tile, clear of PLAY.
            bool menuTile=scene=="Menu" && !result;
            ShowMenuTile(menuTile && available && !SceneLoading.IsBlocking);
            if (!available || SceneLoading.IsBlocking)
            { if(root)root.SetActive(false);FrontEndModalInput.Release(this);return; }
            EnsureView();
            if (!root.activeSelf) root.SetActive(true);
            ArenaForms.Show(entry,!opened && !(menuTile && menuShortcut));
            // Rebind the entry only when its meaning changes (no per-frame closures or listener churn).
            int resultState=result?1:0;
            if (shownResult!=resultState)
            {
                shownResult=resultState;
                ArenaForms.Set(entryLabel,result?"Tố cáo người chơi":"Quyết định & Khiếu nại");
                entry.onClick.RemoveAllListeners();
                if (result) entry.onClick.AddListener(()=> { var current=NetworkMatch.Instance; if (current && current.Roster!=null) OpenReport(current.Roster.matchId); });
                else entry.onClick.AddListener(OpenDecisions);
            }
            if (scene=="Login" && Account.IsRestricted && autoSession!=Account.SessionVersion)
            { autoSession=Account.SessionVersion;OpenDecisions(); }
            if (cover.activeSelf!=opened) cover.SetActive(opened);
            if (opened) FrontEndModalInput.TryAcquire(this);else FrontEndModalInput.Release(this);
            int restricted=Account.IsRestricted?1:0;
            if (shownRestricted!=restricted) { shownRestricted=restricted;ArenaForms.Set(closeLabel,restricted==1?"Đăng xuất":"Đóng"); }
            int count=reporting?targets.Count:decisions.Count;
            submit.interactable=!busy && (reporting?targets.Count>0:decisions.Count>0 && !currentHasAppeal);
            refresh.interactable=dismiss.interactable=!busy;
            previous.interactable=!busy && index>0;
            next.interactable=!busy && index+1<count;
            // Esc closes the dialog; a restricted account must choose "Đăng xuất" explicitly.
            if (opened && !busy && restricted==0 && Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame && !(statement && statement.isFocused))
                Close();
        }
        Button menuShortcut; Scene menuTileScene;
        void ShowMenuTile(bool show)
        {
            var active=SceneManager.GetActiveScene();
            if (!menuShortcut && show && menuTileScene!=active)
            {
                menuTileScene=active;
                menuShortcut=CreateMenuTile();
            }
            if (!menuShortcut || menuShortcut.gameObject.activeSelf==show)
                return;
            // HomeMenuShell notices the changed tile count and re-lays out the navigation.
            menuShortcut.gameObject.SetActive(show);
        }
        Button CreateMenuTile()
        {
            var active=SceneManager.GetActiveScene();
            var progress=FindObjectsByType<Button>(FindObjectsInactive.Include).FirstOrDefault(b=>b.name=="BtnProgression" && b.gameObject.scene==active);
            if (!progress)
                return null;
            var tile=Instantiate(progress,progress.transform.parent,false);
            tile.name="BtnAppeals";
            tile.onClick=new Button.ButtonClickedEvent();
            tile.onClick.AddListener(OpenDecisions);
            tile.interactable=true;
            tile.transform.SetSiblingIndex(progress.transform.GetSiblingIndex()+1);
            var label=tile.transform.Find("Text (TMP)")?.GetComponent<TMP_Text>() ?? tile.GetComponentInChildren<TMP_Text>(true);
            if (label)
                label.text="KHIẾU NẠI";
            var icon=tile.transform.Find("Refresh Icon")?.GetComponent<Image>();
            if (icon)
            {
                icon.enabled=false;
                var mark=new GameObject("Alert Mark",typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                mark.transform.SetParent(icon.transform,false);
                var rect=mark.rectTransform;
                rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
                if (label)
                    mark.font=label.font;
                mark.text="!";
                mark.fontStyle=FontStyles.Bold;
                mark.enableAutoSizing=true;mark.fontSizeMin=12;mark.fontSizeMax=30;
                mark.alignment=TextAlignmentOptions.Center;
                mark.color=icon.color;
                mark.raycastTarget=false;
            }
            return tile;
        }
        public static void ShowReport(string id, Guid player) { if(instance)instance.OpenReport(id, player); }
        void OpenReport(string id, Guid? player = null)
        {
            EnsureView();
            if (busy) return;
            // Match ids reach this overlay as 32-digit history ids or hyphenated API ids. The API
            // binds MatchId as a Guid, which System.Text.Json reads only in the hyphenated form.
            if(Guid.TryParse(id,out var parsedMatch)) id=parsedMatch.ToString("D");
            opened=true;reporting=true;matchId=id;targetPlayer=player;index=0;statement.characterLimit=254;statement.text="";UpdateCounter();
            targets.Clear();
            Render();
            Reload();
        }
        void OpenDecisions() { EnsureView();opened=true;reporting=false;targetPlayer=null;index=0;statement.characterLimit=4000;statement.text="";UpdateCounter();Render();Reload(); }
        void Select(int delta) { index=Mathf.Clamp(index+delta,0,Math.Max(0,(reporting?targets.Count:decisions.Count)-1));statement.text="";Render(); }
        async void Reload()
        {
            if(busy)return;busy=true;int request=++requestVersion,session=Account.SessionVersion;Feedback("Đang tải…");
            try
            {
                if(reporting)
                {
                    var data=await Account.SendAuthorizedAsync<List<ReportTarget>>("GET","api/Matches/"+matchId+"/report-targets");
                    if(!Current(request,session))return;targets.Clear();targets.AddRange(data.Where(target => !targetPlayer.HasValue || target.PlayerId == targetPlayer));
                }
                else
                {
                    var data=await Account.SendAuthorizedAsync<List<SanctionDecision>>("GET","api/Moderation/decisions");
                    var cases=await Account.SendAuthorizedAsync<List<PlayerAppeal>>("GET","api/Moderation/appeals");
                    if(!Current(request,session))return;decisions.Clear();decisions.AddRange(data);appeals.Clear();appeals.AddRange(cases);
                }
                Feedback("");index=Mathf.Clamp(index,0,Math.Max(0,(reporting?targets.Count:decisions.Count)-1));Render();
                Debug.Log("[Moderation] Loaded "+(reporting?"report targets":"account decisions"));
            }
            catch(Exception error) { if(Current(request,session)) Feedback(error.Message,Tone.Problem); }
            finally { if(request==requestVersion)busy=false; }
        }
        bool Current(int request,int session)=>this && root && opened && request==requestVersion && Account.SessionVersion==session;
        void Render()
        {
            if (!heading) return;
            ArenaForms.Set(heading,reporting?"Tố cáo người chơi":"Quyết định và khiếu nại");
            int count=reporting?targets.Count:decisions.Count;
            bool pager=!targetPlayer.HasValue && count>1;
            ArenaForms.Show(previous,pager);ArenaForms.Show(next,pager);
            bool empty=count==0;
            currentHasAppeal=false;
            if(reporting)
            {
                ArenaForms.Set(selected,empty?"Không có người chơi phù hợp để tố cáo":targets[index].PlayerName+(count>1?"  ·  "+(index+1)+"/"+count:""));
                ArenaForms.Set(details,"Tố cáo chỉ áp dụng với người thật cùng trận đã hoàn tất. Mỗi người chỉ được gửi một đơn cho cùng người chơi trong trận. Ban quản trị sẽ đối chiếu bằng chứng và replay.");
                ArenaForms.Set(statementLabel,"Mô tả hành vi vi phạm");
                ArenaForms.Set(statement.placeholder.GetComponent<TMP_Text>(),"Mô tả hành vi vi phạm (3–254 ký tự)…");
                ArenaForms.Set(submitLabel,"Gửi tố cáo");
                selectedIcon.SetGlyph(ArenaGlyph.Shield);selectedIcon.color=empty?ScreenUiTheme.Muted:ArenaForms.Warning;
            }
            else
            {
                ArenaForms.Set(selected,empty?"Tài khoản đang hoạt động bình thường":"Quyết định "+(index+1)+"/"+count);
                selectedIcon.SetGlyph(empty?ArenaGlyph.Shield:ArenaGlyph.Star);selectedIcon.color=empty?ScreenUiTheme.Mint:ArenaForms.Warning;
                if(empty)ArenaForms.Set(details,"Tài khoản của bạn chưa có quyết định xử lý nào, nên không có gì cần khiếu nại.\n\nNếu sau này tài khoản bị tạm ngưng hoặc khóa, quyết định sẽ hiện tại đây kèm lý do, thời hạn và nơi gửi khiếu nại.");
                else
                {
                    var d=decisions[index];var a=appeals.FirstOrDefault(x=>x.SanctionId==d.SanctionId);currentHasAppeal=a!=null;
                    string state=d.RevokedAt!=null?"Đã thu hồi":d.Active?"Đang hiệu lực":"Đã hết hạn";
                    ArenaForms.Set(details,(d.Restriction==UserAccountStatus.Suspended?"Tạm ngưng":"Khóa tài khoản")+" · "+state+"\n"+d.Reason+
                        "\nBan hành: "+Time(d.CreatedAt)+"\nThời hạn: "+(d.ExpiresAt.HasValue?Time(d.ExpiresAt.Value):"Không thời hạn")+
                        (a==null?"\n\nBạn có thể gửi một khiếu nại cho quyết định này.":"\n\nKhiếu nại: "+Status(a.Status)+"\n"+a.Statement+"\nKết luận: "+(a.DecisionReason??"Đang chờ xem xét")));
                }
                ArenaForms.Set(statementLabel,currentHasAppeal?"Bạn đã gửi khiếu nại cho quyết định này":"Nội dung khiếu nại");
                ArenaForms.Set(statement.placeholder.GetComponent<TMP_Text>(),"Trình bày lý do và bằng chứng khiếu nại (10–4000 ký tự)…");
                ArenaForms.Set(submitLabel,"Gửi khiếu nại");
            }
            // Nothing to appeal or report: hide the form instead of offering a dead control, and close the gap it leaves.
            bool form=!empty && !(!reporting && currentHasAppeal);
            ArenaForms.Show(statement,form);ArenaForms.Show(counter,form);ArenaForms.Show(submit,form);ArenaForms.Show(statementLabel,form);
            var size=new Vector2(1160,form?860:660);
            if (panel.sizeDelta!=size) panel.sizeDelta=size;
            var closeRect=(RectTransform)close.transform;var closeAt=new Vector2(form?-316:-40,40);
            if (closeRect.anchoredPosition!=closeAt) closeRect.anchoredPosition=closeAt;
            selected.color=empty&&!reporting?ScreenUiTheme.Mint:ScreenUiTheme.Text;
            if (detailScroll) detailScroll.verticalNormalizedPosition=1;
            UpdateCounter();
        }
        static string Time(DateTime value)=>value.ToUniversalTime().AddHours(7).ToString("dd/MM/yyyy HH:mm");
        static string Status(AppealReviewStatus status)=>status==AppealReviewStatus.Accepted?"Được chấp nhận":status==AppealReviewStatus.Declined?"Bị từ chối":status==AppealReviewStatus.Checking?"Đang xem xét":"Đang chờ";
        async void Send()
        {
            if (reporting && targets.Count == 0 || !reporting && decisions.Count == 0) return;
            if(busy)return;string text=statement.text.Trim();if(text.Length<(reporting?3:10)){Feedback(reporting?"Vui lòng mô tả ít nhất 3 ký tự.":"Vui lòng trình bày ít nhất 10 ký tự.",Tone.Problem);return;}
            busy=true;int request=++requestVersion,session=Account.SessionVersion;
            try
            {
                if(reporting)await Account.SendAuthorizedAsync<ApiMessage>("POST","api/Matches/report",new { MatchId=matchId,ReportedPlayerId=targets[index].PlayerId,Reason=text });
                else await Account.SendAuthorizedAsync<PlayerAppeal>("POST","api/Moderation/appeals",new { SanctionId=decisions[index].SanctionId,Statement=text });
                if(!Current(request,session))return;Feedback(reporting?"Đã gửi tố cáo. Ban quản trị sẽ xem xét.":"Đã gửi khiếu nại. Bấm Làm mới để theo dõi quyết định.",Tone.Success);statement.text="";
                if(!reporting){busy=false;Reload();}
            }
            catch(Exception error){if(Current(request,session))Feedback(error.Message,Tone.Problem);}
            finally{if(request==requestVersion)busy=false;}
        }
        async void Close()
        {
            if(busy)return;
            if(Account.IsRestricted)
            {
                try{await Account.LogoutAsync();}catch(Exception error){Debug.LogWarning("[Moderation] Logout completed locally ("+error.GetType().Name+").");}
                SceneLoading.Load("Login");
            }
            opened=false;requestVersion++;FrontEndModalInput.Release(this);if(cover)cover.SetActive(false);
        }
        void OnDestroy(){SceneManager.sceneLoaded-=Loaded;FrontEndModalInput.Release(this);if(root)Destroy(root);if(instance==this)instance=null;}
    }
}
#endif
