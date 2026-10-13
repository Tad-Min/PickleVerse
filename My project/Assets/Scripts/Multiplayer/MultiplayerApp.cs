using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Matchmaker.Models;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.Multiplayer.Services;

namespace PickleVerse.Multiplayer
{
    public sealed partial class MultiplayerApp : MonoBehaviour, IMultiplayerBackend
    {
        public static MultiplayerApp Instance { get; private set; }
        public static bool OpenDefaultRoomOnLoad;
        public string ActiveProfile => profile;
        public MultiplayerSettings Settings { get; private set; }
        public FlowState State { get; private set; } = FlowState.Menu;
        public PlayMode Mode { get; private set; } = PlayMode.Singles;
        public string Notice { get; private set; } = "Chọn hồ sơ test rồi bấm Play.";
        public string DisplayName { get => identity.DisplayName; set => identity.DisplayName = value; }
        public string Gender = "Male";
        public string PlayerId => identity.PlayerId;
        public string RoomCode => Room?.Code ?? "";
        public string RoomSceneName { get; set; } = "PlayMultiplayer";
        public IPlayerIdentityService IdentityService => identity;
        public IRoomService RoomService { get; private set; }
        public IMatchmakingService MatchmakingService { get; private set; }
        readonly IPlayerIdentityService identity = new PlayerIdentityService();
        public ISession Room { get; private set; }
        public ISession Match { get; private set; }
        public MatchRoster Roster { get; private set; }
        public bool Custom => roomData.custom;
        public bool IsCaptain => Room == null || Room.IsHost;
        public bool CanChangeLocalProfile => Room==null && Match==null && !Searching && State!=FlowState.InMatch && State!=FlowState.Reconnecting;
        public bool Busy { get; private set; }
        public bool Searching => State == FlowState.Delay || State == FlowState.Searching || State == FlowState.Connecting;
        public string Elapsed => clock.Display(Time.realtimeSinceStartupAsDouble);
        public event Action Changed;
        public NetworkManager Network { get; private set; }
        public readonly Dictionary<ulong,string> ClientIdentities = new Dictionary<ulong,string>();
        readonly GroupMatchmaker groupMatcher = new GroupMatchmaker();
        readonly SearchClock clock = new SearchClock();
        readonly PickleVerse.Progression.RankedSearchGate rankedSearch = new PickleVerse.Progression.RankedSearchGate();
        int LocalElo => PickleVerse.Progression.ProgressionRuntime.Exists ? PickleVerse.Progression.ProgressionRuntime.Instance.Player.elo : 100;
        RoomData roomData = new RoomData();
        CancellationTokenSource searchCancellation;
        int operation;
        bool polling, connecting, intentionalShutdown, quitting, retrying, cancelling, returning, courtLoading, preparingSearch, reconnecting;
        double nextPoll, connectionDeadline;
        string observedSearch = "", publishedMatch = "", cancelledSearch = "";
        string profile => identity.Profile;
        Dictionary<string,List<QosResult>> measured = new Dictionary<string,List<QosResult>>();
        readonly HashSet<string> ignoredMatches=new HashSet<string>();

        public static MultiplayerApp Ensure()
        {
            if (Instance != null) return Instance;
            return new GameObject("MultiplayerApp").AddComponent<MultiplayerApp>();
        }
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject); Application.runInBackground = true;
            RoomService = new RoomService(this, identity);
            MatchmakingService = new MatchmakingService(this);
            Settings = Resources.Load<MultiplayerSettings>("MultiplayerSettings");
            if (Settings == null) { Notice = "Thiếu MultiplayerSettings."; return; }
            var go = new GameObject("NetworkManager"); DontDestroyOnLoad(go);
            Network = go.AddComponent<NetworkManager>();
            Network.NetworkConfig = new NetworkConfig();
            var transport = go.AddComponent<UnityTransport>();
            transport.HeartbeatTimeoutMS=500; transport.DisconnectTimeoutMS=5000;
            Network.NetworkConfig.NetworkTransport = transport;
            Network.NetworkConfig.TickRate = 50;
            Network.NetworkConfig.ProtocolVersion = MultiplayerSceneConfiguration.ProtocolVersion;
            Network.NetworkConfig.ConnectionApproval = true;
            Network.NetworkConfig.EnableSceneManagement = true;
            Network.NetworkConfig.ClientConnectionBufferTimeout = 35;
            Network.AddNetworkPrefab(Settings.playerPrefab);
            Network.AddNetworkPrefab(Settings.matchPrefab);
            Network.ConnectionApprovalCallback = ApproveConnection;
            Network.OnClientDisconnectCallback += Disconnected;
            Network.OnClientConnectedCallback += Connected;
            Network.OnTransportFailure += TransportFailed;
        }
        public async Task InitializeAsync(string requestedProfile)
        {
            if (Settings == null) throw new InvalidOperationException("Thiếu cấu hình multiplayer.");
            await identity.InitializeAsync(requestedProfile, Settings.environment);
        }
        async void Update()
        {
            if (Settings==null || quitting || leavingModePage) return;
            double now=Time.realtimeSinceStartupAsDouble;
            if (Mode==PlayMode.Singles && !Custom && (State==FlowState.Delay || State==FlowState.Searching) && rankedSearch.ClaimBot(now))
                await Safe(StartRankedBotAsync());
            else if (Mode==PlayMode.Doubles && Searching && clock.TimedOut(now)) { await Safe(CancelSearchAsync()); SetState(FlowState.WaitingRoom,"Hết thời gian tìm trận. Hãy thử lại."); }
            if (State==FlowState.Delay && clock.CanSubmit(now) && IsCaptain && !connecting && !preparingSearch)
            { SetState(FlowState.Searching,"Đang tìm trận…"); _=Safe(SearchAsync(operation)); }
            // A missed Lobby event must not leave clients waiting forever for
            // Relay metadata. Refresh the match while connecting, instead of
            // spending the client's Lobby request budget on its waiting room.
            if (State==FlowState.Connecting && Match!=null && now>=nextPoll && !polling)
            {
                nextPoll=now+4; polling=true;
                var currentMatch=Match;
                try { await currentMatch.RefreshAsync(); }
                catch(Exception e) { if(Match==currentMatch) Notice="Đang cập nhật kết nối: "+e.Message; }
                finally { polling=false; Changed?.Invoke(); }
            }
            if (Room!=null && State!=FlowState.Connecting && now>=nextPoll && !polling && !connecting && !Busy)
            {
                nextPoll=now+4; polling=true;
                try
                {
                    await Room.RefreshAsync(); ReadRoom();
                    if(Custom && IsCaptain && State==FlowState.WaitingRoom)
                    {
                        foreach(var member in Members().Where(p=>p.team<0).ToArray())
                        {
                            // A SaveProperties response can temporarily omit another
                            // just-joined member from the SDK player cache. Count the
                            // assignments already made by this captain so that the
                            // next join cannot create a third seat on the same team.
                            int team=roomData.teams.Values.Count(t=>t==0)<=roomData.teams.Values.Count(t=>t==1)?0:1;
                            await SetTeamAsync(member.id,team);
                        }
                        foreach(var player in Room.Players)
                        {
                            string request=Prop(player,"teamRequestId");
                            if(string.IsNullOrEmpty(request) || (roomData.handledTeamRequests.TryGetValue(player.Id,out var handled) && handled==request)) continue;
                            roomData.handledTeamRequests[player.Id]=request;
                            try { if(int.TryParse(Prop(player,"teamRequest"),out int team)) await SetTeamAsync(player.Id,team); }
                            catch(InvalidOperationException e) { roomData.notice=e.Message; await SaveRoomAsync(); }
                        }
                    }
                    if (IsCaptain && Searching && Room.Players.Any(p=>Prop(p,"cancel")==observedSearch)) await CancelSearchAsync();
                    if (Searching && !Custom && Mode==PlayMode.Doubles && !measured.ContainsKey(PlayerId)) await PublishQosAsync();
                }
                catch(ConnectionQualityException e)
                {
                    if (Searching) { await CancelSearchAsync(); SetState(FlowState.WaitingRoom,e.Message); }
                }
                catch(OperationCanceledException) { }
                catch(Exception e) { if (Room!=null) Notice="Đang cập nhật phòng: "+e.Message; }
                finally { polling=false; Changed?.Invoke(); }
            }
            if (State==FlowState.Connecting && Match!=null && Network.IsHost)
            {
                if (!courtLoading && Roster.members.Where(m=>!m.bot).All(m=>ClientIdentities.Values.Contains(m.id)) && Network.ConnectedClients.Count==Roster.HumanCount)
                {
                    if (Match.Players.Any(p=>Prop(p,"cancelled")=="1")) { await RetryMatchAsync("Có người hủy; đang ghép lại."); return; }
                    courtLoading=true; SetNotice("Đang tải sân cho tất cả người chơi…");
                    Network.SceneManager.LoadScene("MultiplayerCourt",LoadSceneMode.Single);
                }
                else if(now>connectionDeadline) await RetryMatchAsync("Một người chưa kết nối được; đang ghép lại.");
            }
        }
        async Task ConnectMatchAsync(MatchRoster roster,int generation)
        {
            if(connecting||leavingModePage||generation!=operation) return; connecting=true; Roster=roster; SetState(FlowState.Connecting,"Đủ người; đang kết nối vào sân…");
            try
            {
                // Allow recent room/readiness saves and SDK event refreshes to finish
                // before creating the separate match session (Lobby Get limit: 1/sec).
                if(Custom) await Task.Delay(2200+Math.Max(0,roster.members.FindIndex(m=>m.id==PlayerId))*300);
                if(generation!=operation) return;
                SetConnectionIdentity();
                ISession session=null;
                for(int attempt=0;;attempt++)
                {
                    if(generation!=operation) return;
                    try { session=await MultiplayerService.Instance.CreateOrJoinSessionAsync(roster.matchId,MatchOptions(roster.Count,roster.region)); break; }
                    catch(Exception e) when(attempt<3 && e.Message.Contains("429"))
                    { await Task.Delay(1500*(attempt+1)); }
                }
                if(generation!=operation) { await session.LeaveAsync(); return; }
                Match=session; await ConnectedMatchAsync(generation);
            }
            catch(Exception e)
            { if(generation==operation) { Debug.LogWarning("Match connection failed: "+e.Message); await RetryMatchAsync("Kết nối chưa thành công: "+e.Message); } }
            finally { connecting=false; }
        }
        async Task ConnectedMatchAsync(int generation)
        {
            if(generation!=operation) return;
            // NGO can load the court and signal Started before the SDK join Task returns.
            // A late join completion must never move an active game back to Connecting.
            if(State!=FlowState.InMatch && State!=FlowState.Results) SetState(FlowState.Connecting,"Đủ người; đang kết nối vào sân…");
            AttachMatch(Match);
            nextPoll=Time.realtimeSinceStartupAsDouble+4;
            connectionDeadline=Time.realtimeSinceStartupAsDouble+35;
            if(Network.IsHost) ClientIdentities[Network.LocalClientId]=PlayerId;
            if(Match.IsHost) { Match.AsHost().SetProperty("aborted",new SessionProperty("0")); await Match.AsHost().SavePropertiesAsync(); }
        }
        void SetConnectionIdentity() { MultiplayerSceneConfiguration.Validate(); Network.NetworkConfig.ConnectionData=Encoding.UTF8.GetBytes(PlayerId); intentionalShutdown=false; }
        void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,NetworkManager.ConnectionApprovalResponse response)
        {
            string id=Encoding.UTF8.GetString(request.Payload ?? Array.Empty<byte>());
            if(request.ClientNetworkId==Network.LocalClientId && id.Length==0) id=PlayerId;
            if(State==FlowState.Results)
            { response.Approved=false; response.Pending=false; response.Reason="Trận đã kết thúc."; return; }
            if(Roster==null && id!=PlayerId) { response.Pending=true; StartCoroutine(ApproveWhenRosterReady(request,response,id)); return; }
            bool duplicate=ClientIdentities.Any(p=>p.Value==id && p.Key!=request.ClientNetworkId);
            response.Approved=!duplicate && (id==PlayerId || (Roster!=null && Roster.Find(id)!=null));
            response.CreatePlayerObject=false; response.Pending=false;
            response.Reason=response.Approved ? "" : "Người chơi không thuộc trận hoặc đã kết nối.";
            if(response.Approved) ClientIdentities[request.ClientNetworkId]=id;
        }
        System.Collections.IEnumerator ApproveWhenRosterReady(NetworkManager.ConnectionApprovalRequest request,NetworkManager.ConnectionApprovalResponse response,string id)
        {
            double deadline=Time.realtimeSinceStartupAsDouble+10;
            while(Roster==null && Time.realtimeSinceStartupAsDouble<deadline) yield return null;
            response.Approved=Roster!=null && Roster.Find(id)!=null && !ClientIdentities.Values.Contains(id);
            response.CreatePlayerObject=false; response.Pending=false;
            if(response.Approved) ClientIdentities[request.ClientNetworkId]=id;
        }
        void Connected(ulong id) { if(Network.IsHost && id==Network.LocalClientId) ClientIdentities[id]=PlayerId; }
        void Disconnected(ulong id)
        {
            if(intentionalShutdown || quitting || reconnecting) return;
            if(Network.IsHost)
            {
                if(State==FlowState.Connecting) _=Safe(RetryMatchAsync("Có người hủy hoặc mất kết nối; đang ghép lại."));
                else if(State==FlowState.InMatch) NetworkMatch.Instance?.ReserveDisconnected(id);
            }
            else if(State==FlowState.Connecting) _=Safe(RetryMatchAsync("Kết nối bị dừng; đang ghép lại."));
            else if(State==FlowState.InMatch) _=Safe(ReconnectAsync());
        }
        void TransportFailed()
        {
            if(intentionalShutdown) return;
            if(Searching) { _=Safe(RetryMatchAsync("Kết nối bị mất trước khi trận bắt đầu; đang ghép lại.")); return; }
            if(Match?.IsHost==true) _=Safe(ReturnToRoomAsync("Kết nối host bị mất; trận kết thúc."));
            else if(State==FlowState.InMatch) _=Safe(ReconnectAsync());
        }
        async void MatchPlayerLeft(string id)
        {
            if(State==FlowState.Connecting) await Safe(RetryMatchAsync("Có người rời; đang ghép lại."));
            else if(State==FlowState.InMatch && Network.IsServer) NetworkMatch.Instance?.PlayerLeftSession(id);
        }
        void MatchChanged() { if(State==FlowState.Connecting && Match!=null && ((Match.Properties.TryGetValue("aborted",out var p) && p.Value=="1") || Match.Players.Any(player=>Prop(player,"cancelled")=="1"))) _=Safe(RetryMatchAsync("Có người hủy; đang ghép lại.")); }
        void MatchDeleted() { if(!intentionalShutdown) _=Safe(Searching?RetryMatchAsync("Host hủy kết nối; đang ghép lại."):ReturnToRoomAsync("Host đã rời; trận kết thúc.")); }
        void MatchHostChanged(string id) { if(!intentionalShutdown) _=Safe(Searching?RetryMatchAsync("Host hủy kết nối; đang ghép lại."):ReturnToRoomAsync("Host đã rời; trận kết thúc.")); }
        void AttachMatch(ISession session)
        { session.PlayerHasLeft+=MatchPlayerLeft; session.Deleted+=MatchDeleted; session.Changed+=MatchChanged; session.SessionHostChanged+=MatchHostChanged; }
        void DetachMatch(ISession session)
        { session.PlayerHasLeft-=MatchPlayerLeft; session.Deleted-=MatchDeleted; session.Changed-=MatchChanged; session.SessionHostChanged-=MatchHostChanged; }
        async Task ReconnectAsync()
        {
            if(reconnecting || Match==null) return;
            reconnecting=true;
            int generation=operation;
            var previous=Match;
            SetState(FlowState.Reconnecting,"Mất kết nối; đang kết nối lại (tối đa 30 giây)…");
            double until=Time.realtimeSinceStartupAsDouble+Settings.reconnectSeconds;
            try
            {
                intentionalShutdown=true;
                // NGO invokes disconnect callbacks before its automatic shutdown completes.
                await Task.Delay(350);
                await previous.Network.NetworkHandler.StopAsync();
                while(Network.ShutdownInProgress) await Task.Delay(50);
                intentionalShutdown=false;
                while(Time.realtimeSinceStartupAsDouble<until && generation==operation)
                {
                    try
                    {
                        SetConnectionIdentity();
                        var attempt=((SessionNetworkHandler)previous.Network.NetworkHandler).ResumeAsync(previous,()=>generation==operation && Match==previous && Time.realtimeSinceStartupAsDouble<until);
                        var completed=await Task.WhenAny(attempt,Task.Delay(Math.Max(1,(int)((until-Time.realtimeSinceStartupAsDouble)*1000))));
                        if(completed!=attempt) { _=DiscardLateReconnect(attempt); break; }
                        await attempt;
                        if(generation!=operation) return;
                        while(Time.realtimeSinceStartupAsDouble<until && generation==operation &&
                            (!Network.IsConnectedClient || NetworkMatch.Instance==null || NetworkPlayer.All.All(p=>!p.IsLocalHuman)))
                        {
                            if(!Network.IsListening&&!Network.ShutdownInProgress) break;
                            await Task.Delay(50);
                        }
                        if(generation==operation && Network.IsConnectedClient && NetworkMatch.Instance!=null && NetworkPlayer.All.Any(p=>p.IsLocalHuman))
                        { SetState(FlowState.InMatch,"Đã kết nối lại."); return; }
                        await Task.Delay(500);
                    }
                    catch(Exception e) { Debug.LogWarning("Reconnect attempt failed: "+e.Message); await Task.Delay(1000); }
                }
                if(generation==operation) await ReturnToRoomAsync("Không kết nối lại được hoặc host đã rời.");
            }
            finally { reconnecting=false; }
        }
        async Task DiscardLateReconnect(Task attempt)
        { try { await attempt; } catch { /* Expired work must not replace current UI state. */ } }
        async Task RetryMatchAsync(string reason)
        {
            if(!Searching || retrying || cancelling) return;
            retrying=true;
            try
            {
            if(Custom) { await ReturnToRoomAsync(reason); return; }
            int generation=++operation;
            if(Mode==PlayMode.Singles) { rankedSearch.RetryHuman(); clock.Start(Time.realtimeSinceStartupAsDouble,rankedSearch.Elapsed(Time.realtimeSinceStartupAsDouble)); }
            if(Roster!=null) ignoredMatches.Add(Roster.matchId);
            searchCancellation?.Cancel(); searchCancellation?.Dispose(); searchCancellation=new CancellationTokenSource();
            intentionalShutdown=true;
            await AbortMatchAsync(reason); publishedMatch="";
            courtLoading=false; SetState(FlowState.Searching,reason); ExitCourt();
            if(IsCaptain)
            {
                if(Room!=null) { roomData.phase="searching"; roomData.matchId=""; roomData.notice=reason; await SaveRoomAsync(); }
                _=Safe(SearchAsync(generation));
            }
            }
            finally { retrying=false; }
        }
        async Task AbortMatchAsync(string reason)
        {
            var aborted=Match;
            if(aborted!=null)
            {
                if(aborted.IsHost) { aborted.AsHost().SetProperty("aborted",new SessionProperty("1")); await Safe(aborted.AsHost().SavePropertiesAsync()); }
                else { aborted.CurrentPlayer.SetProperty("cancelled",new PlayerProperty("1")); await Safe(aborted.SaveCurrentPlayerDataAsync()); }
            }
            await LeaveMatchAsync();
        }
        async Task LeaveMatchAsync()
        {
            intentionalShutdown=true;
            var old=Match;
            if(old!=null && leavingModePage)
            {
                // The Back flow must retain the session and surface a failed leave for retry.
                await old.LeaveAsync();
                if(Match==old) { DetachMatch(old); Match=null; }
            }
            else
            {
                Match=null;
                if(old!=null) { DetachMatch(old); await Safe(old.LeaveAsync()); }
            }
            if(Network!=null && Network.IsListening) Network.Shutdown();
            ClientIdentities.Clear(); Roster=null;
            courtLoading=false;
        }
        public async Task ReturnToRoomAsync(string message)
        {
            if(returning) return; returning=true;
            try
            {
            await ResolvePendingProgressionAsync(message);
            CancelLocal(); await LeaveMatchAsync();
            if(Room!=null && IsCaptain)
            {
                roomData.phase="idle"; roomData.matchId=""; roomData.notice=message; Room.AsHost().IsLocked=false;
                await Safe(SaveRoomAsync());
            }
            SetState(FlowState.WaitingRoom,message);
            if(SceneManager.GetActiveScene().name!=RoomSceneName) SceneManager.LoadScene(RoomSceneName);
            }
            finally { returning=false; }
        }
        public async Task LeaveGameAsync()
        {
            if(State==FlowState.InMatch && NetworkMatch.Instance!=null) { NetworkMatch.Instance.RequestLeave(); await Task.Delay(300); }
            await ReturnToRoomAsync("Bạn đã rời trận.");
        }
        void ExitCourt()
        {
            if(SceneManager.GetActiveScene().name=="MultiplayerCourt")
            { SceneManager.LoadScene(RoomSceneName); }
        }
        public void CourtLoaded() { SetState(FlowState.Connecting,"Chờ tất cả máy tải sân…"); }
        public void MatchStarted() { clock.Stop(); SetState(FlowState.InMatch,"Trận đã bắt đầu."); }
        public void ShowResult(string message) { SetState(FlowState.Results,message); }
        public void SetNotice(string message) { Notice=message; Changed?.Invoke(); }
        void SetState(FlowState state,string notice) { State=state; Notice=notice; Changed?.Invoke(); }
        public async Task ExecuteAsync(Func<Task> action)
        {
            if(Busy) return; Busy=true; Changed?.Invoke();
            try { await action(); } catch(Exception e) { Notice=e.Message; Debug.LogWarning("Multiplayer: "+e); }
            finally { Busy=false; Changed?.Invoke(); }
        }
        async Task Safe(Task task) { try { await task; } catch(Exception e) { if(!quitting) { Notice=e.Message; Debug.LogWarning("Multiplayer: "+e.Message); } } }
        async void OnApplicationQuit() { quitting=true; searchCancellation?.Cancel(); if(Match!=null) await Safe(Match.LeaveAsync()); if(Room!=null) await Safe(Room.LeaveAsync()); }
    }
}
