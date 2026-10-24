using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PickleVerse.Multiplayer.Services;
using PickleVerse.Multiplayer.UI;
using PickleVerse.Progression;
using PickleVerse.Services;
using PickleVerse.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using static PickleVerse.Multiplayer.MatchPresentationUi;

namespace PickleVerse.Multiplayer
{
    /// <summary>Authenticated lobby presence and incoming room invitations survive front-end scene changes.</summary>
    public sealed class RoomInvitationsRuntime:MonoBehaviour
    {
        public static RoomInvitationsRuntime Instance{get;private set;}
        public RoomInvitationClient Client{get;private set;}
        public RoomInvitation Current{get;private set;}
        public string Error{get;private set;}="";
        AccountService account;
        string sessionId;
        CancellationTokenSource lifetime;
        readonly SemaphoreSlim publication=new SemaphoreSlim(1,1);
        RoomInvitation[] inbox=Array.Empty<RoomInvitation>();
        bool polling,responding,joining,joinFailed;
        float nextPoll;
        RectTransform root;
        TMP_Text sender,details,feedback,timer;
        UnityEngine.UI.Button accept,decline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){Instance=null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Boot()
        {
            var account=AccountRuntime.Ensure();
            if(!account.GetComponent<RoomInvitationsRuntime>())account.gameObject.AddComponent<RoomInvitationsRuntime>();
        }
        void Awake()
        {
            if(Instance&&Instance!=this){Destroy(this);return;}
            Instance=this;account=AccountRuntime.Ensure().Account;Client=new RoomInvitationClient(account);
            lifetime=new CancellationTokenSource();account.Changed+=AccountChanged;SceneManager.sceneLoaded+=SceneLoaded;AccountChanged();
        }
        void AccountChanged(){sessionId=Guid.NewGuid().ToString("N");inbox=Array.Empty<RoomInvitation>();Current=null;Error="";joinFailed=false;nextPoll=0;if(root)root.gameObject.SetActive(false);}
        void SceneLoaded(Scene scene,LoadSceneMode mode){nextPoll=0;}
        void OnDestroy(){if(account!=null)account.Changed-=AccountChanged;SceneManager.sceneLoaded-=SceneLoaded;lifetime?.Cancel();lifetime?.Dispose();if(Instance==this)Instance=null;}
        public static bool IsLobbyScene(string scene)=>scene=="Menu"||scene=="ModeSelection"||scene=="PlayMultiplayer";
        public bool Available
        {
            get
            {
                if(!account.IsSignedIn||!ProgressionRuntime.Exists||!ProgressionRuntime.Instance.Ready||joining||SceneLoading.IsBlocking||!IsLobbyScene(SceneManager.GetActiveScene().name))return false;
                var app=MultiplayerApp.Instance;
                return !app||!app.Busy&&!app.Searching&&(app.State==FlowState.Menu||app.State==FlowState.WaitingRoom);
            }
        }
        LobbyPresenceRequest Presence()
        {
            var app=MultiplayerApp.Instance;var room=app?.Room;
            return new LobbyPresenceRequest{SessionId=sessionId,State=Available?"lobby":"busy",RoomCode=room?.Code??"",Region=room?.Region??"",
                IsHost=room?.IsHost==true,Custom=app&&app.Custom,Mode=app?(int)app.Mode:1,
                Members=room==null?Array.Empty<string>():app.Members().Select(m=>m.id).ToArray()};
        }
        public async Task PublishNowAsync()
        {
            await publication.WaitAsync(lifetime.Token);
            try
            {
                if(!account.IsSignedIn)return;
                int version=account.SessionVersion;
                var reply=await Client.PublishAsync(Presence(),lifetime.Token);
                if(!this||version!=account.SessionVersion)return;
                if(!joinFailed&&!responding)Error="";
                inbox=reply.Invitations??Array.Empty<RoomInvitation>();
                if(Current==null||!responding&&!joinFailed)Current=inbox.FirstOrDefault();
                nextPoll=Time.unscaledTime+Mathf.Max(1,reply.PollSeconds);
            }
            finally{publication.Release();}
        }
        async Task Poll()
        {
            polling=true;
            try{await PublishNowAsync();}
            catch(OperationCanceledException){ }
            catch(Exception error){if(this)Error=error.Message;}
            finally{polling=false;}
        }
        void Update()
        {
            if(account.IsSignedIn&&!polling&&Time.unscaledTime>=nextPoll){nextPoll=Time.unscaledTime+3;_=Poll();}
            if(Current!=null&&Current.ExpiresAt<=DateTimeOffset.UtcNow.ToUnixTimeSeconds()&&!responding){Current=null;joinFailed=false;}
            bool visible=Current!=null&&(Available||responding);
            if(!root){if(!visible)return;Build();}
            root.gameObject.SetActive(visible);if(!visible)return;
            sender.text=Current.SenderName+" mời bạn vào phòng";
            details.text=(Current.Custom?"CUSTOM":"ĐẤU ĐÔI ONLINE")+"  •  "+(Current.Mode==1?"1vs1":"2vs2");
            timer.text="Còn "+Math.Max(0,Current.ExpiresAt-DateTimeOffset.UtcNow.ToUnixTimeSeconds())+" giây";
            feedback.text=Error;accept.interactable=decline.interactable=!responding;
            accept.GetComponentInChildren<TMP_Text>().text=responding?"ĐANG VÀO PHÒNG…":joinFailed?"THỬ LẠI":"CHẤP NHẬN";
        }
        void Build()
        {
            var font=ScreenUiTheme.Current?ScreenUiTheme.Current.font:TMP_Settings.defaultFontAsset;
            root=Rect("Room Invitation Notification",transform,Vector2.zero,Vector2.one);
            var canvas=root.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1000;
            var scaler=root.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            root.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var safe=Rect("SafeArea",root,Vector2.zero,Vector2.one);safe.gameObject.AddComponent<UiSafeArea>();
            var card=Image("Invitation",safe,new Vector2(.56f,.59f),new Vector2(.98f,.96f),LobbyPanelStyle.Surface,true).rectTransform;
            LobbyPanelStyle.SurfaceStyle(card,true);
            Label("Title",card,font,"LỜI MỜI VÀO PHÒNG",18,new Vector2(.05f,.82f),new Vector2(.95f,.97f),ScreenUiTheme.Gold);
            sender=Label("Sender",card,font,"",22,new Vector2(.05f,.60f),new Vector2(.95f,.82f),ScreenUiTheme.Text);sender.richText=false;
            details=Label("Mode",card,font,"",15,new Vector2(.05f,.47f),new Vector2(.95f,.60f),ScreenUiTheme.Mint);
            timer=Label("Expiry",card,font,"",13,new Vector2(.05f,.36f),new Vector2(.95f,.47f),ScreenUiTheme.Muted);
            accept=Button("Accept Room Invitation",card,font,"CHẤP NHẬN",new Vector2(.05f,.12f),new Vector2(.65f,.32f),true);
            decline=Button("Decline Room Invitation",card,font,"TỪ CHỐI",new Vector2(.68f,.12f),new Vector2(.95f,.32f),false);
            ScreenUiTheme.ButtonStyle(accept,true);ScreenUiTheme.ButtonStyle(decline);
            foreach(var b in new[]{accept,decline})b.GetComponentInChildren<TMP_Text>().fontSize=16;
            feedback=Label("Feedback",card,font,"",12,new Vector2(.03f,.005f),new Vector2(.97f,.11f),ScreenUiTheme.Gold);feedback.richText=false;
            accept.onClick.AddListener(()=>_=RespondAsync(true));decline.onClick.AddListener(()=>_=RespondAsync(false));
        }
        public async Task RespondAsync(bool accepted)
        {
            if(responding||Current==null)return;
            var invitation=Current;int version=account.SessionVersion;responding=true;Error="";
            try
            {
                if(!accepted&&invitation.State=="accepted"){Current=null;joinFailed=false;return;}
                await PublishNowAsync();
                var result=await Client.RespondAsync(invitation.Id,accepted,lifetime.Token);
                if(!this||version!=account.SessionVersion)return;
                if(!accepted){Current=null;joinFailed=false;return;}
                Current=result;joining=true;
                await JoinAsync(result);
                if(!this||version!=account.SessionVersion)return;
                Current=null;joinFailed=false;nextPoll=0;
            }
            catch(OperationCanceledException){ }
            catch(Exception error){if(this&&version==account.SessionVersion){Error=error.Message;joinFailed=Current?.State=="accepted";}}
            finally{responding=joining=false;}
        }
        async Task JoinAsync(RoomInvitation invitation)
        {
            var app=MultiplayerApp.Instance;
            if(app&&app.RoomCode==invitation.RoomCode)return;
            FindAnyObjectByType<PlayMultiplayerPresenter>()?.CloseInvite();
            if(SceneManager.GetActiveScene().name=="PlayMultiplayer")
            {
                bool joined=false;
                await MultiplayerApp.Ensure().ExecuteAsync(async()=>{await MultiplayerApp.Instance.JoinRoomAsync(invitation.RoomCode,invitation.Region);joined=MultiplayerApp.Instance.RoomCode==invitation.RoomCode;});
                if(!joined)throw new InvalidOperationException(MultiplayerApp.Instance.Notice);
                return;
            }
            RoomLaunchRequest.Join(invitation.RoomCode,invitation.Region);MultiplayerApp.OpenDefaultRoomOnLoad=true;
            if(!SceneLoading.Load("PlayMultiplayer"))throw new InvalidOperationException("Chưa thể mở phòng. Vui lòng thử lại.");
            await PhotonSession.WaitAsync(()=>SceneManager.GetActiveScene().name=="PlayMultiplayer"&&!SceneLoading.IsBlocking,"Không mở được màn hình phòng.",30);
            await PhotonSession.WaitAsync(()=>MultiplayerApp.Instance&&MultiplayerApp.Instance.RoomCode==invitation.RoomCode
                ||FindAnyObjectByType<PlayMultiplayerPresenter>() is PlayMultiplayerPresenter presenter&&!presenter.IsInitializing&&MultiplayerApp.Instance&&!MultiplayerApp.Instance.Busy,
                "Không tham gia được phòng. Vui lòng thử lại.",30);
            if(MultiplayerApp.Instance.RoomCode!=invitation.RoomCode)throw new InvalidOperationException(MultiplayerApp.Instance.Notice);
        }
    }
}
