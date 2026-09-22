using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Idempotent editor installer for the offline camera-tracking training demo.</summary>
public static class SetupOfflineTrackingTrainingScene
{
    private const string BallPrefabPath = "Assets/Prefabs/Environment/PickleballBall.prefab";
    private const string PhysicsMaterialPath = "Assets/Materials/Environment/PickleballPhysics.physicMaterial";

    [MenuItem("Tools/PickleVerse/Setup Offline Tracking Training")]
    public static void Install()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != "Assets/Scenes/Training.unity")
            throw new System.InvalidOperationException("Open Training in Edit Mode before setup.");
        GameObject player = GameObject.Find("PlayerRoot");
        if (player == null) player = new GameObject("PlayerRoot");
        RemoveComponent<PoseTrackerMock>(player);

        var calibration = GetOrAdd<CalibrationProfile>(player);
        var presenter = GetOrAdd<SkeletonPresenter>(player);
        var tracker = GetOrAdd<SentisPoseTracker>(player);
        GetOrAdd<TrainingUiSceneLoader>(player);

        tracker.ModelAsset = AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>("Assets/Models/movenet_lightning.onnx");
        tracker.HandModelAsset = AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>("Assets/Models/hand_landmark.onnx");
        if (tracker.ModelAsset == null || tracker.HandModelAsset == null)
            Debug.LogError("Tracking models are missing from Assets/Models.");

        GameObject paddle = KeepSinglePaddle();
        if (paddle == null)
        {
            Debug.LogError("PickleballPaddle prefab/object is missing. Generate it first.");
            return;
        }
        paddle.transform.SetParent(null, true);
        Rigidbody paddleBody = GetOrAdd<Rigidbody>(paddle);
        paddleBody.isKinematic = true;
        paddleBody.interpolation = RigidbodyInterpolation.Interpolate;
        paddleBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        var paddleController = GetOrAdd<PaddleKinematicController>(paddle);

        PhysicsMaterial physicsMaterial = GetOrCreatePhysicsMaterial();
        paddle.GetComponent<Collider>().sharedMaterial = physicsMaterial;
        GameObject ball = GetOrCreateBall(physicsMaterial);
        EnsureCourtColliders();

        var serializedTracker = new SerializedObject(tracker);
        serializedTracker.FindProperty("calibration").objectReferenceValue = calibration;
        serializedTracker.FindProperty("skeletonPresenter").objectReferenceValue = presenter;
        serializedTracker.FindProperty("paddle").objectReferenceValue = paddleController;
        serializedTracker.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = player;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log($"Offline tracking scene ready. Ball: {ball.name}; Paddle: {paddle.name}");
    }

    private static GameObject KeepSinglePaddle()
    {
        GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        GameObject first = null;
        foreach (GameObject candidate in all)
        {
            if (candidate == null || candidate.scene != SceneManager.GetActiveScene() || candidate.name != "PickleballPaddle") continue;
            if (first == null) first = candidate;
            else Object.DestroyImmediate(candidate);
        }
        return first;
    }

    private static GameObject GetOrCreateBall(PhysicsMaterial physicsMaterial)
    {
        GameObject ball = GameObject.Find("PickleballBall");
        if (ball == null)
        {
            ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "PickleballBall";
            ball.transform.position = new Vector3(0f, 1.4f, -2.5f);
            ball.transform.localScale = Vector3.one * 0.074f;
            var renderer = ball.GetComponent<MeshRenderer>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = new Color(0.82f, 0.95f, 0.1f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", material.color);
            renderer.sharedMaterial = material;
            const string visualPath = "Assets/Materials/Environment/PickleballBall.mat";
            var savedMaterial = AssetDatabase.LoadAssetAtPath<Material>(visualPath);
            if (savedMaterial == null) { AssetDatabase.CreateAsset(material, visualPath); savedMaterial = material; }
            else Object.DestroyImmediate(material);
            renderer.sharedMaterial = savedMaterial;
        }
        var collider = GetOrAdd<SphereCollider>(ball);
        collider.sharedMaterial = physicsMaterial;
        Rigidbody body = GetOrAdd<Rigidbody>(ball);
        body.mass = 0.026f;
        body.useGravity = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        GetOrAdd<BallContactController>(ball);
        PrefabUtility.SaveAsPrefabAssetAndConnect(ball, BallPrefabPath, InteractionMode.UserAction);
        return ball;
    }

    private static void EnsureCourtColliders()
    {
        GameObject floor = GameObject.Find("Court_Floor");
        if (floor != null && floor.GetComponent<Collider>() != null) floor.GetComponent<Collider>().isTrigger = false;
        GameObject net = GameObject.Find("Net");
        if (net != null && net.GetComponent<Collider>() != null) net.GetComponent<Collider>().isTrigger = false;
    }

    private static PhysicsMaterial GetOrCreatePhysicsMaterial()
    {
        PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysicsMaterialPath);
        if (material != null) return material;
        if (!AssetDatabase.IsValidFolder("Assets/Materials/Environment")) AssetDatabase.CreateFolder("Assets/Materials", "Environment");
        material = new PhysicsMaterial("PickleballPhysics")
        {
            bounciness = .62f,
            dynamicFriction = .35f,
            staticFriction = .35f,
            frictionCombine = PhysicsMaterialCombine.Average,
            bounceCombine = PhysicsMaterialCombine.Maximum
        };
        AssetDatabase.CreateAsset(material, PhysicsMaterialPath);
        return material;
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component == null ? gameObject.AddComponent<T>() : component;
    }

    private static void RemoveComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component != null) Object.DestroyImmediate(component);
    }
}
