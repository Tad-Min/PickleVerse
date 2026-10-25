using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;
using PickleVerse.Settings;
namespace PickleVerse.Replays
{
    public static class ReplayLaunch
    {
        public static string PlayerId, MatchId;
        public static Action Return;
        public static void Select(string player, string match, Action onReturn = null)
        {
            PlayerId = player;
            MatchId = match;
            Return = onReturn;
        }
        public static void Close()
        {
            var callback = Return;
            Return = null;
            PlayerId = MatchId = null;
            if (callback != null)
                callback();
            else SceneManager.LoadScene("Menu");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            PlayerId = MatchId = null;
            Return = null;
        }
    }
    public sealed class ReplayView : MonoBehaviour
    {
        ReplayMetadata metadata;
        IReplayRepository repository;
        Transform[] actors, paddles;
        Transform[][] bones;
        ReplayChunk current, next;
        int chunkIndex = -1;
        double playhead;
        float speed = 1;
        bool playing, loading;
        int seekGeneration;
        TMP_Text label, playLabel;
        TMP_Text timeLabel;
        float nextLabelUpdate;
        Button[] rateButtons;
        Slider seek;
        public double Playhead => playhead;
        public float PlaybackSpeed => speed;
        public bool IsPlaying => playing;
        public int CachedFrames => (current?.frames.Count ?? 0) + (next?.frames.Count ?? 0);
        async void Start()
        {
            BuildControls();
            repository = ReplayStorage.Repository;
            try
            {
                metadata = repository is IOnlineReplayRepository online ? await online.ReadAsync(ReplayLaunch.PlayerId, ReplayLaunch.MatchId) : repository.Read(ReplayLaunch.PlayerId, ReplayLaunch.MatchId);
                if (!this)
                    return;
                GetComponent<ReplayEnvironment>()?.Apply(metadata?.night ?? false);
                if (metadata == null || metadata.chunks.Count == 0)
                    throw new InvalidDataException("Chưa có dữ liệu replay cho trận này.");
                if (metadata.actors.Count < 2 || metadata.actors.Count > 4)
                    throw new InvalidDataException("Đội hình replay không hợp lệ.");
                actors = new Transform[metadata.actors.Count];
                paddles = new Transform[actors.Length];
                bones = new Transform[actors.Length][];
                for (int i = 0; i < actors.Length; i++)
                {
                    var info = metadata.actors[i];
                    var prefab = Resources.Load<GameObject>("ReplayActors/" + info.visualId);
                    if (!prefab)
                        throw new InvalidDataException("Thiếu hình nhân vật replay: " + info.visualId);
                    actors[i] = Instantiate(prefab, transform).transform;
                    actors[i].name = "Replay / " + info.name;
                    paddles[i] = ReplayRecorder.Find(actors[i], info.paddlePath);
                    if (paddles[i] && !string.IsNullOrEmpty(info.paddleId) && info.paddleId != "paddle.default")
                    {
                        var skin = Resources.Load<GameObject>("ReplayEquipment/" + info.paddleId);
                        if (skin)
                        {
                            foreach (var renderer in paddles[i].GetComponentsInChildren<Renderer>(true))
                                renderer.enabled = false;
                            var instance = Instantiate(skin, paddles[i], false);
                            instance.name = info.skinName;
                        }
                    }
                    bones[i] = new Transform[info.bones.Length];
                    for (int b = 0; b < bones[i].Length; b++)
                        bones[i][b] = ReplayRecorder.Find(actors[i], info.bones[b]);
                }
                seek.maxValue = (float)metadata.duration;
                seek.interactable = true;
                Seek(0);
                playing = true;
            }
            catch (Exception e)
            {
                Error(e);
            }
        }
        void Update()
        {
            if (!playing || metadata == null || loading)
                return;
            Seek(Math.Min(metadata.duration, playhead + Time.unscaledDeltaTime * speed));
            if (playhead >= metadata.duration)
            {
                playing = false;
                playLabel.text = "PHÁT";
            }
        }
        public async void Seek(double time)
        {
            if (metadata == null)
                return;
            int generation = ++seekGeneration;
            try
            {
                playhead = Math.Max(0, Math.Min(time, metadata.duration));
                int index = 0;
                for (int i = 1; i < metadata.chunks.Count && metadata.chunks[i].start <= playhead; i++)
                    index = i;
                if (index != chunkIndex)
                {
                    loading = true;
                    ReplayChunk loaded, following;
                    if (repository is IOnlineReplayRepository online)
                    {
                        loaded = index == chunkIndex + 1 && next != null ? next : await online.ReadChunkAsync(metadata, metadata.chunks[index]);
                        following = index + 1 < metadata.chunks.Count ? await online.ReadChunkAsync(metadata, metadata.chunks[index + 1]) : null;
                    }
                    else
                    {
                        loaded = repository.ReadChunk(metadata, metadata.chunks[index]);
                        following = index + 1 < metadata.chunks.Count ? repository.ReadChunk(metadata, metadata.chunks[index + 1]) : null;
                    }
                    if (!this || generation != seekGeneration)
                        return;
                    current = loaded;
                    next = following;
                    chunkIndex = index;
                }
                int lower = 0;
                while (lower + 1 < current.frames.Count && current.frames[lower + 1].time <= playhead)
                    lower++;
                var a = current.frames[lower];
                var b = lower + 1 < current.frames.Count ? current.frames[lower + 1] : next?.frames[0] ?? a;
                Validate(a);
                Validate(b);
                float t = b.discontinuity || b.time <= a.time ? 0 : Mathf.Clamp01((float)((playhead - a.time) / (b.time - a.time)));
                for (int i = 0; i < actors.Length; i++)
                {
                    ApplyPose(actors[i], a.actors[i].root, b.actors[i].root, 0, t, true);
                    for (int j = 0; j < bones[i].Length; j++)
                        if (bones[i][j])
                            ApplyPose(bones[i][j], a.actors[i].bones, b.actors[i].bones, j * 10, t, false);
                    if (paddles[i])
                        paddles[i].gameObject.SetActive(a.actors[i].paddleVisible);
                }
                var ballObject = transform.Find("Ball");
                if (ballObject)
                {
                    ballObject.position = Vector3.Lerp(new Vector3(a.ball[0], a.ball[1], a.ball[2]), new Vector3(b.ball[0], b.ball[1], b.ball[2]), t);
                    if (a.ballRotation?.Length == 4 && b.ballRotation?.Length == 4)
                        ballObject.rotation = Quaternion.Slerp(new Quaternion(a.ballRotation[0], a.ballRotation[1], a.ballRotation[2], a.ballRotation[3]), new Quaternion(b.ballRotation[0], b.ballRotation[1], b.ballRotation[2], b.ballRotation[3]), t);
                    if (a.ballScale?.Length == 3 && b.ballScale?.Length == 3)
                        ballObject.localScale = Vector3.Lerp(new Vector3(a.ballScale[0], a.ballScale[1], a.ballScale[2]), new Vector3(b.ballScale[0], b.ballScale[1], b.ballScale[2]), t);
                }
                if (Time.unscaledTime >= nextLabelUpdate || !playing)
                {
                    nextLabelUpdate = Time.unscaledTime + .1f;
                    label.text = $"{a.scoreA}  :  {a.scoreB}   ĐIỂM     /     {a.gamesA} – {a.gamesB}  GAME\n{a.status}{(metadata.complete ? "" : " · BẢN GHI CHƯA HOÀN TẤT")}";
                    timeLabel.text = $"{TimeSpan.FromSeconds(playhead):mm\\:ss}  /  {TimeSpan.FromSeconds(metadata.duration):mm\\:ss}";
                }
                seek.SetValueWithoutNotify((float)playhead);
            }
            catch (Exception e)
            {
                if (this && generation == seekGeneration)
                    Error(e);
            }
            finally
            {
                if (generation == seekGeneration)
                    loading = false;
            }
        }
        void Validate(ReplayFrame f)
        {
            if (f.ball == null || f.ball.Length != 3 || f.actors == null || f.actors.Length != actors.Length)
                throw new InvalidDataException("Snapshot replay không hợp lệ.");
            for (int i = 0; i < actors.Length; i++)
                if (f.actors[i]?.root?.Length != 10 || f.actors[i].bones == null || f.actors[i].bones.Length != bones[i].Length * 10)
                    throw new InvalidDataException("Pose replay không hợp lệ.");
        }
        static void ApplyPose(Transform target, float[] a, float[] b, int offset, float t, bool world)
        {
            var p = Vector3.Lerp(new Vector3(a[offset], a[offset + 1], a[offset + 2]), new Vector3(b[offset], b[offset + 1], b[offset + 2]), t);
            var q = Quaternion.Slerp(new Quaternion(a[offset + 3], a[offset + 4], a[offset + 5], a[offset + 6]), new Quaternion(b[offset + 3], b[offset + 4], b[offset + 5], b[offset + 6]), t);
            if (world)
                target.SetPositionAndRotation(p, q);
            else
            {
                target.localPosition = p;
                target.localRotation = q;
            }
            target.localScale = Vector3.Lerp(new Vector3(a[offset + 7], a[offset + 8], a[offset + 9]), new Vector3(b[offset + 7], b[offset + 8], b[offset + 9]), t);
        }
        void Error(Exception e)
        {
            playing = false;
            if (label)
                label.text = "Không thể xem replay\n" + e.Message;
            if (seek)
                seek.interactable = false;
            Debug.LogWarning("Replay: " + e.Message);
        }
        RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        TMP_Text Text(string name, Transform parent, string text, Vector2 min, Vector2 max, int size = 20)
        {
            var rect = Rect(name, parent, min, max);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = ScreenUiTheme.Current ? ScreenUiTheme.Current.font : TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = ScreenUiTheme.Text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }
        UnityEngine.UI.Button Button(string name, Transform parent, string text, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(name, parent, min, max);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(.06f, .24f, .28f, .96f);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            Text("Label", rect, text, new Vector2(.05f, 0), new Vector2(.95f, 1), 16);
            ScreenUiTheme.ButtonStyle(button);
            return button;
        }
        void BuildControls()
        {
            var canvas = new GameObject("Replay Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.transform.SetParent(transform, false);
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 1;
            if (!EventSystem.current)
            {
                var events = new GameObject("Replay EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
            var safe = Rect("Safe Area", canvas.transform, Vector2.zero, Vector2.one);
            safe.gameObject.AddComponent<UiSafeArea>();
            var header = Panel("Replay Header", safe, new Vector2(.03f, .86f), new Vector2(.97f, .97f));
            var back = Button("Back", header, "‹  LỊCH SỬ ĐẤU", new Vector2(.015f, .13f), new Vector2(.20f, .87f), ReplayLaunch.Close);
            Text("Mode", header, "XEM LẠI TRẬN ĐẤU", new Vector2(.22f, .21f), new Vector2(.47f, .79f), 18).color = ScreenUiTheme.Gold;
            label = Text("Status", header, "Đang mở bản ghi…", new Vector2(.49f, .12f), new Vector2(.97f, .88f), 16);
            var dock = Panel("Playback Dock", safe, new Vector2(.03f, .035f), new Vector2(.97f, .17f));
            var play = Button("Play", dock, "TẠM DỪNG", new Vector2(.02f, .18f), new Vector2(.17f, .82f), () =>
            {
                if (metadata == null)
                    return;
                if (!playing && playhead >= metadata.duration)
                    Seek(0);
                playing = !playing;
                playLabel.text = playing ? "TẠM DỪNG" : "PHÁT";
            });
            playLabel = play.GetComponentInChildren<TMP_Text>();
            ScreenUiTheme.ButtonStyle(play, true);
            timeLabel = Text("Time", dock, "00:00 / 00:00", new Vector2(.20f, .60f), new Vector2(.67f, .93f), 14);
            timeLabel.color = ScreenUiTheme.Muted;
            Text("Speed Heading", dock, "TỐC ĐỘ PHÁT", new Vector2(.71f, .64f), new Vector2(.98f, .91f), 11).color = ScreenUiTheme.Muted;
            rateButtons = new Button[3];
            float[] rates = { .5f, 1, 2 };
            for (int i = 0; i < 3; i++)
            {
                float rate = rates[i];
                int index = i;
                rateButtons[i] = Button("Speed" + i, dock, rate + "×", new Vector2(.71f + i * .09f, .15f), new Vector2(.79f + i * .09f, .59f), () =>
                {
                    speed = rate;
                    for (int j = 0; j < 3; j++)
                        ScreenUiTheme.Selection(rateButtons[j], j == index);
                });
            }
            ScreenUiTheme.Selection(rateButtons[1], true);
            var track = Rect("Seek", dock, new Vector2(.20f, .08f), new Vector2(.67f, .60f));
            track.gameObject.AddComponent<Canvas>();
            var raycaster = track.gameObject.AddComponent<GraphicRaycaster>();
            var hit = track.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var rail = Rect("Track", track, new Vector2(0, .43f), new Vector2(1, .57f));
            rail.gameObject.AddComponent<Image>().color = new Color(.18f, .26f, .35f);
            rail.GetComponent<Image>().raycastTarget = false;
            var fillArea = Rect("Fill Area", track, new Vector2(0, .43f), new Vector2(1, .57f));
            var fill = Rect("Fill", fillArea, Vector2.zero, Vector2.one);
            fill.gameObject.AddComponent<Image>().color = ScreenUiTheme.Mint;
            fill.GetComponent<Image>().raycastTarget = false;
            var handleArea = Rect("Handle Area", track, new Vector2(0, .30f), new Vector2(1, .70f));
            var handle = Rect("Handle", handleArea, Vector2.zero, Vector2.one);
            handle.sizeDelta = new Vector2(12, 0);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = ScreenUiTheme.Gold;
            seek = track.gameObject.AddComponent<Slider>();
            seek.fillRect = fill;
            seek.handleRect = handle;
            seek.targetGraphic = handleImage;
            seek.interactable = false;
            seek.onValueChanged.AddListener(v => Seek(v));
            safe.gameObject.AddComponent<UiPanelReveal>();
        }
        RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var r = Rect(name, parent, min, max);
            var image = r.gameObject.AddComponent<Image>();
            image.material = ScreenUiTheme.Current ? ScreenUiTheme.Current.panel : null;
            image.raycastTarget = false;
            return r;
        }
        void OnDestroy()
        {
            if (actors != null)
                foreach (var actor in actors)
                    if (actor)
                        Destroy(actor.gameObject);
        }
    }
}
