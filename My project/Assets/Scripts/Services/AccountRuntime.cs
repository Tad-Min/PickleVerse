using System;
using System.Threading.Tasks;
using PickleVerse.Services.Api;
using UnityEngine;

namespace PickleVerse.Services
{
    [DefaultExecutionOrder(-200)]
    public sealed class AccountRuntime : MonoBehaviour
    {
        public static AccountRuntime Instance { get; private set; }
        public AccountService Account { get; private set; }
        internal ApiClient Api { get; private set; }
        public BackendApiSettings Settings { get; private set; }
        bool checkingSession;
        double nextSessionCheck;
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
        public static AccountRuntime Ensure()
        {
            if (!Instance)
                new GameObject("Online Account").AddComponent<AccountRuntime>();
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
            Settings = Resources.Load<BackendApiSettings>("BackendApiSettings");
            if (!Settings)
                Settings = ScriptableObject.CreateInstance<BackendApiSettings>();
            var transport = gameObject.AddComponent<UnityApiTransport>();
            transport.Settings = Settings;
            Api = new ApiClient(transport);
            Account = new AccountService(Api);
        }
        void Update()
        {
            if (!Account.IsSignedIn || checkingSession || Time.realtimeSinceStartupAsDouble < nextSessionCheck)
                return;
            nextSessionCheck = Time.realtimeSinceStartupAsDouble + 3;
            _ = CheckSession();
        }
        async Task CheckSession()
        {
            checkingSession = true;
            try { await Account.CheckSessionAsync(); }
            catch (Exception error) when (error is ApiException || error is OperationCanceledException) { }
            finally { checkingSession = false; }
        }
        void OnApplicationFocus(bool focused)
        {
            if (focused) nextSessionCheck = 0;
        }
        void OnApplicationPause(bool paused)
        {
            if (!paused) nextSessionCheck = 0;
        }
        void OnDestroy()
        {
            if (Instance == this)
            {
                Account?.Clear();
                Instance = null;
            }
        }
    }
}
