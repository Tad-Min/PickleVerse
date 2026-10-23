using System;
using System.Threading.Tasks;
using PickleVerse.Services;
using UnityEngine;

namespace PickleVerse.Progression
{
    /// <summary>In-memory view of server-owned game state. No local player saves.</summary>
    public sealed class ProgressionRuntime : MonoBehaviour
    {
        static ProgressionRuntime instance;
        static bool shuttingDown;
        AccountService account;
        PlayerProfile player = Empty();
        ProgressionConfig rules;
        Task loading = Task.CompletedTask;
        int loadedSession = -1;
        public OnlineGameSnapshot Snapshot { get; private set; }
        public bool Ready => account != null && account.IsSignedIn && loadedSession == account.SessionVersion && Snapshot != null;
        public string Error { get; private set; }
        public static ProgressionRuntime Instance
        {
            get
            {
                if (!Application.isPlaying || shuttingDown) return null;
                if (!instance) new GameObject("Online Player Profile").AddComponent<ProgressionRuntime>();
                return instance;
            }
        }
        public static bool Exists => !shuttingDown && instance && instance.player != null;
        public PlayerProfile Player => player;
        public bool Online => true;
        public bool IsAuthenticated => Ready;
        public ProgressionEngine Engine { get; private set; }
        public IUtcClock UtcClock { get; set; } = new SystemUtcClock();
        public static bool DeveloperControls => false;
        public static event Action Changed;
        public static event Action Quitting;
        static PlayerProfile Empty() => new PlayerProfile { id = "", playerName = "Chưa đăng nhập", online = true };
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { instance = null; shuttingDown = false; Changed = null; Quitting = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Boot() { var runtime = Instance; }
        void Awake()
        {
            if (instance && instance != this) { Destroy(gameObject); return; }
            instance = this; DontDestroyOnLoad(gameObject);
            rules = ScriptableObject.CreateInstance<ProgressionConfig>();
            rules.definitions.Clear(); Engine = new ProgressionEngine(rules);
            account = AccountRuntime.Ensure().Account; account.Changed += OnAccountChanged; OnAccountChanged();
        }
        void OnAccountChanged()
        {
            if (!account.IsSignedIn || loadedSession != account.SessionVersion)
            {
                player = Empty(); Snapshot = null; loadedSession = -1; Error = null;
            }
            Changed?.Invoke();
            if (account.IsSignedIn) loading = LoadSafelyAsync();
        }
        async Task LoadSafelyAsync()
        {
            int version = account.SessionVersion;
            try { await ReloadAsync(); }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (version != account.SessionVersion || !this) return;
                Error = error.Message; Changed?.Invoke();
            }
        }
        public async Task EnsureReadyAsync()
        {
            await loading;
            if (!Ready) await ReloadAsync();
        }
        public async Task ReloadAsync()
        {
            var state = await account.SendAuthorizedAsync<OnlineGameSnapshot>("GET", "api/Game/state");
            ApplySnapshot(state);
        }
        void ApplySnapshot(OnlineGameSnapshot state)
        {
            if (!this || !account.IsSignedIn) return;
            if (state?.player == null || state.catalog?.definitions == null || state.catalog.lessons == null
                || state.catalog.cosmetics == null || state.inventory?.owned == null || state.tutorial == null
                || state.catalog.matchmaking == null || state.catalog.onlineMatch == null
                || state.catalog.matchmaking.botFallbackSeconds <= 0 || state.catalog.matchmaking.readyConfirmationSeconds <= 0
                || state.catalog.onlineMatch.heartbeatSeconds <= 0
                || state.catalog.onlineMatch.disconnectTimeoutSeconds <= state.catalog.onlineMatch.heartbeatSeconds
                || state.player.id != account.Profile.UserId.ToString("N"))
                throw new InvalidOperationException("Dữ liệu trò chơi từ máy chủ chưa đầy đủ.");
            if (Ready && state.revision < Snapshot.revision) return;
            TrainingCurriculum.Configure(state.catalog.lessons);
            rules.winXp = state.catalog.winXp; rules.lossXp = state.catalog.lossXp;
            rules.dailyBonus = state.catalog.dailyBonus; rules.weeklyBonus = state.catalog.weeklyBonus;
            rules.definitions = state.catalog.definitions;
            state.player.online = true; player = state.player; Snapshot = state;
            loadedSession = account.SessionVersion; Error = null; Changed?.Invoke();
        }
        public async Task<OnlineGameSnapshot> CommandAsync(string action, object request)
        {
            await EnsureReadyAsync();
            var state = await account.SendAuthorizedAsync<OnlineGameSnapshot>("POST", "api/Game/" + action, request);
            ApplySnapshot(state); return state;
        }
        public Task ClaimAsync(string id) => CommandAsync("claim", new { RequestId = Guid.NewGuid().ToString("N"), Code = id });
        public Task<OnlineGameSnapshot> StartTrainingAsync(string id, bool retry) => CommandAsync("training/start",
            new { RequestId = Guid.NewGuid().ToString("N"), LessonCode = id, Retry = retry });
        public Task<OnlineGameSnapshot> SaveTrainingAsync(TrainingAttempt attempt, bool completed, string requestId) =>
            CommandAsync(completed ? "training/complete" : "training/checkpoint", new { RequestId = requestId, Attempt = attempt });
        public Task<OnlineGameSnapshot> StartBotAsync(MatchKind kind,string requestId) =>
            CommandAsync("match/bot/start",new {RequestId=requestId,Kind=kind});
        public Task<OnlineGameSnapshot> FinishBotAsync(MatchResult result,string requestId) =>
            CommandAsync("match/bot/complete",new {RequestId=requestId,Result=result});
        public Task<OnlineGameSnapshot> SetTutorialAsync(string key,string requestId) =>
            CommandAsync("tutorial",new {RequestId=requestId,Key=key});
        public async void RefreshPeriods()
        {
            try { if (Ready) await ReloadAsync(); }
            catch (Exception error) { Error = error.Message; Changed?.Invoke(); }
        }
        // Unported callers cannot award or mutate server-owned state locally.
        public bool Claim(string id) => false;
        public bool Apply(MatchResult result) => false;
        public void BeginMatch(MatchResult match) { }
        public void SetName(string name) => throw new InvalidOperationException("Hãy cập nhật tên trong mục Tài khoản.");
        public void EditLocalPlayer(string name, int elo, long tokenpad, long eliteToken) => throw new InvalidOperationException("Hồ sơ chỉ được cập nhật qua máy chủ.");
        public void SwitchPlayer(string id, string name) => throw new InvalidOperationException("Hãy đăng xuất và đăng nhập tài khoản khác.");
        public void UpdateWallet(Action<PlayerProfile> mutation) => throw new InvalidOperationException("Cửa hàng chưa sẵn sàng.");
        public void Save() { }
        void Update()
        {
            if (Ready && AppActivityRuntime.Instance)
                player.activeSeconds = Math.Max(player.activeSeconds, AppActivityRuntime.Instance.Seconds);
        }
        void OnApplicationQuit() { Quitting?.Invoke(); shuttingDown = true; }
        void OnDestroy()
        {
            if (instance != this) return;
            if (account != null) account.Changed -= OnAccountChanged;
            if (rules) Destroy(rules);
            instance = null; shuttingDown = true;
        }
        public static string FormatTime(double seconds)
        {
            var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
        }
    }
    public static class RankedBotLaunch
    {
        public static bool Active { get; private set; }
        public static int OpponentElo { get; private set; }
        public static void Prepare(PlayerProfile player) { Active = true; OpponentElo = EloPolicy.BotElo(player.elo, player.rankLossStreak); }
        public static void Clear() { Active = false; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() { Clear(); }
    }
}
