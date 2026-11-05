using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Unity.Pipeline.Commands;

namespace PickleVerse.EditorTools
{
    public static class DedicatedServerCiBuild
    {
        // Local licensed Editor builds the native payload; GitHub only packages Docker.
        [CliCommand("global", "pickleverse_build_server", "Build a commit-bound Linux Dedicated Server")]
        public static string BuildLinux([CliArg("commit", "Full source commit", Required = true)] string commit)
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
                throw new BuildFailedException("Stop Play Mode and wait for compilation before building.");
            if (!Regex.IsMatch(commit ?? "", "^[0-9a-f]{40}$"))
                throw new BuildFailedException("A full source commit is required.");

            var scenes = new[] { "Assets/Scenes/ServerBootstrap.unity", "Assets/Scenes/MultiplayerCourt.unity" };
            foreach (var scene in scenes)
                if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(scene))
                    throw new BuildFailedException("Required server scene is missing: " + scene);

            var outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/native-server", commit, Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(outputDirectory);
            var previousHttp = PlayerSettings.insecureHttpOption;
            var previousFlag = Environment.GetEnvironmentVariable("PKV_BUILD_DEDICATED");
            var previousSubtarget = EditorUserBuildSettings.standaloneBuildSubtarget;
            var previousBackend = PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Server);
            var previousVersion = PlayerSettings.bundleVersion;
            var previousTarget = EditorUserBuildSettings.activeBuildTarget;
            var uiPlugins = PluginImporter.GetAllImporters()
                .Where(plugin => plugin.assetPath.IndexOf("AppUINativePlugin", StringComparison.OrdinalIgnoreCase) >= 0
                    && plugin.assetPath.EndsWith(".so", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            try
            {
                // The existing scene processor strips only the temporary court copy.
                Environment.SetEnvironmentVariable("PKV_BUILD_DEDICATED", "1");
                PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
                EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;
                PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Server, ScriptingImplementation.Mono2x);
                PlayerSettings.bundleVersion = "server-" + commit.Substring(0, 12);
                // A cached Editor Library must not retain a destroyed Play Mode pipeline.
                UnityEditor.Rendering.CoreBuildData.instance.Dispose();
                foreach (var plugin in uiPlugins)
                    plugin.SetIncludeInBuildDelegate(_ => false);

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    target = BuildTarget.StandaloneLinux64,
                    subtarget = (int)StandaloneBuildSubtarget.Server,
                    extraScriptingDefines = new[] { "APPUI_PLATFORM_DISABLED" },
                    locationPathName = Path.Combine(outputDirectory, "PickleVerseServer"),
                    options = BuildOptions.DetailedBuildReport
                });

                File.WriteAllText(Path.Combine(outputDirectory, "build-info.json"), JsonUtility.ToJson(new BuildInfo {
                    commit = commit, unity = Application.unityVersion,
                    protocol = PickleVerse.Multiplayer.Services.MultiplayerSceneConfiguration.ProtocolVersion,
                    result = report.summary.result.ToString(), errors = (int)report.summary.totalErrors,
                    warnings = (int)report.summary.totalWarnings }));
                if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors != 0)
                    throw new BuildFailedException($"Dedicated server build {report.summary.result}: {report.summary.totalErrors} errors.");
                if (Directory.GetFiles(outputDirectory, "*AppUINativePlugin*.so", SearchOption.AllDirectories).Any())
                    throw new BuildFailedException("Native App UI plugin must be excluded from the dedicated server.");

                Debug.Log($"Dedicated server built at {outputDirectory} ({report.summary.totalSize} bytes).");
                return outputDirectory;
            }
            finally
            {
                Environment.SetEnvironmentVariable("PKV_BUILD_DEDICATED", previousFlag);
                PlayerSettings.insecureHttpOption = previousHttp;
                EditorUserBuildSettings.standaloneBuildSubtarget = previousSubtarget;
                PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Server, previousBackend);
                PlayerSettings.bundleVersion = previousVersion;
                foreach (var plugin in uiPlugins)
                    plugin.SetIncludeInBuildDelegate(null);
                if (EditorUserBuildSettings.activeBuildTarget != previousTarget)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(previousTarget), previousTarget);
            }
        }
        [Serializable] sealed class BuildInfo
        {
            public string commit, unity, result;
            public int errors, warnings, protocol;
        }
    }
}
