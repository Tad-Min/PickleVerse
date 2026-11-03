using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class ClientBuild
{
    public static string BuildNow()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Stop and compile the Editor first.");
        EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/account-multiplayer-ai-20261103/windows-client/PickleVerse.exe"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var scenes = EditorBuildSettings.globalScenes.Where(s => s.enabled && !s.path.Contains("ServerBootstrap")).Select(s => s.path).ToArray();
        if (scenes.Length == 0 || !scenes[0].EndsWith("Login.unity")) throw new InvalidOperationException("Login must be first.");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, target = BuildTarget.StandaloneWindows64,
            subtarget = (int)StandaloneBuildSubtarget.Player, locationPathName = output,
            options = BuildOptions.Development | BuildOptions.DetailedBuildReport });
        string json = JsonUtility.ToJson(new Result { result = report.summary.result.ToString(), output = output,
            errors = (int)report.summary.totalErrors, warnings = (int)report.summary.totalWarnings,
            seconds = report.summary.totalTime.TotalSeconds, bytes = report.summary.totalSize });
        File.WriteAllText(Path.Combine(Application.dataPath, "../Temp/client-build.json"), json);
        return json;
    }
    [Serializable] sealed class Result { public string result, output; public int errors, warnings; public double seconds; public ulong bytes; }
}
