using UnityEngine;
namespace PickleVerse.Multiplayer
{
    public sealed class CourtBootstrap : MonoBehaviour
    {
        void Start()
        {
            var app=MultiplayerApp.Instance;
            if(app==null || !app.Network.IsListening) { UnityEngine.SceneManagement.SceneManager.LoadScene("MultiplayerTest"); return; }
            if(app.Network.IsServer)
            {
                var go=Instantiate(app.Settings.matchPrefab);
                var match=go.GetComponent<NetworkMatch>(); match.SetRoster(app.Roster);
                go.GetComponent<Unity.Netcode.NetworkObject>().Spawn();
            }
        }
    }
}
