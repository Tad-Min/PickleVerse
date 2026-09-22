using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Gameplay owns an additive UI scene; pre-opened UI scenes are reused.</summary>
[DisallowMultipleComponent]
public sealed class TrainingUiSceneLoader : MonoBehaviour
{
    public const string ScenePath = "Assets/Scenes/TrainingUI.unity";
    private bool owned, destroyed, quitting;
    private void Start()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid() && scene.isLoaded) return;
        if (!Application.CanStreamedLevelBeLoaded(ScenePath))
        {
            Debug.LogError("TrainingUI is not in the active Build Profile scene list.", this);
            return;
        }
        owned = true;
        var operation = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
        if (operation != null) operation.completed += OnLoaded;
    }
    private void OnLoaded(AsyncOperation operation)
    {
        operation.completed -= OnLoaded;
        if (destroyed && !quitting) UnloadOwned();
    }
    private void OnApplicationQuit() => quitting = true;
    private void OnDestroy()
    {
        destroyed = true;
        if (!quitting) UnloadOwned();
    }
    private void UnloadOwned()
    {
        if (!owned) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
        {
            owned = false;
            SceneManager.UnloadSceneAsync(scene);
        }
    }
}

