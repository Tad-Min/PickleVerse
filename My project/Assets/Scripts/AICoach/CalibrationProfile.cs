using UnityEngine;

/// <summary>Converts normalized monocular pose data into a stable court-space root position.</summary>
public class CalibrationProfile : MonoBehaviour
{
    [Header("Calibration")]
    [SerializeField] private float calibrationDuration = 2f;
    [SerializeField] private Vector3 calibratedCourtPosition = new Vector3(0f, 1f, -4.5f);
    [Tooltip("Ignore the outer camera margins so shoulders remain visible at the sidelines.")]
    [Range(0f, .3f)] [SerializeField] private float cameraHorizontalInset = .1f;
    [Min(0f)] [SerializeField] private float courtSidelineMargin = .2f;
    [SerializeField] private float verticalMetersPerNormalizedUnit = 3f;
    [SerializeField] private float depthMetersAtFullScaleChange = 2.5f;
    [SerializeField] private float maximumDepthOffset = 2f;

    public bool IsCalibrated { get; private set; }
    public float CalibrationProgress => Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, calibrationDuration));

    private float _elapsed;
    private Vector2 _hipSum;
    private float _shoulderWidthSum;
    private Vector2 _referenceHip;
    private float _referenceShoulderWidth;

    public void BeginCalibration()
    {
        IsCalibrated = false;
        _elapsed = 0f;
        _hipSum = Vector2.zero;
        _shoulderWidthSum = 0f;
    }

    public bool ProcessSample(Vector2 hipCenter, float shoulderWidth, float deltaTime)
    {
        if (IsCalibrated || shoulderWidth <= 0.0001f)
            return IsCalibrated;

        _elapsed += Mathf.Max(0f, deltaTime);
        _hipSum += hipCenter * deltaTime;
        _shoulderWidthSum += shoulderWidth * deltaTime;

        if (_elapsed < calibrationDuration)
            return false;

        _referenceHip = _hipSum / _elapsed;
        _referenceShoulderWidth = _shoulderWidthSum / _elapsed;
        IsCalibrated = _referenceShoulderWidth > 0.0001f;
        return IsCalibrated;
    }

    public Vector3 MapRootPosition(Vector2 hipCenter, float shoulderWidth)
    {
        if (!IsCalibrated || shoulderWidth <= 0.0001f)
            return calibratedCourtPosition;

        Vector2 delta = hipCenter - _referenceHip;
        float widthRatio = _referenceShoulderWidth / shoulderWidth;
        // Training starts at Z=-4.5; the net is at Z=0. A larger shoulder
        // image means closer to the webcam and must move the player toward +Z.
        float depth = Mathf.Clamp((1f - widthRatio) * depthMetersAtFullScaleChange,
            -maximumDepthOffset, maximumDepthOffset);
        // Pose X is already mirrored, centered in [-.5, .5]. Map each side
        // of the calibrated hip separately, preserving both court endpoints
        // even if the player calibrated slightly off-centre in the webcam.
        float cameraEdge = .5f - Mathf.Clamp(cameraHorizontalInset, 0f, .3f);
        float referenceX = Mathf.Clamp(_referenceHip.x, -cameraEdge + .01f, cameraEdge - .01f);
        float courtEdge = Mathf.Max(0f, PickleballCourtSpec.HalfCourtWidth - courtSidelineMargin);
        float neutralX = Mathf.Clamp(calibratedCourtPosition.x, -courtEdge, courtEdge);
        float courtX = hipCenter.x < referenceX
            ? Mathf.Lerp(-courtEdge, neutralX, Mathf.InverseLerp(-cameraEdge, referenceX, hipCenter.x))
            : Mathf.Lerp(neutralX, courtEdge, Mathf.InverseLerp(referenceX, cameraEdge, hipCenter.x));
        return new Vector3(courtX,
            calibratedCourtPosition.y + delta.y * verticalMetersPerNormalizedUnit,
            calibratedCourtPosition.z + depth);
    }

    private void Reset()
    {
        BeginCalibration();
    }
}
