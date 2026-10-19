using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
public static class LoadingNetworkBuild
{
    static Task running;
    public static object Start()
    {
        File.WriteAllText("../UnityWork/Loading20261019/build.json","{\"status\":\"queued\"}");
        foreach(var callback in (EditorApplication.delayCall?.GetInvocationList()??Array.Empty<Delegate>()).Where(d=>d.Method.DeclaringType.FullName.Contains("LoadingNetworkBuild")))EditorApplication.delayCall-=(EditorApplication.CallbackFunction)callback;
        running=Later();
        return "QA build queued outside the CLI request";
    }
    static async Task Later(){await Task.Delay(700);try{Run();}catch(Exception e){File.WriteAllText("../UnityWork/Loading20261019/build.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{error=e.ToString()}));Debug.LogException(e);}}
    public static object Prepare()
    {
        if(Application.isPlaying)throw new Exception("Stop Play first");
        File.Copy("Tools/LoadingNetworkSmokeSource.txt","Assets/Scripts/Multiplayer/LoadingNetworkSmoke.cs",true);AssetDatabase.Refresh();return "QA source staged (excluded from normal game builds)";
    }
    public static object Run()
    {
        const string folder="../UnityWork/Loading20261019/Build";Directory.CreateDirectory(folder);
        var target=UnityEditor.Build.NamedBuildTarget.Standalone;var backend=PlayerSettings.GetScriptingBackend(target);
        var graphics=PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);bool defaultGraphics=PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64);
        try
        {
            PlayerSettings.SetScriptingBackend(target,ScriptingImplementation.Mono2x);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{UnityEngine.Rendering.GraphicsDeviceType.Direct3D12});
            var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToList();scenes.Remove("Assets/Scenes/UI/ModeSelection.unity");scenes.Insert(0,"Assets/Scenes/UI/ModeSelection.unity");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes.ToArray(),locationPathName=folder+"/PickleVerseLoadingQA.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development,extraScriptingDefines=new[]{"PKV_LOADING_QA"}});
            var result=new{result=report.summary.result.ToString(),report.summary.totalErrors,report.summary.totalWarnings,report.summary.totalSize,path=Path.GetFullPath(folder+"/PickleVerseLoadingQA.exe")};
            File.WriteAllText("../UnityWork/Loading20261019/build.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally{PlayerSettings.SetScriptingBackend(target,backend);PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,graphics);PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,defaultGraphics);}
    }
    public static object Cleanup(){AssetDatabase.DeleteAsset("Assets/Scripts/Multiplayer/LoadingNetworkSmoke.cs");return "QA-only bootstrap removed";}
}
