using System;
using System.Collections.Generic;
using PickleVerse.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PickleVerse.Audio
{
    // Close Esc after front-end controllers have observed the modal input scope.
    [DefaultExecutionOrder(20000)]
    public sealed class MusicUI : MonoBehaviour
    {
        public static event Action<MusicUI> MenuLayoutRequested;
        public Canvas canvas;
        public RectTransform safeArea, mini, miniViewport, miniCaption, discTransform, playlistContent;
        public GameObject panel, miniTextRoot;
        public Image miniDisc, disc, accent;
        public TMP_Text miniTitle, miniState, title, artist, genre, status, elapsed, duration, playLabel, libraryCount, trackIndex;
        public Button miniButton, closeButton, previousButton, playButton, nextButton, settingsButton;
        public Slider progress;
        public MusicTrackRow rowPrefab;
        public UiLineIcon playSymbol;
        public bool IsOpen => panel.activeSelf;
        public float MarqueeOffset => miniCaption.anchoredPosition.x;
        readonly List<MusicTrackRow> rows = new List<MusicTrackRow>();
        MusicPlayer player;
        GraphicRaycaster raycaster;
        GameObject previousSelection;
        string scene;
        string displayedTrack;
        float marqueeTime, textWidth, progressTick;
        int previousSeconds = -1;
        Rect lastSafeArea;
        Vector2 lastResolution;
        Vector2 lastRecordSize;
        GameObject fullScreenBackdrop;
        RectTransform playerContent;
        Camera suspendedCamera;
        bool suspendedCameraWasEnabled;
        void Awake()
        {
            panel.SetActive(false);
            // Background covers the cutout/gesture margins; content keeps the safe-area anchors.
            var backdrop = new GameObject("Full Screen Music Background", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var backdropRect = (RectTransform)backdrop.transform;
            backdropRect.SetParent(canvas.transform, false);
            backdropRect.SetAsFirstSibling();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = backdropRect.offsetMax = Vector2.zero;
            var background = backdrop.GetComponent<UnityEngine.UI.Image>();
            background.color = new Color(.035f, .06f, .105f, 1);
            background.raycastTarget = true;
            fullScreenBackdrop = backdrop;
            fullScreenBackdrop.SetActive(false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.SetParent(canvas.transform, false);
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            var content = new GameObject("Player Safe Content", typeof(RectTransform));
            playerContent = (RectTransform)content.transform;
            playerContent.SetParent(panelRect, false);
            playerContent.anchorMin = Vector2.zero;
            playerContent.anchorMax = Vector2.one;
            playerContent.offsetMin = playerContent.offsetMax = Vector2.zero;
            int childCount = panelRect.childCount - 1;
            for (int i = 0; i < childCount; i++)
                panelRect.GetChild(0).SetParent(playerContent, false);
            // Record rotation must not rebuild the playlist's entire canvas every frame.
            if (!discTransform.GetComponent<Canvas>())
                discTransform.gameObject.AddComponent<Canvas>();
            if (!miniDisc.GetComponent<Canvas>())
                miniDisc.gameObject.AddComponent<Canvas>();
            raycaster = canvas.GetComponent<GraphicRaycaster>();
            miniButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            previousButton.onClick.AddListener(() => player.Previous());
            nextButton.onClick.AddListener(() => player.Next());
            playButton.onClick.AddListener(() => player.TogglePause());
            settingsButton.onClick.AddListener(() =>
            {
                Close();
                SettingsOverlay.Open();
            });
            progress.onValueChanged.AddListener(value => player?.Seek(value));
        }
        public void Bind(MusicPlayer value)
        {
            player = value;
            player.Changed += Refresh;
            libraryCount.text = "THƯ VIỆN  /  " + player.Library.tracks.Length + " BÀI";
            foreach (var track in player.Library.tracks)
            {
                int index = rows.Count;
                var row = Instantiate(rowPrefab, playlistContent);
                row.gameObject.SetActive(true);
                row.Bind(track, index, () => player.Select(index));
                rows.Add(row);
            }
            Refresh();
        }
        public void ApplyScene(string name)
        {
            if (scene != name)
                Close();
            scene = name;
            miniTextRoot.SetActive(MusicScenePolicy.ShowsWidget(name));
            mini.gameObject.SetActive(MusicScenePolicy.ShowsWidget(name));
            mini.anchorMin = mini.anchorMax = new Vector2(.045f, .225f);
            mini.pivot = new Vector2(0, .5f);
            mini.sizeDelta = new Vector2(310, 58);
            mini.anchoredPosition = Vector2.zero;
            Visibility();
            BindCamera();
            if (name == "Menu")
                MenuLayoutRequested?.Invoke(this);
        }
        public void Open()
        {
            if (IsOpen)
                return;
            if (player == null || !player.Allowed || !MusicScenePolicy.ShowsWidget(scene) || SettingsOverlay.IsOpen || SettingsOverlay.Loading || !FrontEndModalInput.TryAcquire(this))
                return;
            previousSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            fullScreenBackdrop.SetActive(true);
            ApplySafeArea();
            panel.SetActive(true);
            Refresh();
            Canvas.ForceUpdateCanvases();
            suspendedCamera = Camera.main;
            if (suspendedCamera)
            {
                suspendedCameraWasEnabled = suspendedCamera.enabled;
                suspendedCamera.enabled = false;
            }
            FitRecord();
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>())
                text.ForceMeshUpdate();
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(playButton.gameObject);
        }
        public void Close()
        {
            if (panel)
                panel.SetActive(false);
            FrontEndModalInput.Release(this);
            if (fullScreenBackdrop)
                fullScreenBackdrop.SetActive(false);
            if (suspendedCamera)
            {
                suspendedCamera.enabled = suspendedCameraWasEnabled;
                suspendedCamera = null;
            }
            if (EventSystem.current && previousSelection && previousSelection.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(previousSelection);
            previousSelection = null;
        }
        void Refresh()
        {
            if (player == null || player.Track == null)
                return;
            var track = player.Track;
            if (displayedTrack != track.id)
            {
                displayedTrack = track.id;
                miniDisc.sprite = disc.sprite = track.disc;
                discTransform.localRotation = Quaternion.identity;
                miniDisc.rectTransform.localRotation = Quaternion.identity;
                lastRecordSize = Vector2.zero;
                miniTitle.text = track.title + "  •  " + track.artist;
                marqueeTime = 0;
                miniCaption.anchoredPosition = Vector2.zero;
                textWidth = miniTitle.GetPreferredValues(miniTitle.text).x;
                miniCaption.sizeDelta = new Vector2(Mathf.Max(miniViewport.rect.width, textWidth), miniCaption.sizeDelta.y);
            }
            title.text = track.title;
            artist.text = track.artist;
            genre.text = track.genre.ToUpperInvariant();
            accent.color = track.accent;
            if (trackIndex)
                trackIndex.text = (player.CurrentIndex + 1).ToString("00") + "  /  LOBBY RADIO";
            playLabel.text = player.UserPaused ? "PHÁT" : "TẠM DỪNG";
            if (playSymbol)
                playSymbol.SetGlyph(player.UserPaused ? ArenaGlyph.Play : ArenaGlyph.Pause);
            miniState.text = player.Loading ? "ĐANG TẢI" : player.UserPaused ? "ĐÃ TẠM DỪNG" : "ĐANG PHÁT";
            status.text = !string.IsNullOrEmpty(player.Error) ? player.Error : player.Loading ? "Đang chuẩn bị bài nhạc…" : player.UserPaused ? "Nhấn Phát để tiếp tục." : "Tự động phát tiếp • Nhạc dừng khi vào trận đấu";
            duration.text = TimeText(player.Duration);
            for (int i = 0; i < rows.Count; i++)
                rows[i].Select(i == player.CurrentIndex, !player.UserPaused && !player.Loading);
            previousSeconds = -1;
        }
        public void FitRecord()
        {
            if (player?.Track?.disc == null || discTransform.parent is not RectTransform holder)
                return;
            Vector2 holderSize = holder.rect.size;
            if (holderSize == lastRecordSize || holderSize.x <= 0 || holderSize.y <= 0)
                return;
            lastRecordSize = holderSize;
            Rect bounds = player.Track.discBounds, source = player.Track.disc.rect;
            float visibleDiameter = Mathf.Max(bounds.width * source.width, bounds.height * source.height);
            if (visibleDiameter <= 0)
                return;
            Vector2 span = source.size * (.92f / visibleDiameter);
            // The pivot is the artwork's center, so rotation never moves the disc inside its frame.
            discTransform.pivot = bounds.center;
            discTransform.anchorMin = new Vector2(.5f, .5f) - Vector2.Scale(span, bounds.center);
            discTransform.anchorMax = discTransform.anchorMin + span;
            discTransform.offsetMin = discTransform.offsetMax = Vector2.zero;
        }
        void Update()
        {
            if (player == null)
                return;
            Visibility();
            if (!canvas.enabled)
                return;
            BindCamera();
            ApplySafeArea();
            if (IsOpen && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                Close();
                return;
            }
            bool spinning = player.Playing && !player.Loading;
            if (spinning)
            {
                float angle = -18 * Time.unscaledDeltaTime;
                if (IsOpen)
                    discTransform.Rotate(0, 0, angle);
                else miniDisc.rectTransform.Rotate(0, 0, angle);
            }
            if (scene == "Menu" && !IsOpen)
            {
                float overflow = textWidth - miniViewport.rect.width;
                if (overflow > 8)
                {
                    marqueeTime += Time.unscaledDeltaTime;
                    float travel = overflow / 28;
                    float t = Mathf.Repeat(marqueeTime, travel + 3);
                    float x = t < 1.5f ? 0 : t < travel + 1.5f ? -28 * (t - 1.5f) : -overflow;
                    miniCaption.anchoredPosition = new Vector2(x, 0);
                }
            }
            if (!IsOpen)
                return;
            FitRecord();
            progressTick += Time.unscaledDeltaTime;
            if (progressTick < .1f)
                return;
            progressTick = 0;
            int seconds = (int)player.Position;
            if (seconds != previousSeconds)
            {
                previousSeconds = seconds;
                elapsed.text = TimeText(player.Position);
            }
            progress.SetValueWithoutNotify(player.Duration > 0 ? player.Position / player.Duration : 0);
        }
        void Visibility()
        {
            bool visible = player != null && player.Allowed && MusicScenePolicy.ShowsWidget(scene) && !SettingsOverlay.IsOpen && !SettingsOverlay.Loading && (!FrontEndModalInput.IsOpen || FrontEndModalInput.IsOwnedBy(this));
            if (!visible && IsOpen)
                Close();
            if (canvas.enabled != visible)
                canvas.enabled = visible;
            if (raycaster && raycaster.enabled != visible)
                raycaster.enabled = visible;
        }
        void BindCamera()
        {
            // Front-end text and disc artwork stay at display resolution when the 3D lobby scales.
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
            }
        }
        void ApplySafeArea()
        {
            var resolution = new Vector2(Screen.width, Screen.height);
            var area = Screen.safeArea;
            if (resolution == lastResolution && area == lastSafeArea || resolution.x <= 0 || resolution.y <= 0)
                return;
            lastResolution = resolution;
            lastSafeArea = area;
            safeArea.anchorMin = new Vector2(area.xMin / resolution.x, area.yMin / resolution.y);
            safeArea.anchorMax = new Vector2(area.xMax / resolution.x, area.yMax / resolution.y);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            if (playerContent)
            {
                playerContent.anchorMin = safeArea.anchorMin;
                playerContent.anchorMax = safeArea.anchorMax;
                playerContent.offsetMin = playerContent.offsetMax = Vector2.zero;
            }
        }
        static string TimeText(float value)
        {
            int seconds = Mathf.Max(0, (int)value);
            return (seconds / 60) + ":" + (seconds % 60).ToString("00");
        }
        void OnDestroy()
        {
            Close();
            if (player != null)
                player.Changed -= Refresh;
        }
    }
}
