using UnityEngine;

[RequireComponent(typeof(TrailRenderer))]
public class BallTrajectoryTrail : MonoBehaviour
{
    private TrailRenderer _trail;
    private int _resumeFrame;

    private void OnEnable()
    {
        _trail = GetComponent<TrailRenderer>();
        ResetTrail();
    }

    public void ResetTrail()
    {
        if (_trail == null) _trail = GetComponent<TrailRenderer>();
        _trail.emitting = false;
        _trail.Clear();
        // Let the interpolated Rigidbody reach its reset pose before emitting again.
        _resumeFrame = Time.frameCount + 2;
    }

    private void LateUpdate()
    {
        if (_trail.emitting) return;
        _trail.Clear();
        if (Time.frameCount >= _resumeFrame) _trail.emitting = true;
    }

    private void OnDisable()
    {
        if (_trail == null) return;
        _trail.emitting = false;
        _trail.Clear();
    }
}
