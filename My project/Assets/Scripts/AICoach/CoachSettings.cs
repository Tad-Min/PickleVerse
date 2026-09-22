using UnityEngine;

[CreateAssetMenu(menuName = "PickleVerse/Offline Coach Settings")]
public sealed class CoachSettings : ScriptableObject
{
    [Header("Demo thresholds - not validated coaching standards")]
    [Range(.01f, 1f)] public float MinimumConfidence = .3f;
    [Min(.01f)] public float StartSpeed = 2.5f;
    [Min(.01f)] public float StartDuration = .1f;
    [Min(.01f)] public float EndSpeed = .8f;
    [Min(.01f)] public float EndDuration = .25f;
    [Range(.2f, 2f)] public float MaximumSwingSeconds = 2f;
    [Min(0)] public float CooldownSeconds = .6f;
    [Range(.01f, .2f)] public float MaximumTrackingGap = .2f;
    [Range(.5f, 1f)] public float MinimumValidRatio = .8f;
    [Range(.1f, .8f)] public float PreparationSeconds = .4f;
    public float OutsideShoulderMargin = .05f;
    public float MinimumInwardTravel = .15f;
    [Range(90, 180)] public float StraightKneeAngle = 170f;
    [Range(.5f, 1)] public float StraightKneeRatio = .8f;
    [Min(0)] public float MinimumFollowThrough = .2f;
    [Header("Offline voice")]
    [Min(4)] public float VoiceInterval = 4f;
    [Min(12)] public float RepeatInterval = 12f;
    [Range(.1f, 3)] public float VoiceExpiry = 3f;
}

