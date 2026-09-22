using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class OfflineCoachValidation
{
    private sealed class Fixture : IDisposable
    {
        public CoachSettings Settings = ScriptableObject.CreateInstance<CoachSettings>();
        public ForehandAnalyzer Analyzer;
        public List<CoachFeedback> Results = new List<CoachFeedback>();
        public Vector3[] Joints = new Vector3[17];
        public float[] Confidence = new float[17];
        public float Time;
        public Fixture() { Analyzer = new ForehandAnalyzer(Settings); Analyzer.Feedback += Results.Add; }
        public void Sample(float wrist = 1.3f, bool straight = false, bool legs = true, bool valid = true, bool mirror = false, float translation = 0, float scale = 1)
        {
            Time += .02f;
            for (int i = 0; i < 17; i++) { Joints[i] = Vector3.zero; Confidence[i] = 1; }
            Joints[5] = new Vector3(-.5f, 1.5f); Joints[6] = new Vector3(.5f, 1.5f);
            Joints[11] = new Vector3(-.35f, 0); Joints[12] = new Vector3(.35f, 0);
            Joints[13] = new Vector3(straight ? -.35f : -.65f, -.6f);
            Joints[14] = new Vector3(straight ? .35f : .65f, -.6f);
            Joints[15] = new Vector3(-.35f, -1.2f); Joints[16] = new Vector3(.35f, -1.2f);
            Joints[10] = new Vector3(wrist, .9f);
            for (int i = 0; i < 17; i++)
            {
                Joints[i] = Joints[i] * scale + new Vector3(translation, translation * .3f, 0);
                if (mirror) Joints[i].x = -Joints[i].x;
            }
            if (!legs) for (int i = 13; i < 17; i++) Confidence[i] = 0;
            if (!valid) Confidence[10] = 0;
            Analyzer.Push(new PoseFrame { ObservationTimestamp = Time, ObservedJoints = Joints,
                ObservedConfidence = Confidence, ObservedWristTracked = valid });
        }
        public void Prepare(bool straight = false, bool legs = true, bool mirror = false, float start = 1.3f)
        { for (int i = 0; i < 30; i++) Sample(start, straight, legs, mirror: mirror); }
        public float Fast(bool straight = false, bool legs = true, bool mirror = false, float start = 1.3f)
        {
            float x = start;
            // Accelerate to a unique final peak, then measure only the follow-through.
            for (int i = 0; i < 8; i++) { x -= .07f + i * .01f; Sample(x, straight, legs, mirror: mirror); }
            return x;
        }
        public void Finish(float x, bool shortFinish = false, bool straight = false, bool legs = true, bool mirror = false)
        {
            if (!shortFinish) for (int i = 0; i < 15; i++) { x -= .04f; Sample(x, straight, legs, mirror: mirror); }
            for (int i = 0; i < 25; i++) Sample(x, straight, legs, mirror: mirror);
        }
        public void Swing(bool straight = false, bool legs = true, bool shortFinish = false, bool mirror = false)
        { Prepare(straight, legs, mirror); float x = Fast(straight, legs, mirror); Finish(x, shortFinish, straight, legs, mirror); }
        public void Dispose() => UnityEngine.Object.DestroyImmediate(Settings);
    }

    [MenuItem("PickleVerse/Coach/Run deterministic validation")]
    public static void RunMenu() => Debug.Log(Run());
    public static string RunRuntime()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Run runtime validation in Play Mode.");
        var coach = UnityEngine.Object.FindAnyObjectByType<OfflineCoach>();
        var panel = UnityEngine.Object.FindAnyObjectByType<CoachPanel>();
        if (!coach || !panel) throw new InvalidOperationException("Coach runtime UI missing.");
        if (panel.gameObject.scene.path != TrainingUiSceneLoader.ScenePath || coach.gameObject.scene == panel.gameObject.scene)
            throw new InvalidOperationException("UI must live in TrainingUI, separate from gameplay.");
        if (UnityEngine.Object.FindObjectsByType<AICoachHUD>().Length != 1 ||
            UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Length != 1)
            throw new InvalidOperationException("Duplicate HUD or EventSystem.");
        var voiceButton = panel.transform.Find("VoiceToggle").GetComponent<UnityEngine.UI.Button>();
        var coachButton = panel.transform.Find("CoachToggle").GetComponent<UnityEngine.UI.Button>();
        var slider = panel.GetComponentInChildren<UnityEngine.UI.Slider>();
        var source = coach.Voice.GetComponent<AudioSource>();
        var testClip = AudioClip.Create("TemporarySilentVoiceControlTest", 24000, 1, 24000, false);
        float oldVolume = coach.Voice.Volume;
        bool wasEnabled = coach.CoachEnabled, wasVoiceEnabled = coach.Voice.VoiceEnabled;
        AudioClip oldClip = source.clip;
        try
        {
            coach.SetCoach(true); coach.Voice.SetVoice(true);
            source.clip = testClip; source.Play();
            voiceButton.onClick.Invoke();
            if (coach.Voice.VoiceEnabled || !coach.CoachEnabled || source.isPlaying)
                throw new InvalidOperationException("Voice toggle must stop speech, not analysis.");
            slider.value = .23f;
            if (Mathf.Abs(source.volume - .23f) > .001f) throw new InvalidOperationException("Volume control not wired.");
            using (var fixture = new Fixture())
            {
                fixture.Analyzer = (ForehandAnalyzer)typeof(OfflineCoach).GetField("analyzer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(coach);
                fixture.Time = UnityEngine.Time.time + .1f;
                fixture.Swing();
                if (!coach.HasFeedback || coach.Latest.Code != CoachCode.Recorded || source.isPlaying)
                    throw new InvalidOperationException("Analysis must produce text while muted.");
            }
            coachButton.onClick.Invoke();
            if (coach.CoachEnabled || coach.HasFeedback || source.isPlaying)
                throw new InvalidOperationException("Coach off must clear analysis and voice.");
            coachButton.onClick.Invoke(); voiceButton.onClick.Invoke();
            if (!coach.CoachEnabled || !coach.Voice.VoiceEnabled) throw new InvalidOperationException("Re-enable failed.");
            coach.GetComponent<SentisPoseTracker>().Recalibrate();
            if (coach.HasFeedback) throw new InvalidOperationException("Recalibrate did not clear results.");
            if (source.spatialBlend != 0 || source.loop || source.playOnAwake)
                throw new InvalidOperationException("Voice must be one non-looping 2D source.");
            return "PASS: runtime Coach/voice buttons, volume, muted analysis, clear/reset, stop speech, 2D source.";
        }
        finally
        {
            source.Stop(); source.clip = oldClip;
            coach.SetCoach(wasEnabled); coach.Voice.SetVoice(wasVoiceEnabled);
            slider.value = oldVolume; coach.ResetSession();
            UnityEngine.Object.Destroy(testClip);
        }
    }
    public static string Run()
    {
        int checks = 0;
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Coach test failed: " + label); checks++; }
        using (var f = new Fixture())
        {
            for (int i = 0; i < 300; i++) f.Sample();
            Check(f.Results.Count == 0, "stationary");
            for (int i = 0; i < 300; i++) f.Sample(translation: i * .2f, scale: 1 + i * .01f);
            Check(f.Results.Count == 0, "walking / camera scale is not a swing");
            Check(f.Analyzer.BufferedSamples <= 151, "three-second window");
        }
        CoachFeedback normal;
        using (var f = new Fixture())
        {
            f.Swing(); Check(f.Results.Count == 1, "one result per swing");
            normal = f.Results[0];
            Check(normal.Code == CoachCode.Recorded, "neutral feedback, bent knees");
            Check(normal.Summary.Forehand && normal.Summary.HitCount == 0, "forehand without contact");
            for (int i = 0; i < 100; i++) f.Sample(.06f);
            Check(f.Results.Count == 1, "no repeated result while still");
        }
        using (var f = new Fixture())
        {
            f.Swing(mirror: true);
            Check(f.Results.Count == 1 && f.Results[0].Code == normal.Code, "mirror invariant");
            Check(Mathf.Abs(f.Results[0].Summary.FollowThrough - normal.Summary.FollowThrough) < .0001f, "mirror geometry");
        }
        using (var f = new Fixture())
        {
            f.Swing(straight: true);
            Check(f.Results.Count == 1 && f.Results[0].Code == CoachCode.StraightKnees, "straight preparation knees");
        }
        using (var f = new Fixture())
        {
            f.Swing(legs: false);
            Check(f.Results.Count == 1 && f.Results[0].Code == CoachCode.Recorded && !f.Results[0].Summary.KneesAvailable, "missing legs skip knee rule");
        }
        using (var f = new Fixture())
        {
            f.Swing(shortFinish: true);
            Check(f.Results.Count == 1 && f.Results[0].Code == CoachCode.ShortFinish, "short finish");
        }
        using (var f = new Fixture())
        {
            f.Swing(straight: true, shortFinish: true);
            Check(f.Results.Count == 1 && f.Results[0].Code == CoachCode.StraightKnees, "knees before short finish");
        }
        using (var f = new Fixture())
        {
            f.Prepare(start: .2f); float x = f.Fast(start: .2f); f.Finish(x);
            Check(f.Results.Count == 1 && f.Results[0].Code == CoachCode.Unknown, "ambiguous movement not guessed");
        }
        using (var f = new Fixture())
        {
            f.Prepare(straight: true); f.Fast(straight: true);
            for (int i = 0; i < 15; i++) f.Sample(valid: false);
            Check(f.Results.Count == 1 && f.Results[0].Code == CoachCode.InsufficientData, "loss > 200 ms, data priority");
            for (int i = 0; i < 50; i++) f.Sample();
            Check(f.Results.Count == 1, "reacquisition no false swing");
            f.Swing(); Check(f.Results.Count == 2, "real swing after recovery");
        }
        using (var f = new Fixture())
        {
            f.Prepare(); f.Fast();
            f.Analyzer.Tick(f.Time + .21f);
            Check(f.Results.Count == 1 && f.Results[0].Code == CoachCode.InsufficientData, "camera unplug without new samples");
        }
        using (var f = new Fixture())
        {
            f.Prepare(); f.Fast();
            for (int i = 0; i < 20; i++) f.Sample(.66f, valid: i % 2 == 0);
            Check(f.Results.Count == 1 && f.Results[0].Code == CoachCode.InsufficientData, "less than 80 percent fresh valid");
        }
        using (var f = new Fixture())
        {
            f.Sample();
            var ring = (SwingSample[])typeof(ForehandAnalyzer).GetField("ring", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(f.Analyzer);
            Vector3 saved = ring[0].Joints[10];
            f.Joints[10] = Vector3.one * 999; f.Confidence[10] = 0;
            Check(ring[0].Joints[10] == saved && ring[0].Confidence[10] == 1, "owns copied buffers");
            for (int i = 0; i < 100; i++) f.Analyzer.Push(new PoseFrame { ObservationTimestamp = f.Time, ObservedJoints = f.Joints, ObservedConfidence = f.Confidence, ObservedWristTracked = true });
            Check(f.Analyzer.BufferedSamples == 1 && f.Results.Count == 0, "duplicate timestamp / held samples ignored");
            f.Prepare(); f.Fast(); f.Analyzer.Reset();
            Check(f.Analyzer.BufferedSamples == 0 && f.Analyzer.Phase == SwingPhase.Preparing, "recalibrate clears history");
        }
        using (var f = new Fixture())
        {
            f.Prepare(); float x = f.Fast();
            f.Analyzer.RegisterHit(new PaddleHitInfo { Timestamp = f.Time, OutgoingVelocity = Vector3.forward * 5 });
            f.Analyzer.RegisterHit(new PaddleHitInfo { Timestamp = -1 });
            f.Finish(x);
            Check(f.Results.Count == 1 && f.Results[0].Summary.HitCount == 1, "hit timestamp association");
            Check(f.Results[0].Summary.LastHit.OutgoingVelocity.z == 5, "contact velocity preserved");
        }
        using (var f = new Fixture())
        {
            f.Prepare();
            // Warm-up first; use stable coordinates to avoid feedback allocations.
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) f.Sample();
            Check(GC.GetAllocatedBytesForCurrentThread() - before == 0, "no per-sample managed allocation");
        }
        using (var f = new Fixture())
        {
            var gate = new CoachVoiceGate(f.Settings);
            var a = new CoachFeedback { Code = CoachCode.Recorded, Timestamp = 0 };
            gate.Offer(a);
            Check(!gate.TryTake(0, true, out _), "no voice overlap");
            Check(gate.TryTake(0, false, out _), "first voice");
            gate.MarkPlayed(a.Code, 0);
            a.Code = CoachCode.ShortFinish; a.Timestamp = 1; gate.Offer(a);
            Check(!gate.TryTake(1, false, out _), "four-second interval");
            Check(gate.TryTake(4, false, out _), "interval boundary"); gate.MarkPlayed(a.Code, 4);
            a.Code = CoachCode.Recorded; a.Timestamp = 8; gate.Offer(a);
            Check(!gate.TryTake(8, false, out _), "same code blocked for twelve seconds");
            Check(!gate.TryTake(12, false, out _), "stale queued feedback discarded");
            a.Timestamp = 12; gate.Offer(a);
            Check(gate.TryTake(12, false, out _), "repeat allowed at twelve seconds");
            gate.Offer(a); gate.Clear();
            Check(!gate.TryTake(12, false, out _), "disable clears pending");
            a.Code = CoachCode.Unknown; gate.Offer(a);
            Check(!gate.TryTake(20, false, out _), "unknown classification text only");
        }
        return "PASS: " + checks + " offline coach assertions.";
    }
}
