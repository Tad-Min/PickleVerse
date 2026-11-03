using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using Newtonsoft.Json;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    [Serializable]
    public sealed class PhotonMessage { public uint target; public string kind, payload; }
    public interface IPhotonMessageTarget { void ReceivePhotonMessage(PlayerRef source, string kind, string payload); }
    [Serializable]
    public sealed class PhotonSeat
    {
        public RoomMember member;
        public int peer;
        public ReadyDecision decision;
    }
    [Serializable]
    public sealed class PhotonRoomData
    {
        public bool party, custom, night;
        public PlayMode mode;
        public CourtVenue venue;
        public string matchId = "", matchSession = "", phase = "idle", notice = "";
        public double deadline;
        public int expectedPlayers;
        public MatchRoster assignment;
        public int authority, revision;
        public List<PhotonSeat> seats = new List<PhotonSeat>();
        public MatchRoster Roster(string region) => new MatchRoster { matchId = matchId, mode = mode, custom = custom, night = night, venue = venue, region = region, members = seats.Select(s => s.member).ToList() };
    }
    public sealed partial class PhotonSession
    {
        static readonly Dictionary<NetworkRunner, PhotonSession> sessions = new Dictionary<NetworkRunner, PhotonSession>();
        public NetworkRunner Runner { get; private set; }
        public PhotonCallbacks Callbacks { get; private set; }
        public PhotonRoomState Lobby { get; private set; }
        public PhotonCommandChannel Channel { get; private set; }
        public void BindChannel(PhotonCommandChannel channel)
        {
            Channel = channel;
        }
        string sessionName = "", sessionRegion = "";
        Task leaving;
        public string Id => Runner && Runner.IsRunning ? Runner.SessionInfo.Name : sessionName;
        public string Region => Runner && Runner.IsRunning ? Runner.SessionInfo.Region : sessionRegion;
        public string Code => Id;
        public bool IsHost => Runner && Runner.IsRunning && (Runner.IsSharedModeMasterClient || Runner.IsServer);
        public int PlayerCount => Data.seats.Count;
        public PhotonRoomData Data => Lobby && Lobby.Object && Lobby.Object.IsValid ? Lobby.Data : dedicatedData ?? new PhotonRoomData();
        public bool Closing { get; private set; }
        public event Action Changed;
        public event Action<NetworkRunner, PlayerRef> PeerJoined, PeerLeft;
        public event Action<ShutdownReason> Stopped;
        public event Action<NetworkRunner> SceneStarted, SceneDone;
        public string Rejected { get; private set; }
        public string LastMessage { get; private set; }
        public readonly List<string> MessageTrace = new List<string>();
        public static PhotonSession Find(NetworkRunner runner) => runner && sessions.TryGetValue(runner, out var session) ? session : null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            sessions.Clear();
        }
        public static PhotonSession Create(string label)
        {
            var go = new GameObject(label);
            UnityEngine.Object.DontDestroyOnLoad(go);
            var session = new PhotonSession { Runner = go.AddComponent<NetworkRunner>(), Callbacks = go.AddComponent<PhotonCallbacks>() };
            sessions.Add(session.Runner, session);
            session.Runner.AddCallbacks(session.Callbacks);
            session.Callbacks.Joined = (r, p) => session.PeerJoined?.Invoke(r, p);
            session.Callbacks.Left = (r, p) =>
            {
                session.PeerLeft?.Invoke(r, p);
                if (session.Lobby)
                    session.Lobby.PlayerGone(p);
            };
            session.Callbacks.Stopped = (r, reason) =>
            {
                if (!session.Closing)
                    session.Stopped?.Invoke(reason);
            };
            session.Callbacks.SceneStarted = r => session.SceneStarted?.Invoke(r);
            session.Callbacks.SceneDone = r => session.SceneDone?.Invoke(r);
            return session;
        }
        public void Send(PlayerRef destination, NetworkObject target, string kind, object payload)
        {
            var message = new PhotonMessage { target = target.Id.Raw, kind = kind, payload = JsonConvert.SerializeObject(payload) };
            if (IsDedicated) { SendDedicated(destination, message); return; }
            if (MessageTrace.Count < 12)
                MessageTrace.Add("send " + kind + " to " + destination.RawEncoded + " object " + target.Id.Raw);
            if (JsonConvert.SerializeObject(message).Length > 3500)
                throw new InvalidOperationException("Lệnh mạng vượt giới hạn.");
            if (destination == Runner.LocalPlayer)
                Deliver(Runner.LocalPlayer, message);
            else Channel.Enqueue(destination, message);
        }
        public bool Deliver(PlayerRef source, PhotonMessage message)
        {
            if (Closing || !Runner || !Runner.IsRunning)
                return false;
            try
            {
                LastMessage = message?.kind + " from " + source.RawEncoded;
                if (MessageTrace.Count < 12)
                    MessageTrace.Add(LastMessage + " object " + message?.target);
                if (message == null || message.kind == null || message.kind.Length > 40 || message.payload == null)
                    return true;
                if (!Runner.TryFindObject(new NetworkId { Raw = message.target }, out var target))
                    return false;
                foreach (var component in target.GetComponents<MonoBehaviour>())
                    if (component is IPhotonMessageTarget receiver)
                        receiver.ReceivePhotonMessage(source, message.kind, message.payload);
                return true;
            }
            catch (JsonException)
            {
                Debug.LogWarning("Bỏ qua lệnh Photon không hợp lệ.");
                return true;
            }
        }
        public static void Bind(PhotonRoomState lobby)
        {
            var session = Find(lobby.Runner);
            if (session == null)
                return;
            session.Lobby = lobby;
            session.Notify();
        }
        public void Notify()
        {
            Changed?.Invoke();
        }
        public void Reject(int peer, string reason)
        {
            if (Runner && Runner.LocalPlayer.RawEncoded == peer)
            {
                Rejected = reason;
                Notify();
            }
        }
        public static FusionAppSettings Settings(string region)
        {
            var settings = PhotonAppSettings.Global.AppSettings.GetCopy();
            // Do not let older index-based clients join path-based scene sessions.
            settings.AppVersion += ".protocol-" + Services.MultiplayerSceneConfiguration.ProtocolVersion;
            if (!string.IsNullOrEmpty(region))
                settings.FixedRegion = region;
            return settings;
        }
        static NetworkSceneInfo InitialScene()
        {
            int index = UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(Services.MultiplayerSceneConfiguration.LobbyPath);
            if (index < 0)
                throw new InvalidOperationException("Thiếu PhotonLobby trong danh sách build.");
            var scene = new NetworkSceneInfo();
            scene.AddSceneRef(SceneRef.FromPath(Services.MultiplayerSceneConfiguration.LobbyPath), UnityEngine.SceneManagement.LoadSceneMode.Additive);
            return scene;
        }
        public async Task StartAsync(string name, string playerId, string region, PhotonRoomData initial, GameObject statePrefab)
        {
            var sceneManager = Runner.gameObject.AddComponent<PhotonSceneManager>();
            var result = await Runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Shared,
                  Config = ConfigForSession(false),
                SessionName = name,
                PlayerCount = initial.party ? (initial.custom ? 2 * (int)initial.mode : 2) : 2 * (int)initial.mode,
                IsVisible = false,
                CustomLobbyName = "pickleverse-online",
                SceneManager = sceneManager,
                Scene = InitialScene(),
                AuthValues = new Photon.Realtime.AuthenticationValues(playerId),
                CustomPhotonAppSettings = Settings(region),
                SessionProperties = new Dictionary<string, SessionProperty>{{"kind", initial.party?0:initial.custom?2:1}, {"mode", (int)initial.mode}, {"protocol", (int)Services.MultiplayerSceneConfiguration.ProtocolVersion}, {"stage", 0}, {"fit1", true}, {"fit2", true},
                    {"elo", Progression.ProgressionRuntime.Instance.Player.elo}, {"losses", Progression.ProgressionRuntime.Instance.Player.rankLossStreak}, {"expanded", false}}
            });
            if (!result.Ok)
                throw new InvalidOperationException("Không kết nối được Photon. Vui lòng kiểm tra mạng và thử lại.");
            sessionName = Runner.SessionInfo.Name;
            sessionRegion = Runner.SessionInfo.Region;
            if (Runner.IsSharedModeMasterClient)
            {
                initial.authority = Runner.LocalPlayer.RawEncoded;
                if (string.IsNullOrEmpty(initial.matchId))
                    initial.matchId = Guid.NewGuid().ToString("N");
                await Runner.SpawnAsync(statePrefab, onBeforeSpawned: (r, obj) => obj.GetComponent<PhotonRoomState>().Initialize(initial));
            }
            await WaitAsync(() => Lobby, "Chưa nhận được trạng thái phòng Photon.");
            await CreateChannel();
        }
        public async Task JoinAsync(string name, string playerId, string region, bool party)
        {
            var sceneManager = Runner.gameObject.AddComponent<PhotonSceneManager>();
            var result = await Runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Shared,
                  Config = ConfigForSession(false),
                SessionName = name,
                EnableClientSessionCreation = false,
                CustomLobbyName = "pickleverse-online",
                SceneManager = sceneManager,
                Scene = InitialScene(),
                AuthValues = new Photon.Realtime.AuthenticationValues(playerId),
                CustomPhotonAppSettings = Settings(region)
            });
            if (!result.Ok)
                throw new InvalidOperationException("Không vào được phòng Photon. Vui lòng thử lại hoặc chọn phòng khác.");
            sessionName = Runner.SessionInfo.Name;
            sessionRegion = Runner.SessionInfo.Region;
            await WaitAsync(() => Lobby, "Chưa nhận được trạng thái phòng Photon.");
            await CreateChannel();
        }
        async Task CreateChannel()
        {
            var prefab = Resources.Load<GameObject>("Photon/Command Channel");
            if (!prefab)
                throw new InvalidOperationException("Thiếu prefab kênh lệnh Photon.");
            await Runner.SpawnAsync(prefab);
            await WaitAsync(() => Channel, "Chưa tạo được kênh lệnh Photon.");
        }
        public async Task ReserveAsync(List<RoomMember> members)
        {
            Rejected = null;
            Lobby.Reserve(JsonConvert.SerializeObject(members));
            string id = Runner.GetPlayerUserId(Runner.LocalPlayer);
            await WaitAsync(() => Rejected != null || Data.seats.Any(s => s.member.id == id && s.peer == Runner.LocalPlayer.RawEncoded), "Phòng chưa xác nhận chỗ của bạn.");
            if (Rejected != null)
                throw new InvalidOperationException(Rejected);
        }
        public static async Task WaitAsync(Func<bool> condition, string message, double seconds = 15)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble >= until)
                    throw new TimeoutException(message);
                await Task.Delay(50);
            }
        }
        public Task LeaveAsync() => leaving ?? (leaving = LeaveCoreAsync());
        async Task LeaveCoreAsync()
        {
            Closing = true;
            var runner = Runner;
            try
            {
                if (runner)
                    await runner.Shutdown();
            }
            finally
            {
                if (runner)
                {
                    sessions.Remove(runner);
                    UnityEngine.Object.Destroy(runner.gameObject);
                }
                Lobby = null;
            }
        }
    }
}
