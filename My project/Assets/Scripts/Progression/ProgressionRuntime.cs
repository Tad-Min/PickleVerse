using System;
using System.IO;
using UnityEngine;

namespace PickleVerse.Progression
{
    public sealed class ProgressionRuntime : MonoBehaviour
    {
        static ProgressionRuntime instance;
        static bool shuttingDown;
        public static ProgressionRuntime Instance
        {
            get
            {
                if (!Application.isPlaying || shuttingDown) return null;
                if (!instance) new GameObject("Player Progression").AddComponent<ProgressionRuntime>();
                return instance;
            }
        }
        public static bool Exists => !shuttingDown && instance && instance.store?.Current != null;
        public PlayerProfile Player => store.Current;
        public ProgressionEngine Engine { get; private set; }
        public static event Action Changed;
        public static event Action Quitting;
        LocalPlayerStore store;
        double lastTime, nextSave, nextPeriodCheck;
        bool focused, paused;
        public static bool DeveloperControls => Application.isEditor || Debug.isDebugBuild;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            if (instance && instance.store != null) instance.store.Dispose();
            instance = null; shuttingDown = false; Changed = null; Quitting = null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { var runtime = Instance; }
        void Awake()
        {
            if (instance && instance != this) { Destroy(gameObject); return; }
            instance = this; DontDestroyOnLoad(gameObject);
            var config = Resources.Load<ProgressionConfig>("Progression Config") ?? ProgressionConfig.Defaults();
            Engine = new ProgressionEngine(config);
            store = new LocalPlayerStore(Path.Combine(Application.persistentDataPath, "LocalPlayers"));
            string requested = null;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-pkvProfile") requested = args[i + 1];
            store.Open(requested);
            if (Player.currencyVersion < 1) store.Transaction(p => CurrencyPolicy.Migrate(p));
            RecoverPending(); RefreshPeriods();
            focused = Application.isFocused; lastTime = Time.realtimeSinceStartupAsDouble; nextSave = lastTime + 30;
        }
        void RecoverPending()
        {
            if (Player.pendingMatch == null) return;
            store.Transaction(p =>
            {
                var abandoned = p.pendingMatch; abandoned.outcome = MatchOutcome.Interrupted;
                abandoned.reason = "Phiên trước bị gián đoạn"; abandoned.endedUtcTicks = DateTime.UtcNow.Ticks;
                Engine.Apply(p, abandoned);
            });
        }
        void TickTime()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (store?.Current != null && focused && !paused) Player.activeSeconds += Math.Max(0, now - lastTime);
            lastTime = now;
        }
        void Update()
        {
            if(store?.Current==null) return;
            TickTime(); double now = Time.realtimeSinceStartupAsDouble;
            if (now >= nextSave) { store.Save(); nextSave = now + 30; }
            if (now >= nextPeriodCheck) { RefreshPeriods(); nextPeriodCheck = now + 1; }
        }
        void OnApplicationFocus(bool focus) { TickTime(); focused = focus; store?.Save(); }
        void OnApplicationPause(bool pause) { TickTime(); paused = pause; store?.Save(); }
        void OnApplicationQuit()
        {
            TickTime();
            try { Quitting?.Invoke(); store?.Save(); }
            finally { shuttingDown = true; }
        }
        void OnDestroy()
        {
            if (instance != this) return;
            shuttingDown = true;
            store?.Dispose(); store = null; instance = null;
        }
        public void RefreshPeriods()
        {
            var utc = DateTime.UtcNow;
            if (Player.daily.key == ProgressionEngine.DayKey(utc) && Player.weekly.key == ProgressionEngine.WeekKey(utc)) return;
            store.Transaction(p => Engine.RefreshPeriods(p, utc)); Changed?.Invoke();
        }
        public bool Claim(string id)
        {
            bool accepted = false;
            store.Transaction(p => accepted = Engine.Claim(p, id, DateTime.UtcNow)); Changed?.Invoke(); return accepted;
        }
        public bool Apply(MatchResult result)
        {
            bool applied = false;
            store.Transaction(p => applied = Engine.Apply(p, result)); Changed?.Invoke(); return applied;
        }
        public void BeginMatch(MatchResult match)
        {
            if (Player.processedMatches.Contains(match.id)) return;
            store.Transaction(p => p.pendingMatch = match);
        }
        public void SetName(string name)
        {
            name = LocalPlayerStore.CleanName(name);
            if (name.Length == 0) throw new ArgumentException("Tên player không được để trống.");
            if (name == Player.playerName) return;
            store.Transaction(p => p.playerName = name); Changed?.Invoke();
        }
        public void EditLocalPlayer(string name, int elo, long tokenpad, long eliteToken)
        {
            if (!DeveloperControls) throw new InvalidOperationException("Chỉ chỉnh player trong Editor hoặc Development Build.");
            if (Player.pendingMatch != null) throw new InvalidOperationException("Kết thúc trận và trở về Menu trước khi chỉnh player.");
            name = LocalPlayerStore.CleanName(name);
            if (name.Length == 0) throw new ArgumentException("Tên player không được để trống.");
            if (elo < EloPolicy.Minimum) throw new ArgumentException("Elo phải từ 100 trở lên.");
            if (tokenpad < 0 || eliteToken < 0) throw new ArgumentException("Số dư tiền không được âm.");
            store.Transaction(p =>
            {
                p.playerName = name; p.elo = elo; p.coins = tokenpad; p.gems = eliteToken;
            });
            Changed?.Invoke();
        }
        public void SwitchPlayer(string id, string name)
        {
            TickTime(); store.Switch(id, name);
            if (Player.currencyVersion < 1) store.Transaction(p => CurrencyPolicy.Migrate(p));
            RecoverPending(); RefreshPeriods(); Changed?.Invoke();
        }
        public void UpdateWallet(Action<PlayerProfile> mutation)
        { store.Transaction(mutation); Changed?.Invoke(); }
        public void Save() { TickTime(); store.Save(); }
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
