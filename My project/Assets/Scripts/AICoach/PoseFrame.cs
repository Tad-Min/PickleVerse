using UnityEngine;

/// <summary>One filtered body/hand sample consumed by the training scene.</summary>
public struct PoseFrame
{
    public const int JointCount = 17;
    public const int RightWristIndex = 10;

    public Vector3[] LocalJoints;
    public float[] Confidence;
    public Vector3 RootPosition;
    public Vector3 HandForward;
    public Vector3 HandUp;
    public float Timestamp;
    public bool BodyTracked;
    public bool HandTracked;
    public bool WristTracked;
    public float WristTimestamp;
    public float HandTimestamp;

    public Vector3 RightWristWorldPosition => RootPosition + LocalJoints[RightWristIndex];
}
