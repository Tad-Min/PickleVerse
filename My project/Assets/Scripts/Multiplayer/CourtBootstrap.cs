using UnityEngine;
namespace PickleVerse.Multiplayer
{
    public sealed class CourtBootstrap : MonoBehaviour
    {
        async void Start()
        {
            try
            {
            var app = MultiplayerApp.Instance;
            if (app == null || !app.Network || !app.Network.IsRunning)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(app?.RoomSceneName ?? "PlayMultiplayer");
                return;
            }
            if (app.Network.IsSharedModeMasterClient || app.Network.IsServer)
            {
                if (app.Match?.IsDedicated == true)
                    await PhotonSession.WaitAsync(() => app.ClientIdentities.Count >= app.Roster.HumanCount, "Chưa đủ người tham gia máy chủ.", 120);
                var prefab = app.Settings.MatchPrefab(app.Match?.IsDedicated == true);
                if (!prefab) throw new System.InvalidOperationException("Thiếu prefab trận cho cấu hình máy chủ.");
                await app.Network.SpawnAsync(prefab, onBeforeSpawned: (runner, obj) => obj.GetComponent<NetworkMatch>().SetRoster(app.Roster));
            }
            }
            catch (System.Exception error)
            {
                Debug.LogError("[Court] Không tải được trận: " + error.GetType().Name + ": " + error.Message + "\n" + error.StackTrace);
#if UNITY_SERVER
                DedicatedWorker.Instance?.Abort();
#endif
            }
        }
    }
}
