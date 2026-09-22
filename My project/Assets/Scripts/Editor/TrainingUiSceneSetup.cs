using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TrainingUiSceneSetup
{
    [MenuItem("PickleVerse/UI/Separate Training UI scene")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var gameplay = SceneManager.GetSceneByPath("Assets/Scenes/Training.unity");
        if (!gameplay.IsValid() || !gameplay.isLoaded) throw new InvalidOperationException("Open Training first.");
        var player = gameplay.GetRootGameObjects().First(x => x.name == "PlayerRoot");
        var oldHud = player.GetComponent<AICoachHUD>();
        var ui = SceneManager.GetSceneByPath(TrainingUiSceneLoader.ScenePath);
        if (!ui.IsValid() || !ui.isLoaded)
            ui = AssetDatabase.LoadAssetAtPath<SceneAsset>(TrainingUiSceneLoader.ScenePath)
                ? EditorSceneManager.OpenScene(TrainingUiSceneLoader.ScenePath, OpenSceneMode.Additive)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(ui);
        try
        {
            var hud = ui.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<AICoachHUD>(true)).FirstOrDefault();
            if (!hud)
            {
                var uiRoot = new GameObject("TrainingUI");
                Undo.RegisterCreatedObjectUndo(uiRoot, "Create separate UI root");
                hud = uiRoot.AddComponent<AICoachHUD>();
                var serialized = new SerializedObject(hud);
                var font = oldHud ? new SerializedObject(oldHud).FindProperty("coachFont").objectReferenceValue : null;
                serialized.FindProperty("coachFont").objectReferenceValue = font ? font :
                    AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/OfflineCoach/CoachVietnamese.asset");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                hud.BuildLayout();
            }
            if (!player.GetComponent<TrainingUiSceneLoader>()) Undo.AddComponent<TrainingUiSceneLoader>(player);
            // Only remove the obsolete behaviour, not any gameplay components.
            if (oldHud) Undo.DestroyObjectImmediate(oldHud);
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (string path in new[] { gameplay.path, TrainingUiSceneLoader.ScenePath })
            {
                int index = scenes.FindIndex(x => x.path == path);
                if (index < 0) scenes.Add(new EditorBuildSettingsScene(path, true));
                else scenes[index].enabled = true;
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            EditorSceneManager.MarkSceneDirty(ui);
            EditorSceneManager.SaveScene(ui, TrainingUiSceneLoader.ScenePath);
            EditorSceneManager.MarkSceneDirty(gameplay);
            EditorSceneManager.SaveScene(gameplay);
            Selection.activeGameObject = hud.gameObject;
        }
        finally { SceneManager.SetActiveScene(gameplay); }
    }
}

