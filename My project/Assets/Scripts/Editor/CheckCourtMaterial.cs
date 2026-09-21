using UnityEngine;
using UnityEditor;

public class CheckCourtMaterial : EditorWindow
{
    [MenuItem("Tools/Check Court Material")]
    public static void Check()
    {
        GameObject go = GameObject.Find("Court_Floor");
        if (go != null)
        {
            var mat = go.GetComponent<MeshRenderer>().sharedMaterial;
            if (mat != null)
            {
                Debug.Log("Material Name: " + mat.name);
                if (mat.shader != null)
                {
                    Debug.Log("Shader Name: " + mat.shader.name);
                    Debug.Log("Shader is supported: " + mat.shader.isSupported);
                }
                else
                {
                    Debug.LogError("Shader is NULL on Material!");
                }
            }
            else
            {
                Debug.LogError("Material is NULL!");
            }
        }
        else
        {
            Debug.LogError("Court_Floor not found in scene!");
        }
    }
}
