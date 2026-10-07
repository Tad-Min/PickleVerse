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

namespace PickleVerse.Multiplayer
{
    public sealed class MultiplayerApp : MonoBehaviour
    {
        public static MultiplayerApp Instance { get; private set; }
        public static bool OpenRoomOnLoad;
        public static bool OpenDefaultRoomOnLoad;
        public string ActiveProfile => profile;
        public MultiplayerSettings Settings { get; private set; }
        public FlowState State { get; private set; } = FlowState.Menu;
        public PlayMode Mode { get; private set; } = PlayMode.Singles;
        public string Notice { get; private set; } = "Chọn hồ sơ test rồi bấm Play.";
        public string DisplayName = "Player", Gender = "Male";
        public string PlayerId => UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn ? AuthenticationService.Instance.PlayerId : "";
        public ISession Room { get; private set; }
        public ISession Match { get; private set; }
        public MatchRoster Roster { get; private set; }
        public bool Custom => roomData.custom;
        public bool IsCaptain => Room == null || Room.IsHost;
        public bool Busy { get; private set; }
        public bool Searching => State == FlowState.Delay || State == FlowState.Searching || State == FlowState.Connecting;
        public bool CanChangeMode => !Busy && State == FlowState.WaitingRoom && !Custom && (Room == null || Room.PlayerCount == 1);
        public string Elapsed => clock.Display(Time.realtimeSinceStartupAsDouble);
        public event Action Changed;
        public NetworkManager Network { get; private set; }
        public readonly Dictionary<ulong,string> ClientIdentities = new Dictionary<ulong,string>();
        readonly GroupMatchmaker groupMatcher = new GroupMatchmaker();
        readonly SearchClock clock = new SearchClock();
        RoomData roomData = new RoomData();
        CancellationTokenSource searchCancellation;
        int operation;
        bool polling, connecting, intentionalShutdown, quitting, retrying, cancelling, returning, courtLoading, preparingSearch, reconnecting;
        double nextPoll, connectionDeadline;
        string observedSearch = "", publishedMatch = "", cancelledSearch = "", profile;
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
            Settings = Resources.Load<MultiplayerSettings>("MultiplayerSettings");
            if (Settings == null) { Notice = "Thiếu MultiplayerSettings."; return; }
            var go = new GameObject("NetworkManager"); DontDestroyOnLoad(go);
            Network = go.AddComponent<NetworkManager>();
            Network.NetworkConfig = new NetworkConfig();
            var transport = go.AddComponent<UnityTransport>();
            transport.HeartbeatTimeoutMS=500; transport.DisconnectTimeoutMS=5000;
            Network.NetworkConfig.NetworkTransport = transport;
            Network.NetworkConfig.TickRate = 50;
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
            requestedProfile = new string((requestedProfile ?? "").Where(char.IsLetterOrDigit).Take(24).ToArray());
            if (requestedProfile.Length == 0) throw new InvalidOperationException("Nhập hồ sơ test, ví dụ Player1 hoặc Player2.");
            if (profile != null && profile != requestedProfile) throw new InvalidOperationException("Khởi động lại game để đổi hồ sơ.");
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync(new InitializationOptions().SetProfile(requestedProfile).SetEnvironmentName(Settings.environment));
            profile = requestedProfile;
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        public async Task PlayAsync(string requestedProfile)
        {
            await InitializeAsync(requestedProfile);
            await LeaveRoomAsync(); Mode = PlayMode.Singles; roomData = new RoomData();
            SetState(FlowState.WaitingRoom, "Phòng 1vs1 — bấm Tìm trận khi sẵn sàng.");
        }
        public async Task SelectModeAsync(PlayMode mode)
        {
            if (State != FlowState.WaitingRoom || Custom || (Room != null && Room.PlayerCount > 1)) throw new InvalidOperationException("Hủy tìm trận hoặc rời nhóm trước khi đổi chế độ.");
            if (mode == Mode) return;
            await LeaveRoomAsync(); Mode = mode; roomData = new RoomData { mode = mode };
            if (mode == PlayMode.Doubles) await CreateRoomAsync(false, mode);
            SetState(FlowState.WaitingRoom, "Phòng " + (mode == PlayMode.Singles ? "1vs1" : "2vs2"));
        }
        Dictionary<string,PlayerProperty> PlayerProperties() => new Dictionary<string,PlayerProperty>
        {
            ["name"] = new PlayerProperty(DisplayName), ["gender"] = new PlayerProperty(Gender),
            ["team"] = new PlayerProperty("-1"), ["ready"] = new PlayerProperty("0"),
            ["cancel"] = new PlayerProperty(""), ["qos"] = new PlayerProperty(""), ["group"] = new PlayerProperty(Room?.Id ?? PlayerId)
        };
        public async Task CreateCustomAsync(PlayMode mode)
        {
            if (Searching) throw new InvalidOperationException("Hủy tìm trận trước.");
            await LeaveRoomAsync(); Mode = mode; await CreateRoomAsync(true, mode);
            await SetTeamAsync(PlayerId, 0); SetState(FlowState.WaitingRoom, "Phòng custom đã tạo.");
        }
        async Task CreateRoomAsync(bool custom, PlayMode mode)
        {
            roomData = new RoomData { custom = custom, mode = mode };
            var options = new SessionOptions { Name = custom ? "PickleVerse Custom" : "PickleVerse Duo", Type = "room", IsPrivate = true,
                MaxPlayers = custom ? 2 * (int)mode : 2, PlayerProperties = PlayerProperties(),
                SessionProperties = new Dictionary<string,SessionProperty> { ["room"] = new SessionProperty(JsonConvert.SerializeObject(roomData), VisibilityPropertyOptions.Member) } };
            AttachRoom(await MultiplayerService.Instance.CreateSessionAsync(options));
        }
        public async Task JoinRoomAsync(string code)
        {
            if (Searching) throw new InvalidOperationException("Hủy tìm trận trước.");
            await LeaveRoomAsync();
            var joined = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant(), new JoinSessionOptions { Type = "room", PlayerProperties = PlayerProperties() });
            AttachRoom(joined); ReadRoom();
            if (roomData.phase != "idle" && (Custom || (roomData.phase=="match" && roomData.roster?.Find(PlayerId)==null)))
            { await LeaveRoomAsync(); throw new InvalidOperationException("Phòng đang tìm hoặc đang chơi."); }
            Mode = roomData.mode;
            if(roomData.phase=="idle") SetState(FlowState.WaitingRoom, "Đã tham gia phòng.");
            else SetNotice("Đã vào nhóm; chủ nhóm đang tìm trận.");
        }
        void AttachRoom(ISession session)
        {
            Room = session;
            nextPoll=Time.realtimeSinceStartupAsDouble+4;
            Room.Changed += RoomChanged;
            Room.RemovedFromSession += RemovedFromRoom;
            Room.Deleted += RemovedFromRoom;
            Room.SessionHostChanged += RoomHostChanged;
            Room.PlayerHasLeft += RoomPlayerLeft;
        }
        void DetachRoom()
        {
            if (Room == null) return;
            Room.Changed -= RoomChanged; Room.RemovedFromSession -= RemovedFromRoom; Room.Deleted -= RemovedFromRoom;
            Room.SessionHostChanged -= RoomHostChanged; Room.PlayerHasLeft -= RoomPlayerLeft;
        }
        void RoomChanged()
        {
            if (!quitting) ReadRoom();
            if(!quitting && Room!=null && IsCaptain && Searching && !cancelling && Room.Players.Any(p=>Prop(p,"cancel")==observedSearch)) _=Safe(CancelSearchAsync());
            Changed?.Invoke();
        }
        void RemovedFromRoom() { DetachRoom(); Room = null; roomData = new RoomData(); CancelLocal(); SetState(FlowState.WaitingRoom,"Phòng đã đóng hoặc bạn đã được mời ra."); }
        async void RoomHostChanged(string id)
        {
            if (Custom) { await Safe(LeaveRoomAsync()); SetState(FlowState.WaitingRoom,"Chủ phòng custom đã rời."); }
            else if (IsCaptain && Searching) await Safe(CancelSearchAsync());
        }
        async void RoomPlayerLeft(string id) { if (IsCaptain && Searching) await Safe(CancelSearchAsync()); Changed?.Invoke(); }
        void ReadRoom()
        {
            if (Room == null || !Room.Properties.TryGetValue("room",out var value)) return;
            var data = JsonConvert.DeserializeObject<RoomData>(value.Value);
            if (data == null) return;
            int previousVersion=roomData.teamVersions.TryGetValue(PlayerId,out var previous)?previous:0;
            roomData = data; Mode = data.mode;
            if(data.custom && data.teamVersions.TryGetValue(PlayerId,out var version) && version!=previousVersion)
                Notice="Đội của bạn đã đổi; cần Sẵn sàng lại.";
            if(data.searchId == cancelledSearch && data.phase != "idle") return;
            if (State == FlowState.InMatch || State == FlowState.Reconnecting || State == FlowState.Results) return;
            if (data.phase == "idle")
            {
                if (Searching && !IsCaptain) { CancelLocal(); SetState(FlowState.WaitingRoom, data.notice); }
                return;
            }
            if (data.searchId != observedSearch)
            {
                CancelLocal(); observedSearch = data.searchId;
                double elapsed = Math.Max(0,(DateTime.UtcNow.Ticks - data.startedUtcTicks)/(double)TimeSpan.TicksPerSecond);
                clock.Start(Time.realtimeSinceStartupAsDouble,elapsed);
                searchCancellation = new CancellationTokenSource();
            }
            if (!IsCaptain && data.phase != "match") SetState(clock.CanSubmit(Time.realtimeSinceStartupAsDouble) ? FlowState.Searching : FlowState.Delay,"Chủ nhóm đang tìm trận.");
            if (data.phase == "match" && !ignoredMatches.Contains(data.matchId) && !connecting && publishedMatch != data.matchId && data.roster != null)
            { publishedMatch = data.matchId; _ = Safe(ConnectMatchAsync(data.roster, operation)); }
        }
        public List<RoomMember> Members()
        {
            if (Room == null) return new List<RoomMember> { new RoomMember { id=PlayerId,name=DisplayName,gender=Gender,team=0,group=PlayerId } };
            return Room.Players.Select(p => new RoomMember { id=p.Id,name=Prop(p,"name","Player"),gender=Prop(p,"gender","Male"),
                team=roomData.custom?(roomData.teams.TryGetValue(p.Id,out var t)?t:-1):0,
                ready=Prop(p,"ready","0")=="1" && Prop(p,"readyEpoch")==roomData.readyEpoch.ToString() && roomData.teamVersions.TryGetValue(p.Id,out var version) && Prop(p,"readyVersion")==version.ToString(),group=Room.Id }).ToList();
        }
        public static string Prop(IReadOnlyPlayer p,string key,string fallback="") => p.Properties.TryGetValue(key,out var value) ? value.Value : fallback;
        public async Task SetTeamAsync(string id,int team)
        {
            if (!Custom || State != FlowState.WaitingRoom || (id != PlayerId && !IsCaptain)) throw new InvalidOperationException("Không được đổi đội lúc này.");
            if (team < 0 || team > 1) throw new InvalidOperationException("Đội không hợp lệ.");
            var members=Members(); int previousTeam=members.Find(m=>m.id==id)?.team??-1;
            if(previousTeam==team) return;
            bool full=members.Count(p=>p.team==team && p.id!=id)>=(int)Mode;
            if(full && !IsCaptain) throw new InvalidOperationException("Đội đã đủ người.");
            if(!IsCaptain)
            {
                Room.CurrentPlayer.SetProperty("teamRequest",new PlayerProperty(team.ToString()));
                Room.CurrentPlayer.SetProperty("teamRequestId",new PlayerProperty(Guid.NewGuid().ToString("N")));
                await Room.SaveCurrentPlayerDataAsync(); SetNotice("Đang yêu cầu đổi đội…"); return;
            }
            if(full)
            {
                var swap=members.First(p=>p.team==team&&p.id!=id);
                roomData.teams[swap.id]=previousTeam;
                roomData.teamVersions[swap.id]=(roomData.teamVersions.TryGetValue(swap.id,out var swappedVersion)?swappedVersion:0)+1;
            }
            roomData.teams[id]=team;
            roomData.teamVersions[id]=(roomData.teamVersions.TryGetValue(id,out var version)?version:0)+1;
            roomData.notice = full?"Đã hoán đổi đội; cần Sẵn sàng lại.":"Đã đổi đội; cần Sẵn sàng lại."; await SaveRoomAsync();
            Changed?.Invoke();
        }
        public async Task ReadyAsync()
        {
            if (!Custom || State!=FlowState.WaitingRoom) return;
            var member=Members().Find(m=>m.id==PlayerId); if(member==null || member.team<0) return;
            var p=Room.CurrentPlayer; p.SetProperty("ready",new PlayerProperty(member.ready?"0":"1"));
            p.SetProperty("readyEpoch",new PlayerProperty(roomData.readyEpoch.ToString()));
            p.SetProperty("readyVersion",new PlayerProperty(roomData.teamVersions[PlayerId].ToString()));
            await Room.SaveCurrentPlayerDataAsync(); Changed?.Invoke();
        }
        public async Task KickAsync(string id)
        {
            if (!Custom || !IsCaptain || State!=FlowState.WaitingRoom || id==PlayerId) throw new InvalidOperationException("Không được mời người này ra.");
            await Room.AsHost().RemovePlayerAsync(id);
        }
        public async Task FindAsync()
        {
            if (State!=FlowState.WaitingRoom || Custom || !IsCaptain || Searching) return;
            CancelLocal(); observedSearch=Guid.NewGuid().ToString("N"); publishedMatch="";
            cancelledSearch=""; measured.Clear(); ignoredMatches.Clear();
            searchCancellation=new CancellationTokenSource(); clock.Start(Time.realtimeSinceStartupAsDouble);
            SetState(FlowState.Delay,"Đang chờ trước khi tìm trận.");
            int requestGeneration=operation;
            if(Mode==PlayMode.Doubles && Room==null)
            {
                preparingSearch=true;
                try { await CreateRoomAsync(false,PlayMode.Doubles); }
                finally { preparingSearch=false; }
                if(requestGeneration!=operation) return;
            }
            if (Room!=null)
            {
                roomData.phase="delay"; roomData.searchId=observedSearch; roomData.startedUtcTicks=DateTime.UtcNow.Ticks; roomData.matchId=""; roomData.notice=""; roomData.generation++;
                int generation=operation;
                Room.AsHost().IsLocked=true; await SaveRoomAsync();
                if(generation!=operation && Room!=null && IsCaptain)
                { roomData.phase="idle"; Room.AsHost().IsLocked=false; await SaveRoomAsync(); }
            }
        }
        public async Task CancelSearchAsync()
        {
            if (!Searching || cancelling) return;
            cancelling=true; cancelledSearch=observedSearch;
            try
            {
            if (Room!=null && !IsCaptain)
            {
                CancelLocal();
                Room.CurrentPlayer.SetProperty("cancel",new PlayerProperty(cancelledSearch)); await Room.SaveCurrentPlayerDataAsync();
                await AbortMatchAsync("Một thành viên nhóm đã hủy."); SetState(FlowState.WaitingRoom,"Đã hủy tìm trận cho nhóm."); ExitCourt(); return;
            }
            CancelLocal(); await AbortMatchAsync("Một người đã hủy tìm trận.");
            if (Room!=null && IsCaptain) { roomData.phase="idle"; roomData.matchId=""; roomData.notice="Đã hủy tìm trận cho nhóm."; Room.AsHost().IsLocked=false; await SaveRoomAsync(); }
            SetState(FlowState.WaitingRoom,"Đã hủy tìm trận.");
            ExitCourt();
            }
            finally { cancelling=false; Changed?.Invoke(); }
        }
        void CancelLocal() { operation++; searchCancellation?.Cancel(); searchCancellation?.Dispose(); searchCancellation=null; clock.Stop(); }
        async Task SaveRoomAsync() { Room.AsHost().SetProperty("room",new SessionProperty(JsonConvert.SerializeObject(roomData),VisibilityPropertyOptions.Member)); await Room.AsHost().SavePropertiesAsync(); nextPoll=Time.realtimeSinceStartupAsDouble+4; }
        async void Update()
        {
            if (Settings==null || quitting) return;
            double now=Time.realtimeSinceStartupAsDouble;
            if (Searching && clock.TimedOut(now)) { await Safe(CancelSearchAsync()); SetState(FlowState.WaitingRoom,"Hết thời gian tìm trận. Hãy thử lại."); }
            if (State==FlowState.Delay && clock.CanSubmit(now) && IsCaptain && !connecting && !preparingSearch)
            { SetState(FlowState.Searching,"Đang tìm trận…"); _=Safe(SearchAsync(operation)); }
            if (Room!=null && now>=nextPoll && !polling && !connecting)
            {
                nextPoll=now+4; polling=true;
                try
                {
                    await Room.RefreshAsync(); ReadRoom();
                    if(Custom && IsCaptain && State==FlowState.WaitingRoom)
                    {
                        foreach(var member in Members().Where(p=>p.team<0).ToArray())
                        { int team=Members().Count(p=>p.team==0)<(int)Mode?0:1; await SetTeamAsync(member.id,team); }
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
                    if (Searching && Mode==PlayMode.Doubles && !measured.ContainsKey(PlayerId)) await PublishQosAsync();
                }
                catch(Exception e) { if (Room!=null) Notice="Đang cập nhật phòng: "+e.Message; }
                finally { polling=false; Changed?.Invoke(); }
            }
            if (State==FlowState.Connecting && Match!=null && Network.IsHost)
            {
                if (!courtLoading && Roster.members.All(m=>ClientIdentities.Values.Contains(m.id)) && Network.ConnectedClients.Count==Roster.Count)
                {
                    if (Match.Players.Any(p=>Prop(p,"cancelled")=="1")) { await RetryMatchAsync("Có người hủy; đang ghép lại."); return; }
                    courtLoading=true; SetNotice("Đang tải sân cho tất cả người chơi…");
                    Network.SceneManager.LoadScene("MultiplayerCourt",LoadSceneMode.Single);
                }
                else if(now>connectionDeadline) await RetryMatchAsync("Một người chưa kết nối được; đang ghép lại.");
            }
        }
        async Task PublishQosAsync()
        {
            var result=await groupMatcher.MeasureAsync(Settings.doublesQueue,Settings.maximumLatencyMs);
            measured[PlayerId]=result;
            if(Room!=null) { Room.CurrentPlayer.SetProperty("qos",new PlayerProperty(JsonConvert.SerializeObject(result))); await Room.SaveCurrentPlayerDataAsync(); }
        }
        async Task SearchAsync(int generation)
        {
            if(generation!=operation || searchCancellation==null) return;
            var token=searchCancellation.Token;
            try
            {
                if(Room!=null && IsCaptain) { roomData.phase="searching"; await SaveRoomAsync(); }
                if(Mode==PlayMode.Singles)
                {
                    var qos=await groupMatcher.MeasureAsync(Settings.singlesQueue,Settings.maximumLatencyMs); token.ThrowIfCancellationRequested();
                    var properties=PlayerProperties(); properties["region"]=new PlayerProperty(qos[0].RegionId);
                    SetConnectionIdentity();
                    var session=await MultiplayerService.Instance.MatchmakeSessionAsync(new MatchmakerOptions { QueueName=Settings.singlesQueue,PlayerProperties=properties },MatchOptions(2,qos[0].RegionId),token);
                    if(generation!=operation || token.IsCancellationRequested) { await session.LeaveAsync(); return; }
                    Match=session;
                    var result=session.GetMatchmakingResults();
                    Roster=GroupMatchmaker.Roster(result.MatchId,result.MatchProperties.Region,result.MatchProperties.Teams,result.MatchProperties.Players,Mode);
                    await ConnectedMatchAsync(generation);
                }
                else
                {
                    if(!measured.ContainsKey(PlayerId)) await PublishQosAsync();
                    var members=Members(); var qos=new Dictionary<string,List<QosResult>>();
                    while(!token.IsCancellationRequested)
                    {
                        foreach(var member in members)
                        {
                            var json=Prop(Room.GetPlayer(member.id),"qos");
                            if(!string.IsNullOrEmpty(json)) qos[member.id]=JsonConvert.DeserializeObject<List<QosResult>>(json);
                        }
                        if(qos.Count==members.Count) break;
                        await Task.Delay(500,token);
                    }
                    token.ThrowIfCancellationRequested();
                    var result=await groupMatcher.FindAsync(Settings.doublesQueue,members,qos,token);
                    if(generation!=operation) return;
                    var roster=GroupMatchmaker.Roster(result.MatchId,result.MatchProperties.Region,result.MatchProperties.Teams,result.MatchProperties.Players,Mode);
                    roomData.roster=roster; roomData.matchId=roster.matchId; roomData.phase="match";
                    publishedMatch=roster.matchId; await SaveRoomAsync(); await ConnectMatchAsync(roster,generation);
                }
            }
            catch(Exception e)
            {
                if(generation!=operation || token.IsCancellationRequested) return;
                Debug.LogWarning("Matchmaking attempt failed: "+e);
                await CancelSearchAsync(); SetState(FlowState.WaitingRoom,"Không tìm được trận: "+e.Message);
            }
        }
        SessionOptions MatchOptions(int count,string region)
        {
            int generation=operation;
            return new SessionOptions { Name="PickleVerse Match",Type="match",MaxPlayers=count,IsPrivate=true,
                PlayerProperties=new Dictionary<string,PlayerProperty> { ["identity"]=new PlayerProperty(PlayerId),["cancelled"]=new PlayerProperty("0") } }.WithRelayNetwork(new RelayNetworkOptions(region,true)).WithNetworkHandler(new SessionNetworkHandler(Network,()=>generation==operation && !quitting));
        }
        public async Task StartCustomAsync()
        {
            if(!Custom || !IsCaptain || State!=FlowState.WaitingRoom) return;
            var members=Members();
            if(members.Count!=2*(int)Mode || members.Any(m=>!m.ready) || members.Count(m=>m.team==0)!=(int)Mode || members.Count(m=>m.team==1)!=(int)Mode)
                throw new InvalidOperationException("Cần đủ người, đủ hai đội và tất cả Sẵn sàng.");
            CancelLocal(); searchCancellation=new CancellationTokenSource(); observedSearch=Guid.NewGuid().ToString("N");
            roomData.roster=new MatchRoster { matchId="custom_"+Guid.NewGuid().ToString("N"),mode=Mode,members=members };
            roomData.phase="match"; roomData.matchId=roomData.roster.matchId; roomData.searchId=observedSearch; roomData.startedUtcTicks=DateTime.UtcNow.Ticks;
            // Readiness is consumed when the captain commits this match. Every peer observes
            // the new epoch before connecting, so returning guests cannot reuse stale readiness.
            roomData.readyEpoch++;
            Room.AsHost().IsLocked=true; publishedMatch=roomData.matchId; await SaveRoomAsync(); await ConnectMatchAsync(roomData.roster,operation);
        }
        async Task ConnectMatchAsync(MatchRoster roster,int generation)
        {
            if(connecting) return; connecting=true; Roster=roster; SetState(FlowState.Connecting,"Đủ người; đang kết nối vào sân…");
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
            connectionDeadline=Time.realtimeSinceStartupAsDouble+35;
            if(Network.IsHost) ClientIdentities[Network.LocalClientId]=PlayerId;
            if(Match.IsHost) { Match.AsHost().SetProperty("aborted",new SessionProperty("0")); await Match.AsHost().SavePropertiesAsync(); }
        }
        void SetConnectionIdentity() { Network.NetworkConfig.ConnectionData=Encoding.UTF8.GetBytes(PlayerId); intentionalShutdown=false; }
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
                            (!Network.IsConnectedClient || NetworkMatch.Instance==null || NetworkPlayer.All.All(p=>!p.IsOwner)))
                        {
                            if(!Network.IsListening&&!Network.ShutdownInProgress) break;
                            await Task.Delay(50);
                        }
                        if(generation==operation && Network.IsConnectedClient && NetworkMatch.Instance!=null && NetworkPlayer.All.Any(p=>p.IsOwner))
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
            var old=Match; Match=null;
            if(old!=null) { DetachMatch(old); await Safe(old.LeaveAsync()); }
            if(Network!=null && Network.IsListening) Network.Shutdown();
            ClientIdentities.Clear(); Roster=null;
            courtLoading=false;
        }
        public async Task LeaveRoomAsync()
        {
            if(Searching) await CancelSearchAsync();
            var old=Room; DetachRoom(); Room=null;
            if(old!=null)
            {
                if(old.IsHost && roomData.custom) await old.AsHost().DeleteAsync();
                else { if(old.IsHost && old.PlayerCount>1) { old.AsHost().Host=old.Players.First(p=>p.Id!=PlayerId).Id; await old.AsHost().SavePropertiesAsync(); } await old.LeaveAsync(); }
            }
            roomData=new RoomData(); measured.Clear();
        }
        public async Task ReturnToRoomAsync(string message)
        {
            if(returning) return; returning=true;
            try
            {
            CancelLocal(); await LeaveMatchAsync();
            if(Room!=null && IsCaptain)
            {
                roomData.phase="idle"; roomData.matchId=""; roomData.notice=message; Room.AsHost().IsLocked=false;
                await Safe(SaveRoomAsync());
            }
            SetState(FlowState.WaitingRoom,message); OpenRoomOnLoad=true;
            if(SceneManager.GetActiveScene().name!="MultiplayerTest") SceneManager.LoadScene("MultiplayerTest");
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
            { OpenRoomOnLoad=true; SceneManager.LoadScene("MultiplayerTest"); }
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
