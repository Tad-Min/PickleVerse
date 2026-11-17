using UnityEngine;

namespace PickleVerse.Settings
{
    /// <summary>One way out of the game for the login screen, Settings and the window close button.</summary>
    public static class GameExit
    {
        /// <summary>iOS and the web do not let an app close itself.</summary>
        public static bool Supported => Application.platform != RuntimePlatform.IPhonePlayer && Application.platform != RuntimePlatform.WebGLPlayer;
        public static bool PromptOpen => GameExitPrompt.IsOpen;
        /// <summary>Asks for confirmation first.</summary>
        public static void Request()
        {
            if (Supported)
                GameExitPrompt.Show();
        }
        /// <summary>Quits now. A signed-in session still gets its short activity flush (AppActivityRuntime).</summary>
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
