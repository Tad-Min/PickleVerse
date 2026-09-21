using UnityEngine;
using UnityEditor;
using System.Text;

public class CheckONNX : EditorWindow
{
    [MenuItem("Tools/Check ONNX")]
    public static void Run()
    {
        var a = AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>("Assets/Models/hand_landmark.onnx");
        if(a == null) 
        {
            Debug.LogError("No hand ONNX found");
            return;
        }
        var m = Unity.InferenceEngine.ModelLoader.Load(a);
        
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("INPUTS:");
        foreach(var input in m.inputs)
            sb.AppendLine($"{input.name} {input.shape}");
            
        sb.AppendLine("OUTPUTS:");
        foreach(var output in m.outputs)
            sb.AppendLine($"{output.name}");
            
        Debug.Log(sb.ToString());
    }
}
