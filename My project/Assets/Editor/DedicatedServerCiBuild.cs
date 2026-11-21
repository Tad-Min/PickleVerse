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
        [CliCommand("global", "pickleverse_build_server", "Build a commit-bound Linux Dedicated Server")]
        public static string BuildLinux([CliArg("commit", "Full source commit", Required = true)] string commit)
        {
            ValidateCommit(commit);
            var outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/native-server", commit, Guid.NewGuid().ToString("N")));
            return BuildLinux(commit, outputDirectory);
        }

        // Invoked by the CI Editor with -executeMethod.
        public static void BuildFromCi()
        {
            var arguments = Environment.GetCommandLineArgs();
            string Required(string name)
            {
                var index = Array.IndexOf(arguments, name);
                if (index < 0 || index + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index + 1]) || arguments[index + 1].StartsWith("-", StringComparison.Ordinal))
                    throw new BuildFailedException("Missing build argument " + name);
                return arguments[index + 1];
            }

            var commit = Required("-pkvCommit");
            ValidateCommit(commit);
            var destination = Path.GetFullPath(Required("-customBuildPath"));
            BuildLinux(commit, Path.GetDirectoryName(destination));
        }

        static void ValidateCommit(string commit)
        {
            if (!Regex.IsMatch(commit ?? "", "^[0-9a-f]{40}$"))
                throw new BuildFailedException("A full source commit is required.");
        }

        static string BuildLinux(string commit, string outputDirectory)
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
                throw new BuildFailedException("Stop Play Mode and wait for compilation before building.");

            var scenes = new[] { "Assets/Scenes/ServerBootstrap.unity", "Assets/Scenes/MultiplayerCourt.unity" };
            foreach (var scene in scenes)
                if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(scene))
                    throw new BuildFailedException("Required server scene is missing: " + scene);

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
                {
                    var errors = report.steps.SelectMany(step => step.messages
                        .Where(message => message.type is LogType.Error or LogType.Exception or LogType.Assert)
                        .Select(message => $"{step.name}: {message.content}"));
                    var summary = $"Dedicated server build {report.summary.result}: {report.summary.totalErrors} errors.";
                    throw new BuildFailedException(string.Join("\n", errors.Prepend(summary)));
                }
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
                if (!Application.isBatchMode && EditorUserBuildSettings.activeBuildTarget != previousTarget)
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
