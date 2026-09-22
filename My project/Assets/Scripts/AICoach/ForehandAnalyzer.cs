using System;
using UnityEngine;

/// <summary>Pure sequence analysis. No scene, audio, network or per-sample allocation.</summary>
public sealed class ForehandAnalyzer
{
    private readonly CoachSettings settings;
    private readonly SwingSample[] ring = new SwingSample[512];
    private readonly PaddleHitInfo[] hits = new PaddleHitInfo[32];
    private int head, count, hitHead, hitCount;
    private float lastTimestamp = float.NegativeInfinity, lastValid = float.NegativeInfinity;
    private float candidate = -1, slowSince = -1, cooldownUntil, startX, minimumX, peak, path;
    private Vector2 previous;
    private bool hasPrevious, kneeAvailable, straightKnees, startsOutside;
    private int total, valid;
    public SwingPhase Phase { get; private set; }
    public int BufferedSamples => count;
    public event Action<CoachFeedback> Feedback;

    public ForehandAnalyzer(CoachSettings settings)
    {
        this.settings = settings;
        for (int i = 0; i < ring.Length; i++) ring[i] = new SwingSample();
    }

    public void Reset()
    {
        head = count = hitHead = hitCount = 0;
        lastTimestamp = lastValid = float.NegativeInfinity;
        candidate = slowSince = -1;
        cooldownUntil = 0; hasPrevious = false;
        Phase = SwingPhase.Preparing;
    }

    public void RegisterHit(PaddleHitInfo hit)
    {
        if (!float.IsFinite(hit.Timestamp)) return;
        hits[hitHead] = hit; hitHead = (hitHead + 1) % hits.Length;
        hitCount = Mathf.Min(hitCount + 1, hits.Length);
    }

    public void Tick(float now)
    {
        if (candidate >= 0 && now - lastValid > settings.MaximumTrackingGap)
            Complete(now, true, "Tracking gap exceeded 200 ms limit.");
        if (Phase == SwingPhase.Cooldown && now >= cooldownUntil) Phase = SwingPhase.Preparing;
    }

    public void Push(PoseFrame frame)
    {
        float now = frame.ObservationTimestamp;
        if (!float.IsFinite(now) || now <= lastTimestamp) return; // held / duplicate observation
        Tick(now);
        lastTimestamp = now;
        while (count > 0 && now - ring[(head - count + ring.Length) % ring.Length].Timestamp > 3f) count--;
        SwingSample sample = ring[head];
        head = (head + 1) % ring.Length; count = Mathf.Min(count + 1, ring.Length);
        sample.Timestamp = now;
        sample.WristTracked = frame.ObservedWristTracked;
        for (int i = 0; i < 17; i++)
        {
            sample.Joints[i] = frame.ObservedJoints != null && frame.ObservedJoints.Length > i ? frame.ObservedJoints[i] : Vector3.zero;
            float confidence = frame.ObservedConfidence != null && frame.ObservedConfidence.Length > i ? frame.ObservedConfidence[i] : 0;
            sample.Confidence[i] = float.IsFinite(confidence) && Finite(sample.Joints[i]) ? confidence : 0;
        }
        sample.Valid = sample.WristTracked && Has(sample, 5) && Has(sample, 6) &&
            Has(sample, 10) && Has(sample, 11) && Has(sample, 12);
        sample.LegsValid = false;
        Vector3 right = sample.Joints[6] - sample.Joints[5];
        float width = right.magnitude;
        Vector3 hip = (sample.Joints[11] + sample.Joints[12]) * .5f;
        Vector3 up = (sample.Joints[5] + sample.Joints[6]) * .5f - hip;
        if (width > .0001f) { right /= width; up -= right * Vector3.Dot(up, right); }
        sample.Valid &= width > .0001f && up.sqrMagnitude > .000001f;
        if (!sample.Valid)
        {
            if (candidate >= 0) total++;
            hasPrevious = false;
            return;
        }
        Vector3 relative = (sample.Joints[10] - hip) / width;
        sample.Wrist = new Vector2(Vector3.Dot(relative, right), Vector3.Dot(relative, up.normalized));
        sample.OutsideRightShoulder = Vector3.Dot(sample.Joints[10] - sample.Joints[6], right) / width;
        sample.LegsValid = Has(sample, 13) && Has(sample, 14) && Has(sample, 15) && Has(sample, 16) &&
            TryKnee(sample, 11, 13, 15, out sample.LeftKnee) && TryKnee(sample, 12, 14, 16, out sample.RightKnee);
        float dt = now - lastValid;
        float distance = hasPrevious ? Vector2.Distance(previous, sample.Wrist) : 0;
        float speed = hasPrevious && dt > .0001f && dt <= settings.MaximumTrackingGap ? distance / dt : 0;
        previous = sample.Wrist; hasPrevious = true; lastValid = now;
        if (Phase == SwingPhase.Cooldown) return;
        if (candidate < 0)
        {
            if (speed <= settings.StartSpeed) return;
            candidate = now; startX = sample.Wrist.x; minimumX = startX;
            startsOutside = sample.OutsideRightShoulder > settings.OutsideShoulderMargin;
            // The immediately preceding sample is the actual start of movement.
            if (count > 1)
            {
                var before = ring[(head - 2 + ring.Length) % ring.Length];
                if (before.Valid && now - before.Timestamp <= settings.MaximumTrackingGap)
                {
                    startX = before.Wrist.x;
                    startsOutside = before.OutsideRightShoulder > settings.OutsideShoulderMargin;
                }
            }
            peak = speed; path = 0; total = valid = 1; slowSince = -1;
            InspectPreparation(now);
            return;
        }
        total++; valid++;
        minimumX = Mathf.Min(minimumX, sample.Wrist.x);
        if (Phase == SwingPhase.Preparing)
        {
            if (speed <= settings.StartSpeed) { candidate = -1; return; }
            if (now - candidate + .00001f >= settings.StartDuration) Phase = SwingPhase.Swinging;
        }
        if (speed > peak) { peak = speed; path = 0; } else path += distance;
        if (speed < settings.EndSpeed) { if (slowSince < 0) slowSince = now; } else slowSince = -1;
        if (Phase == SwingPhase.Swinging && ((slowSince >= 0 && now - slowSince >= settings.EndDuration) ||
            now - candidate >= settings.MaximumSwingSeconds)) Complete(now, false, null);
    }

    private void InspectPreparation(float now)
    {
        int samples = 0, legs = 0, straight = 0;
        for (int i = 1; i < count; i++)
        {
            SwingSample s = ring[(head - 1 - i + ring.Length) % ring.Length];
            if (now - s.Timestamp > settings.PreparationSeconds) break;
            samples++;
            if (!s.Valid || !s.LegsValid) continue;
            legs++;
            if (s.LeftKnee > settings.StraightKneeAngle && s.RightKnee > settings.StraightKneeAngle) straight++;
        }
        kneeAvailable = samples >= 3 && legs >= samples * settings.MinimumValidRatio;
        straightKnees = kneeAvailable && straight >= legs * settings.StraightKneeRatio;
    }

    private void Complete(float now, bool insufficient, string reason)
    {
        var summary = new SwingSummary {
            StartTime = candidate, EndTime = now, SampleCount = total, ValidSamples = valid,
            ValidRatio = total > 0 ? (float)valid / total : 0, PeakSpeed = peak, FollowThrough = path,
            Forehand = startsOutside && startX - minimumX >= settings.MinimumInwardTravel,
            KneesAvailable = kneeAvailable, StraightKnees = straightKnees
        };
        for (int i = 0; i < hitCount; i++)
        {
            var hit = hits[(hitHead - hitCount + i + hits.Length) % hits.Length];
            if (hit.Timestamp >= candidate && hit.Timestamp <= now) { summary.HitCount++; summary.LastHit = hit; }
        }
        CoachCode code = insufficient || summary.ValidRatio < settings.MinimumValidRatio ? CoachCode.InsufficientData :
            !summary.Forehand ? CoachCode.Unknown : straightKnees ? CoachCode.StraightKnees :
            path < settings.MinimumFollowThrough ? CoachCode.ShortFinish : CoachCode.Recorded;
        var feedback = new CoachFeedback { Code = code, Summary = summary, Quality = summary.ValidRatio,
            Timestamp = now, Reason = reason ?? (code == CoachCode.Unknown ? "No clear outside-right-to-centre sequence." :
            code == CoachCode.StraightKnees ? "Both observed knees nearly straight during preparation." :
            code == CoachCode.ShortFinish ? "Observed wrist path after peak speed below demo threshold." :
            code == CoachCode.InsufficientData ? "Too few fresh valid observations." : "No selected demo warning detected; not a technique score.") };
        candidate = slowSince = -1;
        Phase = SwingPhase.Cooldown; cooldownUntil = now + settings.CooldownSeconds;
        Feedback?.Invoke(feedback);
    }

    private bool Has(SwingSample s, int i) => s.Confidence[i] >= settings.MinimumConfidence;
    private static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    private static bool TryKnee(SwingSample s, int h, int k, int a, out float angle)
    {
        Vector3 u = s.Joints[h] - s.Joints[k], v = s.Joints[a] - s.Joints[k];
        angle = 0;
        if (u.sqrMagnitude < .000001f || v.sqrMagnitude < .000001f) return false;
        angle = Vector3.Angle(u, v); return true;
    }
}
