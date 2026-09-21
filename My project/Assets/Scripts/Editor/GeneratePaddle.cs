using UnityEngine;
using UnityEditor;

public class GeneratePaddle : EditorWindow
{
    [MenuItem("Tools/Generate 3D Pickleball Paddle")]
    public static void Generate()
    {
        // 1. Create Materials and Save them
        string matPath = "Assets/Materials/Equipment";
        if (!AssetDatabase.IsValidFolder(matPath))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }
            AssetDatabase.CreateFolder("Assets/Materials", "Equipment");
        }

        Material faceMat = GetOrCreateMaterial($"{matPath}/PaddleFaceMat.mat", new Color(0.1f, 0.1f, 0.15f));
        faceMat.SetFloat("_Smoothness", 0.2f);
        
        Material handleMat = GetOrCreateMaterial($"{matPath}/PaddleHandleMat.mat", new Color(0.05f, 0.05f, 0.05f));
        handleMat.SetFloat("_Smoothness", 0.1f);
        
        Material edgeGuardMat = GetOrCreateMaterial($"{matPath}/PaddleEdgeGuardMat.mat", new Color(0.2f, 0.2f, 0.2f));
        edgeGuardMat.SetFloat("_Smoothness", 0.5f);

        // 2. Create Root Object
        GameObject root = new GameObject("PickleballPaddle");

        // We'll approximate the paddle using basic primitives since we don't have a 3D model.

        // Paddle Face (Box)
        GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cube);
        face.name = "Paddle_Face";
        face.transform.SetParent(root.transform);
        // Face dimensions (excluding handle)
        float faceW = PickleballPaddleSpec.StandardWidth;
        float faceL = PickleballPaddleSpec.MaxLength - PickleballPaddleSpec.HandleLength;
        float faceThickness = PickleballPaddleSpec.StandardThicknessMid; 
        face.transform.localScale = new Vector3(faceW, faceThickness, faceL);
        // Offset so handle attaches at Z=0
        face.transform.position = new Vector3(0, 0, (faceL / 2f) + PickleballPaddleSpec.HandleLength);
        face.GetComponent<MeshRenderer>().sharedMaterial = faceMat;

        // Handle (Cylinder)
        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        handle.name = "Paddle_Handle";
        handle.transform.SetParent(root.transform);
        float handleRadius = PickleballPaddleSpec.HandleDiameter / 2f;
        // Cylinder default is 2 units tall, oriented along Y.
        handle.transform.localScale = new Vector3(handleRadius * 2, PickleballPaddleSpec.HandleLength / 2f, handleRadius * 2);
        // Rotate to lay flat along Z axis
        handle.transform.rotation = Quaternion.Euler(90, 0, 0);
        handle.transform.position = new Vector3(0, 0, PickleballPaddleSpec.HandleLength / 2f);
        handle.GetComponent<MeshRenderer>().sharedMaterial = handleMat;
        
        // Edge Guard (just slightly larger box around the face)
        GameObject edgeGuard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        edgeGuard.name = "Paddle_EdgeGuard";
        edgeGuard.transform.SetParent(root.transform);
        float edgeThickness = 0.002f; // 2mm edge guard thickness
        edgeGuard.transform.localScale = new Vector3(faceW + edgeThickness, faceThickness + 0.001f, faceL + edgeThickness);
        edgeGuard.transform.position = face.transform.position;
        edgeGuard.GetComponent<MeshRenderer>().sharedMaterial = edgeGuardMat;

        // Remove colliders from primitives and add a single BoxCollider to root
        DestroyImmediate(face.GetComponent<Collider>());
        DestroyImmediate(handle.GetComponent<Collider>());
        DestroyImmediate(edgeGuard.GetComponent<Collider>());

        BoxCollider bc = root.AddComponent<BoxCollider>();
        bc.size = new Vector3(faceW, faceThickness, PickleballPaddleSpec.MaxLength);
        bc.center = new Vector3(0, 0, PickleballPaddleSpec.MaxLength / 2f);

        // Save Prefab
        string prefabPath = "Assets/Prefabs/Environment/PickleballPaddle.prefab";
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Environment"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            AssetDatabase.CreateFolder("Assets/Prefabs", "Environment");
        }

        PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.UserAction, out bool success);
        
        if (success)
        {
            Debug.Log($"Paddle successfully generated and saved to {prefabPath}");
        }
        else
        {
            Debug.LogError("Failed to save paddle prefab.");
        }
    }

    private static Material GetOrCreateMaterial(string path, Color color)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        else
        {
            mat.color = color;
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }
}
