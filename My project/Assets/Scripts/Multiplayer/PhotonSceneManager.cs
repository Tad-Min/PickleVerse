using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Fusion;
using PickleVerse.Multiplayer.Services;
using UnityEngine.SceneManagement;

namespace PickleVerse.Multiplayer
{
    // Build indices differ between the Editor, Windows profile and mobile builds.
    // Fusion supports path hashes for built-in scenes and resolves them locally.
    public class PhotonSceneManager : NetworkSceneManagerDefault
    {
        bool preparingCourt;
        public override bool IsBusy => preparingCourt || Runner && base.IsBusy;

        protected override IEnumerator OnSceneLoaded(SceneRef sceneRef, Scene scene, NetworkLoadSceneParameters sceneParams)
        {
            var expectedRunner = Runner;
            bool courtScene = sceneRef.IsPath(MultiplayerSceneConfiguration.CourtPath);
            if (courtScene) preparingCourt = true;
            try
            {
                yield return base.OnSceneLoaded(sceneRef, scene, sceneParams);
                if (!expectedRunner || Runner != expectedRunner || !courtScene)
                    yield break;

                // In Multiple Peer mode Fusion merges the court into this runner's scene.
                // Keep IsBusy true until the untracked frontend scenes are gone, so court
                // warm-up/readiness cannot reveal the old Menu during a device takeover.
                var court = MultiPeerScene.IsValid() ? MultiPeerScene : scene;
                var session = PhotonSession.Find(expectedRunner);
                yield return PrepareCourtScene(court, MultiplayerApp.Instance?.RoomSceneName ?? "PlayMultiplayer",
                    () => this && Runner == expectedRunner && session?.Closing != true);
            }
            finally
            {
                if (courtScene) preparingCourt = false;
            }
        }

        public static bool IsFrontEndScene(string name, string roomSceneName) => !string.IsNullOrEmpty(name) &&
            (name == "Menu" || name == "ModeSelection" || name == "PlayMultiplayer" || name == roomSceneName);

        public static IEnumerator PrepareCourtScene(Scene court, string roomSceneName, Func<bool> isCurrent)
        {
            if (!court.IsValid() || !court.isLoaded || isCurrent == null || !isCurrent())
                yield break;
            if (SceneManager.GetActiveScene() != court && !SceneManager.SetActiveScene(court))
                throw new InvalidOperationException("Không thể kích hoạt sân thi đấu.");

            var frontend = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var candidate = SceneManager.GetSceneAt(i);
                if (candidate != court && candidate.isLoaded && IsFrontEndScene(candidate.name, roomSceneName))
                    frontend.Add(candidate);
            }
            foreach (var candidate in frontend)
            {
                if (!isCurrent() || !court.IsValid() || !court.isLoaded)
                    yield break;
                if (candidate.IsValid() && candidate.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(candidate);
            }
        }

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
