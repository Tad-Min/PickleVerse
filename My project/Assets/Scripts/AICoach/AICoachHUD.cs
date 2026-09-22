using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>Offline runtime HUD for camera, calibration and physics diagnostics.</summary>
public class AICoachHUD : MonoBehaviour
{
    [SerializeField] private TMPro.TMP_FontAsset coachFont;
    private SentisPoseTracker _tracker;
    private PaddleKinematicController _paddle;
    private BallContactController _ball;
    [SerializeField] private Text _telemetry;
    [SerializeField] private RawImage _preview;
    [SerializeField] private TMPro.TextMeshProUGUI _previewToggleLabel;
    [SerializeField] private UnityEngine.UI.Button recalibrateButton, cameraButton;
    public bool CameraPreviewVisible => _preview != null && _preview.gameObject.activeSelf;

    public void ToggleCameraPreview()
    {
        if (_preview == null) return;
        bool visible = !CameraPreviewVisible;
        _preview.gameObject.SetActive(visible);
        _previewToggleLabel.text = visible ? "Hide camera" : "Show camera";
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += SceneLoaded;
        SceneManager.sceneUnloaded += SceneUnloaded;
        BindGameplay();
    }
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneUnloaded -= SceneUnloaded;
    }
    private void Start()
    {
        BindGameplay();
        if (_preview) _preview.gameObject.SetActive(false);
        if (_previewToggleLabel) _previewToggleLabel.text = "Show camera";
    }
    private void SceneLoaded(Scene scene, LoadSceneMode mode) => BindGameplay();
    private void SceneUnloaded(Scene scene) => BindGameplay();
    public void Recalibrate() { if (_tracker) _tracker.Recalibrate(); }
    private void BindGameplay()
    {
        _tracker = FindAnyObjectByType<SentisPoseTracker>();
        _paddle = FindAnyObjectByType<PaddleKinematicController>();
        _ball = FindAnyObjectByType<BallContactController>();
        if (_preview) _preview.texture = null;
        if (recalibrateButton) recalibrateButton.interactable = _tracker != null;
        if (cameraButton) cameraButton.interactable = _tracker != null;
        if (!_tracker && _telemetry) _telemetry.text = "PickleVerse UI — waiting for Training";
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

#if UNITY_EDITOR
    // Editor-only construction. Runtime reads the saved, editable scene hierarchy.
    public void BuildLayout()
    {
        if (GetComponentInChildren<Canvas>(true)) return;
        if (!GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true))
        {
            var events = new GameObject("TrainingEventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            events.transform.SetParent(transform, false);
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
        _preview.raycastTarget = false;
        // Hiding the preview never stops the webcam or tracking worker.
        _preview.gameObject.SetActive(false);
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
        recalibrateButton = button;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, Recalibrate);

        var previewButton = new GameObject("CameraPreviewToggleButton", typeof(RectTransform)).AddComponent<UnityEngine.UI.Button>();
        previewButton.transform.SetParent(canvasObject.transform, false);
        var previewButtonImage = previewButton.gameObject.AddComponent<UnityEngine.UI.Image>();
        previewButtonImage.color = new Color(.1f, .45f, .7f, .9f);
        previewButtonImage.raycastTarget = true;
        previewButton.targetGraphic = previewButtonImage;
        var toggleRect = previewButton.GetComponent<RectTransform>();
        toggleRect.anchorMin = toggleRect.anchorMax = new Vector2(0, 1);
        toggleRect.pivot = new Vector2(0, 1);
        toggleRect.anchoredPosition = new Vector2(20, -234);
        toggleRect.sizeDelta = new Vector2(180, 42);
        _previewToggleLabel = new GameObject("Label", typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
        _previewToggleLabel.transform.SetParent(previewButton.transform, false);
        _previewToggleLabel.rectTransform.anchorMin = Vector2.zero;
        _previewToggleLabel.rectTransform.anchorMax = Vector2.one;
        _previewToggleLabel.rectTransform.offsetMin = _previewToggleLabel.rectTransform.offsetMax = Vector2.zero;
        _previewToggleLabel.fontSize = 18;
        _previewToggleLabel.alignment = TMPro.TextAlignmentOptions.Center;
        _previewToggleLabel.color = Color.white;
        _previewToggleLabel.raycastTarget = false;
        _previewToggleLabel.text = "Show camera";
        cameraButton = previewButton;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(previewButton.onClick, ToggleCameraPreview);
        CoachPanel.Create(canvasObject.transform, coachFont);
        _telemetry.text = "PickleVerse Offline Training";
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
#endif
}
