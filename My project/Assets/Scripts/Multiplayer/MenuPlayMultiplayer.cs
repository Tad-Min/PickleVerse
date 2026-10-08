using UnityEngine;
using UnityEngine.SceneManagement;
namespace PickleVerse.Multiplayer
{
    public sealed class MenuPlayMultiplayer : MonoBehaviour
    {
        public void Play() { MultiplayerApp.OpenRoomOnLoad=true; MultiplayerApp.OpenDefaultRoomOnLoad=true; SceneManager.LoadScene("PlayMultiplayer"); }
    }
}
