using UnityEngine;
using UnityEditor;

public class CheckShaders : EditorWindow
{
    [MenuItem("Tools/Check Shaders")]
    public static void Check()
    {
        Shader urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp != null) Debug.Log("URP Lit is FOUND");
        else Debug.LogError("URP Lit is MISSING");

        Shader std = Shader.Find("Standard");
        if (std != null) Debug.Log("Standard is FOUND");
        else Debug.LogError("Standard is MISSING");
        
        var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        if (rp != null) Debug.Log("Current Render Pipeline Asset: " + rp.name);
        else Debug.Log("Current Render Pipeline Asset is NULL (Using Built-in)");
    }
}
