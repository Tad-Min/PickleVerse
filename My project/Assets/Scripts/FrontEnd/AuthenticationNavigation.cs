#if !UNITY_SERVER
using PickleVerse.Services;
using PickleVerse.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PickleVerse.FrontEnd
{
    // Navigation only. This persistent object never owns a Canvas or modal.
    public sealed class AuthenticationNavigation : MonoBehaviour
    {
        public const string SceneName = "Authentication";
        bool navigating, quitting;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() => new GameObject("Authentication Navigation").AddComponent<AuthenticationNavigation>();
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            AccountRuntime.Ensure().Account.Changed += Check;
            SceneManager.sceneLoaded += SceneLoaded;
        }
        void SceneLoaded(Scene scene, LoadSceneMode mode) { navigating = false; Check(); }
        async void Check()
        {
            if (quitting || navigating || SceneManager.GetActiveScene().name == SceneName) return;
            if (AccountRuntime.Ensure().Account.IsSignedIn && ProgressionRuntime.Instance.Ready) return;
            navigating = true;
            Time.timeScale = 1;
            var app = PickleVerse.Multiplayer.MultiplayerApp.Instance;
            if (app)
            {
                try { await app.SignOutFromLobbyAsync(); }
                catch (System.Exception error) { Debug.LogWarning("[Authentication] Session cleanup: " + error.GetType().Name); }
            }
            if (quitting || !this) return;
            SceneManager.LoadSceneAsync(SceneName);
        }
        void OnApplicationQuit() => quitting = true;
        void OnDestroy()
        {
            if (AccountRuntime.Instance) AccountRuntime.Instance.Account.Changed -= Check;
            SceneManager.sceneLoaded -= SceneLoaded;
        }
    }
}
#endif
