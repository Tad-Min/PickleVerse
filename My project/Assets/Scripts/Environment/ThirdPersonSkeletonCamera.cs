using UnityEngine;

/// <summary>Follows the body root in court axes, independent of wrist/paddle rotation.</summary>
[RequireComponent(typeof(Camera))]
public class ThirdPersonSkeletonCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 2f, -4f);
    [SerializeField] private Vector3 lookOffset = new Vector3(0f, .5f, 1f);
    [Min(.01f)] [SerializeField] private float smoothing = 6f;
    [SerializeField] private float minimumHeight = .5f;

    private void LateUpdate()
    {
        if (target == null) return;
        float blend = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        Vector3 desired = target.position + followOffset;
        desired.y = Mathf.Max(minimumHeight, desired.y);
        transform.position = Vector3.Lerp(transform.position, desired, blend);
        Vector3 direction = target.position + lookOffset - transform.position;
        if (direction.sqrMagnitude > .0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(direction, Vector3.up), blend);
    }
}
