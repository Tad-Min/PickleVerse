using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AccountSceneSetup
{
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Dừng Play trước khi tạo scene xác thực.");
        const string path = "Assets/Scenes/UI/Authentication.unity";
        var previous = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(path);
        bool openedHere = !scene.isLoaded;
        if (openedHere) scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path)
            ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        if (!scene.GetRootGameObjects().Any(g => g.GetComponent<PickleVerse.FrontEnd.AccountScreen>()))
            new GameObject("Authentication").AddComponent<PickleVerse.FrontEnd.AccountScreen>();
        EditorSceneManager.SaveScene(scene, path);
        if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        if (openedHere) EditorSceneManager.CloseScene(scene, true);
        EditorBuildSettings.globalScenes = First(EditorBuildSettings.globalScenes, path);
        foreach (var guid in AssetDatabase.FindAssets("t:BuildProfile"))
        {
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(AssetDatabase.GUIDToAssetPath(guid));
            if (!profile || profile.name.IndexOf("Server", StringComparison.OrdinalIgnoreCase) >= 0 || !profile.overrideGlobalScenes) continue;
            profile.scenes = First(profile.scenes, path);
            EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
        }
        return "Authentication scene saved; client build scene lists start with Authentication.";
    }
    static EditorBuildSettingsScene[] First(EditorBuildSettingsScene[] scenes, string first) =>
        new[] { new EditorBuildSettingsScene(first, true) }.Concat(scenes.Where(s => s.path != first)).ToArray();
}
