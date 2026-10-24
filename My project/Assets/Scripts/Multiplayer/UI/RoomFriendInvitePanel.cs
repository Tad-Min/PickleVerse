using System;
using System.Linq;
using System.Threading.Tasks;
using PickleVerse.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using static PickleVerse.Multiplayer.MatchPresentationUi;

namespace PickleVerse.Multiplayer.UI
{
    /// <summary>Search the authenticated player's friends and send real, expiring room invitations.</summary>
    public sealed class RoomFriendInvitePanel:MonoBehaviour
    {
        public TMP_InputField Search{get;private set;}
        public InvitableFriendsPage Page{get;private set;}=new InvitableFriendsPage();
        RectTransform card,content;
        UnityEngine.UI.ScrollRect scroll;
        UiRowPool rows;
        TMP_Text status;
        TMP_FontAsset font;
        int generation;
        bool loading,sending;
        float searchAt=-1,nextRefresh;

        public static RoomFriendInvitePanel Attach(PlayMultiplayerPresenter presenter)
        {
            var panel=presenter.inviteDialog.GetComponent<RoomFriendInvitePanel>();
            if(panel)return panel;
            panel=presenter.inviteDialog.AddComponent<RoomFriendInvitePanel>();panel.Build(presenter);return panel;
        }
        void Build(PlayMultiplayerPresenter presenter)
        {
            card=(RectTransform)presenter.inviteClose.transform.parent;
            foreach(Transform child in card)child.gameObject.SetActive(child==presenter.inviteClose.transform);
            font=ScreenUiTheme.Current?ScreenUiTheme.Current.font:TMP_Settings.defaultFontAsset;
            card.anchorMin=card.anchorMax=card.pivot=new Vector2(.5f,.5f);card.anchoredPosition=Vector2.zero;
            var close=(RectTransform)presenter.inviteClose.transform;close.anchorMin=new Vector2(.90f,.90f);close.anchorMax=new Vector2(.98f,.985f);close.offsetMin=close.offsetMax=Vector2.zero;
            presenter.inviteClose.GetComponentInChildren<TMP_Text>(true).text="×";
            Label("Friend Picker Title",card,font,"MỜI BẠN BÈ",28,new Vector2(.04f,.91f),new Vector2(.87f,.985f),ScreenUiTheme.Gold);
            Label("Friend Picker Subtitle",card,font,"Chọn bạn đang ở lobby để mời vào phòng",15,new Vector2(.04f,.85f),new Vector2(.96f,.91f),ScreenUiTheme.Muted);
            var search=Image("Friend Search",card,new Vector2(.04f,.74f),new Vector2(.96f,.83f),LobbyPanelStyle.Background,true).rectTransform;
            Search=search.gameObject.AddComponent<TMP_InputField>();Search.characterLimit=64;
            var input=Label("Text",search,font,"",18,new Vector2(.025f,.08f),new Vector2(.975f,.92f),ScreenUiTheme.Text);input.alignment=TextAlignmentOptions.MidlineLeft;input.richText=false;
            var hint=Label("Placeholder",search,font,"Tìm theo tên hoặc ID bạn bè…",18,new Vector2(.025f,.08f),new Vector2(.975f,.92f),ScreenUiTheme.Muted);hint.alignment=TextAlignmentOptions.MidlineLeft;
            Search.textViewport=search;Search.textComponent=input;Search.placeholder=hint;Search.targetGraphic=search.GetComponent<UnityEngine.UI.Image>();
            Search.onValueChanged.AddListener(_=>searchAt=Time.unscaledTime+.3f);
            var area=Rect("Friend List",card,new Vector2(.04f,.12f),new Vector2(.96f,.71f));
            scroll=area.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();scroll.horizontal=false;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
            var viewport=Image("Viewport",area,Vector2.zero,Vector2.one,Color.clear,true).rectTransform;viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            content=Rect("Content",viewport,new Vector2(0,1),Vector2.one);content.pivot=new Vector2(.5f,1);
            var layout=content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.spacing=8;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport;scroll.content=content;rows=content.gameObject.AddComponent<UiRowPool>();
            status=Label("Friend Picker Status",card,font,"",14,new Vector2(.04f,.015f),new Vector2(.96f,.105f),ScreenUiTheme.Mint);status.richText=false;
            Layout();
        }
        void Layout(){if(!card)return;var size=((RectTransform)card.parent).rect.size;card.sizeDelta=new Vector2(Mathf.Min(800,size.x-36),Mathf.Min(570,size.y-36));}
        public void Open()
        {
            Search.SetTextWithoutNotify("");Page=new InvitableFriendsPage();rows.ReleaseAll();scroll.verticalNormalizedPosition=1;
            gameObject.SetActive(true);transform.SetAsLastSibling();
            Layout();_=Fetch(false);EventSystem.current?.SetSelectedGameObject(Search.gameObject);
        }
        void OnDisable(){generation++;loading=false;searchAt=-1;}
        void Update()
        {
            Layout();
            if(searchAt>=0&&Time.unscaledTime>=searchAt){searchAt=-1;_=Fetch(false);}
            else if(!loading&&!sending&&Page.Offset==0&&Time.unscaledTime>=nextRefresh)_=Fetch(false);
        }
        public async Task Fetch(bool append)
        {
            int run=++generation;loading=true;nextRefresh=Time.unscaledTime+3;
            if(Page.Players.Length==0)status.text="Đang tải danh sách bạn bè…";
            try
            {
                var runtime=RoomInvitationsRuntime.Instance;
                await runtime.PublishNowAsync();
                var page=await runtime.Client.FriendsAsync(Search.text,append?Page.Offset+50:0);
                if(!this||!gameObject.activeInHierarchy||run!=generation)return;
                if(append)page.Players=Page.Players.Concat(page.Players).GroupBy(p=>p.Id).Select(g=>g.Last()).ToArray();
                Page=page;Render();status.text=page.Players.Length==0?string.IsNullOrWhiteSpace(Search.text)?"Chưa có bạn bè. Hãy kết bạn từ menu Bạn bè.":"Không tìm thấy bạn phù hợp.":"Chỉ bạn đang ở lobby mới nhận được lời mời.";
            }
            catch(Exception error){if(this&&gameObject.activeInHierarchy&&run==generation)status.text=error.Message;}
            finally{if(run==generation)loading=false;}
        }
        void Render()
        {
            float position=scroll.verticalNormalizedPosition;rows.ReleaseAll();
            foreach(var friend in Page.Players.OrderByDescending(p=>p.CanInvite).ThenBy(p=>p.Status=="offline").ThenBy(p=>p.Name,StringComparer.CurrentCultureIgnoreCase))
            {
                var row=rows.Take("friend",friend.Id,78);
                var name=row.Find("Friend Name")?.GetComponent<TMP_Text>()??Label("Friend Name",row,font,"",21,new Vector2(.04f,.43f),new Vector2(.72f,.93f),ScreenUiTheme.Text);
                name.alignment=TextAlignmentOptions.MidlineLeft;name.richText=false;name.text=friend.Name;
                var state=row.Find("Friend State")?.GetComponent<TMP_Text>()??Label("Friend State",row,font,"",14,new Vector2(.04f,.08f),new Vector2(.72f,.43f),ScreenUiTheme.Muted);
                state.alignment=TextAlignmentOptions.MidlineLeft;state.text=(friend.Status=="lobby"?"Đang ở lobby":friend.Status=="in_room"?"Đã ở trong phòng":friend.Status=="busy"?"Đang bận":"Offline")+"  •  "+friend.Elo+" Elo";
                var button=row.Find("Invite Friend")?.GetComponent<UnityEngine.UI.Button>()??Button("Invite Friend",row,font,"MỜI",new Vector2(.76f,.20f),new Vector2(.97f,.80f),true);
                ScreenUiTheme.ButtonStyle(button,true);button.GetComponentInChildren<TMP_Text>().fontSize=16;
                button.GetComponentInChildren<TMP_Text>().text=friend.Pending?"ĐÃ GỬI":"MỜI";button.interactable=friend.CanInvite&&!sending;
                button.onClick.AddListener(()=>_=SendAsync(friend));
            }
            if(Page.HasMore)
            {
                var row=rows.Take("more","More friends",48);
                var more=row.Find("More")?.GetComponent<UnityEngine.UI.Button>()??Button("More",row,font,"XEM THÊM",Vector2.zero,Vector2.one,false);
                ScreenUiTheme.ButtonStyle(more);more.onClick.AddListener(()=>_=Fetch(true));
            }
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);scroll.verticalNormalizedPosition=position;
        }
        public async Task SendAsync(InvitableFriend friend)
        {
            if(sending||!friend.CanInvite)return;sending=true;Render();
            try
            {
                await RoomInvitationsRuntime.Instance.PublishNowAsync();
                await RoomInvitationsRuntime.Instance.Client.SendAsync(friend.Id);
                if(!this)return;friend.Pending=true;friend.CanInvite=false;status.text="Đã gửi lời mời cho "+friend.Name+".";
            }
            catch(Exception error){if(this)status.text=error.Message;}
            finally{sending=false;if(this&&gameObject.activeInHierarchy)Render();}
        }
    }
}
