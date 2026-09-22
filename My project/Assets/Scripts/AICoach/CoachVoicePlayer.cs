using UnityEngine;

public sealed class CoachVoiceGate
{
    private readonly CoachSettings settings;
    private readonly float[] lastCode = new float[5];
    private float lastStart;
    private CoachFeedback pending;
    private bool hasPending;
    public CoachVoiceGate(CoachSettings settings) { this.settings = settings; Reset(); }
    public void Reset() { Clear(); lastStart = float.NegativeInfinity; for (int i = 0; i < lastCode.Length; i++) lastCode[i] = float.NegativeInfinity; }
    public void Clear() => hasPending = false;
    public void Offer(CoachFeedback feedback) { pending = feedback; hasPending = feedback.Code != CoachCode.Unknown; }
    public bool TryTake(float now, bool playing, out CoachFeedback feedback)
    {
        feedback = default;
        if (!hasPending) return false;
        if (now - pending.Timestamp > settings.VoiceExpiry) { Clear(); return false; }
        if (playing || now - lastStart < Mathf.Max(4, settings.VoiceInterval) ||
            now - lastCode[(int)pending.Code] < Mathf.Max(12, settings.RepeatInterval)) return false;
        feedback = pending; Clear(); return true;
    }
    public void MarkPlayed(CoachCode code, float now) { lastStart = now; lastCode[(int)code] = now; }
}

[RequireComponent(typeof(AudioSource))]
public sealed class CoachVoicePlayer : MonoBehaviour
{
    public AudioClip InsufficientData, StraightKnees, ShortFinish, Recorded;
    private AudioSource source;
    private CoachVoiceGate gate;
    private bool available;
    private readonly bool[] warned = new bool[4];
    public bool VoiceEnabled { get; private set; } = true;
    public bool CoachEnabled { get; private set; } = true;
    public float Volume { get; private set; } = .8f;
    public bool HasAllClips => InsufficientData && StraightKnees && ShortFinish && Recorded;
    public string ResourceStatus => HasAllClips ? "Giọng Việt offline" : "Thiếu clip giọng Việt — chỉ hiện chữ";
    public void Initialize(CoachSettings settings)
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false; source.loop = false; source.spatialBlend = 0;
        source.volume = Volume; gate = new CoachVoiceGate(settings); available = true;
    }
    public void SetVoice(bool enabled) { VoiceEnabled = enabled; if (!enabled) StopAndClear(); }
    public void SetCoach(bool enabled) { CoachEnabled = enabled; if (!enabled) StopAndClear(); }
    public void SetVolume(float volume) { Volume = Mathf.Clamp01(volume); if (source) source.volume = Volume; }
    public void Offer(CoachFeedback feedback) { if (available && VoiceEnabled && CoachEnabled) gate.Offer(feedback); }
    public void StopAndClear() { if (source) source.Stop(); gate?.Clear(); }
    public void ResetSession() { StopAndClear(); gate?.Reset(); }
    private void OnDisable() => StopAndClear();
    private void Update()
    {
        if (!available || !VoiceEnabled || !CoachEnabled || !gate.TryTake(Time.time, source.isPlaying, out var feedback)) return;
        AudioClip clip = Clip(feedback.Code);
        if (!clip)
        {
            int i = (int)feedback.Code;
            if (i < warned.Length && !warned[i]) { warned[i] = true; Debug.LogWarning("Coach: missing offline Vietnamese clip: " + feedback.Code, this); }
            return;
        }
        source.clip = clip; source.Play(); gate.MarkPlayed(feedback.Code, Time.time);
    }
    public AudioClip Clip(CoachCode code)
    {
        switch (code)
        {
            case CoachCode.InsufficientData: return InsufficientData;
            case CoachCode.StraightKnees: return StraightKnees;
            case CoachCode.ShortFinish: return ShortFinish;
            case CoachCode.Recorded: return Recorded;
            default: return null;
        }
    }
}

