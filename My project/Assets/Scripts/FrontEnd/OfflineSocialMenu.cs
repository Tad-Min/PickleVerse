using System;
using System.Linq;
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
        bool leaderboard, directory, opened, priorInteraction;
        CanvasGroup menuInteraction;
        GameObject priorSelection;
        static readonly Color Ink=new Color(.025f,.065f,.095f), Surface=new Color(.045f,.115f,.15f), Card=new Color(.075f,.16f,.20f), Mint=new Color(.45f,.86f,.77f), Lime=new Color(.79f,.96f,.45f), White=new Color(.94f,.97f,.96f), Muted=new Color(.63f,.75f,.79f);
        OfflineSocialDirectory Data => OfflineSocialSession.Directory;

        void Awake()
        {
            ui=GetComponent<PlayerProgressionMenu>();
            if(!friendsButton || !leaderboardButton) ConfigureWelcomeCard();
            if(!modal) BuildShell();
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
            search.onValueChanged.AddListener(_=> { feedback.text=""; Refresh(); });
            ProgressionRuntime.Changed+=Refresh;
            modal.gameObject.SetActive(false);
        }
        void OnDestroy() { ProgressionRuntime.Changed-=Refresh; }
        void Update()
        {
            if(!opened) return;
            ApplySafeArea();
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true) Close();
        }
        public void OpenFriends() { Open(false); }
        public void OpenLeaderboard() { Open(true); }
        void Open(bool ranking)
        {
            if(!opened)
            {
                ui.Close();
                priorSelection=EventSystem.current?.currentSelectedGameObject;
                priorInteraction=menuInteraction.interactable;
                menuInteraction.interactable=false;
                opened=true;
            }
            leaderboard=ranking; directory=false;
            search.SetTextWithoutNotify(""); feedback.text="";
            modal.gameObject.SetActive(true); modal.SetAsLastSibling(); ConfigureOverlay(); ApplySafeArea(); Refresh();
            EventSystem.current?.SetSelectedGameObject(closeButton.gameObject);
        }
        public void Close()
        {
            if(!opened) return;
            opened=false; modal.gameObject.SetActive(false);
            if(menuInteraction) menuInteraction.interactable=priorInteraction;
            if(EventSystem.current && priorSelection && priorSelection.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(priorSelection);
        }
        void ApplySafeArea()
        {
            var area=Screen.safeArea;
            if(Screen.width<=0 || Screen.height<=0) return;
            modalSafe.anchorMin=new Vector2(area.xMin/Screen.width,area.yMin/Screen.height);
            modalSafe.anchorMax=new Vector2(area.xMax/Screen.width,area.yMax/Screen.height);
            modalSafe.offsetMin=modalSafe.offsetMax=Vector2.zero;
        }
        void SetDirectory(bool value)
        { directory=value; search.SetTextWithoutNotify(""); feedback.text=""; Refresh(); }
        public void Refresh()
        {
            if(!opened || !ProgressionRuntime.Exists) return;
            var player=ProgressionRuntime.Instance.Player;
            var all=Data.Leaderboard(player);
            title.text=leaderboard?"BẢNG XẾP HẠNG":"BẠN BÈ";
            selfName.text=player.playerName; selfId.text="ID  "+player.id;
            selfElo.text=EloPolicy.Rank(player.elo)+"\n"+player.elo.ToString("N0")+" ELO";
            selfPosition.text=$"VỊ TRÍ CỦA BẠN\n#{all.Single(e=>e.Player.Id==player.id).Position} / {all.Count}";
            Select(friendsTab,!leaderboard); Select(leaderboardTab,leaderboard);
            listButton.gameObject.SetActive(!leaderboard); directoryButton.gameObject.SetActive(!leaderboard);
            myPositionButton.gameObject.SetActive(leaderboard);
            Select(listButton,!directory); Select(directoryButton,directory);
            ClearRows(); selfRow=null;
            if(leaderboard)
            {
                var entries=Data.Leaderboard(player,search.text);
                count.text=$"{entries.Count} / {all.Count} người chơi  ·  Elo giảm dần";
                foreach(var entry in entries) AddPlayer(entry.Player,entry.Position,entry.Player.Id==player.id);
            }
            else
            {
                var players=directory?Data.Search(search.text):Data.GetFriends(player.id,search.text);
                count.text=directory?$"{players.Count} người mẫu  ·  {Data.FriendCount(player.id)} bạn":$"{players.Count} / {Data.FriendCount(player.id)} bạn";
                foreach(var friend in players) AddPlayer(friend,0,false);
            }
            if(content.childCount==0)
            {
                var empty=Row("Empty",88,Card);
                Caption("EmptyText",empty,string.IsNullOrWhiteSpace(search.text)?"Danh sách bạn đang trống. Bấm TÌM BẠN để thêm người mẫu.":"Không tìm thấy tên hoặc ID phù hợp.",17,Muted,Vector2.zero,Vector2.one);
            }
            UnityEngine.Canvas.ForceUpdateCanvases(); UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.verticalNormalizedPosition=1;
        }
        void AddPlayer(SocialPlayer player,int position,bool self)
        {
            var row=Row(player.Id,82,self?new Color(.10f,.25f,.27f):Card);
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
                Caption("Tag",row,self?"BẠN":"DEMO",12,self?Lime:Muted,new Vector2(.80f,0),new Vector2(.98f,1));
            else
            {
                bool added=Data.IsFriend(ProgressionRuntime.Instance.Player.id,player.Id);
                var action=Button("FriendAction",row,added?(directory?"ĐÃ KẾT BẠN":"XÓA BẠN"):"THÊM BẠN",new Vector2(.80f,.23f),new Vector2(.975f,.77f),added?Surface:Mint,added?Muted:Ink,14);
                action.interactable=!directory || !added;
                action.onClick.AddListener(()=>ChangeFriend(player,!added));
            }
        }
        void ChangeFriend(SocialPlayer player,bool add)
        {
            try
            {
                string id=ProgressionRuntime.Instance.Player.id;
                bool changed=add?Data.AddFriend(id,player.Id):Data.RemoveFriend(id,player.Id);
                Refresh();
                feedback.text=changed?(add?"Đã thêm ":"Đã xóa ")+player.Name+(add?" vào danh sách bạn.":" khỏi danh sách bạn."):"Danh sách đã được cập nhật.";
                feedback.richText=false;
            }
            catch(Exception e) { feedback.text=e.Message; }
        }
        public void ShowMyPosition()
        {
            search.SetTextWithoutNotify(""); Refresh();
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
            Caption("Subtitle",window,"DEMO OFFLINE  ·  Dữ liệu mẫu và danh sách bạn reset khi chạy lại game",13,Mint,new Vector2(.02f,.855f),new Vector2(.90f,.905f),TextAlignmentOptions.MidlineLeft);
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
