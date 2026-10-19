using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using PickleVerse.Progression;
public static class LoadingFinalize
{
    public static object Cleanup()
    {
        if(Application.isPlaying)throw new Exception("Stop Play first");
        AssetDatabase.DeleteAsset("Assets/Scripts/Multiplayer/LoadingNetworkSmoke.cs");
        foreach(var path in new[]{"Assets/Resources/PerformanceTestRunInfo.json","Assets/Resources/PerformanceTestRunSettings.json"})if(File.Exists(path))AssetDatabase.DeleteAsset(path);
        var settings=Resources.FindObjectsOfTypeAll<PlayerSettings>().First();var data=new SerializedObject(settings);
        var preloaded=data.FindProperty("preloadedAssets");for(int i=preloaded.arraySize-1;i>=0;i--)if(AssetDatabase.GetAssetPath(preloaded.GetArrayElementAtIndex(i).objectReferenceValue)=="Packages/com.unity.behavior/Authoring/Behavior App UI Settings.asset"){preloaded.GetArrayElementAtIndex(i).objectReferenceValue=null;preloaded.DeleteArrayElementAtIndex(i);}
        var backends=data.FindProperty("scriptingBackend");for(int i=backends.arraySize-1;i>=0;i--)if(backends.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue=="Standalone")backends.DeleteArrayElementAtIndex(i);
        var apis=data.FindProperty("m_BuildTargetGraphicsAPIs");for(int i=apis.arraySize-1;i>=0;i--)if(apis.GetArrayElementAtIndex(i).FindPropertyRelative("m_BuildTarget").stringValue=="WindowsStandaloneSupport")apis.DeleteArrayElementAtIndex(i);
        data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(settings);
        typeof(PlayerSettings).GetMethod("SetDirty",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,null);AssetDatabase.SaveAssets();
        string root=Path.Combine(Application.persistentDataPath,"LocalPlayers"),preferred=Path.Combine(root,"preferred.txt");int removed=0;
        string receipt="../UnityWork/Loading20261019/previous-profile.txt";
        if(File.Exists(receipt)&&File.Exists(preferred)&&Regex.IsMatch(File.ReadAllText(preferred).Trim(),"^(Loading(Host|Guest)[1-5]|LoadNetQA[0-9a-f]{8})$"))
        {using(var store=new LocalPlayerStore(root)){store.Open();store.Switch(File.ReadAllText(receipt).Trim());}}
        foreach(var path in Directory.GetFiles(root))if(Regex.IsMatch(Path.GetFileName(path),"^(Loading(Host|Guest)[1-5]|LoadNetQA[0-9a-f]{8})\\.(json(\\.bak)?|lock)$")){File.Delete(path);removed++;}
        AssetDatabase.DesiredWorkerCount=3;
        if(SessionState.GetBool("PKV.Loading.RefreshPaused",false)){AssetDatabase.AllowAutoRefresh();SessionState.SetBool("PKV.Loading.RefreshPaused",false);}
        foreach(var callback in (EditorApplication.delayCall?.GetInvocationList()??Array.Empty<Delegate>()).Where(d=>d.Method.DeclaringType.FullName.Contains("LoadingNetworkBuild")))EditorApplication.delayCall-=(EditorApplication.CallbackFunction)callback;
        var lit=Shader.Find("Universal Render Pipeline/Lit");if(lit)ShaderUtil.ClearShaderMessages(lit);
        var entries=typeof(Editor).Assembly.GetType("UnityEditor.LogEntries");entries.GetMethod("Clear",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static).Invoke(null,null);
        return new{qaFilesRemoved=removed,preferred=File.Exists(preferred)?File.ReadAllText(preferred).Trim():"",graphicsDefault=PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64),backend=PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone).ToString()};
    }
}
