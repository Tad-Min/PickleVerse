using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PickleVerse.EditorTools
{
    public static class LoginBenchmarkBuild
    {
        public static object Schedule()
        {
            if(Application.isPlaying) throw new InvalidOperationException("Stop Play first.");
            EditorApplication.delayCall+=Build;
            return new{scheduled=true};
        }
        static void Build()
        {
            string output=Path.Combine(LoginSceneInstaller.Output,"player"); Directory.CreateDirectory(output);
            var backend=PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            bool timing=PlayerSettings.enableFrameTimingStats;
            string settingsPath=Path.GetFullPath(Path.Combine(Application.dataPath,"../ProjectSettings/ProjectSettings.asset"));
            byte[] settingsBeforeBuild=File.ReadAllBytes(settingsPath);
            var playerSettings=Resources.FindObjectsOfTypeAll<PlayerSettings>()[0];
            var serialized=new SerializedObject(playerSettings);
            var backends=serialized.FindProperty("scriptingBackend");
            bool hadStandalone=false;
            for(int i=0;i<backends.arraySize;i++) hadStandalone|=backends.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue=="Standalone";
            try
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
                PlayerSettings.enableFrameTimingStats=true;
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{LoginSceneInstaller.ScenePath},locationPathName=Path.Combine(output,"PickleVerseLogin.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
                File.WriteAllText(Path.Combine(LoginSceneInstaller.Output,"player-build.json"),JsonUtility.ToJson(new Result{success=report.summary.result==BuildResult.Succeeded,result=report.summary.result.ToString(),errors=(int)report.summary.totalErrors,seconds=report.summary.totalTime.TotalSeconds},true));
            }
            catch(Exception e) { File.WriteAllText(Path.Combine(LoginSceneInstaller.Output,"player-build.json"),e.ToString()); }
            finally
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,backend); PlayerSettings.enableFrameTimingStats=timing;
                if(!hadStandalone)
                {
                    serialized.Update(); backends=serialized.FindProperty("scriptingBackend");
                    for(int i=backends.arraySize-1;i>=0;i--) if(backends.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue=="Standalone") backends.DeleteArrayElementAtIndex(i);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                // Preserve the exact source asset; Unity's build-time preload/settings save is temporary.
                File.WriteAllBytes(settingsPath,settingsBeforeBuild);
            }
        }
        [Serializable] sealed class Result { public bool success; public string result; public int errors; public double seconds; }
    }
}
