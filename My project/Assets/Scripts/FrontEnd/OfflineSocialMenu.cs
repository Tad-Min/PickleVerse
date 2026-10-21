using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using PickleVerse.Services;
using PickleVerse.Progression;
using PickleVerse.Social;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PickleVerse.FrontEnd
{
    [DefaultExecutionOrder(20)]
    public sealed class OfflineSocialMenu : MonoBehaviour
    {
        public PlayerProgressionMenu ui;
        public UnityEngine.UI.Button friendsButton, leaderboardButton, friendsTab, leaderboardTab, listButton, directoryButton, myPositionButton, closeButton;
        public RectTransform modal, modalSafe, window, content;
        public UnityEngine.UI.ScrollRect scroll;
        public TMP_InputField search;
        public TMP_Text title, selfName, selfId, selfElo, selfPosition, count, feedback;
        public RectTransform selfRow;
        public bool drawerMode;
        public UnityEngine.UI.Image drawerShade;
        public bool IsOpen=>opened;
        float drawerProgress;
        Rect lastDrawerSafe;
        Vector2 lastDrawerScreen;
        bool leaderboard, directory, opened, priorInteraction;
        CanvasGroup menuInteraction;
        GameObject priorSelection;
        static readonly Color Ink=new Color(.025f,.065f,.095f), Surface=new Color(.045f,.115f,.15f), Card=new Color(.075f,.16f,.20f), Mint=new Color(.45f,.86f,.77f), Lime=new Color(.79f,.96f,.45f), White=new Color(.94f,.97f,.96f), Muted=new Color(.63f,.75f,.79f);
        [Serializable] sealed class OnlinePlayer {public string id,name,relationship;public int elo,position,requestId;}
        [Serializable] sealed class OnlinePage {public List<OnlinePlayer> players=new List<OnlinePlayer>();public int total,myPosition,playerCount,friendCount,offset;public bool hasMore;}
        OnlinePage page=new OnlinePage();
        bool loading,mutating;
        int fetchGeneration;
        float searchAt=-1;
        float DeviceDpi=>GetComponent<HomeMenuShell>()?.Dpi??Screen.dpi;

        void Awake()
        {
            ui=GetComponent<PlayerProgressionMenu>();
            if(!friendsButton || !leaderboardButton) ConfigureWelcomeCard();
            if(!modal) BuildShell();
            var subtitle=window.Find("Subtitle")?.GetComponent<TMP_Text>();
            if(subtitle)subtitle.text="BẠN BÈ / XẾP HẠNG ELO · ONLINE";
            ConfigureOverlay();
            var safe=transform.Find("HomeSafeArea");
            menuInteraction=safe.GetComponent<CanvasGroup>();
            if(!menuInteraction) menuInteraction=safe.gameObject.AddComponent<CanvasGroup>();
            friendsButton.onClick.AddListener(OpenFriends);
            leaderboardButton.onClick.AddListener(OpenLeaderboard);
            friendsTab.onClick.AddListener(OpenFriends);
            leaderboardTab.onClick.AddListener(OpenLeaderboard);
            listButton.onClick.AddListener(()=>SetDirectory(false));
            directoryButton.onClick.AddListener(()=>SetDirectory(true));
            myPositionButton.onClick.AddListener(ShowMyPosition);
            closeButton.onClick.AddListener(Close);
            search.onValueChanged.AddListener(_=> { feedback.text="";searchAt=Time.unscaledTime+.3f; });
            ProgressionRuntime.Changed+=Refresh;
            modal.gameObject.SetActive(false);
        }
        void OnDestroy() { ProgressionRuntime.Changed-=Refresh;PickleVerse.Settings.FrontEndModalInput.Release(this); }
        void OnDisable()
        {
            fetchGeneration++;page=new OnlinePage();
            if(!opened)return;
            opened=false;drawerProgress=0;PickleVerse.Settings.FrontEndModalInput.Release(this);
            if(menuInteraction)menuInteraction.interactable=priorInteraction;
            if(modal)modal.gameObject.SetActive(false);
        }
        void Update()
        {
            if(!opened&&(!drawerMode||drawerProgress<=0)) return;
            ApplySafeArea();
            if(drawerMode)
            {
                float next=Mathf.MoveTowards(drawerProgress,opened?1:0,Time.unscaledDeltaTime/.24f);
                if(next!=drawerProgress){drawerProgress=next;PlaceDrawer();}
                if(!opened&&drawerProgress<=0)modal.gameObject.SetActive(false);
            }
            if(opened&&Keyboard.current?.escapeKey.wasPressedThisFrame==true) Close();
            if(opened&&searchAt>=0&&Time.unscaledTime>=searchAt){searchAt=-1;_=FetchAsync(0);}
        }
        public void OpenFriends() { Open(false); }
        public void OpenLeaderboard() { Open(true); }
        void Open(bool ranking)
        {
            if(!opened)
            {
                ui.Close();
                if(!PickleVerse.Settings.FrontEndModalInput.TryAcquire(this))return;
                priorSelection=EventSystem.current?.currentSelectedGameObject;
                priorInteraction=menuInteraction.interactable;
                menuInteraction.interactable=false;
                opened=true;
            }
            leaderboard=ranking; directory=false;
            search.SetTextWithoutNotify(""); feedback.text="";
            modal.gameObject.SetActive(true); modal.SetAsLastSibling(); ConfigureOverlay(); ApplySafeArea();ConfigureDrawerLayout(); Refresh();
            EventSystem.current?.SetSelectedGameObject(closeButton.gameObject);
            _=FetchAsync(0);
        }
        public void Close()
        {
            if(!opened) return;
            opened=false;
            fetchGeneration++;loading=false;
            if(!drawerMode)modal.gameObject.SetActive(false);
            PickleVerse.Settings.FrontEndModalInput.Release(this);
            if(menuInteraction) menuInteraction.interactable=priorInteraction;
            if(EventSystem.current && priorSelection && priorSelection.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(priorSelection);
        }
        void ApplySafeArea()
        {
            var area=Screen.safeArea;
            if(Screen.width<=0 || Screen.height<=0) return;
            var size=new Vector2(Screen.width,Screen.height);
            if(area==lastDrawerSafe&&size==lastDrawerScreen)return;
            lastDrawerSafe=area;lastDrawerScreen=size;
            modalSafe.anchorMin=new Vector2(area.xMin/Screen.width,area.yMin/Screen.height);
            modalSafe.anchorMax=new Vector2(area.xMax/Screen.width,area.yMax/Screen.height);
            modalSafe.offsetMin=modalSafe.offsetMax=Vector2.zero;
        }
        public void ConfigureDrawerLayout()
        {
            if(!drawerMode||!window||!modalSafe)return;
            var canvas=ui.GetComponent<Canvas>();
            bool touch=Application.isMobilePlatform||Touchscreen.current!=null;
            float dp=touch?HomeMenuShell.DpUnits(DeviceDpi,Mathf.Min(Screen.width,Screen.height),canvas.scaleFactor):1;
            window.anchorMin=window.anchorMax=new Vector2(0,.5f);window.pivot=new Vector2(0,.5f);
            window.sizeDelta=new Vector2(Mathf.Min(modalSafe.rect.width-24,Mathf.Max(540,270*dp)),modalSafe.rect.height-32);
            float target=touch?Mathf.Max(44,44*dp):44;
            float factor=touch?Mathf.Max(1,dp*.75f):1;
            Top((RectTransform)friendsTab.transform,104,target);Top((RectTransform)leaderboardTab.transform,104,target);
            var close=(RectTransform)closeButton.transform;close.anchorMin=close.anchorMax=Vector2.one;close.pivot=Vector2.one;
            close.sizeDelta=Vector2.one*target;close.anchoredPosition=new Vector2(-12,-12);
            Top((RectTransform)selfName.transform.parent,118+target,90);
            float searchTop=220+target;
            foreach(var r in new[]{(RectTransform)search.transform,(RectTransform)listButton.transform,(RectTransform)directoryButton.transform,(RectTransform)myPositionButton.transform})Top(r,searchTop,target);
            var scrollRoot=(RectTransform)scroll.transform;float top=searchTop+target+14;
            scrollRoot.anchorMin=new Vector2(.02f,0);scrollRoot.anchorMax=new Vector2(.98f,1);scrollRoot.offsetMin=new Vector2(0,42);scrollRoot.offsetMax=new Vector2(0,-top);
            Top(count.rectTransform,75,22);count.rectTransform.anchorMin=new Vector2(.77f,1);count.rectTransform.anchorMax=new Vector2(.98f,1);
            title.fontSizeMax=28*factor;selfName.fontSizeMax=22*factor;selfId.fontSizeMax=13*factor;selfElo.fontSizeMax=18*factor;selfPosition.fontSizeMax=16*factor;
            foreach(var button in new[]{friendsTab,leaderboardTab,listButton,directoryButton,myPositionButton})button.GetComponentInChildren<TMP_Text>().fontSizeMax=16*factor;
            search.textComponent.fontSizeMax=17*factor;
            ((TMP_Text)search.placeholder).fontSizeMax=15*factor;
            PlaceDrawer();
        }
        static void Top(RectTransform rect,float top,float height)
        {
            rect.anchorMin=new Vector2(rect.anchorMin.x,1);rect.anchorMax=new Vector2(rect.anchorMax.x,1);
            rect.offsetMin=new Vector2(rect.offsetMin.x,-top-height);rect.offsetMax=new Vector2(rect.offsetMax.x,-top);
        }
        void PlaceDrawer()
        {
            float eased=Mathf.SmoothStep(0,1,drawerProgress);
            window.anchoredPosition=new Vector2(12-(window.rect.width+24)*(1-eased),0);
            if(drawerShade){var c=drawerShade.color;c.a=.36f*eased;drawerShade.color=c;}
        }
        void SetDirectory(bool value)
        { directory=value; search.SetTextWithoutNotify(""); feedback.text="";_=FetchAsync(0); }
        async Task FetchAsync(int offset,bool focusSelf=false)
        {
            int generation=++fetchGeneration;loading=true;
            if(offset==0)page=new OnlinePage();Refresh();
            try
            {
                string view=leaderboard?"leaderboard":directory?"directory":"friends";
                var result=await AccountRuntime.Ensure().Account.SendAuthorizedAsync<OnlinePage>("GET","api/Game/social?view="+view+"&q="+Uri.EscapeDataString(search.text)+"&offset="+offset);
                if(!this||!opened||generation!=fetchGeneration)return;
                foreach(var player in result.players)player.id=Guid.Parse(player.id).ToString("N");
                if(offset>0&&!focusSelf)result.players=page.players.Concat(result.players).GroupBy(p=>p.id).Select(g=>g.Last()).ToList();
                page=result;loading=false;Refresh();
                if(focusSelf)FocusSelf();
            }
            catch(Exception error){if(this&&opened&&generation==fetchGeneration){loading=false;Refresh();feedback.text=error.Message;feedback.richText=false;}}
        }
        public void Refresh()
        {
            if(!opened || !ProgressionRuntime.Exists) return;
            var player=ProgressionRuntime.Instance.Player;
            title.text=leaderboard?"BẢNG XẾP HẠNG":"BẠN BÈ";
            selfName.text=player.playerName; selfId.text="ID  "+player.id;
            selfElo.text=EloPolicy.Rank(player.elo)+"\n"+player.elo.ToString("N0")+" ELO";
            selfPosition.text=page.myPosition>0?$"VỊ TRÍ CỦA BẠN\n#{page.myPosition} / {page.playerCount}":"Đang tải vị trí…";
            Select(friendsTab,!leaderboard); Select(leaderboardTab,leaderboard);
            listButton.gameObject.SetActive(!leaderboard); directoryButton.gameObject.SetActive(!leaderboard);
            myPositionButton.gameObject.SetActive(leaderboard);
            Select(listButton,!directory); Select(directoryButton,directory);
            ClearRows(); selfRow=null;
            if(leaderboard)
            {
                count.text=$"{page.total} người chơi  ·  Elo giảm dần";
                foreach(var entry in page.players) AddPlayer(new SocialPlayer(entry.id,entry.name,entry.elo),entry.position,entry.id==player.id);
            }
            else
            {
                count.text=directory?$"{page.total} người chơi  ·  {page.friendCount} bạn":$"{page.friendCount} bạn  ·  {page.total} mục";
                foreach(var entry in page.players) AddPlayer(new SocialPlayer(entry.id,entry.name,entry.elo),0,false);
            }
            if(page.hasMore&&!loading)
            {
                var more=Row("LoadMore",55,Card);var button=Button("LoadMoreButton",more,"XEM THÊM",Vector2.zero,Vector2.one,Surface,Mint,16);
                button.onClick.AddListener(()=>_=FetchAsync(page.offset+50));
            }
            if(content.childCount==0)
            {
                var empty=Row("Empty",88,Card);
                Caption("EmptyText",empty,loading?"Đang tải danh sách…":string.IsNullOrWhiteSpace(search.text)?"Danh sách đang trống. Bấm TÌM BẠN để gửi lời mời.":"Không tìm thấy tên hoặc ID phù hợp.",17,Muted,Vector2.zero,Vector2.one);
            }
            UnityEngine.Canvas.ForceUpdateCanvases(); UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.verticalNormalizedPosition=1;
        }
        void AddPlayer(SocialPlayer player,int position,bool self)
        {
            bool touch=Application.isMobilePlatform||Touchscreen.current!=null;
            float dp=touch?HomeMenuShell.DpUnits(DeviceDpi,Mathf.Min(Screen.width,Screen.height),ui.GetComponent<Canvas>().scaleFactor):1;
            var row=Row(player.Id,drawerMode?Mathf.Max(90,64*dp):82,self?new Color(.10f,.25f,.27f):Card);
            if(self) selfRow=row;
            Caption("Position",row,leaderboard?$"#{position}":"•",21,position<=3 && leaderboard?Lime:Mint,new Vector2(.01f,0),new Vector2(.08f,1));
            var face=ui.Panel("Avatar",row,self?Mint:Surface,new Vector2(.09f,.16f),new Vector2(.15f,.84f));
            Caption("Initial",face,player.Name.Substring(0,1).ToUpperInvariant(),23,self?Ink:Mint,Vector2.zero,Vector2.one);
            var name=Caption("Name",row,player.Name+(self?"  (BẠN)":""),20,self?Lime:White,new Vector2(.17f,.45f),new Vector2(.58f,.96f),TextAlignmentOptions.MidlineLeft);
            name.richText=false;
            Caption("Id",row,player.Id,13,Muted,new Vector2(.17f,.04f),new Vector2(.58f,.43f),TextAlignmentOptions.MidlineLeft).richText=false;
            Caption("Rank",row,EloPolicy.Rank(player.Elo),17,Mint,new Vector2(.59f,.47f),new Vector2(.78f,.94f));
            Caption("Elo",row,player.Elo.ToString("N0")+" ELO",16,White,new Vector2(.59f,.05f),new Vector2(.78f,.49f));
            if(leaderboard)
                Caption("Tag",row,self?"BẠN":"",12,self?Lime:Muted,new Vector2(.80f,0),new Vector2(.98f,1));
            else
            {
                var entry=page.players.Single(p=>p.id==player.Id);string relation=entry.relationship;
                string label=relation=="friend"?"XÓA BẠN":relation=="outgoing"?"ĐÃ GỬI":relation=="incoming"?"ĐỒNG Ý":"KẾT BẠN";
                var action=Button("FriendAction",row,label,new Vector2(.80f,relation=="incoming"?.48f:drawerMode?.15f:.23f),new Vector2(.975f,drawerMode?.85f:.83f),Mint,Ink,14);
                action.interactable=!mutating&&relation!="outgoing";
                action.onClick.AddListener(()=>ChangeFriend(entry,relation=="friend"?"remove":relation=="incoming"?"accept":"request"));
                if(relation=="incoming")
                {
                    var decline=Button("Decline",row,"TỪ CHỐI",new Vector2(.80f,.08f),new Vector2(.975f,.43f),Surface,Muted,12);
                    decline.interactable=!mutating;decline.onClick.AddListener(()=>ChangeFriend(entry,"decline"));
                }
            }
        }
        async void ChangeFriend(OnlinePlayer player,string action)
        {
            if(mutating)return;mutating=true;Refresh();
            try
            {
                await AccountRuntime.Ensure().Account.SendAuthorizedAsync<PickleVerse.Services.Api.ApiMessage>("POST","api/Game/social",new{PlayerId=player.id,Action=action,RequestId=player.requestId});
                await FetchAsync(0);
                if(!this)return;
                feedback.text=action=="request"?"Đã gửi lời mời kết bạn.":"Danh sách đã được cập nhật.";
                feedback.richText=false;
            }
            catch(Exception e) { if(this)feedback.text=e.Message; }
            finally{if(this){mutating=false;Refresh();}}
        }
        public void ShowMyPosition()
        {
            search.SetTextWithoutNotify("");_=FetchAsync(Math.Max(0,(page.myPosition-1)/50*50),true);
        }
        void FocusSelf()
        {
            if(!selfRow) return;
            float overflow=Mathf.Max(0,content.rect.height-scroll.viewport.rect.height);
            float top=-selfRow.anchoredPosition.y-selfRow.rect.height*.5f;
            scroll.verticalNormalizedPosition=overflow<=0?1:1-Mathf.Clamp01((top-(scroll.viewport.rect.height-selfRow.rect.height)*.5f)/overflow);
        }
        void ClearRows()
        {
            foreach(Transform row in content.Cast<Transform>().ToArray())
            { row.gameObject.SetActive(false); row.SetParent(null); if(Application.isPlaying) Destroy(row.gameObject); else DestroyImmediate(row.gameObject); }
        }
        RectTransform Row(string name,float height,Color color)
        {
            var row=ui.Panel(name,content,color);
            var layout=row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>(); layout.minHeight=layout.preferredHeight=height;
            return row;
        }
        TMP_Text Caption(string name,Transform parent,string value,int size,Color color,Vector2 min,Vector2 max,TextAlignmentOptions align=TextAlignmentOptions.Center)
        {
            if(drawerMode&&(Application.isMobilePlatform||Touchscreen.current!=null))size=Mathf.RoundToInt(size*Mathf.Max(1,HomeMenuShell.DpUnits(DeviceDpi,Mathf.Min(Screen.width,Screen.height),ui.GetComponent<Canvas>().scaleFactor)*.75f));
            var text=ui.Text(name,parent,value,size,color,min,max,new Vector2(6,0),new Vector2(-6,0),align);
            text.enableAutoSizing=true; text.fontSizeMin=11; text.fontSizeMax=size;
            return text;
        }
        UnityEngine.UI.Button Button(string name,Transform parent,string value,Vector2 min,Vector2 max,Color color,Color textColor,int size=16)
        {
            var button=ui.MakeButton(name,parent,value,()=>{},min,max,color,textColor,size);
            var text=button.GetComponentInChildren<TMP_Text>(); text.enableAutoSizing=true; text.fontSizeMin=11; text.fontSizeMax=size;
            return button;
        }
        static void Select(UnityEngine.UI.Button button,bool selected)
        { button.GetComponent<UnityEngine.UI.Image>().color=selected?Mint:Card; button.GetComponentInChildren<TMP_Text>().color=selected?Ink:White; }

        // Author once through Unity APIs; reuse these bindings in both Editor and Player builds.
        public void BuildShell()
        {
            ui=GetComponent<PlayerProgressionMenu>();
            modal=ui.Panel("SocialModal",transform,new Color(0,0,0,.75f));
            modalSafe=ui.Rect("SafeArea",modal,Vector2.zero,Vector2.one);
            window=ui.Panel("Window",modalSafe,Ink,new Vector2(.045f,.065f),new Vector2(.955f,.935f));
            var outline=window.gameObject.AddComponent<UnityEngine.UI.Outline>(); outline.effectColor=new Color(.45f,.86f,.77f,.3f); outline.effectDistance=new Vector2(1,-1);
            title=Caption("Title",window,"BẠN BÈ",29,White,new Vector2(.02f,.90f),new Vector2(.85f,.985f),TextAlignmentOptions.MidlineLeft);
            Caption("Subtitle",window,"Bạn bè và thứ hạng được cập nhật từ tài khoản online",13,Mint,new Vector2(.02f,.855f),new Vector2(.90f,.905f),TextAlignmentOptions.MidlineLeft);
            closeButton=Button("Close",window,"×",new Vector2(.925f,.895f),new Vector2(.98f,.977f),Card,White,28);
            friendsTab=Button("FriendsTab",window,"BẠN BÈ",new Vector2(.02f,.77f),new Vector2(.25f,.845f),Mint,Ink);
            leaderboardTab=Button("LeaderboardTab",window,"BẢNG XẾP HẠNG",new Vector2(.265f,.77f),new Vector2(.51f,.845f),Card,White);
            count=Caption("Count",window,"",14,Muted,new Vector2(.52f,.77f),new Vector2(.98f,.845f),TextAlignmentOptions.MidlineRight);
            var identity=ui.Panel("CurrentPlayer",window,Surface,new Vector2(.02f,.60f),new Vector2(.98f,.75f));
            selfName=Caption("Name",identity,"PLAYER",22,White,new Vector2(.01f,.44f),new Vector2(.50f,.96f),TextAlignmentOptions.MidlineLeft); selfName.richText=false;
            selfId=Caption("Id",identity,"ID",13,Muted,new Vector2(.01f,.05f),new Vector2(.50f,.44f),TextAlignmentOptions.MidlineLeft); selfId.richText=false;
            selfElo=Caption("Elo",identity,"",19,Mint,new Vector2(.51f,.05f),new Vector2(.73f,.95f));
            selfPosition=Caption("Position",identity,"",19,Lime,new Vector2(.75f,.05f),new Vector2(.99f,.95f));
            var input=ui.Panel("Search",window,Surface,new Vector2(.02f,.51f),new Vector2(.51f,.58f));
            search=input.gameObject.AddComponent<TMP_InputField>(); search.characterLimit=40; search.targetGraphic=input.GetComponent<UnityEngine.UI.Image>();
            var viewport=ui.Rect("TextArea",input,Vector2.zero,Vector2.one); viewport.offsetMin=new Vector2(12,3); viewport.offsetMax=new Vector2(-12,-3); viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            search.textComponent=Caption("Text",viewport,"",17,White,Vector2.zero,Vector2.one,TextAlignmentOptions.MidlineLeft);
            search.textComponent.richText=false; search.textViewport=viewport;
            search.placeholder=Caption("Placeholder",viewport,"Tìm theo tên hoặc ID…",15,Muted,Vector2.zero,Vector2.one,TextAlignmentOptions.MidlineLeft);
            search.caretColor=Mint; search.customCaretColor=true;
            listButton=Button("MyFriends",window,"BẠN CỦA TÔI",new Vector2(.53f,.51f),new Vector2(.745f,.58f),Mint,Ink,14);
            directoryButton=Button("FindFriends",window,"TÌM BẠN",new Vector2(.76f,.51f),new Vector2(.98f,.58f),Card,White,14);
            myPositionButton=Button("MyPosition",window,"VỊ TRÍ CỦA TÔI",new Vector2(.70f,.51f),new Vector2(.98f,.58f),Lime,Ink,14);
            var scrollRoot=ui.Rect("Scroll",window,new Vector2(.02f,.095f),new Vector2(.98f,.49f));
            scroll=scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>(); scroll.horizontal=false; scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=32;
            var mask=ui.Rect("Viewport",scrollRoot,Vector2.zero,Vector2.one); mask.offsetMax=new Vector2(-12,0);
            mask.gameObject.AddComponent<UnityEngine.UI.Image>(); mask.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;
            content=ui.Rect("Content",mask,new Vector2(0,1),Vector2.one); content.pivot=new Vector2(.5f,1);
            var layout=content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>(); layout.spacing=8; layout.childControlHeight=layout.childControlWidth=true; layout.childForceExpandHeight=false; layout.childForceExpandWidth=true;
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=mask; scroll.content=content;
            var track=ui.Panel("Scrollbar",scrollRoot,Surface,new Vector2(.99f,0),Vector2.one);
            var bar=track.gameObject.AddComponent<UnityEngine.UI.Scrollbar>(); bar.direction=UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            var sliding=ui.Rect("Sliding",track,Vector2.zero,Vector2.one); var handle=ui.Panel("Handle",sliding,Mint);
            bar.handleRect=handle; bar.targetGraphic=handle.GetComponent<UnityEngine.UI.Image>(); scroll.verticalScrollbar=bar;
            feedback=Caption("Feedback",window,"",14,Mint,new Vector2(.02f,.015f),new Vector2(.98f,.085f),TextAlignmentOptions.MidlineLeft);
            modal.gameObject.SetActive(false);
        }
        public void ConfigureOverlay()
        {
            // Music's persistent UI uses order 500. Keep the modal and its input blocker above it.
            var canvas=modal.GetComponent<Canvas>();
            if(!canvas) canvas=modal.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting=true; canvas.sortingOrder=600;
            if(!modal.GetComponent<UnityEngine.UI.GraphicRaycaster>()) modal.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }
        public void ConfigureWelcomeCard()
        {
            ui=GetComponent<PlayerProgressionMenu>();
            var card=transform.Find("HomeSafeArea/Body/WelcomeCard");
            Place(card.Find("WelcomeTitle"),.075f,.61f,.95f,.86f);
            var heading=card.Find("WelcomeTitle").GetComponent<TMP_Text>(); heading.enableAutoSizing=true; heading.fontSizeMin=24; heading.fontSizeMax=40;
            Place(card.Find("WelcomeBody"),.075f,.445f,.94f,.60f);
            var body=card.Find("WelcomeBody").GetComponent<TMP_Text>(); body.enableAutoSizing=true; body.fontSizeMin=12; body.fontSizeMax=16;
            Place(card.Find("BtnPlay"),.075f,.27f,.625f,.43f);
            Place(ui.missionsButton.transform,.645f,.27f,.925f,.43f);
            card.Find("PlayHint").gameObject.SetActive(false);
            var friends=card.Find("BtnFriends");
            friendsButton=friends?friends.GetComponent<UnityEngine.UI.Button>():Button("BtnFriends",card,"BẠN BÈ",new Vector2(.075f,.075f),new Vector2(.485f,.235f),Card,Mint,15);
            var ranking=card.Find("BtnLeaderboard");
            leaderboardButton=ranking?ranking.GetComponent<UnityEngine.UI.Button>():Button("BtnLeaderboard",card,"BXH ELO",new Vector2(.515f,.075f),new Vector2(.925f,.235f),Card,Lime,15);
        }
        static void Place(Transform target,float xMin,float yMin,float xMax,float yMax)
        { var rect=(RectTransform)target; rect.anchorMin=new Vector2(xMin,yMin); rect.anchorMax=new Vector2(xMax,yMax); rect.offsetMin=rect.offsetMax=Vector2.zero; }
    }
}
