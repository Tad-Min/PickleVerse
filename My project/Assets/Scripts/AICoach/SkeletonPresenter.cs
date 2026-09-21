using UnityEngine;

/// <summary>Debug skeleton that is allocated once and then updated from PoseFrame samples.</summary>
public class SkeletonPresenter : MonoBehaviour
{
    [SerializeField] private float jointRadius = 0.045f;
    [SerializeField] private float boneWidth = 0.025f;
    [SerializeField] private Color jointColor = Color.red;
    [SerializeField] private Color boneColor = Color.white;

    private readonly int[,] _bones =
    {
        { 0, 5 }, { 0, 6 }, { 5, 6 }, { 5, 7 }, { 7, 9 }, { 6, 8 }, { 8, 10 },
        { 5, 11 }, { 6, 12 }, { 11, 12 }, { 11, 13 }, { 13, 15 }, { 12, 14 }, { 14, 16 }
    };
    private Transform[] _joints;
    private LineRenderer[] _lines;
    private Material _jointMaterial;
    private Material _lineMaterial;

    public void EnsureCreated()
    {
        if (_joints != null)
            return;

        _jointMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        _jointMaterial.color = jointColor;
        if (_jointMaterial.HasProperty("_BaseColor")) _jointMaterial.SetColor("_BaseColor", jointColor);
        _lineMaterial = new Material(Shader.Find("Sprites/Default"));

        _joints = new Transform[PoseFrame.JointCount];
        for (int i = 0; i < _joints.Length; i++)
        {
            var joint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            joint.name = $"PoseJoint_{i}";
            joint.transform.SetParent(transform, false);
            joint.transform.localScale = Vector3.one * (jointRadius * 2f);
            Destroy(joint.GetComponent<Collider>());
            joint.GetComponent<MeshRenderer>().sharedMaterial = _jointMaterial;
            _joints[i] = joint.transform;
        }

        _lines = new LineRenderer[_bones.GetLength(0)];
        for (int i = 0; i < _lines.Length; i++)
        {
            var lineObject = new GameObject($"PoseBone_{i}");
            lineObject.transform.SetParent(transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.startWidth = boneWidth;
            line.endWidth = boneWidth;
            line.useWorldSpace = true;
            line.sharedMaterial = _lineMaterial;
            line.startColor = boneColor;
            line.endColor = boneColor;
            _lines[i] = line;
        }
    }

    public void Present(PoseFrame frame)
    {
        EnsureCreated();
        transform.position = frame.RootPosition;

        for (int i = 0; i < _joints.Length; i++)
        {
            bool visible = frame.Confidence[i] > 0f;
            _joints[i].gameObject.SetActive(visible);
            if (visible)
                _joints[i].localPosition = frame.LocalJoints[i];
        }

        for (int i = 0; i < _lines.Length; i++)
        {
            int a = _bones[i, 0];
            int b = _bones[i, 1];
            bool visible = frame.Confidence[a] > 0f && frame.Confidence[b] > 0f;
            _lines[i].gameObject.SetActive(visible);
            if (visible)
            {
                _lines[i].SetPosition(0, frame.RootPosition + frame.LocalJoints[a]);
                _lines[i].SetPosition(1, frame.RootPosition + frame.LocalJoints[b]);
            }
        }
    }

    private void OnDestroy()
    {
        Destroy(_jointMaterial);
        Destroy(_lineMaterial);
    }
}
