using UnityEngine;
using UnityEngine.SceneManagement;
namespace PickleVerse.Multiplayer
{
    public sealed class MenuPlayMultiplayer : MonoBehaviour
    {
        // Retain the existing Menu button component and serialized listener.
        public void Play()
        {
            MultiplayerApp.OpenDefaultRoomOnLoad = true;
            SceneManager.LoadScene("ModeSelection");
        }
    }
}
