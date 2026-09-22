using UnityEngine;
using UnityEditor;

public class SetupAICoachScene : EditorWindow
{
    [MenuItem("Tools/Setup AI Coach Scene")]
    public static void SetupScene()
    {
        // 1. Setup Camera
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            mainCam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
        }
        // Position camera behind baseline, looking slightly down
        mainCam.transform.position = new Vector3(0, 1.8f, -7.5f);
        mainCam.transform.rotation = Quaternion.Euler(10, 0, 0);

        // 2. Setup Lighting
        Light dirLight = Object.FindFirstObjectByType<Light>();
        if (dirLight == null || dirLight.type != LightType.Directional)
        {
            GameObject lightObj = new GameObject("Directional Light");
            dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
        }

        // 3. Load and Instantiate Court
        GameObject courtPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/PickleballCourt.prefab");
        if (courtPrefab != null)
        {
            GameObject existingCourt = GameObject.Find("PickleballCourt");
            if (existingCourt == null)
            {
                GameObject court = (GameObject)PrefabUtility.InstantiatePrefab(courtPrefab);
                court.name = "PickleballCourt";
            }
        }
        else
        {
            Debug.LogError("Could not find PickleballCourt.prefab. Please run the court generator first.");
        }

        // 4. Create empty container for Player/Mock setup
        GameObject playerRoot = GameObject.Find("PlayerRoot");
        if (playerRoot == null)
        {
            playerRoot = new GameObject("PlayerRoot");
            // Position player near baseline
            playerRoot.transform.position = new Vector3(0, 0, -5.5f);
        }

        if (playerRoot.GetComponent<SentisPoseTracker>() == null)
        {
            var tracker = playerRoot.AddComponent<SentisPoseTracker>();
            var model = AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>("Assets/Models/movenet_lightning.onnx");
            tracker.ModelAsset = model;
        }

        if (playerRoot.GetComponent<TrainingUiSceneLoader>() == null)
        {
            playerRoot.AddComponent<TrainingUiSceneLoader>();
        }

        // 5. Load and Instantiate Paddle under player
        GameObject paddlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/PickleballPaddle.prefab");
        if (paddlePrefab != null)
        {
            GameObject existingPaddle = GameObject.Find("PickleballPaddle");
            if (existingPaddle == null)
            {
                GameObject paddle = (GameObject)PrefabUtility.InstantiatePrefab(paddlePrefab);
                paddle.name = "PickleballPaddle";
                paddle.transform.SetParent(playerRoot.transform);
                paddle.transform.localPosition = new Vector3(0.5f, 1f, 0.5f);
                paddle.transform.localRotation = Quaternion.Euler(0, -45, 0);
            }
        }
        else
        {
            Debug.LogError("Could not find PickleballPaddle.prefab. Please run the paddle generator first.");
        }

        // 6. Create Tracking Camera Visual Mockup
        GameObject trackerCam = GameObject.Find("TrackingCameraVisual");
        if (trackerCam == null)
        {
            trackerCam = new GameObject("TrackingCameraVisual");
            trackerCam.transform.position = new Vector3(0, 1.2f, -1.0f); // Placed slightly behind the kitchen line, looking at player
            trackerCam.transform.rotation = Quaternion.Euler(15, 180, 0); // Looking towards the baseline (player)
            
            // Tripod (Cylinder)
            GameObject tripod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tripod.name = "Tripod";
            tripod.transform.SetParent(trackerCam.transform);
            tripod.transform.localPosition = new Vector3(0, -0.6f, 0);
            tripod.transform.localScale = new Vector3(0.05f, 0.6f, 0.05f);
            
            // Camera Body (Cube)
            GameObject camBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
            camBody.name = "CameraBody";
            camBody.transform.SetParent(trackerCam.transform);
            camBody.transform.localPosition = new Vector3(0, 0, 0);
            camBody.transform.localScale = new Vector3(0.15f, 0.1f, 0.2f);
            
            // Lens (Cylinder)
            GameObject lens = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lens.name = "Lens";
            lens.transform.SetParent(camBody.transform);
            lens.transform.localPosition = new Vector3(0, 0, 0.6f);
            lens.transform.localRotation = Quaternion.Euler(90, 0, 0);
            lens.transform.localScale = new Vector3(0.6f, 0.2f, 0.6f);
            
            // Material
            Material camMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (camMat.HasProperty("_BaseColor")) camMat.SetColor("_BaseColor", Color.black);
            else camMat.color = Color.black;
            tripod.GetComponent<MeshRenderer>().sharedMaterial = camMat;
            camBody.GetComponent<MeshRenderer>().sharedMaterial = camMat;
            lens.GetComponent<MeshRenderer>().sharedMaterial = camMat;
            
            // Remove colliders
            Object.DestroyImmediate(tripod.GetComponent<Collider>());
            Object.DestroyImmediate(camBody.GetComponent<Collider>());
            Object.DestroyImmediate(lens.GetComponent<Collider>());
        }

        Debug.Log("AI Coach Scene Setup Complete!");
    }
}
