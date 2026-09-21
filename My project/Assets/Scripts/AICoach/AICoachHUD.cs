using UnityEngine;
using UnityEngine.UI;

/// <summary>Offline runtime HUD for camera, calibration and physics diagnostics.</summary>
public class AICoachHUD : MonoBehaviour
{
    private SentisPoseTracker _tracker;
    private PaddleKinematicController _paddle;
    private BallContactController _ball;
    private Text _telemetry;
    private RawImage _preview;

    private void Start()
    {
        _tracker = FindFirstObjectByType<SentisPoseTracker>();
        _paddle = FindFirstObjectByType<PaddleKinematicController>();
        _ball = FindFirstObjectByType<BallContactController>();
        CreateHud();
    }

    private void Update()
    {
        if (_tracker == null || _telemetry == null) return;
        if (_preview.texture == null && _tracker.WebCam != null) _preview.texture = _tracker.WebCam;
        _preview.uvRect = _tracker.MirrorCamera ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1);
        Vector3 paddleSpeed = _paddle == null ? Vector3.zero : _paddle.LinearVelocity;
        Vector3 ballVelocity = _ball == null ? Vector3.zero : _ball.Velocity;
        int hits = _ball == null ? 0 : _ball.HitCount;
        _telemetry.text = $"<b>PickleVerse Offline Training</b>\n{_tracker.Status}\n" +
            $"Wrist: {(_paddle != null && _paddle.WristTracked ? "tracking" : "holding")} | Rotation: {(_paddle != null && _paddle.OrientationTracked ? "tracking" : "holding")}\n" +
            $"Wrist: {_tracker.CurrentWristAngle:F0}°  Swing: {_tracker.SwingSpeed:F1} m/s\n" +
            $"Paddle: {paddleSpeed.magnitude:F1} m/s  Ball: {ballVelocity.magnitude:F1} m/s  Hits: {hits}";
    }

    private void CreateHud()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            var events = new GameObject("TrainingEventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
        var canvasObject = new GameObject("OfflineTrainingHUD");
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        _telemetry = CreateText("Telemetry", canvasObject.transform, font, 20, Color.white, new Vector2(20, -20), new Vector2(510, 145), new Vector2(0, 1));
        _preview = new GameObject("CameraPreview").AddComponent<RawImage>();
        _preview.transform.SetParent(canvasObject.transform, false);
        var previewRect = _preview.rectTransform;
        previewRect.anchorMin = previewRect.anchorMax = new Vector2(1, 0);
        previewRect.pivot = new Vector2(1, 0);
        previewRect.anchoredPosition = new Vector2(-20, 20);
        previewRect.sizeDelta = new Vector2(320, 240);

        var button = new GameObject("RecalibrateButton").AddComponent<Button>();
        button.transform.SetParent(canvasObject.transform, false);
        var image = button.gameObject.AddComponent<Image>();
        image.color = new Color(.1f, .45f, .7f, .9f);
        button.targetGraphic = image;
        var buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0, 1);
        buttonRect.pivot = new Vector2(0, 1);
        buttonRect.anchoredPosition = new Vector2(20, -180);
        buttonRect.sizeDelta = new Vector2(180, 42);
        CreateText("Label", button.transform, font, 18, Color.white, Vector2.zero, buttonRect.sizeDelta, new Vector2(.5f, .5f)).alignment = TextAnchor.MiddleCenter;
        button.GetComponentInChildren<Text>().text = "Recalibrate";
        button.onClick.AddListener(() => _tracker?.Recalibrate());
    }

    private static Text CreateText(string name, Transform parent, Font font, int size, Color color, Vector2 position, Vector2 dimensions, Vector2 anchor)
    {
        var text = new GameObject(name).AddComponent<Text>();
        text.transform.SetParent(parent, false);
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        return text;
    }
}
