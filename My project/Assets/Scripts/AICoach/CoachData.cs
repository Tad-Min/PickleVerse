using UnityEngine;

public enum CoachCode { InsufficientData, StraightKnees, ShortFinish, Recorded, Unknown }
public enum SwingPhase { Preparing, Swinging, Cooldown }

// Slots are allocated once and COPY tracker buffers; never retain borrowed arrays.
public sealed class SwingSample
{
    public readonly Vector3[] Joints = new Vector3[17];
    public readonly float[] Confidence = new float[17];
    public float Timestamp;
    public bool WristTracked, Valid, LegsValid;
    public Vector2 Wrist;
    public float OutsideRightShoulder;
    public float LeftKnee, RightKnee;
}

public struct PaddleHitInfo
{
    public float Timestamp;
    public Vector3 IncomingVelocity, OutgoingVelocity, PaddleVelocity;
}

public struct SwingSummary
{
    public float StartTime, EndTime, ValidRatio, PeakSpeed, FollowThrough;
    public int SampleCount, ValidSamples, HitCount;
    public bool Forehand, KneesAvailable, StraightKnees;
    public PaddleHitInfo LastHit;
}

public struct CoachFeedback
{
    public CoachCode Code;
    public string Reason;
    public float Quality, Timestamp;
    public SwingSummary Summary;
    public string Text => Message(Code);
    public static string Message(CoachCode code)
    {
        switch (code)
        {
            case CoachCode.InsufficientData: return "Chưa thấy rõ động tác. Hãy giữ vai, hông và tay trong khung hình.";
            case CoachCode.StraightKnees: return "Thử chùng gối nhẹ ở tư thế chuẩn bị.";
            case CoachCode.ShortFinish: return "Thử hoàn tất động tác vung vợt.";
            case CoachCode.Recorded: return "Đã ghi nhận cú đánh. Tiếp tục giữ nhịp nhé.";
            default: return "Chưa xác định được forehand. Hãy thử lại động tác tay phải.";
        }
    }
}
