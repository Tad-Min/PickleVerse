using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class PaddleKinematicController : MonoBehaviour
{
    [Tooltip("Point on the paddle held at the wrist, in paddle-local metres.")]
    [SerializeField] private Vector3 localGripPoint = new Vector3(0, 0, 0.05715f);
    [UnityEngine.Serialization.FormerlySerializedAs("paddleRotationOffset")]
    [SerializeField] private Vector3 gripRotationOffset = Vector3.zero;
    [SerializeField] private float positionSmoothing = 20f;
    [SerializeField] private float rotationSmoothing = 18f;
    [SerializeField] private float maximumLinearSpeed = 14f;
    [SerializeField] private float maximumAngularSpeed = 1440f;

    public Vector3 LinearVelocity { get; private set; }
    public Vector3 AngularVelocity { get; private set; }
    public bool HasTarget { get; private set; }
    public bool WristTracked { get; private set; }
    public bool OrientationTracked { get; private set; }
    public Vector3 GripWorldPosition => transform.TransformPoint(localGripPoint);

    private Rigidbody _body;
    private Vector3 _targetPosition;
    private Quaternion _targetRotation;
    private Vector3 _lastPosition;
    private Quaternion _lastRotation;
    private float _lastTargetTime = float.NegativeInfinity;

    private void Awake()
    {
        _body = GetComponent<Rigidbody>();
        _body.isKinematic = true;
        _body.interpolation = RigidbodyInterpolation.Interpolate;
        _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        _lastPosition = _body.position;
        _lastRotation = _body.rotation;
        _targetRotation = _body.rotation;
    }

    public void SetTarget(Vector3 wristPosition, Quaternion handRotation)
    {
        _targetRotation = handRotation * Quaternion.Euler(gripRotationOffset);
        _targetPosition = wristPosition;
        HasTarget = true;
        _lastTargetTime = Time.time;
    }

    public void SetTrackingTarget(PoseFrame frame, float now)
    {
        WristTracked = frame.WristTracked && now - frame.WristTimestamp <= .25f;
        OrientationTracked = WristTracked && frame.HandTracked && now - frame.HandTimestamp <= .25f;
        if (!WristTracked) return;
        if (OrientationTracked)
            _targetRotation = Quaternion.LookRotation(frame.HandForward, frame.HandUp) * Quaternion.Euler(gripRotationOffset);
        _targetPosition = frame.RightWristWorldPosition;
        HasTarget = true;
        _lastTargetTime = now;
    }

    private void FixedUpdate()
    {
        if (!HasTarget)
            return;
        if (Time.time - _lastTargetTime > 0.25f)
        {
            WristTracked = OrientationTracked = false;
            LinearVelocity = AngularVelocity = Vector3.zero;
            _lastPosition = _body.position;
            _lastRotation = _body.rotation;
            return;
        }

        float dt = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        float positionT = 1f - Mathf.Exp(-positionSmoothing * dt);
        float rotationT = 1f - Mathf.Exp(-rotationSmoothing * dt);
        Vector3 scaledGrip = Vector3.Scale(localGripPoint, transform.lossyScale);
        Vector3 currentGrip = _body.position + _body.rotation * scaledGrip;
        Vector3 nextGrip = Vector3.Lerp(currentGrip, _targetPosition, positionT);
        Quaternion nextRotation = Quaternion.Slerp(_body.rotation, _targetRotation, rotationT);
        nextRotation = Quaternion.RotateTowards(_body.rotation, nextRotation, maximumAngularSpeed * dt);
        float rotationTravel = Vector3.Distance(_body.rotation * scaledGrip, nextRotation * scaledGrip);
        if (rotationTravel > maximumLinearSpeed * dt)
            nextRotation = Quaternion.Slerp(_body.rotation, nextRotation, maximumLinearSpeed * dt / rotationTravel);
        rotationTravel = Vector3.Distance(_body.rotation * scaledGrip, nextRotation * scaledGrip);
        nextGrip = Vector3.MoveTowards(currentGrip, nextGrip, Mathf.Max(0, maximumLinearSpeed * dt - rotationTravel));
        Vector3 nextPosition = nextGrip - nextRotation * scaledGrip;

        LinearVelocity = Vector3.ClampMagnitude((nextPosition - _lastPosition) / dt, maximumLinearSpeed);
        Quaternion delta = nextRotation * Quaternion.Inverse(_lastRotation);
        delta.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        AngularVelocity = axis.sqrMagnitude > 0.0001f
            ? Vector3.ClampMagnitude(axis * angle * Mathf.Deg2Rad / dt, maximumAngularSpeed * Mathf.Deg2Rad)
            : Vector3.zero;

        _body.MovePosition(nextPosition);
        _body.MoveRotation(nextRotation);
        _lastPosition = nextPosition;
        _lastRotation = nextRotation;
    }
}
