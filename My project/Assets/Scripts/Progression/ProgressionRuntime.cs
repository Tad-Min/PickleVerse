using System;
using PickleVerse.Services;
using UnityEngine;

namespace PickleVerse.Progression
{
    /// <summary>Read-only compatibility view of the authenticated backend profile. No local save or guest account.</summary>
    public sealed class ProgressionRuntime : MonoBehaviour
    {
        static ProgressionRuntime instance;
        static bool shuttingDown;
        AccountService account;
        PlayerProfile player = Empty();
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
        public bool IsAuthenticated => account != null && account.IsSignedIn;
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
            Engine = new ProgressionEngine(Resources.Load<ProgressionConfig>("Progression Config") ?? ProgressionConfig.Defaults());
            account = AccountRuntime.Ensure().Account; account.Changed += ReadServerProfile; ReadServerProfile();
        }
        void ReadServerProfile()
        {
            var profile = account.Profile;
            player = profile == null ? Empty() : new PlayerProfile
            {
                online = true, id = profile.UserId.ToString("N"), playerName = profile.PlayerName,
                avatarId = profile.Avatar, elo = profile.Elo, level = profile.Level, levelXp = profile.Exp,
                rankWinStreak = profile.WinStreak, wins = profile.WinCount, losses = profile.LoseCount,
                activeSeconds = profile.TimePlayedSeconds
            };
            Changed?.Invoke();
        }
        // Legacy consumers remain compilable while their backend integration is deferred.
        // Their entry points are disabled by AccountScreen; these guards reject local rewards and writes too.
        public void RefreshPeriods() { }
        public bool Claim(string id) => false;
        public bool Apply(MatchResult result) => false;
        public void BeginMatch(MatchResult match) { }
        public void SetName(string name) => throw new InvalidOperationException("Hãy cập nhật tên trong mục Tài khoản.");
        public void EditLocalPlayer(string name, int elo, long tokenpad, long eliteToken) => throw new InvalidOperationException("Hồ sơ chỉ được cập nhật qua máy chủ.");
        public void SwitchPlayer(string id, string name) => throw new InvalidOperationException("Hãy đăng xuất và đăng nhập tài khoản khác.");
        public void UpdateWallet(Action<PlayerProfile> mutation) => throw new InvalidOperationException("Cửa hàng chưa sẵn sàng.");
        public void Save() { }
        void OnApplicationQuit() { Quitting?.Invoke(); shuttingDown = true; }
        void OnDestroy()
        {
            if (instance != this) return;
            if (account != null) account.Changed -= ReadServerProfile;
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
