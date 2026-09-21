using UnityEngine;

public class PoseTrackerMock : MonoBehaviour
{
    [Header("Tracking Data Output")]
    public float CurrentWristAngle;
    public float CurrentKneeBend;
    public float SwingSpeed;

    // Full Body Joints
    private Transform _head, _neck, _hips;
    private Transform _lShoulder, _lElbow, _lWrist;
    private Transform _rShoulder, _rElbow, _rWrist;
    private Transform _lHip, _lKnee, _lAnkle;
    private Transform _rHip, _rKnee, _rAnkle;

    private float _time;
    private GameObject _paddle;

    void Start()
    {
        CreateFullBodySkeleton();
        
        _paddle = GameObject.Find("PickleballPaddle");
        if (_paddle != null && _rWrist != null)
        {
            _paddle.transform.SetParent(_rWrist);
            _paddle.transform.localPosition = new Vector3(0, 0, 0);
            _paddle.transform.localRotation = Quaternion.Euler(0, 90, 0);
        }
    }

    void CreateFullBodySkeleton()
    {
        // Central body (Relative to root)
        _hips = CreateJoint("HipsCenter", new Vector3(0, 1.0f, 0), transform);
        _neck = CreateJoint("Neck", new Vector3(0, 0.5f, 0), _hips); // Neck is 0.5m above hips
        _head = CreateJoint("Head", new Vector3(0, 0.2f, 0), _neck); // Head is 0.2m above neck

        // Left Arm (Relative to Neck)
        _lShoulder = CreateJoint("LShoulder", new Vector3(-0.2f, 0, 0), _neck);
        _lElbow = CreateJoint("LElbow", new Vector3(0, -0.3f, 0), _lShoulder); // Arm hangs down
        _lWrist = CreateJoint("LWrist", new Vector3(0, -0.3f, 0), _lElbow);

        // Right Arm (Relative to Neck)
        _rShoulder = CreateJoint("RShoulder", new Vector3(0.2f, 0, 0), _neck);
        _rElbow = CreateJoint("RElbow", new Vector3(0, -0.3f, 0), _rShoulder);
        _rWrist = CreateJoint("RWrist", new Vector3(0, -0.3f, 0), _rElbow);

        // Left Leg (Relative to Hips)
        _lHip = CreateJoint("LHip", new Vector3(-0.15f, 0, 0), _hips);
        _lKnee = CreateJoint("LKnee", new Vector3(0, -0.5f, 0), _lHip);
        _lAnkle = CreateJoint("LAnkle", new Vector3(0, -0.4f, 0), _lKnee);

        // Right Leg (Relative to Hips)
        _rHip = CreateJoint("RHip", new Vector3(0.15f, 0, 0), _hips);
        _rKnee = CreateJoint("RKnee", new Vector3(0, -0.5f, 0), _rHip);
        _rAnkle = CreateJoint("RAnkle", new Vector3(0, -0.4f, 0), _rKnee);

        // Add line renderers to draw "bones"
        AddBoneRenderer(_head.gameObject, _head, _neck);
        AddBoneRenderer(_neck.gameObject, _neck, _hips);
        
        AddBoneRenderer(_lShoulder.gameObject, _neck, _lShoulder);
        AddBoneRenderer(_lElbow.gameObject, _lShoulder, _lElbow);
        AddBoneRenderer(_lWrist.gameObject, _lElbow, _lWrist);
        
        AddBoneRenderer(_rShoulder.gameObject, _neck, _rShoulder);
        AddBoneRenderer(_rElbow.gameObject, _rShoulder, _rElbow);
        AddBoneRenderer(_rWrist.gameObject, _rElbow, _rWrist);

        AddBoneRenderer(_lHip.gameObject, _hips, _lHip);
        AddBoneRenderer(_lKnee.gameObject, _lHip, _lKnee);
        AddBoneRenderer(_lAnkle.gameObject, _lKnee, _lAnkle);

        AddBoneRenderer(_rHip.gameObject, _hips, _rHip);
        AddBoneRenderer(_rKnee.gameObject, _rHip, _rKnee);
        AddBoneRenderer(_rAnkle.gameObject, _rKnee, _rAnkle);
    }

    Transform CreateJoint(string jointName, Vector3 localPos, Transform parent)
    {
        // Logical Bone (Scale 1)
        GameObject joint = new GameObject(jointName);
        joint.transform.SetParent(parent, false);
        joint.transform.localPosition = localPos;
        joint.transform.localScale = Vector3.one;

        // Visual Mesh (Scale 0.08)
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = jointName + "_Visual";
        visual.transform.SetParent(joint.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = Vector3.one * 0.08f;
        
        Destroy(visual.GetComponent<Collider>());
        
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.red);
        else mat.color = Color.red;
        visual.GetComponent<MeshRenderer>().sharedMaterial = mat;
        
        return joint.transform;
    }

    void AddBoneRenderer(GameObject owner, Transform start, Transform end)
    {
        LineRenderer lr = owner.AddComponent<LineRenderer>();
        lr.startWidth = 0.025f;
        lr.endWidth = 0.025f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = Color.white;
        lr.endColor = Color.white;
        lr.useWorldSpace = true;
    }

    void Update()
    {
        if (_rShoulder == null || _rElbow == null) return;

        _time += Time.deltaTime;

        // Pickleball Forehand Swing Mock Animation
        float swingCycle = Mathf.Sin(_time * 2f); // -1 to 1
        
        // Torso rotation (coiling)
        _hips.localRotation = Quaternion.Euler(0, swingCycle * 30f, 0);

        // Right Arm (Swing)
        _rShoulder.localRotation = Quaternion.Euler(swingCycle * 60f, 0, swingCycle * 20f);
        float elbowBend = Mathf.Lerp(10f, 45f, (swingCycle + 1f) / 2f);
        _rElbow.localRotation = Quaternion.Euler(0, 0, elbowBend);
        _rWrist.localRotation = Quaternion.Euler(0, swingCycle * -20f, 0);

        // Left Arm (Balance)
        _lShoulder.localRotation = Quaternion.Euler(30f, 0, -20f);
        _lElbow.localRotation = Quaternion.Euler(0, 0, -30f);

        // Legs (Knee bend/bounce)
        float bounce = Mathf.Sin(_time * 4f); 
        float kneeBend = Mathf.Lerp(5f, 25f, (bounce + 1f) / 2f);
        _lKnee.localRotation = Quaternion.Euler(kneeBend, 0, 0);
        _rKnee.localRotation = Quaternion.Euler(kneeBend, 0, 0);
        
        // Hips bounce up and down slightly
        _hips.localPosition = new Vector3(0, 1.0f - (kneeBend * 0.005f), 0);

        // Calculate mock stats
        CurrentWristAngle = elbowBend; // Approximation
        CurrentKneeBend = kneeBend;
        SwingSpeed = Mathf.Abs(Mathf.Cos(_time * 2f)) * 25f; 

        // Update all bones
        UpdateBoneRenderer(_head.gameObject, _head, _neck);
        UpdateBoneRenderer(_neck.gameObject, _neck, _hips);
        UpdateBoneRenderer(_lShoulder.gameObject, _neck, _lShoulder);
        UpdateBoneRenderer(_lElbow.gameObject, _lShoulder, _lElbow);
        UpdateBoneRenderer(_lWrist.gameObject, _lElbow, _lWrist);
        UpdateBoneRenderer(_rShoulder.gameObject, _neck, _rShoulder);
        UpdateBoneRenderer(_rElbow.gameObject, _rShoulder, _rElbow);
        UpdateBoneRenderer(_rWrist.gameObject, _rElbow, _rWrist);
        UpdateBoneRenderer(_lHip.gameObject, _hips, _lHip);
        UpdateBoneRenderer(_lKnee.gameObject, _lHip, _lKnee);
        UpdateBoneRenderer(_lAnkle.gameObject, _lKnee, _lAnkle);
        UpdateBoneRenderer(_rHip.gameObject, _hips, _rHip);
        UpdateBoneRenderer(_rKnee.gameObject, _rHip, _rKnee);
        UpdateBoneRenderer(_rAnkle.gameObject, _rKnee, _rAnkle);
    }

    void UpdateBoneRenderer(GameObject owner, Transform start, Transform end)
    {
        LineRenderer lr = owner.GetComponent<LineRenderer>();
        if (lr != null && start != null && end != null)
        {
            lr.SetPosition(0, start.position);
            lr.SetPosition(1, end.position);
        }
    }
}
