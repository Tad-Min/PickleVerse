using UnityEngine;
namespace PickleVerse.Multiplayer
{
    public sealed class CourtBootstrap : MonoBehaviour
    {
        async void Start()
        {
            var app = MultiplayerApp.Instance;
            if (app == null || !app.Network || !app.Network.IsRunning)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(app?.RoomSceneName ?? "PlayMultiplayer");
                return;
            }
            if (app.Network.IsSharedModeMasterClient)
            {
                await app.Network.SpawnAsync(app.Settings.matchPrefab, onBeforeSpawned: (runner, obj) => obj.GetComponent<NetworkMatch>().SetRoster(app.Roster));
            }
        }
    }
}
