using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using PickleVerse.Multiplayer.Services;
using PickleVerse.Progression;
using PickleVerse.Services;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PickleVerse.Multiplayer
{
    public sealed partial class MultiplayerApp:MonoBehaviour,IMultiplayerBackend
    {
        public static MultiplayerApp Instance{get;private set;}
        public static bool OpenDefaultRoomOnLoad;
        public MultiplayerSettings Settings{get;private set;}
        public FlowState State{get;private set;}=FlowState.Menu;
        public PlayMode Mode{get;private set;}=PlayMode.Singles;
        public string Notice{get;private set;}="Đăng nhập để chơi online.";
        public string DisplayName{get=>identity.DisplayName;set=>identity.DisplayName=value;}
        public string Gender{get;set;}="Male";
        public string PlayerId=>identity.PlayerId;
        public string ActiveProfile=>identity.Profile;
        public string RoomCode=>Room?.Code??"";
        public string RoomSceneName{get;set;}="PlayMultiplayer";
        public IPlayerIdentityService IdentityService=>identity;
        public IRoomService RoomService{get;private set;}
        public IMatchmakingService MatchmakingService{get;private set;}
        readonly IPlayerIdentityService identity=new PlayerIdentityService();
        public PhotonSession Room{get;private set;}
        public PhotonSession Match{get;private set;}
        public NetworkRunner Network=>Match?.Runner;
        public MatchRoster Roster{get;private set;}
        public bool Custom=>Room?.Data.custom??false;
        public bool IsCaptain=>Room==null||Room.IsHost;
        public bool CanChangeLocalProfile=>Room==null&&Match==null&&!Searching&&State!=FlowState.InMatch&&State!=FlowState.Reconnecting;
        public bool Busy{get;private set;}
        public bool Searching=>State==FlowState.Delay||State==FlowState.Searching||State==FlowState.Connecting;
        public string Elapsed=>clock.Display(Time.realtimeSinceStartupAsDouble);
        public event Action Changed;
        public event Action<ulong> PeerConnected;
        public readonly Dictionary<ulong,string> ClientIdentities=new Dictionary<ulong,string>();
        readonly SearchClock clock=new SearchClock();
        readonly ModeNavigationGuard navigation=new ModeNavigationGuard();
        readonly HashSet<string> ignored=new HashSet<string>();
        PhotonSession browser;
        int operation;
        bool working,closing,quitting,courtLoading,savingReady,reconnecting,joining;
        double searchBegan,connectingUntil;
        double rankedBotDeadline=double.PositiveInfinity;
        string rankedSearchId="";
        bool startingSearch;
        [Serializable]sealed class RankedSearchTicket{public double remainingSeconds;public bool roomReady;public string state,matchId,session,hostPlayerId,region;public MatchRoster roster;}
        double assignmentObserved;
        string lastPartyMatch="";
        GameObject StatePrefab=>Resources.Load<GameObject>("Photon/Session State");
        public bool CourtSceneSynchronized{get;private set;}=true;
        public bool ReadyCheckActive=>Match!=null&&State==FlowState.Connecting&&Match.Data.phase=="ready";
        public bool ReadyCheckAccepted=>Match?.Data.seats.Find(s=>s.member.id==PlayerId)?.decision==ReadyDecision.Accepted;
        public bool ReadyCheckBusy=>savingReady;
        public int ReadyConfirmationSeconds=>ProgressionRuntime.Instance.Snapshot.catalog.matchmaking.readyConfirmationSeconds;
        public float ReadySeconds=>Match==null?0:Mathf.Max(0,(float)Math.Min(Match.Data.deadline-Match.Runner.SimulationTime,serverReadyUntil-Time.realtimeSinceStartupAsDouble));
        public bool CanAcceptMatch=>ReadyCheckActive&&ReadySeconds>0&&!savingReady&&!ReadyCheckAccepted;
        public int ReadyCount=>Match?.Data.seats.Count(s=>s.decision==ReadyDecision.Accepted)??0;
        public static MultiplayerApp Ensure(){if(!Instance)new GameObject("Photon Multiplayer").AddComponent<MultiplayerApp>();return Instance;}
        void Awake()
        {
            if(Instance&&Instance!=this){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);Application.runInBackground=true;
            Settings=Resources.Load<MultiplayerSettings>("MultiplayerSettings");RoomService=new RoomService(this,identity);MatchmakingService=new MatchmakingService(this);
            if(!GetComponent<MatchReadyOverlay>())gameObject.AddComponent<MatchReadyOverlay>();
        }
        public async Task InitializeAsync(string profile)
        {
            if(!Settings||!StatePrefab)throw new InvalidOperationException("Thiếu cấu hình Photon.");
            await identity.InitializeAsync(profile,Settings.environment);Gender=ProgressionRuntime.Instance.Snapshot.isMale?"Male":"Female";MultiplayerSceneConfiguration.Validate();
        }
        public async Task PlayAsync(string profile){navigation.Open();await InitializeAsync(profile);await LeaveRoomAsync();Mode=PlayMode.Singles;SetState(FlowState.WaitingRoom,"Đấu đơn • Bấm Tìm trận khi sẵn sàng.");}
        public Task SelectModeAsync(PlayMode mode)=>SwitchRoomAsync(mode,false);
        public Task CreateCustomAsync(PlayMode mode)=>SwitchRoomAsync(mode,true);
        public async Task SwitchRoomAsync(PlayMode mode,bool custom)
        {
            if(State!=FlowState.WaitingRoom)throw new InvalidOperationException("Hủy tìm trận trước khi đổi phòng.");
            if(mode!=PlayMode.Singles&&mode!=PlayMode.Doubles)throw new InvalidOperationException("Chế độ không hợp lệ.");
            await LeaveRoomAsync();Mode=mode;
            if(custom||mode==PlayMode.Doubles)
            {
                var session=PhotonSession.Create("Photon Party");int generation=operation;
                try
                {
                    await session.StartAsync(Guid.NewGuid().ToString("N").Substring(0,8).ToUpperInvariant(),PlayerId,Settings.photonRegion,new PhotonRoomData{party=true,custom=custom,mode=mode,phase="idle"},StatePrefab);
                    await session.ReserveAsync(new List<RoomMember>{Self()});
                    if(generation!=operation){await session.LeaveAsync();return;}AttachRoom(session);
                }
                catch{await session.LeaveAsync();throw;}
            }
            SetState(FlowState.WaitingRoom,custom?"Chia sẻ mã phòng để mời bạn bè.":"Sẵn sàng tìm trận.");
        }
        public async Task JoinRoomAsync(string code)
        {
            if(Searching)throw new InvalidOperationException("Hủy tìm trận trước.");await InitializeAsync(PlayerId);await LeaveRoomAsync();
            var session=PhotonSession.Create("Photon Party");int generation=operation;
            try
            {
                await session.JoinAsync(code.Trim().ToUpperInvariant(),PlayerId,Settings.photonRegion,true);
                if(!session.Data.party||session.Data.phase!="idle")throw new InvalidOperationException("Phòng đang tìm hoặc đang chơi.");
                await session.ReserveAsync(new List<RoomMember>{Self()});
                if(generation!=operation){await session.LeaveAsync();return;}Mode=session.Data.mode;AttachRoom(session);SetState(FlowState.WaitingRoom,"Đã tham gia phòng.");
            }
            catch{await session.LeaveAsync();throw;}
        }
        void AttachRoom(PhotonSession session){Room=session;session.Changed+=RoomChanged;session.Stopped+=reason=>{if(!closing)_=Safe(ClosePartyAsync("Mất kết nối phòng chờ."));};}
        RoomMember Self()=>new RoomMember{id=PlayerId,name=DisplayName,gender=Gender,elo=ProgressionRuntime.Instance.Player.elo,team=0,group=Room?.Id??PlayerId,paddleId=ProgressionRuntime.Instance.Snapshot.inventory.equippedPaddle};
        public List<RoomMember> Members()=>Room==null?new List<RoomMember>{Self()}:Room.Data.seats.Select(s=>s.member).ToList();
        public async Task UpdateNameAsync(string name)
        {
            if(Room!=null||Searching)throw new InvalidOperationException("Rời phòng trước khi đổi tên.");
            await AccountRuntime.Ensure().Account.UpdateProfileAsync(new PickleVerse.Services.Api.UpdateProfileRequest{PlayerName=identity.ResolveName(name)});
            await ProgressionRuntime.Instance.ReloadAsync();await identity.InitializeAsync(PlayerId,Settings.environment);Changed?.Invoke();
        }
        public Task SetTeamAsync(string id,int team){if(!Custom||State!=FlowState.WaitingRoom)throw new InvalidOperationException("Chỉ đổi đội trong phòng riêng.");Room.Lobby.Team(id,team);return Task.CompletedTask;}
        public Task KickAsync(string id){if(!Custom||!IsCaptain||State!=FlowState.WaitingRoom)throw new InvalidOperationException("Không có quyền mời ra.");Room.Lobby.Kick(id);return Task.CompletedTask;}
        public async Task LeaveRoomAsync()
        {
            if(Searching)await CancelSearchAsync();var room=Room;
            if(room!=null){if(room.IsHost){room.Lobby.Command("close","");await Task.Delay(300);}await room.LeaveAsync();if(Room==room){room.Changed-=RoomChanged;Room=null;}}
        }
        void RoomChanged()
        {
            if(Room==null||closing)return;var data=Room.Data;
            if(data.phase=="closed"||Room.Rejected!=null){_=Safe(ClosePartyAsync(data.notice.Length>0?data.notice:Room.Rejected));return;}
            if(data.phase=="searching"&&!IsCaptain&&State==FlowState.WaitingRoom&&!startingSearch)_=Safe(BeginQueueAsync(false));
            if(Custom&&data.matchSession.Length>0&&data.matchSession!=lastPartyMatch&&!working&&!joining&&(Match==null||!IsCaptain&&Match.Id!=data.matchSession))
            {lastPartyMatch=data.matchSession;_=Safe(FollowPartyMatchAsync(data.matchSession,Room.Region,operation));}
            if(data.phase=="idle"&&Searching&&!IsCaptain)_=Safe(CancelSearchAsync());Changed?.Invoke();
        }
        async Task FollowPartyMatchAsync(string name,string region,int generation)
        {
            working=true;
            try{if(Match!=null)await LeaveMatchAsync();await JoinMatchAsync(name,region,generation);}
            finally{working=false;}
        }
        async Task ClosePartyAsync(string reason){if(closing)return;closing=true;try{CancelLocal();await LeaveMatchAsync();var room=Room;Room=null;if(room!=null)await room.LeaveAsync();SetState(FlowState.WaitingRoom,reason);}finally{closing=false;}}
        public Task FindAsync()
        {
            if(State!=FlowState.WaitingRoom||Custom||!IsCaptain)return Task.CompletedTask;
            return BeginQueueAsync(true);
        }
        async Task BeginQueueAsync(bool captain)
        {
            CancelLocal();int generation=operation;ignored.Clear();searchBegan=Time.realtimeSinceStartupAsDouble;rankedBotDeadline=double.PositiveInfinity;
            assignmentObserved=0;clock.Start(searchBegan);startingSearch=true;SetState(FlowState.Delay,"Đang vào hàng chờ online…");
            if(captain)Room?.Lobby.Command("search","");
            string key=rankedSearchId=Guid.NewGuid().ToString("N");
            try
            {
                var ticket=await AccountRuntime.Ensure().Account.SendAuthorizedAsync<RankedSearchTicket>("POST","api/Game/ranked/search",new{
                    SearchId=Guid.Parse(key),Mode=Mode.ToString(),Region=Room?.Region??Settings.photonRegion,
                    Protocol=MultiplayerSceneConfiguration.ProtocolVersion,Group=Room?.Id??PlayerId,Members=Members().Select(m=>Guid.Parse(m.id)).ToArray()});
                if(generation!=operation){await CancelRankedTicketAsync(key);return;}
                rankedBotDeadline=Time.realtimeSinceStartupAsDouble+ticket.remainingSeconds;
                SetState(FlowState.Searching,"Đang tìm đối thủ online…");
            }
            catch
            {
                try{await CancelRankedTicketAsync(key);}catch(Exception){}
                if(generation==operation){CancelLocal();Room?.Lobby.Command("idle","");SetState(FlowState.WaitingRoom,"Chưa mở được lượt tìm trận online.");}
                throw;
            }
            finally{if(generation==operation)startingSearch=false;}
        }
        public async Task StartCustomAsync()
        {
            if(!Custom||!IsCaptain||State!=FlowState.WaitingRoom)return;var members=Members();
            if(!new MatchRoster{custom=true,mode=Mode,members=members}.IsValid())throw new InvalidOperationException("Mỗi đội cần ít nhất một người và không quá sức chứa của chế độ.");
            CancelLocal();Room.Lobby.Command("search","");await CreateMatchAsync(true,operation);
        }
        async void Update()
        {
            if(quitting||closing||navigation.Leaving)return;
            TickOnlineHeartbeat();
            // The captain may start searching before JoinRoomAsync has subscribed to the
            // party's Changed event. Reconcile that initial state on the next frame too.
            if(Room!=null&&!Custom&&!IsCaptain&&Room.Data.phase=="searching"&&State==FlowState.WaitingRoom&&!startingSearch&&!working)
                await Safe(BeginQueueAsync(false));
            if((State==FlowState.Delay||State==FlowState.Searching)&&!working&&!startingSearch&&clock.CanSubmit(Time.realtimeSinceStartupAsDouble))await Safe(SearchStepAsync(operation));
            if(Match!=null&&State==FlowState.Connecting&&!working&&!courtLoading)
            {
                if(Match.Data.phase=="loading")await Safe(LoadAcceptedCourtAsync());
                else if(Match.Data.phase=="rejected")await Safe(RejectedMatchAsync());
                else if(Time.realtimeSinceStartupAsDouble>connectingUntil&&Match.Data.phase!="ready")await Safe(ReturnToRoomAsync("Không đủ người kết nối để xác nhận trận."));
            }
            if(Match!=null&&Match.Data.phase=="interrupted"&&State==FlowState.InMatch)await Safe(ReturnToRoomAsync(Match.Data.notice));
        }
        async Task SearchStepAsync(int generation)
        {
            working=true;
            try
            {
                if(generation!=operation||string.IsNullOrEmpty(rankedSearchId)||Match!=null&&Match.Data.phase!="searching")return;
                var ticket=await AccountRuntime.Ensure().Account.SendAuthorizedAsync<RankedSearchTicket>("POST","api/Game/ranked/poll",new{SearchId=Guid.Parse(rankedSearchId)});
                if(generation!=operation)return;
                if(ticket.state=="MATCHED")
                {
                    if(assignmentObserved==0)assignmentObserved=Time.realtimeSinceStartupAsDouble;
                    if(Match==null)
                    {
                        if(ticket.hostPlayerId==PlayerId)await CreateAssignedMatchAsync(ticket,generation);
                        else if(ticket.roomReady)
                        {
                            try{await JoinMatchAsync(ticket.session,ticket.region,generation);}
                            catch(Exception){if(Time.realtimeSinceStartupAsDouble-assignmentObserved>30){await CancelSearchAsync();SetNotice("Không kết nối được phòng trận. Hãy tìm lại.");return;}}
                        }
                    }
                }
                else if(ticket.state=="SEARCHING")
                {
                    if(Mode==PlayMode.Singles&&ticket.remainingSeconds<=0){await AddRankedOpponentAsync(generation);return;}
                    if(Mode==PlayMode.Doubles&&clock.TimedOut(Time.realtimeSinceStartupAsDouble)){await CancelSearchAsync();SetNotice("Hết thời gian tìm trận. Hãy thử lại.");return;}
                }
                await Task.Delay(1000);
            }
            catch(Exception error)
            {
                if(generation==operation){await CancelSearchAsync();SetNotice("Lượt tìm trận đã dừng: "+error.Message);}
            }
            finally{working=false;}
        }
        async Task CreateAssignedMatchAsync(RankedSearchTicket ticket,int generation)
        {
            var session=PhotonSession.Create("Photon Match");
            try
            {
                await session.StartAsync(ticket.session,PlayerId,ticket.region,new PhotonRoomData{mode=Mode,custom=false,
                    matchId=ticket.matchId,expectedPlayers=ticket.roster.HumanCount,assignment=ticket.roster,
                    phase="searching",night=ticket.roster.night,venue=ticket.roster.venue},StatePrefab);
                await session.ReserveAsync(Members());
                if(generation!=operation){await session.LeaveAsync();return;}AttachMatch(session);
                await AccountRuntime.Ensure().Account.SendAuthorizedAsync<RankedSearchTicket>("POST","api/Game/ranked/room-ready",new{SearchId=Guid.Parse(rankedSearchId)});
            }
            catch{await session.LeaveAsync();throw;}
        }
        async Task AddRankedOpponentAsync(int generation)
        {
            if(Match==null)await CreateMatchAsync(false,generation);
            var match=Match;
            if(generation!=operation||match==null||!match.Lobby.TryBeginRankedFallback())return;
            try
            {
                await StopBrowser();
                var bot=await AccountRuntime.Ensure().Account.SendAuthorizedAsync<RoomMember>("POST","api/Game/ranked/bot",new{SearchId=Guid.Parse(rankedSearchId),MatchId=Guid.Parse(match.Data.matchId),Session=match.Id});
                if(generation!=operation||Match!=match)return;
                if(!RankedBotAccounts.Contains(bot.id))throw new InvalidOperationException("Đối thủ dự phòng không thuộc danh sách tài khoản ảo.");
                match.Lobby.CompleteRankedFallback(bot);
            }
            catch{if(Match==match)match.Lobby.ResumeHumanSearch();await Task.Delay(750);throw;}
        }
        Task CancelRankedTicketAsync(string key)=>string.IsNullOrEmpty(key)?Task.CompletedTask:AccountRuntime.Ensure().Account.SendAuthorizedAsync<RankedSearchTicket>("POST","api/Game/ranked/cancel",new{SearchId=Guid.Parse(key)});
        static int Int(SessionInfo session,string key)=>session.Properties.TryGetValue(key,out var value)?(int)value:int.MinValue;
        static bool Bool(SessionInfo session,string key)=>session.Properties.TryGetValue(key,out var value)&&(bool)value;
        bool Compatible(SessionInfo session,bool expanded)
        {
            if(Mode!=PlayMode.Singles)return true;var player=ProgressionRuntime.Instance.Player;
            return EloPolicy.PairingKeys(player.elo,player.rankLossStreak,expanded).Intersect(EloPolicy.PairingKeys(Int(session,"elo"),Int(session,"losses"),Bool(session,"expanded"))).Any();
        }
        async Task CreateMatchAsync(bool custom,int generation)
        {
            var session=PhotonSession.Create("Photon Match");
            try
            {
                await session.StartAsync("PKV-"+Guid.NewGuid().ToString("N"),PlayerId,Room?.Region??Settings.photonRegion,new PhotonRoomData{mode=Mode,custom=custom,expectedPlayers=custom?Members().Count:0,phase="searching",night=PickleVerse.LocalMatch.BotMatchLaunchSettings.Night,venue=(CourtVenue)UnityEngine.Random.Range(0,2)},StatePrefab);
                await session.ReserveAsync(Members());if(generation!=operation){await session.LeaveAsync();return;}AttachMatch(session);
                if(Room!=null){lastPartyMatch=session.Id;Room.Lobby.Command("match",session.Id);}
            }
            catch{await session.LeaveAsync();throw;}
        }
        async Task JoinMatchAsync(string name,string region,int generation,MatchRoster restoredRoster=null)
        {
            if(joining)return;joining=true;
            var session=PhotonSession.Create("Photon Match");
            // Joining an in-progress Fusion session can load its court before JoinAsync
            // returns. Expose the runner/roster now so court components can bind safely.
            if(restoredRoster!=null){Match=session;canonicalRoster=Roster=restoredRoster;}
            try
            {
                await session.JoinAsync(name,PlayerId,region,false);Mode=session.Data.mode;await session.ReserveAsync(IsCaptain?Members():new List<RoomMember>{Self()});
                if(generation!=operation){await session.LeaveAsync();return;}AttachMatch(session);
                if(Room!=null&&IsCaptain){lastPartyMatch=session.Id;Room.Lobby.Command("match",session.Id);}
            }
            catch{if(Match==session){Match=null;Roster=null;canonicalRoster=null;}await session.LeaveAsync();throw;}
            finally{joining=false;}
        }
        void AttachMatch(PhotonSession session)
        {
            Match=session;session.Changed+=MatchChanged;session.SceneStarted+=SceneStarted;session.SceneDone+=SceneDone;
            session.PeerLeft+=(r,p)=>{if(!closing&&State==FlowState.InMatch&&session.IsHost)NetworkMatch.Instance?.ReserveDisconnected((ulong)p.RawEncoded);};
            session.Stopped+=reason=>{if(!closing&&!quitting)_=Safe(State==FlowState.InMatch?ReconnectAsync(session):ReturnToRoomAsync("Mất kết nối Photon."));};
            connectingUntil=Time.realtimeSinceStartupAsDouble+30;MatchChanged();
        }
        void MatchChanged()
        {
            if(Match==null||closing)return;Roster=canonicalRoster??Match.Data.Roster(Match.Runner.SessionInfo.Region);
            foreach(var seat in Match.Data.seats.Where(s=>s.peer!=0)){ulong key=(ulong)seat.peer;bool joined=!ClientIdentities.ContainsKey(key);ClientIdentities[key]=seat.member.id;if(joined)PeerConnected?.Invoke(key);}
            if(Match.Data.phase=="ready"&&State!=FlowState.InMatch&&State!=FlowState.Results){SetState(FlowState.Connecting,"Đã tìm thấy trận • Xác nhận để tham gia.");_=Safe(EnsureReadyOfferAsync());}Changed?.Invoke();
        }
        public async Task AcceptMatchAsync()
        {
            if(!CanAcceptMatch)return;savingReady=true;Changed?.Invoke();
            try{await EnsureReadyOfferAsync();await PrepareOnlineMatchAsync();Match.Lobby.Ready(true);await PhotonSession.WaitAsync(()=>Match==null||ReadyCheckAccepted||Match.Data.phase!="ready","Chưa gửi được xác nhận.");}
            catch(Exception error){SetNotice(error.Message);}finally{savingReady=false;Changed?.Invoke();}
        }
        public async Task DeclineMatchAsync(){if(Match!=null)Match.Lobby.Ready(false);await CancelSearchAsync();}
        async Task RejectedMatchAsync()
        {
            bool retry=ReadyCheckAccepted&&!Custom;string message=Match.Data.notice;ignored.Add(Match.Id);await CancelRankedTicketAsync(rankedSearchId);rankedSearchId="";await LeaveMatchAsync();if(Room!=null&&IsCaptain)Room.Lobby.Command("idle","");SetState(FlowState.WaitingRoom,message);if(retry&&IsCaptain)await FindAsync();
        }
        async Task LoadAcceptedCourtAsync()
        {
            working=true;
            // SceneStarted/SceneDone own CourtSceneSynchronized. A guest can receive
            // SceneDone while its HTTP request is still in flight; do not reset it here.
            try{await StopBrowser();if(Match==null||Match.Data.phase!="loading")return;await StartOnlineMatchAsync();courtLoading=true;SetNotice("Mọi người đã xác nhận • Đang tải sân…");PickleVerse.Settings.SceneLoading.BeginExternal("MultiplayerCourt");if(Match.IsHost)await Match.Runner.LoadScene("MultiplayerCourt",LoadSceneMode.Single);}
            finally{working=false;}
        }
        void SceneStarted(NetworkRunner runner){if(Match?.Runner==runner&&Match.Data.phase=="loading"){CourtSceneSynchronized=false;PickleVerse.Settings.SceneLoading.BeginExternal("MultiplayerCourt");}}
        async void SceneDone(NetworkRunner runner)
        {
            if(Match?.Runner!=runner||!courtLoading&&Match.Data.phase!="loading")return;CourtSceneSynchronized=true;
            var menu=SceneManager.GetSceneByName(RoomSceneName);if(menu.IsValid()&&menu.isLoaded)await SceneManager.UnloadSceneAsync(menu);
        }
        async Task ReconnectAsync(PhotonSession previous)
        {
            if(reconnecting)return;reconnecting=true;string name=previous.Id,region=previous.Region;var previousRoster=Roster;int generation=operation;
            SetState(FlowState.Reconnecting,"Đang kết nối lại, tối đa 30 giây…");double deadline=Time.realtimeSinceStartupAsDouble+Settings.reconnectSeconds;
            try{await LeaveMatchAsync();while(generation==operation&&Time.realtimeSinceStartupAsDouble<deadline){try{await JoinMatchAsync(name,region,generation,previousRoster);SetState(FlowState.InMatch,"Đã kết nối lại.");return;}catch{await Task.Delay(1000);}}await ReturnToRoomAsync("Không thể kết nối lại trận.");}
            finally{reconnecting=false;}
        }
        void CancelLocal(){operation++;startingSearch=false;clock.Stop();}
        async Task StopBrowser(){var old=browser;browser=null;if(old!=null)await old.LeaveAsync();}
        public async Task CancelSearchAsync()
        {
            CancelLocal();string key=rankedSearchId;rankedSearchId="";Room?.Lobby.Command("cancel","");
            await LeaveMatchAsync();await StopBrowser();lastPartyMatch="";SetState(FlowState.WaitingRoom,"Đã hủy tìm trận.");
            if(AccountRuntime.Ensure().Account.IsSignedIn)await CancelRankedTicketAsync(key);
        }
        async Task LeaveMatchAsync(){var old=Match;Match=null;readyOffer=null;serverReadyUntil=double.PositiveInfinity;if(old!=null){old.Changed-=MatchChanged;old.SceneStarted-=SceneStarted;old.SceneDone-=SceneDone;await old.LeaveAsync();}Roster=null;canonicalRoster=null;ClientIdentities.Clear();courtLoading=false;CourtSceneSynchronized=true;PickleVerse.Settings.SceneLoading.CancelExternal();}
        public async Task ReturnToRoomAsync(string message)
        {
            if(closing)return;closing=true;
            try
            {
                CancelLocal();string ticket=rankedSearchId;rankedSearchId="";
                if(AccountRuntime.Ensure().Account.IsSignedIn)await Safe(CancelRankedTicketAsync(ticket));
                await LeaveMatchAsync();await StopBrowser();lastPartyMatch="";
                if(Room!=null&&Room.IsHost)Room.Lobby.Command("idle","");SetState(FlowState.WaitingRoom,message);
                var scene=SceneManager.GetSceneByName(RoomSceneName);
                if(!scene.IsValid()||!scene.isLoaded){await SceneManager.LoadSceneAsync(RoomSceneName,LoadSceneMode.Additive);scene=SceneManager.GetSceneByName(RoomSceneName);}
                if(scene.IsValid()&&scene.isLoaded)SceneManager.SetActiveScene(scene);
            }
            finally{closing=false;}
        }
        public async Task LeaveGameAsync(){await ForfeitOnlineMatchAsync();NetworkMatch.Instance?.RequestLeave();await Task.Delay(300);await ReturnToRoomAsync("Bạn đã rời trận.");}
        public Task ReturnToModeSelectionAsync()=>navigation.ReturnAsync(CancelLocal,CancelSearchAsync,LeaveMatchAsync,LeaveRoomAsync,()=>{OpenDefaultRoomOnLoad=false;SetState(FlowState.Menu,"Đã rời phòng.");SceneManager.LoadScene("ModeSelection");});
        public async Task SignOutFromLobbyAsync(){await CancelSearchAsync();await LeaveRoomAsync();SetState(FlowState.Menu,"Đã đăng xuất.");}
        public void CourtLoaded(){SetState(FlowState.Connecting,"Chờ mọi người tải xong sân…");}
        public void MatchStarted(){clock.Stop();if(Match?.IsHost==true)Match.Lobby.Command("playing","");SetState(FlowState.InMatch,"Trận đã bắt đầu.");}
        public void ShowResult(string text){SetState(FlowState.Results,text);}
        public void SetNotice(string text){Notice=text;Changed?.Invoke();}
        void SetState(FlowState state,string text){State=state;SetNotice(text);}
        public async Task ExecuteAsync(Func<Task> action){if(Busy)return;Busy=true;Changed?.Invoke();try{await action();}catch(Exception error){SetNotice(error.Message);}finally{Busy=false;Changed?.Invoke();}}
        async Task Safe(Task task){try{await task;}catch(Exception error){if(!quitting)SetNotice(error.Message);}}
        async void OnApplicationQuit(){quitting=true;CancelLocal();await Safe(LeaveMatchAsync());await Safe(LeaveRoomAsync());await Safe(StopBrowser());}
        void OnDestroy(){if(Instance==this){Instance=null;PickleVerse.Settings.SceneLoading.CancelExternal();}}
    }
    public static class PlayerSignOut{public static Task FromLobbyAsync()=>MultiplayerApp.Instance?MultiplayerApp.Instance.SignOutFromLobbyAsync():Task.CompletedTask;}
}
