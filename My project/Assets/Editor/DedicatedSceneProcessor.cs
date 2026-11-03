using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.Multiplayer;

namespace PickleVerse.EditorTools
{
    // Only the temporary build copy is stripped. The authored client court is untouched.
    public sealed class DedicatedSceneProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 1000;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null || report.summary.platform != BuildTarget.StandaloneLinux64 ||
                System.Environment.GetEnvironmentVariable("PKV_BUILD_DEDICATED") != "1" || scene.name != "MultiplayerCourt") return;
            foreach (var root in scene.GetRootGameObjects())
                Object.DestroyImmediate(root);
            var bootstrap = new GameObject("Server Court", typeof(CourtBootstrap));
            SceneManager.MoveGameObjectToScene(bootstrap, scene);
        }
    }
}
