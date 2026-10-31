using System;
using System.IO;
using Fusion;
using UnityEngine.SceneManagement;

namespace PickleVerse.Multiplayer
{
    // Build indices differ between the Editor, Windows profile and mobile builds.
    // Fusion supports path hashes for built-in scenes and resolves them locally.
    public class PhotonSceneManager : NetworkSceneManagerDefault
    {
        public override SceneRef GetSceneRef(string sceneNameOrPath)
        {
            if (string.IsNullOrEmpty(sceneNameOrPath))
                return SceneRef.None;
            // Resolve the path directly: in the Editor, disabled scenes can make
            // Fusion's filtered indices differ from SceneUtility's index list too.
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (string.Equals(path, sceneNameOrPath, StringComparison.Ordinal) ||
                    string.Equals(Path.GetFileNameWithoutExtension(path), sceneNameOrPath, StringComparison.Ordinal) ||
                    string.Equals(Path.GetFileName(path), sceneNameOrPath, StringComparison.Ordinal))
                    return SceneRef.FromPath(path);
            }
            return SceneRef.None;
        }
    }
}
