using UnityEngine;
using UnityEngine.SceneManagement;

namespace PickleVerse.LocalMatch
{
    public sealed class TutorialLauncher : MonoBehaviour
    {
        public void Replay()
        {
            SceneManager.LoadScene("Tutorial");
        }
    }
}
