using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace PickleVerse.Settings
{
    /// <summary>Asynchronous native video decoding with an immediate, matching first-frame poster.</summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class CinematicArtwork : MonoBehaviour
    {
        public VideoClip clip;
        public Texture2D poster;
        public bool playOnEnable = true;
        public bool IsReady { get; private set; }
        public bool Failed { get; private set; }
        public VideoPlayer Player => player;
        RawImage image;
        VideoPlayer player;
        int width, height;
        double deadline;
        bool requested;

        void Awake() { image = GetComponent<RawImage>(); }
        void OnEnable()
        {
            if (!image) image = GetComponent<RawImage>();
            if (playOnEnable && Application.isPlaying) Play(clip, poster);
        }
        public void Play(VideoClip movie, Texture2D firstFrame)
        {
            Stop();
            clip = movie;
            poster = firstFrame;
            if (!image) image = GetComponent<RawImage>();
            image.texture = poster;
            Fit();
            if (!clip) { Failed = true; return; }
            if (!Application.isPlaying) return;
            requested = true;
            if (!player)
            {
                player = gameObject.AddComponent<VideoPlayer>();
                player.playOnAwake = false;
                player.isLooping = true;
                player.renderMode = VideoRenderMode.APIOnly;
                player.audioOutputMode = VideoAudioOutputMode.None;
                player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
                player.waitForFirstFrame = true;
                player.skipOnDrop = true;
                player.prepareCompleted += Prepared;
                player.frameReady += FirstFrame;
                player.errorReceived += Error;
            }
            player.clip = clip;
            player.sendFrameReadyEvents = true;
            deadline = Time.realtimeSinceStartupAsDouble + 8;
            player.Prepare();
        }
        void Prepared(VideoPlayer source) { if (isActiveAndEnabled) source.Play(); }
        void FirstFrame(VideoPlayer source, long frame)
        {
            if (!isActiveAndEnabled || !source.texture) return;
            image.texture = source.texture;
            IsReady = true;
            // Native decoder updates the same texture; no managed per-frame uploads/callbacks.
            source.sendFrameReadyEvents = false;
            Fit();
        }
        void Error(VideoPlayer source, string message)
        {
            Failed = true;
            requested = false;
            source.sendFrameReadyEvents = false;
            source.Stop();
            source.clip = null;
            image.texture = poster;
            IsReady = false;
            Fit();
            Debug.LogWarning("Cinematic artwork could not play; keeping its poster. " + message, this);
        }
        public void Stop()
        {
            requested = false;
            IsReady = Failed = false;
            if (player)
            {
                player.sendFrameReadyEvents = false;
                player.Stop();
                player.clip = null;
            }
            if (image) image.texture = poster;
        }
        void Update()
        {
            if (width != Screen.width || height != Screen.height) Fit();
            if (requested && player && !IsReady && !Failed && Time.realtimeSinceStartupAsDouble > deadline)
                Error(player, "Video preparation timed out.");
        }
        void Fit()
        {
            width = Screen.width; height = Screen.height;
            if (!image || !image.texture) return;
            float source = image.texture.width / (float)image.texture.height;
            var fitter = GetComponent<AspectRatioFitter>();
            if (fitter)
            {
                fitter.aspectRatio = source;
                image.uvRect = new Rect(0, 0, 1, 1);
            }
            else
            {
                float target = width / (float)Mathf.Max(1, height);
                image.uvRect = target > source
                    ? new Rect(0, (1 - source / target) * .5f, 1, source / target)
                    : new Rect((1 - target / source) * .5f, 0, target / source, 1);
            }
        }
        void OnDisable() { Stop(); }
        void OnDestroy()
        {
            if (!player) return;
            player.prepareCompleted -= Prepared;
            player.frameReady -= FirstFrame;
            player.errorReceived -= Error;
        }
    }
}
