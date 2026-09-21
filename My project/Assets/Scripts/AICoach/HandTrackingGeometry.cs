using UnityEngine;

/// <summary>Camera image coordinates are unmirrored, top-left origin; world is Y-up.</summary>
public static class HandTrackingGeometry
{
    public static Rect Crop(Vector2 wrist, Vector2 elbow, int width, int height)
    {
        Vector2 w = Vector2.Scale(wrist, new Vector2(width, height));
        Vector2 e = Vector2.Scale(elbow, new Vector2(width, height));
        Vector2 forearm = w - e;
        float side = Mathf.Clamp(forearm.magnitude * 1.6f, Mathf.Min(width, height) * .16f, Mathf.Min(width, height) * .8f);
        Vector2 center = w + forearm * .25f;
        // Do not shift the crop at image edges: outside pixels are padded with black.
        return new Rect(center.x - side / 2, center.y - side / 2, side, side);
    }

    public static bool TryRotation(Vector3 wrist, Vector3 index, Vector3 middle, Vector3 pinky,
        bool mirror, out Quaternion rotation)
    {
        rotation = Quaternion.identity;
        Vector3 across = index - pinky; // pinky -> index/thumb side: handle-to-head for a closed grip
        Vector3 distal = middle - wrist;
        across = new Vector3(mirror ? -across.x : across.x, -across.y, across.z);
        distal = new Vector3(mirror ? -distal.x : distal.x, -distal.y, distal.z);
        if (!Finite(across) || !Finite(distal) || across.sqrMagnitude < 1e-5f || distal.sqrMagnitude < 1e-5f) return false;
        Vector3 normal = Vector3.Cross(across.normalized, distal.normalized);
        if (normal.sqrMagnitude < .04f) return false;
        // Generated paddle: +Z handle-to-head, +Y face normal. No arbitrary 90-degree correction.
        rotation = Quaternion.LookRotation(across.normalized, normal.normalized);
        return true;
    }

    public static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
}
