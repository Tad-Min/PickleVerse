using UnityEngine;
using UnityEditor;

public class InstantiateCollectionPrefab
{
    [MenuItem("Tools/Instantiate Collection Prefab")]
    public static void InstantiatePrefab()
    {
        GameObject container = null;
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach(var go in allObjects)
        {
            if (go.name == "CollectionContainer" && go.scene.isLoaded)
            {
                container = go;
                break;
            }
        }

        if (container == null)
        {
            Debug.LogError("CollectionContainer not found!");
            return;
        }

        // Clear existing children
        while (container.transform.childCount > 0)
        {
            GameObject.DestroyImmediate(container.transform.GetChild(0).gameObject);
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CollectionComponent.prefab");
        if (prefab != null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container.transform);
            
            // Stretch the instance
            RectTransform rt = instance.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Debug.Log("Instantiated CollectionComponent into CollectionContainer");
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        }
        else
        {
            Debug.LogError("Could not load CollectionComponent.prefab");
        }
    }
}
