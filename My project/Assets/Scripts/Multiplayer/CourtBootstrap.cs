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
                var session = app?.Match;
                var runner = session?.Runner;
                if (!runner || !runner.IsRunning)
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(app?.RoomSceneName ?? "PlayMultiplayer");
                    return;
                }
                if (!runner.IsSharedModeMasterClient && !runner.IsServer) return;
                var roster = app.Roster;
                if (session.IsDedicated)
                    await PhotonSession.WaitAsync(() => !this || app.Match != session || !runner || !runner.IsRunning ||
                        roster.members.TrueForAll(member => member.bot || System.Linq.Enumerable.Contains(app.ClientIdentities.Values, member.id)),
                        "Chưa đủ người thuộc đội hình vào máy chủ.", 100);
                if (!this || app.Match != session || !runner || !runner.IsRunning) return;
                var prefab = app.Settings.MatchPrefab(session.IsDedicated);
                if (!prefab) throw new System.InvalidOperationException("Thiếu prefab trận cho cấu hình máy chủ.");
                await runner.SpawnAsync(prefab, onBeforeSpawned: (_, obj) => obj.GetComponent<NetworkMatch>().SetRoster(roster));
                Debug.Log($"[Court] Spawned match={roster.matchId} players={roster.Count}");
            }
            catch (System.Exception error)
            {
                Debug.LogError("[Court] Không tải được trận: " + error.GetType().Name + ": " + error.Message + "\n" + error.StackTrace);
#if UNITY_SERVER
                DedicatedWorker.Instance?.Abort();
#else
                if (this && MultiplayerApp.Instance)
                    await MultiplayerApp.Instance.ReturnToRoomAsync(error.Message);
#endif
            }
        }
    }
}
