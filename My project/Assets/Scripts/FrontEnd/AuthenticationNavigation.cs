#if !UNITY_SERVER
using System.Collections;
using PickleVerse.Multiplayer;
using PickleVerse.Services;
using PickleVerse.Progression;
using PickleVerse.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PickleVerse.FrontEnd
{
    // Navigation only. This persistent object never owns a Canvas or modal.
    public sealed class AuthenticationNavigation : MonoBehaviour
    {
        public const string SceneName = "Login";
        bool navigating, quitting;
        int checkedMatchSession = -1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() => new GameObject("Authentication Navigation").AddComponent<AuthenticationNavigation>();
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            AccountRuntime.Ensure().Account.Changed += Check;
            SceneManager.sceneLoaded += SceneLoaded;
        }
        void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            navigating = false;
            Check();
            if (scene.name == "Menu") StartCoroutine(ResumeMatchAfterLogin(scene));
        }
        IEnumerator ResumeMatchAfterLogin(Scene menu)
        {
            yield return null;
            while (menu.isLoaded && SceneLoading.IsBlocking) yield return null;
            if (quitting || SceneManager.GetActiveScene() != menu) yield break;

            var account = AccountRuntime.Ensure().Account;
            var progress = ProgressionRuntime.Instance;
            if (!progress.IsAuthenticated || checkedMatchSession == account.SessionVersion) yield break;
            checkedMatchSession = account.SessionVersion;

            var pending = progress.Player.pendingMatch;
            if (progress.Snapshot.catalog.dedicatedServerEnabled && pending != null &&
                pending.kind != MatchKind.BotPractice && System.Guid.TryParse(pending.id, out _))
                _ = MultiplayerApp.Ensure().ResumeDedicatedMatchAsync(pending.id);
        }
        async void Check()
        {
            if (quitting || navigating || SceneManager.GetActiveScene().name == SceneName) return;
            // Authentication and Menu warm-up share an opaque Loading scene. The
            // prepared transition owns rollback until both account and game data are ready.
            if (SceneLoading.IsPreparedTransition && SceneLoading.Destination == "Menu") return;
            if (ProgressionRuntime.Instance.IsOffline && ProgressionRuntime.Instance.Ready &&
                (SceneManager.GetActiveScene().name == "Menu" || SceneManager.GetActiveScene().name == "Training" || SceneManager.GetActiveScene().name == "Tutorial")) return;
            if (AccountRuntime.Ensure().Account.IsSignedIn && ProgressionRuntime.Instance.Ready) return;
            navigating = true;
            Time.timeScale = 1;
            var app = MultiplayerApp.Instance;
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
