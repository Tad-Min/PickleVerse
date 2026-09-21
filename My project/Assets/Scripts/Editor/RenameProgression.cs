using UnityEngine;
using UnityEditor;

public class RenameProgression
{
    [MenuItem("Tools/Rename Progression Container")]
    public static void Rename()
    {
        GameObject container = GameObject.Find("ProgressionContainer");
        if (container != null)
        {
            container.name = "ProgressionComponent";
            string path = "Assets/Prefabs/UI/ProgressionComponent.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(container, path, InteractionMode.AutomatedAction);
            Debug.Log("Renamed and saved as Prefab: " + path);
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        }
        else
        {
            Debug.LogWarning("Could not find ProgressionContainer.");
        }
    }
}
