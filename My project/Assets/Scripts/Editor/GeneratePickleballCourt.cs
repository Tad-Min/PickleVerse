using UnityEngine;
using UnityEditor;

public class GeneratePickleballCourt : EditorWindow
{
    [MenuItem("Tools/Generate 3D Pickleball Court")]
    public static void Generate()
    {
        // 1. Create Materials and Save them
        string matPath = "Assets/Materials/Environment";
        if (!AssetDatabase.IsValidFolder(matPath))
        {
            AssetDatabase.CreateFolder("Assets/Materials", "Environment");
        }

        Material courtMat = GetOrCreateMaterial($"{matPath}/CourtMat.mat", new Color(0.1f, 0.3f, 0.5f));
        Material runOffMat = GetOrCreateMaterial($"{matPath}/RunOffMat.mat", new Color(0.1f, 0.5f, 0.3f));
        Material lineMat = GetOrCreateMaterial($"{matPath}/LineMat.mat", Color.white);
        
        Material netMat = GetOrCreateMaterial($"{matPath}/NetMat.mat", new Color(0.2f, 0.2f, 0.2f, 0.8f));
        netMat.SetFloat("_Surface", 1); // Transparent
        netMat.SetOverrideTag("RenderType", "Transparent");
        netMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        
        Material postMat = GetOrCreateMaterial($"{matPath}/PostMat.mat", Color.black);
        postMat.SetFloat("_Metallic", 0.8f);

        // 2. Create Root Object
        GameObject root = new GameObject("PickleballCourt");
        
        // 3. Create Floor
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Court_Floor";
        floor.transform.SetParent(root.transform);
        // Plane is 10x10 units by default.
        floor.transform.localScale = new Vector3(PickleballCourtSpec.TotalWidthTournament / 10f, 1, PickleballCourtSpec.TotalLengthTournament / 10f);
        floor.GetComponent<MeshRenderer>().sharedMaterial = runOffMat;

        // Inner court color (optional, but nice)
        GameObject innerFloor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        innerFloor.name = "Court_Inner_Floor";
        innerFloor.transform.SetParent(root.transform);
        innerFloor.transform.localScale = new Vector3(PickleballCourtSpec.CourtWidth / 10f, 1, PickleballCourtSpec.CourtLength / 10f);
        innerFloor.transform.position = new Vector3(0, 0.001f, 0); // slightly above outer floor
        innerFloor.GetComponent<MeshRenderer>().sharedMaterial = courtMat;
        DestroyImmediate(innerFloor.GetComponent<Collider>()); // Avoid duplicate colliders

        // 4. Create Lines
        float yOffset = 0.002f;
        float lw = PickleballCourtSpec.LineWidth;
        float halfW = PickleballCourtSpec.HalfCourtWidth;
        float halfL = PickleballCourtSpec.HalfCourtLength;
        float kDepth = PickleballCourtSpec.KitchenDepth;
        float cDepth = PickleballCourtSpec.ServiceAreaDepth;

        // Quad local Y becomes World Z when rotated 90 on X
        // Baselines (Z = +-6.7056)
        CreateLine(root, "Line_Baseline_Near", new Vector3(0, yOffset, -halfL), new Vector3(PickleballCourtSpec.CourtWidth + lw, lw, 1), lineMat);
        CreateLine(root, "Line_Baseline_Far", new Vector3(0, yOffset, halfL), new Vector3(PickleballCourtSpec.CourtWidth + lw, lw, 1), lineMat);

        // Sidelines (X = +-3.048)
        CreateLine(root, "Line_Sideline_Left", new Vector3(-halfW, yOffset, 0), new Vector3(lw, PickleballCourtSpec.CourtLength + lw, 1), lineMat);
        CreateLine(root, "Line_Sideline_Right", new Vector3(halfW, yOffset, 0), new Vector3(lw, PickleballCourtSpec.CourtLength + lw, 1), lineMat);

        // Kitchen Lines (Z = +-2.1336)
        CreateLine(root, "Line_Kitchen_Near", new Vector3(0, yOffset, -kDepth), new Vector3(PickleballCourtSpec.CourtWidth, lw, 1), lineMat);
        CreateLine(root, "Line_Kitchen_Far", new Vector3(0, yOffset, kDepth), new Vector3(PickleballCourtSpec.CourtWidth, lw, 1), lineMat);

        // Centerlines (from Kitchen to Baseline)
        float cCenterZ = kDepth + (cDepth / 2f); // 2.1336 + 2.286 = 4.4196
        CreateLine(root, "Line_Centerline_Near", new Vector3(0, yOffset, -cCenterZ), new Vector3(lw, cDepth, 1), lineMat);
        CreateLine(root, "Line_Centerline_Far", new Vector3(0, yOffset, cCenterZ), new Vector3(lw, cDepth, 1), lineMat);

        // 5. Create Net Posts
        CreatePost(root, "Net_Post_Left", new Vector3(-halfW, PickleballCourtSpec.NetHeightSide / 2f, 0), postMat);
        CreatePost(root, "Net_Post_Right", new Vector3(halfW, PickleballCourtSpec.NetHeightSide / 2f, 0), postMat);

        // 6. Create Sagging Net Mesh
        CreateNet(root, netMat);
        
        // 7. Add Gameplay Triggers
        CreateTrigger(root, "Trigger_Kitchen_Near", new Vector3(0, 0.5f, -kDepth / 2f), new Vector3(PickleballCourtSpec.CourtWidth, 1, kDepth));
        CreateTrigger(root, "Trigger_Kitchen_Far", new Vector3(0, 0.5f, kDepth / 2f), new Vector3(PickleballCourtSpec.CourtWidth, 1, kDepth));
        
        float sWidth = PickleballCourtSpec.ServiceAreaWidth;
        CreateTrigger(root, "Trigger_Service_Near_Left", new Vector3(-sWidth/2f, 0.5f, -cCenterZ), new Vector3(sWidth, 1, cDepth));
        CreateTrigger(root, "Trigger_Service_Near_Right", new Vector3(sWidth/2f, 0.5f, -cCenterZ), new Vector3(sWidth, 1, cDepth));
        CreateTrigger(root, "Trigger_Service_Far_Left", new Vector3(-sWidth/2f, 0.5f, cCenterZ), new Vector3(sWidth, 1, cDepth));
        CreateTrigger(root, "Trigger_Service_Far_Right", new Vector3(sWidth/2f, 0.5f, cCenterZ), new Vector3(sWidth, 1, cDepth));

        // Save Prefab
        string prefabPath = "Assets/Prefabs/Environment/PickleballCourt.prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.UserAction, out bool success);
        if (success)
        {
            Debug.Log($"Court successfully generated and saved to {prefabPath}");
        }
        else
        {
            Debug.LogError("Failed to save prefab.");
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

    private static void CreateLine(GameObject root, string name, Vector3 position, Vector3 scale, Material mat)
    {
        GameObject line = GameObject.CreatePrimitive(PrimitiveType.Quad);
        line.name = name;
        line.transform.SetParent(root.transform);
        line.transform.position = position;
        line.transform.rotation = Quaternion.Euler(90, 0, 0); // Lay flat
        line.transform.localScale = scale; // X = width, Y = length
        line.GetComponent<MeshRenderer>().sharedMaterial = mat;
        DestroyImmediate(line.GetComponent<Collider>());
    }

    private static void CreatePost(GameObject root, string name, Vector3 position, Material mat)
    {
        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.name = name;
        post.transform.SetParent(root.transform);
        post.transform.position = position;
        post.transform.localScale = new Vector3(0.08f, PickleballCourtSpec.NetHeightSide / 2f, 0.08f); // 8cm diameter
        post.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    private static void CreateNet(GameObject root, Material mat)
    {
        GameObject net = new GameObject("Net");
        net.transform.SetParent(root.transform);
        net.transform.position = Vector3.zero;

        MeshFilter mf = net.AddComponent<MeshFilter>();
        MeshRenderer mr = net.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;

        Mesh mesh = new Mesh();
        mesh.name = "SaggingNetMesh";

        float w = PickleballCourtSpec.HalfCourtWidth; // 3.048
        float hSide = PickleballCourtSpec.NetHeightSide; // 0.9144
        float hCenter = PickleballCourtSpec.NetHeightCenter; // 0.8636

        // We will create a double-sided net using 2 quads (left half, right half)
        // Vertices (bottom left, top left, bottom center, top center, bottom right, top right)
        Vector3[] vertices = new Vector3[6]
        {
            new Vector3(-w, 0, 0),       // 0: BL
            new Vector3(-w, hSide, 0),   // 1: TL
            new Vector3(0, 0, 0),        // 2: BC
            new Vector3(0, hCenter, 0),  // 3: TC
            new Vector3(w, 0, 0),        // 4: BR
            new Vector3(w, hSide, 0)     // 5: TR
        };

        Vector2[] uv = new Vector2[6]
        {
            new Vector2(0, 0),
            new Vector2(0, 1),
            new Vector2(0.5f, 0),
            new Vector2(0.5f, 1),
            new Vector2(1, 0),
            new Vector2(1, 1)
        };

        // Triangles for front face
        int[] triangles = new int[]
        {
            // Left quad
            0, 1, 2,
            1, 3, 2,
            // Right quad
            2, 3, 4,
            3, 5, 4,
            // Back face (left quad)
            2, 1, 0,
            2, 3, 1,
            // Back face (right quad)
            4, 3, 2,
            4, 5, 3
        };

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        mf.sharedMesh = mesh;
        
        // Add a box collider for the net (approximate)
        BoxCollider bc = net.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, hSide / 2f, 0);
        bc.size = new Vector3(w * 2, hSide, 0.02f);
    }

    private static void CreateTrigger(GameObject root, string name, Vector3 position, Vector3 size)
    {
        GameObject trigger = new GameObject(name);
        trigger.transform.SetParent(root.transform);
        trigger.transform.position = position;
        
        BoxCollider bc = trigger.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = size;
    }
}
