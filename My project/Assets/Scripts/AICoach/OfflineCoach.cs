using UnityEngine;

[RequireComponent(typeof(SentisPoseTracker), typeof(CoachVoicePlayer))]
public sealed class OfflineCoach : MonoBehaviour
{
    [SerializeField] private CoachSettings settings;
    [SerializeField] private bool coachEnabled = true;
    private SentisPoseTracker tracker;
    private BallContactController ball;
    private ForehandAnalyzer analyzer;
    private CoachSettings transientSettings;
    public CoachVoicePlayer Voice { get; private set; }
    public CoachFeedback Latest { get; private set; }
    public bool HasFeedback { get; private set; }
    public bool CoachEnabled => coachEnabled;
    public SwingPhase Phase => analyzer == null ? SwingPhase.Preparing : analyzer.Phase;
    public string Status => !coachEnabled ? "Coach đã tắt" : Phase == SwingPhase.Swinging ? "Đang phân tích cú vung" :
        Phase == SwingPhase.Cooldown ? "Nghỉ giữa hai cú" : "Chuẩn bị forehand tay phải";
    private void Awake()
    {
        tracker = GetComponent<SentisPoseTracker>();
        Voice = GetComponent<CoachVoicePlayer>();
        if (!settings) { transientSettings = ScriptableObject.CreateInstance<CoachSettings>(); settings = transientSettings; }
        analyzer = new ForehandAnalyzer(settings);
        analyzer.Feedback += OnFeedback;
        Voice.Initialize(settings); Voice.SetCoach(coachEnabled);
    }
    private void OnEnable()
    {
        tracker.PoseSampled += OnSample;
        tracker.SessionReset += ResetSession;
        ball = FindAnyObjectByType<BallContactController>();
        if (ball) ball.PaddleHit += OnHit;
    }
    private void OnDisable()
    {
        tracker.PoseSampled -= OnSample;
        tracker.SessionReset -= ResetSession;
        if (ball) ball.PaddleHit -= OnHit;
        ResetSession();
    }
    private void OnDestroy() { if (transientSettings) Destroy(transientSettings); }
    private void Update() { if (coachEnabled) analyzer.Tick(Time.time); }
    private void OnSample(PoseFrame frame) { if (coachEnabled) analyzer.Push(frame); }
    private void OnHit(PaddleHitInfo hit) { if (coachEnabled) analyzer.RegisterHit(hit); }
    private void OnFeedback(CoachFeedback feedback) { Latest = feedback; HasFeedback = true; Voice.Offer(feedback); }
    public void SetCoach(bool enabled) { coachEnabled = enabled; Voice.SetCoach(enabled); ResetSession(); }
    public void ResetSession() { analyzer?.Reset(); HasFeedback = false; Latest = default; Voice?.ResetSession(); }
}
