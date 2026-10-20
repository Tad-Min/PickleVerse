using PickleVerse.Services.Api;
using UnityEngine;

namespace PickleVerse.Services
{
    [DefaultExecutionOrder(-200)]
    public sealed class AccountRuntime : MonoBehaviour
    {
        public static AccountRuntime Instance { get; private set; }
        public AccountService Account { get; private set; }
        public BackendApiSettings Settings { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() { Instance = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Boot() { Ensure(); }
        public static AccountRuntime Ensure()
        {
            if (!Instance) new GameObject("Online Account").AddComponent<AccountRuntime>();
            return Instance;
        }
        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            Settings = Resources.Load<BackendApiSettings>("BackendApiSettings");
            if (!Settings) Settings = ScriptableObject.CreateInstance<BackendApiSettings>();
            var transport = gameObject.AddComponent<UnityApiTransport>(); transport.Settings = Settings;
            Account = new AccountService(new ApiClient(transport));
        }
        void OnDestroy() { if (Instance == this) { Account?.Clear(); Instance = null; } }
    }
}
