using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.Settings
{
    public sealed class LoadingScreenView : MonoBehaviour
    {
        public CanvasGroup group;
        public RawImage artwork, nextArtwork;
        public Texture2D[] pictures;
        public string[] pictureCaptions;
        public TMP_Text destination, status, percentage, caption, tip;
        public RectTransform track, fill, ball;
        public RawImage ballImage;
        public Texture2D[] ballFrames;
        public float DisplayedProgress { get; private set; }
        public float TargetProgress { get; private set; }
        float age, changeTime; int picture, shownPercent = -1, shownBall = -1;
        int lastWidth, lastHeight;
        static int nextPicture;
        void Awake()
        {
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
                SceneLoading.Attach(this);
            }
        }
        public void Begin(string scene)
        {
            age = changeTime = 0;
            DisplayedProgress = TargetProgress = 0;
            shownPercent = shownBall = -1;
            group.alpha = 1;
            destination.text = scene == "BotMatch" ? "ĐẤU VỚI BOT" : scene == "Training" ? "HỌC VIỆN PICKLEVERSE" : scene == "Tutorial" ? "LÀM QUEN SÂN ĐẤU" : "ĐẤU TRỰC TUYẾN";
            tip.text = scene == "MultiplayerCourt" ? "Một đội hình. Cùng nhịp đấu." : "Giữ nhịp di chuyển. Đón bóng. Tỏa sáng.";
            picture = pictures.Length > 0 ? nextPicture++ % pictures.Length : 0;
            if (pictures.Length > 0)
                artwork.texture = pictures[picture];
            if (caption && pictureCaptions.Length > picture)
                caption.text = pictureCaptions[picture];
            nextArtwork.color = new Color(1, 1, 1, 0);
            UpdateLayout();
            RenderProgress();
        }
        public void SetProgress(float value, string message)
        {
            TargetProgress = Mathf.Max(TargetProgress, Mathf.Clamp01(value));
            if (status.text != message)
                status.text = message;
        }
        void Update()
        {
            age += Time.unscaledDeltaTime;
            changeTime += Time.unscaledDeltaTime;
            DisplayedProgress = Mathf.MoveTowards(DisplayedProgress, TargetProgress, Time.unscaledDeltaTime * .65f);
            RenderProgress();
            if (Screen.width != lastWidth || Screen.height != lastHeight)
                UpdateLayout();
            if (pictures.Length < 2 || changeTime < 7)
                return;
            int next = (picture + 1) % pictures.Length;
            nextArtwork.texture = pictures[next];
            Crop(nextArtwork);
            float weight = Mathf.Clamp01((changeTime - 7) / .85f);
            nextArtwork.color = new Color(1, 1, 1, weight);
            if (weight >= 1)
            {
                picture = next;
                artwork.texture = pictures[picture];
                Crop(artwork);
                nextArtwork.color = new Color(1, 1, 1, 0);
                changeTime = 0;
                if (caption && pictureCaptions.Length > picture)
                    caption.text = pictureCaptions[picture];
            }
        }
        void RenderProgress()
        {
            int number = TargetProgress >= 1 && DisplayedProgress >= .9999f ? 100 : Mathf.Min(99, Mathf.FloorToInt(DisplayedProgress * 100));
            if (number != shownPercent)
            {
                percentage.SetText("{0:0}%", number);
                shownPercent = number;
            }
            fill.anchorMax = new Vector2(DisplayedProgress, 1);
            float width = track.rect.width;
            ball.anchoredPosition = new Vector2(width * DisplayedProgress, 0);
            if (ballFrames.Length > 0)
            {
                int frame = Mathf.FloorToInt(width * DisplayedProgress / (Mathf.PI * ball.rect.height) * ballFrames.Length) % ballFrames.Length;
                if (frame != shownBall)
                {
                    ballImage.texture = ballFrames[frame];
                    shownBall = frame;
                }
            }
        }
        void UpdateLayout()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            Crop(artwork);
            Crop(nextArtwork);
        }
        static void Crop(RawImage image)
        {
            if (!image || !image.texture)
                return;
            float source = (float)image.texture.width / image.texture.height, target = (float)Screen.width / Mathf.Max(1, Screen.height);
            image.uvRect = target > source ? new Rect(0, (1 - source / target) * .5f, 1, source / target) : new Rect((1 - target / source) * .5f, 0, target / source, 1);
        }
    }
}
