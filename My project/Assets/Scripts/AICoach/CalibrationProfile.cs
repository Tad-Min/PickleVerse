using UnityEngine;

/// <summary>Converts normalized monocular pose data into a stable court-space root position.</summary>
public class CalibrationProfile : MonoBehaviour
{
    [Header("Calibration")]
    [SerializeField] private float calibrationDuration = 2f;
    [SerializeField] private Vector3 calibratedCourtPosition = new Vector3(0f, 1f, -4.5f);
    [SerializeField] private float horizontalMetersPerNormalizedUnit = 5f;
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
        float depth = Mathf.Clamp((widthRatio - 1f) * depthMetersAtFullScaleChange,
            -maximumDepthOffset, maximumDepthOffset);
        return calibratedCourtPosition + new Vector3(
            delta.x * horizontalMetersPerNormalizedUnit,
            delta.y * verticalMetersPerNormalizedUnit,
            depth);
    }

    private void Reset()
    {
        BeginCalibration();
    }
}
