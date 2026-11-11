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
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PickleVerse.FrontEnd
{
    public sealed class ModerationOverlay : MonoBehaviour
    {
        static ModerationOverlay instance;
        GameObject root, cover; TMP_Text heading, details, feedback, selected;
        TMP_InputField statement; Button submit, refresh, close, previous, next, entry;
        readonly List<SanctionDecision> decisions=new List<SanctionDecision>();readonly List<PlayerAppeal> appeals=new List<PlayerAppeal>();
        readonly List<ReportTarget> targets=new List<ReportTarget>();
        int index, requestVersion, autoSession=-1; bool opened, reporting, busy; string matchId;
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
            if (root) return;root=RuntimeForms.Canvas("Quyết định và tố cáo",1600);SceneManager.MoveGameObjectToScene(root,SceneManager.GetActiveScene());
            entry=RuntimeForms.Button("Moderation",root.transform,"Quyết định & Khiếu nại",()=>OpenDecisions(),new Vector2(.015f,.018f),new Vector2(.21f,.075f));
            cover=RuntimeForms.Panel("Cover",root.transform,Vector2.zero,Vector2.one,new Color(0,0,0,.9f)).gameObject;
            var panel=RuntimeForms.Panel("Panel",cover.transform,new Vector2(.19f,.08f),new Vector2(.81f,.92f),new Color(.04f,.08f,.1f,1));
            heading=RuntimeForms.Text("Title",panel,"",34,new Vector2(.04f,.9f),new Vector2(.96f,.98f));
            selected=RuntimeForms.Text("Selected",panel,"",24,new Vector2(.04f,.81f),new Vector2(.96f,.9f));
            details=RuntimeForms.ScrollText(panel,new Vector2(.04f,.41f),new Vector2(.96f,.8f));
            statement=RuntimeForms.Input(panel,"Nhập nội dung khiếu nại…",new Vector2(.04f,.2f),new Vector2(.96f,.39f),4000);
            feedback=RuntimeForms.Text("Feedback",panel,"",19,new Vector2(.04f,.13f),new Vector2(.96f,.2f));
            previous=RuntimeForms.Button("Previous",panel,"Trước",()=>Select(-1),new Vector2(.04f,.04f),new Vector2(.18f,.12f));
            next=RuntimeForms.Button("Next",panel,"Sau",()=>Select(1),new Vector2(.2f,.04f),new Vector2(.34f,.12f));
            submit=RuntimeForms.Button("Submit",panel,"Gửi",Send,new Vector2(.36f,.04f),new Vector2(.57f,.12f));
            refresh=RuntimeForms.Button("Refresh",panel,"Làm mới",()=>Reload(),new Vector2(.59f,.04f),new Vector2(.77f,.12f));
            close=RuntimeForms.Button("Close",panel,"Đóng",Close,new Vector2(.79f,.04f),new Vector2(.96f,.12f));cover.SetActive(false);
        }
        void Update()
        {
            string scene=SceneManager.GetActiveScene().name;var match=NetworkMatch.Instance;
            bool result=match && match.Finished && match.Roster!=null;
            bool available=Account.IsSignedIn && (scene=="Login" && Account.IsRestricted || scene=="Menu" || result);
            // In the home menu the entry lives in the bottom navigation as a "!" tile, clear of PLAY.
            bool menuTile=scene=="Menu" && !result;
            ShowMenuTile(menuTile && available && !SceneLoading.IsBlocking);
            if (!available || SceneLoading.IsBlocking)
            { if(root)root.SetActive(false);FrontEndModalInput.Release(this);return; }
            EnsureView();root.SetActive(true);entry.gameObject.SetActive(!opened && !(menuTile && menuShortcut));
            entry.GetComponentInChildren<TMP_Text>().text=result?"Tố cáo người chơi":"Quyết định & Khiếu nại";
            entry.onClick.RemoveAllListeners();entry.onClick.AddListener(()=> { if(result)OpenReport(match.Roster.matchId);else OpenDecisions(); });
            if (scene=="Login" && Account.IsRestricted && autoSession!=Account.SessionVersion)
            { autoSession=Account.SessionVersion;OpenDecisions(); }
            cover.SetActive(opened);
            if (opened) FrontEndModalInput.TryAcquire(this);else FrontEndModalInput.Release(this);
            close.GetComponentInChildren<TMP_Text>().text=Account.IsRestricted?"Đăng xuất":"Đóng";
            submit.interactable=!busy && (reporting?targets.Count>0:decisions.Count>0 && !appeals.Any(a=>a.SanctionId==decisions[index].SanctionId));
            refresh.interactable=previous.interactable=next.interactable=!busy;
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
            menuShortcut.gameObject.SetActive(show);
            FitMenuNavigation(menuShortcut.transform.parent);
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
        // The home navigation is a single-row grid; keep one column per visible tile.
        static void FitMenuNavigation(Transform row)
        {
            var grid=row ? row.GetComponent<GridLayoutGroup>() : null;
            if (!grid || grid.constraint!=GridLayoutGroup.Constraint.FixedColumnCount)
                return;
            int count=0;
            foreach (Transform child in row)
                if (child.gameObject.activeSelf)
                    count++;
            count=Mathf.Max(1,count);
            grid.constraintCount=count;
            var rect=(RectTransform)row;
            rect.sizeDelta=new Vector2(grid.padding.horizontal+count*grid.cellSize.x+(count-1)*grid.spacing.x,rect.sizeDelta.y);
        }
        public static void ShowReport(string id) { if(instance)instance.OpenReport(id); }
        void OpenReport(string id) { EnsureView();opened=true;reporting=true;matchId=id;index=0;statement.text="";statement.characterLimit=254;Reload(); }
        void OpenDecisions() { EnsureView();opened=true;reporting=false;index=0;statement.text="";statement.characterLimit=4000;Reload(); }
        void Select(int delta) { index=Mathf.Clamp(index+delta,0,Math.Max(0,(reporting?targets.Count:decisions.Count)-1));statement.text="";Render(); }
        async void Reload()
        {
            if(busy)return;busy=true;int request=++requestVersion,session=Account.SessionVersion;feedback.text="Đang tải…";
            try
            {
                if(reporting)
                {
                    var data=await Account.SendAuthorizedAsync<List<ReportTarget>>("GET","api/Matches/"+matchId+"/report-targets");
                    if(!Current(request,session))return;targets.Clear();targets.AddRange(data);
                }
                else
                {
                    var data=await Account.SendAuthorizedAsync<List<SanctionDecision>>("GET","api/Moderation/decisions");
                    var cases=await Account.SendAuthorizedAsync<List<PlayerAppeal>>("GET","api/Moderation/appeals");
                    if(!Current(request,session))return;decisions.Clear();decisions.AddRange(data);appeals.Clear();appeals.AddRange(cases);
                }
                feedback.text="";index=Mathf.Clamp(index,0,Math.Max(0,(reporting?targets.Count:decisions.Count)-1));Render();
                Debug.Log("[Moderation] Loaded "+(reporting?"report targets":"account decisions"));
            }
            catch(Exception error) { if(Current(request,session)) feedback.text=error.Message; }
            finally { if(request==requestVersion)busy=false; }
        }
        bool Current(int request,int session)=>this && root && opened && request==requestVersion && Account.SessionVersion==session;
        void Render()
        {
            heading.text=reporting?"Tố cáo người chơi":"Quyết định và khiếu nại";
            if(reporting)
            {
                selected.text=targets.Count==0?"Không có người chơi phù hợp để tố cáo":targets[index].PlayerName+" · "+(index+1)+"/"+targets.Count;
                details.text="Tố cáo chỉ áp dụng với người thật cùng trận đã hoàn tất. Mỗi người chỉ được gửi một đơn cho cùng người chơi trong trận. Ban quản trị sẽ đối chiếu bằng chứng và replay.";
                statement.placeholder.GetComponent<TMP_Text>().text="Mô tả hành vi vi phạm (3–254 ký tự)…";
                submit.GetComponentInChildren<TMP_Text>().text="Gửi tố cáo";
            }
            else
            {
                selected.text=decisions.Count==0?"Tài khoản chưa có quyết định xử lý":"Quyết định "+(index+1)+"/"+decisions.Count;
                if(decisions.Count==0)details.text="Không có quyết định xử lý để khiếu nại.";
                else
                {
                    var d=decisions[index];var a=appeals.FirstOrDefault(x=>x.SanctionId==d.SanctionId);
                    string state=d.RevokedAt!=null?"Đã thu hồi":d.Active?"Đang hiệu lực":"Đã hết hạn";
                    details.text=(d.Restriction==UserAccountStatus.Suspended?"Tạm ngưng":"Khóa tài khoản")+" · "+state+"\n"+d.Reason+
                        "\nBan hành: "+Time(d.CreatedAt)+"\nThời hạn: "+(d.ExpiresAt.HasValue?Time(d.ExpiresAt.Value):"Không thời hạn")+
                        (a==null?"\nBạn có thể gửi một khiếu nại cho quyết định này.":"\n\nKhiếu nại: "+Status(a.Status)+"\n"+a.Statement+"\nKết luận: "+(a.DecisionReason??"Đang chờ xem xét"));
                }
                statement.placeholder.GetComponent<TMP_Text>().text="Trình bày lý do và bằng chứng khiếu nại (10–4000 ký tự)…";
                submit.GetComponentInChildren<TMP_Text>().text="Gửi khiếu nại";
            }
        }
        static string Time(DateTime value)=>value.ToUniversalTime().AddHours(7).ToString("dd/MM/yyyy HH:mm");
        static string Status(AppealReviewStatus status)=>status==AppealReviewStatus.Accepted?"Được chấp nhận":status==AppealReviewStatus.Declined?"Bị từ chối":status==AppealReviewStatus.Checking?"Đang xem xét":"Đang chờ";
        async void Send()
        {
            if(busy)return;string text=statement.text.Trim();if(text.Length<(reporting?3:10)){feedback.text="Vui lòng nhập nội dung đầy đủ.";return;}
            busy=true;int request=++requestVersion,session=Account.SessionVersion;
            try
            {
                if(reporting)await Account.SendAuthorizedAsync<ApiMessage>("POST","api/Matches/report",new { MatchId=matchId,ReportedPlayerId=targets[index].PlayerId,Reason=text });
                else await Account.SendAuthorizedAsync<PlayerAppeal>("POST","api/Moderation/appeals",new { SanctionId=decisions[index].SanctionId,Statement=text });
                if(!Current(request,session))return;feedback.text=reporting?"Đã gửi tố cáo.":"Đã gửi khiếu nại. Bấm Làm mới để theo dõi quyết định.";statement.text="";
                if(!reporting){busy=false;Reload();}
            }
            catch(Exception error){if(Current(request,session))feedback.text=error.Message;}
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
