using UnityEngine;
using UnityEditor;
using Unity.InferenceEngine;
using System.Text;

public class ProbeONNX
{
    [MenuItem("Tools/Probe ONNX")]
    public static void Run()
    {
        var asset = AssetDatabase.LoadAssetAtPath<ModelAsset>("Assets/Models/hand_landmark.onnx");
        if (asset == null) {
            Debug.LogError("Asset not found!");
            return;
        }
        var model = ModelLoader.Load(asset);
        var sb = new StringBuilder();
        sb.AppendLine("Inputs:");
        foreach (var input in model.inputs)
        {
            sb.AppendLine($"- {input.name} [{input.shape.ToString()}]");
        }
        sb.AppendLine("Outputs:");
        foreach (var output in model.outputs)
        {
            sb.AppendLine($"- {output.name}");
        }
        Debug.Log(sb.ToString());
    }
}
