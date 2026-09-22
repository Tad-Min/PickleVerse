using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class BallContactController : MonoBehaviour
{
    [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 1.4f, -2.5f);
    [SerializeField] private float maximumBallSpeed = 18f;
    [SerializeField] private float paddleVelocityTransfer = 1.25f;
    [SerializeField] private float resetHeight = -1f;
    [SerializeField] private float resetDistance = 14f;
    [SerializeField] private float resetAfterSeconds = 12f;

    public int HitCount { get; private set; }
    public event System.Action<PaddleHitInfo> PaddleHit;
    public Vector3 Velocity => _body == null ? Vector3.zero : _body.linearVelocity;

    private Rigidbody _body;
    private float _lastResetTime;
    private Vector3 _incomingVelocity;
    private SentisPoseTracker _tracker;

    private void Awake()
    {
        _body = GetComponent<Rigidbody>();
        _body.useGravity = true;
        _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _body.interpolation = RigidbodyInterpolation.Interpolate;
        _tracker = FindAnyObjectByType<SentisPoseTracker>();
    }

    private void Start() => ResetBall();

    private void FixedUpdate()
    {
        _incomingVelocity = _body.linearVelocity;
        if (transform.position.y < resetHeight || transform.position.magnitude > resetDistance ||
            Time.time - _lastResetTime > resetAfterSeconds)
            ResetBall();
    }

    private void OnCollisionEnter(Collision collision)
    {
        var paddle = collision.collider.GetComponentInParent<PaddleKinematicController>();
        if (paddle == null || collision.contactCount == 0)
            return;

        ContactPoint contact = collision.GetContact(0);
        Vector3 normal = contact.normal.normalized;
        Vector3 paddlePointVelocity = paddle.LinearVelocity + Vector3.Cross(
            paddle.AngularVelocity, contact.point - paddle.transform.position);
        Vector3 relative = _incomingVelocity - paddlePointVelocity;
        if (Vector3.Dot(relative, normal) > 0f) normal = -normal;
        Vector3 outgoing = relative - (1f + 0.75f) * Vector3.Dot(relative, normal) * normal +
            paddlePointVelocity * paddleVelocityTransfer;
        _body.linearVelocity = Vector3.ClampMagnitude(outgoing, maximumBallSpeed);
        HitCount++;
        PaddleHit?.Invoke(new PaddleHitInfo {
            Timestamp = Time.time, IncomingVelocity = _incomingVelocity,
            OutgoingVelocity = _body.linearVelocity, PaddleVelocity = paddlePointVelocity
        });
    }

    public void ResetBall()
    {
        if (TryGetComponent<BallTrajectoryTrail>(out var trail)) trail.ResetTrail();
        _body.position = spawnPosition;
        if (_tracker != null && _tracker.IsTracking && _tracker.CurrentFrame.LocalJoints != null)
            _body.position = _tracker.CurrentFrame.RightWristWorldPosition + new Vector3(0, 0.65f, 0);
        _body.rotation = Quaternion.identity;
        _body.linearVelocity = Vector3.zero;
        _body.angularVelocity = Vector3.zero;
        _lastResetTime = Time.time;
    }
}
