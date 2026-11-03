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
        const string path = "Assets/Scenes/UI/Login.unity";
        var previous = EditorSceneManager.GetSceneManagerSetup();
        var scene = SceneManager.GetSceneByPath(path);
        bool openedHere = !scene.isLoaded;
        if (openedHere) scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path)
            ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var view = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PickleVerse.FrontEnd.AccountPanelView>(true)).FirstOrDefault();
        if (!view) throw new InvalidOperationException("Login is missing its authored AccountPanelView.");
        SceneManager.SetActiveScene(scene);
        if (!view.forgot) view.forgot = Link(view, "Forgot Password", "Quên mật khẩu", view.verifyLink.transform.GetSiblingIndex() + 1);
        if (!view.back) view.back = Link(view, "Back To Login", "Về đăng nhập", view.resend.transform.GetSiblingIndex() + 1);
        view.back.gameObject.SetActive(false);
        view.navigation = view.card.GetComponentsInChildren<UnityEngine.UI.Selectable>(true);
        EditorUtility.SetDirty(view);
        EditorSceneManager.SaveScene(scene, path);
        EditorSceneManager.RestoreSceneManagerSetup(previous);
        EditorBuildSettings.globalScenes = First(EditorBuildSettings.globalScenes, path);
        foreach (var guid in AssetDatabase.FindAssets("t:BuildProfile"))
        {
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(AssetDatabase.GUIDToAssetPath(guid));
            if (!profile || profile.name.IndexOf("Server", StringComparison.OrdinalIgnoreCase) >= 0 || !profile.overrideGlobalScenes) continue;
            profile.scenes = First(profile.scenes, path);
            EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
        }
        return "Login scene saved; client build scene lists start with Login.";
    }
    static EditorBuildSettingsScene[] First(EditorBuildSettingsScene[] scenes, string first) =>
        new[] { new EditorBuildSettingsScene(first, true) }.Concat(scenes.Where(s => s.path != first && s.path != "Assets/Scenes/UI/Authentication.unity")).ToArray();
    static UnityEngine.UI.Button Link(PickleVerse.FrontEnd.AccountPanelView view, string name, string text, int index)
    {
        var button = UnityEngine.Object.Instantiate(view.verifyLink, view.card);
        button.name = name;
        button.transform.SetSiblingIndex(index);
        button.GetComponentInChildren<TMPro.TMP_Text>().text = text;
        return button;
    }
}
