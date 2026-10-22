using System;
using System.Threading.Tasks;
using PickleVerse.Services.Api;
using UnityEngine;

namespace PickleVerse.Services
{
    // App lifetime telemetry is independent of scenes, match clocks and Time.timeScale.
    public sealed class AppActivityRuntime : MonoBehaviour
    {
        [Serializable] sealed class Receipt { public long totalSeconds, elapsedSeconds; }
        public static AppActivityRuntime Instance { get; private set; }
        AccountService account;
        ApiClient api;
        string session = Guid.NewGuid().ToString("N"), previousOwner = "";
        double began, next;
        int generation;
        bool opened, allowQuit, quitRequested, closing;
        Receipt receipt;
        Task pending = Task.CompletedTask;
        public string Error { get; private set; }
        public double Seconds => receipt == null ? 0 : receipt.totalSeconds + Math.Max(0, Elapsed - receipt.elapsedSeconds);
        double Elapsed => Math.Max(0, Time.realtimeSinceStartupAsDouble - began);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() { Instance = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Boot() { Ensure(); }
        public static AppActivityRuntime Ensure()
        {
            if (!Instance) new GameObject("App Activity Session").AddComponent<AppActivityRuntime>();
            return Instance;
        }
        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            var runtime = AccountRuntime.Ensure(); account = runtime.Account;
            api = new ApiClient(runtime.GetComponent<UnityApiTransport>());
            account.BeforeSessionEnd = () => FlushAsync(true);
            account.Changed += AccountChanged; Application.wantsToQuit += WantsToQuit;
        }
        void AccountChanged()
        {
            string owner = account.IsSignedIn ? account.Profile.UserId.ToString("N") : "";
            if (previousOwner.Length > 0 && owner != previousOwner)
            {
                generation++; session = Guid.NewGuid().ToString("N"); began = Time.realtimeSinceStartupAsDouble;
                opened = false; receipt = null; closing = false;
            }
            previousOwner = owner; next = 0;
        }
        void Update()
        {
            if (!closing && pending.IsCompleted && Time.realtimeSinceStartupAsDouble >= next)
            { next = Time.realtimeSinceStartupAsDouble + 15; pending = SendAsync(false); }
        }
        async Task SendAsync(bool close)
        {
            int version = generation; string key = session;
            object Request(bool final) => new { SessionId = Guid.Parse(key), ElapsedSeconds = (long)Elapsed, Close = final };
            try
            {
                if (!opened)
                {
                    await api.SendAsync<Receipt>("POST", "api/Game/activity/open", Request(false));
                    if (version != generation || !this) return; opened = true;
                }
                if (account.IsSignedIn)
                {
                    var saved = await account.SendAuthorizedAsync<Receipt>("POST", "api/Game/activity/checkpoint", Request(close));
                    if (version != generation || !this) return; receipt = saved;
                }
                if (version == generation) Error = null;
            }
            catch (Exception error) { if (version == generation && this) Error = error.Message; }
        }
        public async Task FlushAsync(bool close = false)
        {
            if (close) closing = true;
            await pending;
            pending = SendAsync(close); await pending;
        }
        bool WantsToQuit()
        {
            if (allowQuit || !account.IsSignedIn) return true;
            if (!quitRequested) { quitRequested = true; FinishQuit(); }
            return false;
        }
        async void FinishQuit()
        {
            await Task.WhenAny(FlushAsync(true), Task.Delay(2500));
            allowQuit = true; Application.Quit();
        }
        void OnApplicationQuit() { if (!allowQuit && account.IsSignedIn) _ = FlushAsync(true); }
        void OnDestroy()
        {
            if (Instance != this) return;
            if (account != null) { account.Changed -= AccountChanged; account.BeforeSessionEnd = null; }
            Application.wantsToQuit -= WantsToQuit; Instance = null;
        }
    }
}
