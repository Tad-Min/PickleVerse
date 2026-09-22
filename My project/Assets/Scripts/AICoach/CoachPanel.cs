using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Small addition to the existing HUD; created once, refreshed at 10 Hz.</summary>
public sealed class CoachPanel : MonoBehaviour
{
    private OfflineCoach coach;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private TextMeshProUGUI state, message, resources, coachLabel, voiceLabel;
    [SerializeField] private UnityEngine.UI.Button coachButton, voiceButton;
    [SerializeField] private UnityEngine.UI.Slider volumeSlider;
    private float nextUpdate;

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
    private void Start() => BindGameplay();
    private void SceneLoaded(Scene scene, LoadSceneMode mode) => BindGameplay();
    private void SceneUnloaded(Scene scene) => BindGameplay();
    private void BindGameplay()
    {
        coach = FindAnyObjectByType<OfflineCoach>();
        bool ready = coach && coach.Voice;
        if (coachButton) coachButton.interactable = ready;
        if (voiceButton) voiceButton.interactable = ready;
        if (volumeSlider)
        {
            volumeSlider.interactable = ready;
            if (ready) volumeSlider.SetValueWithoutNotify(coach.Voice.Volume);
        }
        Refresh();
    }
    public void ToggleCoach() { if (coach && coach.Voice) coach.SetCoach(!coach.CoachEnabled); }
    public void ToggleVoice() { if (coach && coach.Voice) coach.Voice.SetVoice(!coach.Voice.VoiceEnabled); }
    public void SetVolume(float value) { if (coach && coach.Voice) coach.Voice.SetVolume(value); }

#if UNITY_EDITOR
    public static void Create(Transform canvas, TMP_FontAsset font)
    {
        var panel = new GameObject("CoachDemoPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panel.transform.SetParent(canvas, false);
        RectTransform rect = (RectTransform)panel.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-20, -20); rect.sizeDelta = new Vector2(320, 192);
        var background = panel.GetComponent<UnityEngine.UI.Image>();
        background.color = new Color(.025f, .07f, .12f, .94f); background.raycastTarget = false;
        var ui = panel.AddComponent<CoachPanel>(); ui.font = font;
        ui.Label("Title", "Coach demo", 10, 7, 300, 22, 20);
        ui.state = ui.Label("State", "", 10, 32, 300, 20, 14);
        ui.message = ui.Label("Feedback", "", 10, 54, 300, 54, 16);
        ui.coachLabel = ui.Button("CoachToggle", 10, ui.ToggleCoach);
        ui.voiceLabel = ui.Button("VoiceToggle", 165, ui.ToggleVoice);
        ui.coachButton = panel.transform.Find("CoachToggle").GetComponent<UnityEngine.UI.Button>();
        ui.voiceButton = panel.transform.Find("VoiceToggle").GetComponent<UnityEngine.UI.Button>();
        ui.resources = ui.Label("Resources", "", 10, 142, 300, 18, 12);
        ui.Label("VolumeLabel", "Âm lượng", 10, 166, 72, 18, 13);
        var track = ui.Rect("VolumeSlider", panel.transform, 85, 164, 225, 22);
        var trackImage = track.gameObject.AddComponent<UnityEngine.UI.Image>();
        trackImage.color = new Color(.15f, .28f, .38f); trackImage.raycastTarget = true;
        var slider = track.gameObject.AddComponent<UnityEngine.UI.Slider>();
        var handle = ui.Rect("Handle", track, 0, 0, 16, 22);
        handle.anchorMin = Vector2.zero; handle.anchorMax = new Vector2(0, 1);
        handle.pivot = new Vector2(.5f, .5f);
        handle.sizeDelta = new Vector2(16, 0); handle.anchoredPosition = Vector2.zero;
        var handleImage = handle.gameObject.AddComponent<UnityEngine.UI.Image>();
        handleImage.color = new Color(.2f, .85f, .78f); handleImage.raycastTarget = true;
        slider.handleRect = handle; slider.targetGraphic = handleImage;
        slider.minValue = 0; slider.maxValue = 1; slider.value = .8f;
        ui.volumeSlider = slider;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(slider.onValueChanged, ui.SetVolume);
        ui.Refresh();
    }
#endif

    private void Update()
    {
        if (!coach || Time.unscaledTime < nextUpdate) return;
        nextUpdate = Time.unscaledTime + .1f; Refresh();
    }
    private void Refresh()
    {
        if (!state) return;
        if (!coach || !coach.Voice)
        {
            state.text = "Waiting for Training";
            message.text = "Giữ vai, hông và tay trong khung hình.";
            resources.text = "";
            coachLabel.text = "Coach"; voiceLabel.text = "Giọng";
            return;
        }
        state.text = coach.Status;
        message.text = coach.HasFeedback ? coach.Latest.Text : "Giữ vai, hông và tay trong khung hình.";
        resources.text = coach.Voice.ResourceStatus;
        coachLabel.text = coach.CoachEnabled ? "Coach: Bật" : "Coach: Tắt";
        voiceLabel.text = coach.Voice.VoiceEnabled ? "Giọng: Bật" : "Giọng: Tắt";
    }
#if UNITY_EDITOR
    private TextMeshProUGUI Button(string name, float x, UnityEngine.Events.UnityAction action)
    {
        var rect = Rect(name, transform, x, 112, 145, 26);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(.1f, .4f, .55f); image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, action);
        var label = Label(name + "Label", "", x, 112, 145, 26, 14);
        label.alignment = TextAlignmentOptions.Center;
        return label;
    }
    private TextMeshProUGUI Label(string name, string text, float x, float y, float w, float h, float size)
    {
        var rect = Rect(name, transform, x, y, w, h);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font) label.font = font;
        label.fontSize = size; label.enableAutoSizing = false; label.color = Color.white;
        label.raycastTarget = false; label.text = text;
        return label;
    }
    private RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
        return rect;
    }
#endif
}
