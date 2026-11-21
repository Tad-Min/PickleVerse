using System.Threading.Tasks;
using UnityEngine;

namespace PickleVerse.Services
{
    // App lifetime telemetry is independent of scenes, match clocks and Time.timeScale.
    public sealed class AppActivityRuntime : MonoBehaviour
    {
        public static AppActivityRuntime Instance { get; private set; }
        AccountService account;
        AppActivitySession session;
        bool allowQuit, quitRequested;
        public string Error => session?.Error;
        public double Seconds => session?.Seconds ?? 0;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Instance = null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
#if !UNITY_SERVER
            Ensure();
#endif
        }
        public static AppActivityRuntime Ensure()
        {
            if (!Instance)
                new GameObject("App Activity Session").AddComponent<AppActivityRuntime>();
            return Instance;
        }
        void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            var runtime = AccountRuntime.Ensure();
            account = runtime.Account;
            session = new AppActivitySession(account, runtime.Api, () => Time.realtimeSinceStartupAsDouble);
            account.BeforeSessionEnd = () => FlushAsync(true);
            Application.wantsToQuit += WantsToQuit;
        }
        void Update() => session?.Tick();
        public Task FlushAsync(bool close = false) => session?.FlushAsync(close) ?? Task.CompletedTask;
        bool WantsToQuit()
        {
            if (allowQuit || !account.IsSignedIn)
                return true;
            if (!quitRequested)
            {
                quitRequested = true;
                FinishQuit();
            }
            return false;
        }
        async void FinishQuit()
        {
            await Task.WhenAny(FlushAsync(true), Task.Delay(2500));
            allowQuit = true;
            Application.Quit();
        }
        void OnApplicationQuit()
        {
            if (!allowQuit && account != null && account.IsSignedIn)
                _ = FlushAsync(true);
        }
        void OnDestroy()
        {
            if (Instance != this)
                return;
            session?.Dispose();
            if (account != null)
                account.BeforeSessionEnd = null;
            Application.wantsToQuit -= WantsToQuit;
            Instance = null;
        }
    }
}
