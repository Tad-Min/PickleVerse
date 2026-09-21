using UnityEngine;
using UnityEditor;

public class InstantiateCareerPrefab
{
    [MenuItem("Tools/Instantiate Career Prefab")]
    public static void Instantiate()
    {
        GameObject container = GameObject.Find("CareerContainer");
        if (container == null)
        {
            Debug.LogError("Could not find CareerContainer in the scene.");
            return;
        }

        // Delete existing children
        for (int i = container.transform.childCount - 1; i >= 0; i--)
        {
            GameObject.DestroyImmediate(container.transform.GetChild(i).gameObject);
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CareerComponent.prefab");
        if (prefab == null)
        {
            Debug.LogError("Could not find CareerComponent.prefab.");
            return;
        }

        GameObject inst = PrefabUtility.InstantiatePrefab(prefab, container.transform) as GameObject;
        RectTransform rt = inst.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("Successfully instantiated and saved!");
    }
}
