using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DedicatedBuild
{
    public static string BuildNow()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Editor must be stopped and compiled.");
        const string bootstrap = "Assets/Scenes/ServerBootstrap.unity";
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(bootstrap))
        {
            var before = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(scene, bootstrap); EditorSceneManager.CloseScene(scene, true);
            if (before.IsValid()) SceneManager.SetActiveScene(before);
        }
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/account-multiplayer-ai-20261103/linux-server/PickleVerseServer"));
        string workspace = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        string directory = Path.GetDirectoryName(output);
        string allowed = Path.Combine(workspace, "artifacts", "account-multiplayer-ai-20261103", "linux-server");
        if (!string.Equals(directory, Path.GetFullPath(allowed), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing to clean an unexpected build directory.");
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        Directory.CreateDirectory(directory);
        var previousHttp = PlayerSettings.insecureHttpOption;
        var uiPlugins = PluginImporter.GetAllImporters().Where(p => p.assetPath.IndexOf("AppUINativePlugin", StringComparison.OrdinalIgnoreCase) >= 0 && p.assetPath.EndsWith(".so", StringComparison.OrdinalIgnoreCase)).ToArray();
        File.WriteAllText(Path.Combine(Application.dataPath, "../Temp/dedicated-build.json"), "{\"state\":\"building\"}");
            try
            {
                Environment.SetEnvironmentVariable("PKV_BUILD_DEDICATED", "1");
                PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
                foreach (var plugin in uiPlugins) plugin.SetIncludeInBuildDelegate(_ => false);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { bootstrap, "Assets/Scenes/MultiplayerCourt.unity" },
                    target = BuildTarget.StandaloneLinux64, subtarget = (int)StandaloneBuildSubtarget.Server,
                    extraScriptingDefines = new[] { "APPUI_PLATFORM_DISABLED" },
                    locationPathName = output, options = BuildOptions.DetailedBuildReport });
                bool uiPluginExcluded = !Directory.GetFiles(directory, "*AppUINativePlugin*.so", SearchOption.AllDirectories).Any();
                if (report.summary.result == BuildResult.Succeeded && !uiPluginExcluded)
                    throw new InvalidOperationException("Native App UI plugin was not excluded from the server output.");
                string json = JsonUtility.ToJson(new Result { state = "completed", result = report.summary.result.ToString(),
                    errors = (int)report.summary.totalErrors, warnings = (int)report.summary.totalWarnings,
                    seconds = report.summary.totalTime.TotalSeconds, bytes = report.summary.totalSize, output = output,
                    serverHttp = "AlwaysAllowed-internal-server", appUiPluginExcluded = uiPluginExcluded });
                File.WriteAllText(Path.Combine(Application.dataPath, "../Temp/dedicated-build.json"), json);
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(Application.dataPath, "../Temp/dedicated-build.json"), JsonUtility.ToJson(new Result { state = "failed", result = error.ToString() }));
                Debug.LogException(error);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PKV_BUILD_DEDICATED", null);
                PlayerSettings.insecureHttpOption = previousHttp;
                foreach (var plugin in uiPlugins) plugin.SetIncludeInBuildDelegate(null);
                AssetDatabase.SaveAssets();
                EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            }
        return File.ReadAllText(Path.Combine(Application.dataPath, "../Temp/dedicated-build.json"));
    }
    [Serializable] sealed class Result { public string state, result, output, serverHttp; public bool appUiPluginExcluded; public int errors, warnings; public double seconds; public ulong bytes; }
}
