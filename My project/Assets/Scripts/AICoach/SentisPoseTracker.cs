using Unity.Collections;
using Unity.InferenceEngine;
using UnityEngine;

/// <summary>Local webcam pose and hand tracker for the offline training scene.</summary>
public class SentisPoseTracker : MonoBehaviour
{
    [Header("Models")]
    public WebCamTexture WebCam;
    public ModelAsset ModelAsset;
    public ModelAsset HandModelAsset;

    [Header("Camera")]
    [SerializeField] private bool autoStartWebcam = true;
    [SerializeField] private int requestedCameraWidth = 640;
    [SerializeField] private int requestedCameraHeight = 480;
    [SerializeField] private int requestedCameraFps = 30;
    [SerializeField] private bool mirrorCamera = true;

    [Header("Tracking")]
    [Range(0f, 1f)] [SerializeField] private float bodyConfidenceThreshold = 0.3f;
    [Range(0f, 1f)] [SerializeField] private float handPresenceThreshold = 0.5f;
    [SerializeField] private string handPresenceOutput = "Identity_1";
    [SerializeField] private string handLandmarksOutput = "Identity";
    [SerializeField] private float lostTrackingGraceSeconds = 0.25f;
    [SerializeField] private float bodySmoothing = 18f;
    [SerializeField] private float bodyScale = 2.6f;
    [SerializeField] private float handRotationSmoothing = 18f;
    [SerializeField] private float maximumHandAngularSpeed = 720f;

    [Header("Scene References")]
    [SerializeField] private CalibrationProfile calibration;
    [SerializeField] private SkeletonPresenter skeletonPresenter;
    [SerializeField] private PaddleKinematicController paddle;

    [Header("Tracking Data Output")]
    public float CurrentWristAngle;
    public float CurrentKneeBend;
    public float SwingSpeed;
    public bool IsTracking { get; private set; }
    public bool IsHandTracking { get; private set; }
    public string Status { get; private set; } = "Starting";
    public PoseFrame CurrentFrame { get; private set; }
    public bool MirrorCamera => mirrorCamera;
    public float HandPresence { get; private set; }
    public event System.Action<PoseFrame> PoseSampled;
    public event System.Action SessionReset;

    private Worker _bodyWorker;
    private Worker _handWorker;
    private RenderTexture _bodyRenderTexture, _cameraRenderTexture;
    private Texture2D _bodyTexture, _cameraTexture;
    private Quaternion _lastHandRotation = Quaternion.identity;
    private bool _hasHandRotation;
    private float _lastHandTime = float.NegativeInfinity;
    private float _lastWristTime = float.NegativeInfinity;
    private bool _freshBody;
    private int[] _bodyInput;
    private float[] _handInput;
    private readonly Vector3[] _filteredLocalJoints = new Vector3[PoseFrame.JointCount];
    private readonly float[] _confidence = new float[PoseFrame.JointCount];
    private readonly float[] _observedConfidence = new float[PoseFrame.JointCount];
    private readonly Vector3[] _observedJoints = new Vector3[PoseFrame.JointCount];
    private readonly Vector3[] _lastValidLocalJoints = new Vector3[PoseFrame.JointCount];
    private float _lastValidBodyTime = float.NegativeInfinity;
    private Vector3 _previousWristPosition;
    private float _previousWristTime;
    private bool _ownsWebcam;
    private readonly Vector3[] _unscaled = new Vector3[PoseFrame.JointCount];
    private readonly float[] _lastJointTime = new float[PoseFrame.JointCount];
    private float _lastCameraFrameTime;
    private float _sampleDelta;

    private void Start()
    {
        calibration ??= GetComponent<CalibrationProfile>();
        if (calibration == null) calibration = gameObject.AddComponent<CalibrationProfile>();
        skeletonPresenter ??= GetComponent<SkeletonPresenter>();
        if (skeletonPresenter == null) skeletonPresenter = gameObject.AddComponent<SkeletonPresenter>();
        paddle ??= FindFirstObjectByType<PaddleKinematicController>();
        LoadDefaultModelsInEditor();
        InitializeModels();
        if (autoStartWebcam) StartWebcam();
        calibration.BeginCalibration();
        _previousWristTime = 0f;
    }

    public void StartWebcam()
    {
        if (WebCam != null && WebCam.isPlaying) return;
        var devices = WebCamTexture.devices;
        if (devices.Length == 0) { Status = "No webcam detected"; return; }
        WebCam = new WebCamTexture(devices[0].name, requestedCameraWidth, requestedCameraHeight, requestedCameraFps);
        WebCam.Play();
        _ownsWebcam = true;
        Status = "Waiting for webcam frames";
    }

    public void Recalibrate()
    {
        SessionReset?.Invoke();
        calibration.BeginCalibration();
        Status = "Calibration: stand facing the camera";
    }

    public void InitializeModel(ModelAsset asset)
    {
        ModelAsset = asset;
        InitializeModels();
    }

    private void InitializeModels()
    {
        DisposeWorkers();
        if (ModelAsset == null) { Status = "MoveNet model is not assigned"; return; }
        try
        {
            _bodyWorker = new Worker(ModelLoader.Load(ModelAsset), BackendType.CPU);
            if (HandModelAsset != null)
            {
                var handModel = ModelLoader.Load(HandModelAsset);
                bool hasLandmarks = false, hasPresence = false;
                foreach (var output in handModel.outputs)
                {
                    hasLandmarks |= output.name == handLandmarksOutput;
                    hasPresence |= output.name == handPresenceOutput;
                }
                if (!hasLandmarks || !hasPresence) throw new System.InvalidOperationException("Hand model output names do not match its configured contract");
                _handWorker = new Worker(handModel, BackendType.CPU);
            }
            Status = "Ready - calibrate in front of camera";
        }
        catch (System.Exception exception)
        {
            Status = $"Model initialization failed: {exception.Message}";
            Debug.LogError(Status, this);
        }
    }

    private void Update()
    {
        if (_bodyWorker == null) return;
        if (WebCam == null || !WebCam.isPlaying || WebCam.width <= 16)
        {
            InvalidateSample();
            IsTracking = IsHandTracking = false;
            Status = "Camera unavailable or waiting for frames";
            return;
        }
        if (!WebCam.didUpdateThisFrame)
        {
            if (Time.time - _lastCameraFrameTime > 1f)
            {
                InvalidateSample();
                IsTracking = IsHandTracking = false;
                Status = "Camera frames stopped";
            }
            return;
        }
        InvalidateSample();
        _sampleDelta = Mathf.Clamp(Time.time - _lastCameraFrameTime, 0.001f, 0.1f);
        _lastCameraFrameTime = Time.time;
        EnsureTextures();
        if (!TryRunBodyModel(out float[] output)) { IsTracking = IsHandTracking = false; return; }
        BuildBodyFrame(output);
        TryUpdateHandFrame(output);
        var observation = CurrentFrame;
        observation.ObservedJoints = _observedJoints;
        observation.ObservedConfidence = _observedConfidence;
        observation.ObservationTimestamp = Time.time;
        observation.ObservedWristTracked = _observedConfidence[10] >= bodyConfidenceThreshold;
        CurrentFrame = observation;
        PoseSampled?.Invoke(observation);
        if (IsTracking) skeletonPresenter.Present(CurrentFrame);
        if (paddle != null) paddle.SetTrackingTarget(CurrentFrame, Time.time);
    }

    private void InvalidateSample()
    {
        _freshBody = false;
        IsHandTracking = false;
        PoseFrame frame = CurrentFrame;
        frame.WristTracked = frame.HandTracked = false;
        CurrentFrame = frame;
    }

    private void EnsureTextures()
    {
        if (_bodyRenderTexture == null) _bodyRenderTexture = new RenderTexture(192, 192, 0, RenderTextureFormat.ARGB32);
        if (_cameraRenderTexture == null || _cameraRenderTexture.width != WebCam.width || _cameraRenderTexture.height != WebCam.height)
        {
            ReleaseTexture(ref _cameraRenderTexture);
            Destroy(_cameraTexture);
            _cameraRenderTexture = new RenderTexture(WebCam.width, WebCam.height, 0, RenderTextureFormat.ARGB32);
            _cameraTexture = new Texture2D(WebCam.width, WebCam.height, TextureFormat.RGBA32, false);
        }
        if (_bodyTexture == null) _bodyTexture = new Texture2D(192, 192, TextureFormat.RGBA32, false);
        if (_bodyInput == null) _bodyInput = new int[192 * 192 * 3];
        if (_handInput == null) _handInput = new float[224 * 224 * 3];
    }

    private bool TryRunBodyModel(out float[] output)
    {
        output = null;
        Graphics.Blit(WebCam, _cameraRenderTexture);
        Graphics.Blit(_cameraRenderTexture, _bodyRenderTexture);
        CopyRenderTextureToTexture(_bodyRenderTexture, _bodyTexture);
        NativeArray<Color32> pixels = _bodyTexture.GetRawTextureData<Color32>();
        for (int y = 0; y < 192; y++)
        for (int x = 0; x < 192; x++)
        {
            int sourceY = 191 - y;
            int sourceX = x;
            Color32 color = pixels[sourceY * 192 + sourceX];
            int index = (y * 192 + x) * 3;
            _bodyInput[index] = color.r;
            _bodyInput[index + 1] = color.g;
            _bodyInput[index + 2] = color.b;
        }
        try
        {
            using var input = new Tensor<int>(new TensorShape(1, 192, 192, 3), _bodyInput);
            _bodyWorker.Schedule(input);
            var tensor = _bodyWorker.PeekOutput() as Tensor<float>;
            if (tensor == null) { Status = "MoveNet output format is invalid"; return false; }
            using var cpuTensor = tensor.ReadbackAndClone();
            output = cpuTensor.DownloadToArray();
            return output.Length >= PoseFrame.JointCount * 3;
        }
        catch (System.Exception exception) { Status = $"Pose tracking failed: {exception.Message}"; return false; }
    }

    private void BuildBodyFrame(float[] output)
    {
        int validCount = 0;
        var unscaled = _unscaled;
        for (int i = 0; i < PoseFrame.JointCount; i++)
        {
            float score = output[i * 3 + 2];
            _observedConfidence[i] = float.IsFinite(score) && float.IsFinite(output[i * 3]) && float.IsFinite(output[i * 3 + 1]) ? Mathf.Clamp01(score) : 0f;
            float imageX = output[i * 3 + 1] - 0.5f;
            Vector3 observed = new Vector3(mirrorCamera ? -imageX : imageX, 0.5f - output[i * 3], 0f);
            // Restore source image aspect ratio for geometric angles and distances.
            _observedJoints[i] = _observedConfidence[i] > 0 ? new Vector3(observed.x * WebCam.width / WebCam.height, observed.y, 0) : Vector3.zero;
            if (_observedConfidence[i] < bodyConfidenceThreshold) continue;
            unscaled[i] = observed;
            validCount++;
        }
        if (validCount == 0 || _observedConfidence[11] < bodyConfidenceThreshold || _observedConfidence[12] < bodyConfidenceThreshold || _observedConfidence[5] < bodyConfidenceThreshold || _observedConfidence[6] < bodyConfidenceThreshold)
        {
            KeepLastFrameWhenTrackingIsBrieflyLost();
            return;
        }
        Vector2 hipCenter = (new Vector2(unscaled[11].x, unscaled[11].y) + new Vector2(unscaled[12].x, unscaled[12].y)) * 0.5f;
        float shoulderWidth = Vector3.Distance(unscaled[5], unscaled[6]);
        if (shoulderWidth <= 0.0001f) { KeepLastFrameWhenTrackingIsBrieflyLost(); return; }
        calibration.ProcessSample(hipCenter, shoulderWidth, _sampleDelta);
        Vector3 root = calibration.MapRootPosition(hipCenter, shoulderWidth);
        Vector3 hipOrigin = new Vector3(hipCenter.x, hipCenter.y, 0f);
        float blend = 1f - Mathf.Exp(-bodySmoothing * _sampleDelta);
        if (CurrentFrame.LocalJoints != null) root = Vector3.Lerp(CurrentFrame.RootPosition, root, blend);
        for (int i = 0; i < PoseFrame.JointCount; i++)
        {
            _confidence[i] = _observedConfidence[i] >= bodyConfidenceThreshold ? _observedConfidence[i] : 0f;
            if (_confidence[i] > 0f)
            {
                Vector3 target = (unscaled[i] - hipOrigin) * bodyScale;
                _filteredLocalJoints[i] = Vector3.Lerp(_filteredLocalJoints[i], target, blend);
                _lastValidLocalJoints[i] = _filteredLocalJoints[i];
                _lastJointTime[i] = Time.time;
            }
            else
            {
                _filteredLocalJoints[i] = _lastValidLocalJoints[i];
                if (_lastJointTime[i] > 0f && Time.time - _lastJointTime[i] <= lostTrackingGraceSeconds)
                    _confidence[i] = 0.01f;
            }
        }
        CurrentFrame = new PoseFrame
        {
            LocalJoints = _filteredLocalJoints, Confidence = _confidence,
            RootPosition = root, Timestamp = Time.time, BodyTracked = validCount >= 8,
            HandTracked = false, HandForward = _lastHandRotation * Vector3.forward, HandUp = _lastHandRotation * Vector3.up,
            WristTracked = validCount >= 8 && output[32] >= bodyConfidenceThreshold && _confidence[10] > .01f,
            HandTimestamp = _lastHandTime, WristTimestamp = _lastWristTime
        };
        _freshBody = validCount >= 8;
        if (CurrentFrame.WristTracked)
        {
            _lastWristTime = Time.time;
            var frame = CurrentFrame;
            frame.WristTimestamp = _lastWristTime;
            CurrentFrame = frame;
        }
        IsTracking = CurrentFrame.BodyTracked;
        _lastValidBodyTime = Time.time;
        Status = calibration.IsCalibrated ? "Tracking" : $"Calibrating {calibration.CalibrationProgress:P0}";
        UpdateMetrics();
    }

    private void KeepLastFrameWhenTrackingIsBrieflyLost()
    {
        IsTracking = Time.time - _lastValidBodyTime <= lostTrackingGraceSeconds;
        IsHandTracking = false;
        Status = IsTracking ? "Tracking temporarily lost" : "Move into camera view";
    }

    private void TryUpdateHandFrame(float[] bodyOutput)
    {
        IsHandTracking = false;
        HandPresence = 0f;
        if (!_freshBody || !CurrentFrame.WristTracked || _handWorker == null || !float.IsFinite(bodyOutput[26]) || bodyOutput[26] < bodyConfidenceThreshold ||
            !float.IsFinite(bodyOutput[24]) || !float.IsFinite(bodyOutput[25])) return;
        Vector2 wrist = new Vector2(bodyOutput[31], bodyOutput[30]);
        Vector2 elbow = new Vector2(bodyOutput[25], bodyOutput[24]);
        Rect crop = HandTrackingGeometry.Crop(wrist, elbow, _cameraTexture.width, _cameraTexture.height);
        CopyRenderTextureToTexture(_cameraRenderTexture, _cameraTexture);
        NativeArray<Color32> pixels = _cameraTexture.GetRawTextureData<Color32>();
        for (int y = 0; y < 224; y++)
        for (int x = 0; x < 224; x++)
        {
            int sourceX = Mathf.FloorToInt(crop.x + (x + .5f) * crop.width / 224);
            int sourceY = _cameraTexture.height - 1 - Mathf.FloorToInt(crop.y + (y + .5f) * crop.height / 224);
            Color32 color = sourceX >= 0 && sourceX < _cameraTexture.width && sourceY >= 0 && sourceY < _cameraTexture.height
                ? pixels[sourceY * _cameraTexture.width + sourceX] : new Color32(0, 0, 0, 255);
            int index = y * 224 + x;
            _handInput[index] = color.r / 255f;
            _handInput[224 * 224 + index] = color.g / 255f;
            _handInput[2 * 224 * 224 + index] = color.b / 255f;
        }
        try
        {
            using var input = new Tensor<float>(new TensorShape(1, 3, 224, 224), _handInput);
            _handWorker.Schedule(input);
            // This model exports landmarks, presence, then handedness. Never interpret
            // handedness as detection confidence or use landmarks from an empty crop.
            var presence = _handWorker.PeekOutput(handPresenceOutput) as Tensor<float>;
            if (presence == null || presence.shape.length != 1) return;
            using (var cpuPresence = presence.ReadbackAndClone())
            {
                float probability = cpuPresence.DownloadToArray()[0];
                HandPresence = probability;
                if (!float.IsFinite(probability) || probability < handPresenceThreshold) return;
            }
            {
                var tensor = _handWorker.PeekOutput(handLandmarksOutput) as Tensor<float>;
                if (tensor == null || tensor.shape.length != 63) { Status = "Hand landmarks must contain 21 XYZ points"; return; }
                using var cpuTensor = tensor.ReadbackAndClone();
                float[] data = cpuTensor.DownloadToArray();
                if (data.Length >= 63) ApplyHandRotation(data);
            }
        }
        catch (System.Exception exception) { Status = $"Hand tracking unavailable: {exception.Message}"; }
    }

    private void ApplyHandRotation(float[] data)
    {
        Vector3 wrist = ReadHandPoint(data, 0), index = ReadHandPoint(data, 5), middle = ReadHandPoint(data, 9), pinky = ReadHandPoint(data, 17);
        if (!HandTrackingGeometry.TryRotation(wrist, index, middle, pinky, mirrorCamera, out Quaternion rotation)) return;
        if (_hasHandRotation)
        {
            // Time since the camera sample, not since last detection: reacquisition cannot jump.
            rotation = Quaternion.RotateTowards(_lastHandRotation, rotation, maximumHandAngularSpeed * _sampleDelta);
            rotation = Quaternion.Slerp(_lastHandRotation, rotation, 1f - Mathf.Exp(-handRotationSmoothing * _sampleDelta));
        }
        _lastHandRotation = rotation;
        _hasHandRotation = true;
        _lastHandTime = Time.time;
        PoseFrame frame = CurrentFrame;
        frame.HandForward = rotation * Vector3.forward;
        frame.HandUp = rotation * Vector3.up;
        frame.HandTimestamp = _lastHandTime;
        frame.HandTracked = true;
        CurrentFrame = frame;
        IsHandTracking = true;
    }

    private Vector3 ReadHandPoint(float[] data, int landmark)
    {
        int i = landmark * 3;
        const float divisor = 224f;
        return new Vector3(data[i], data[i + 1], data[i + 2]) / divisor;
    }

    private void UpdateMetrics()
    {
        if (!CurrentFrame.WristTracked) { _previousWristTime = 0; SwingSpeed = 0; return; }
        Vector3 wrist = CurrentFrame.RightWristWorldPosition;
        if (_previousWristTime > 0f && Time.time - _previousWristTime <= lostTrackingGraceSeconds) SwingSpeed = Vector3.Distance(wrist, _previousWristPosition) / Mathf.Max(.0001f, Time.time - _previousWristTime);
        else SwingSpeed = 0;
        _previousWristPosition = wrist;
        _previousWristTime = Time.time;
        CurrentWristAngle = Vector3.Angle(CurrentFrame.LocalJoints[8] - CurrentFrame.LocalJoints[10], Vector3.up);
        CurrentKneeBend = Vector3.Angle(CurrentFrame.LocalJoints[12] - CurrentFrame.LocalJoints[14], CurrentFrame.LocalJoints[16] - CurrentFrame.LocalJoints[14]);
    }

    private static void CopyRenderTextureToTexture(RenderTexture source, Texture2D destination)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = source;
        destination.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
        destination.Apply(false, false);
        RenderTexture.active = previous;
    }

    private void OnDestroy()
    {
        DisposeWorkers();
        ReleaseTexture(ref _bodyRenderTexture); ReleaseTexture(ref _cameraRenderTexture);
        Destroy(_bodyTexture); Destroy(_cameraTexture);
        if (_ownsWebcam && WebCam != null && WebCam.isPlaying) WebCam.Stop();
        if (_ownsWebcam && WebCam != null) Destroy(WebCam);
    }

    private void DisposeWorkers() { _bodyWorker?.Dispose(); _bodyWorker = null; _handWorker?.Dispose(); _handWorker = null; }
    private static void ReleaseTexture(ref RenderTexture texture) { if (texture == null) return; texture.Release(); Destroy(texture); texture = null; }

    private void LoadDefaultModelsInEditor()
    {
#if UNITY_EDITOR
        if (ModelAsset == null) ModelAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<ModelAsset>("Assets/Models/movenet_lightning.onnx");
        if (HandModelAsset == null) HandModelAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<ModelAsset>("Assets/Models/hand_landmark.onnx");
#endif
    }
}
