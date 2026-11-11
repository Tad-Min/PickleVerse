using PickleVerse.Progression;
using PickleVerse.Settings;
using PickleVerse.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PickleVerse.FrontEnd
{
    [DefaultExecutionOrder(100)]
    public sealed class HomeMenuShell : MonoBehaviour
    {
        public LobbyUiTheme theme;
        public RectTransform safe, profile, wallet, nav, play, drawerHandle, character, atmosphere;
        public RectTransform paddle, ball;
        public UnityEngine.UI.Image flash;
        public TMP_Text playTitle, playHint;
        public UnityEngine.UI.Button playButton;
        public UnityEngine.UI.GridLayoutGroup navigation;
        public OfflineSocialMenu social;
        public bool IsPlayingAnimation => playing;
        public float AnimationTime => animationTime;
#if UNITY_EDITOR
        [System.NonSerialized] public float editorPreviewDpi;
#endif
        public float Dpi
        {
            get
            {
#if UNITY_EDITOR
                if(editorPreviewDpi>0)return editorPreviewDpi;
#endif
                return Screen.dpi;
            }
        }
        bool playing;
        float animationTime;
        Vector2 lastSize, basePaddle, baseBall, ballContactOffset;
        bool lastTouch, lastPortrait;
        int lastTiles;
        float lastDpi;
        UnityEngine.UI.CanvasScaler scaler;
        Canvas canvas;
        HomeLobbyPresentation home;
        PickleVerse.Audio.MusicUI music;
        bool portraitView;
        void Awake()
        {
            canvas = GetComponent<Canvas>();
            scaler = GetComponent<UnityEngine.UI.CanvasScaler>();
            home = FindAnyObjectByType<HomeLobbyPresentation>();
            var progression = GetComponent<PlayerProgressionMenu>();
            if (progression && progression.editButton)
                progression.editButton.gameObject.SetActive(false);
            // Keep the reference pose stable during hover/focus; the card still
            // responds with its accent/shine and the paddle keeps its strike.
            var feedback = playButton.GetComponent<LobbyUiFeedback>();
            if (feedback && feedback.symbol == paddle)
                feedback.symbol = null;
            paddle.localScale = Vector3.one;
            basePaddle = paddle.anchoredPosition;
            baseBall = ball.anchoredPosition;
            ApplyLayout(true);
        }
        void LateUpdate()
        {
            ApplyLayout(false);
            bool portrait = Screen.height > Screen.width;
            if (home && (portrait || portraitView))
            {
                home.RefreshView();
                var actor = home.IsMaleSelected ? home.maleVisual : home.femaleVisual;
                if (portrait && actor && home.lobbyCamera)
                    home.lobbyCamera.transform.LookAt(actor.position + Vector3.up * .9f);
            }
            portraitView = portrait;
            if (playing)
                AdvancePlay(Time.unscaledDeltaTime);
        }
        void OnEnable()
        {
            PickleVerse.Audio.MusicUI.MenuLayoutRequested += ArrangeMusicWidget;
            if (!music)
                music = FindAnyObjectByType<PickleVerse.Audio.MusicUI>(FindObjectsInactive.Include);
            if (music)
                ArrangeMusicWidget(music);
        }
        void OnDisable()
        {
            PickleVerse.Audio.MusicUI.MenuLayoutRequested -= ArrangeMusicWidget;
            ResetPlay();
        }
        public void Play()
        {
            if (playing || FrontEndModalInput.IsOpen || SettingsOverlay.IsOpen || SettingsOverlay.Loading || LobbyNavigation.IsBusy)
                return;
            playing = true;
            animationTime = 0;
            playButton.interactable = false;
        }
        public void AdvancePlay(float dt)
        {
            if (!playing)
                return;
            animationTime += dt;
            paddle.localRotation = Quaternion.Euler(0, 0, theme.paddleAngle.Evaluate(animationTime) - theme.paddleAngle.Evaluate(0));
            paddle.anchoredPosition = basePaddle + Vector2.right * (theme.paddleX.Evaluate(animationTime) * play.rect.width);
            float travel = play.rect.width * .38f;
            float approach = Mathf.SmoothStep(0, 1, animationTime / Mathf.Max(.01f, theme.impactTime));
            ball.anchoredPosition = baseBall + ballContactOffset * approach + new Vector2(theme.ballX.Evaluate(animationTime) * travel, theme.ballY.Evaluate(animationTime) * travel);
            float hit = Mathf.Clamp01((animationTime - theme.impactTime) / .15f);
            var tint = flash.color;
            tint.a = animationTime < theme.impactTime ? 0 : Mathf.Sin(hit * Mathf.PI) * .9f;
            flash.color = tint;
            flash.rectTransform.localScale = Vector3.one * Mathf.Lerp(.4f, 1.15f, hit);
            if (animationTime >= theme.strikeDuration)
            {
                ResetPlay();
                if (ProgressionRuntime.Instance?.IsOffline == true)
                    LobbyNavigation.OpenTraining();
                else
                    LobbyNavigation.OpenModes();
            }
        }
        void ResetPlay()
        {
            playing = false;
            animationTime = 0;
            if (playButton)
                playButton.interactable = true;
            if (paddle)
            {
                paddle.anchoredPosition = basePaddle;
                paddle.localRotation = Quaternion.identity;
            }
            if (ball)
                ball.anchoredPosition = baseBall;
            if (flash)
            {
                var c = flash.color;
                c.a = 0;
                flash.color = c;
            }
        }
        public static float DpUnits(float dpi, float pixelHeight, float canvasScale)
        {
            return (dpi >= 100 && dpi <= 1000 ? dpi / 160f : pixelHeight / 360f) / Mathf.Max(.01f, canvasScale);
        }
        void ApplyLayout(bool force)
        {
            if (!safe || Screen.width <= 0 || Screen.height <= 0)
                return;
            bool portrait = Screen.height > Screen.width;
            if (scaler && !Mathf.Approximately(scaler.matchWidthOrHeight, portrait ? 0 : 1))
                scaler.matchWidthOrHeight = portrait ? 0 : 1;
            Vector2 size = safe.rect.size;
            bool touch = Application.isMobilePlatform || Touchscreen.current != null;
            // Tiles can be added at runtime (the signed-in "!" appeals tile), so lay out what is visible.
            int tiles = 0;
            foreach (Transform tile in navigation.transform)
                if (tile.gameObject.activeSelf)
                    tiles++;
            tiles = Mathf.Max(1, tiles);
            if (!force && size == lastSize && touch == lastTouch && portrait == lastPortrait && lastDpi == Dpi && tiles == lastTiles)
                return;
            lastTiles = tiles;
            lastSize = size;
            lastTouch = touch;
            lastPortrait = portrait;
            lastDpi = Dpi;
            float dp = touch ? DpUnits(Dpi, Mathf.Min(Screen.width, Screen.height), canvas.scaleFactor) : 1;
            float pad = portrait ? 28 : 24;
            float target = touch ? Mathf.Max(60, 44 * dp) : 60;
            float gap = 18, bottom = 18;
            float playWidth = Mathf.Min(Mathf.Max(294, 175 * dp), size.x * .34f);
            float playHeight = Mathf.Max(88, target + 16);
            float availableNav = Mathf.Max(80, size.x - pad * 2 - gap - playWidth);
            float minimumCell = touch ? Mathf.Max(86, 60 * dp) : 90;
            int columns = Mathf.Clamp(Mathf.FloorToInt((availableNav + 7) / (minimumCell + 7)), 1, portrait ? 3 : tiles);
            int rows = Mathf.CeilToInt((float)tiles / columns);
            float navWidth = Mathf.Min(availableNav, columns * (touch ? Mathf.Max(106, 76 * dp) : 106) + (columns - 1) * 7);
            float rowHeight = target + 2;
            float navHeight = rowHeight * rows + 7 * (rows - 1), dockHeight = Mathf.Max(playHeight, navHeight);
            Place(nav, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-pad, bottom + (dockHeight - navHeight) * .5f), new Vector2(navWidth, navHeight));
            navigation.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            navigation.constraintCount = columns;
            navigation.cellSize = new Vector2((navWidth - 7 * (columns - 1)) / columns, rowHeight);
            navigation.spacing = new Vector2(7, 7);
            float profileWidth = Mathf.Max(264, portrait ? 140 * dp : 130 * dp), headHeight = Mathf.Max(64, target);
            profileWidth = Mathf.Min(profileWidth, size.x - pad * 2);
            Place(profile, new Vector2(0, 1), new Vector2(0, 1), new Vector2(pad, -18), new Vector2(profileWidth, headHeight));
            float face = headHeight * .68f;
            Place((RectTransform)profile.Find("ImgAvatar"), new Vector2(0, .5f), new Vector2(.5f, .5f), new Vector2(14 + face * .5f, 0), Vector2.one * face);
            Place((RectTransform)profile.Find("Refresh Avatar Ring"), new Vector2(0, .5f), new Vector2(.5f, .5f), new Vector2(14 + face * .5f, 0), Vector2.one * (face * 1.24f));
            foreach (string label in new[] { "TxtName", "TxtLevel" })
            {
                var r = (RectTransform)profile.Find(label);
                r.anchorMin = new Vector2(0, r.anchorMin.y);
                r.anchorMax = new Vector2(1, r.anchorMax.y);
                r.offsetMin = new Vector2(face + 30, 0);
                r.offsetMax = new Vector2(-14, 0);
            }
            float walletWidth = Mathf.Min(portrait ? size.x - pad * 2 : size.x * .43f, Mathf.Max(396, 200 * dp));
            Place(wallet, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-pad, portrait ? -headHeight - 30 : -18), new Vector2(walletWidth, Mathf.Max(56, target * .86f)));
            Place(play, new Vector2(0, 0), new Vector2(0, .5f), new Vector2(pad, bottom + dockHeight * .5f), new Vector2(playWidth, playHeight));
            // Match the reference: head over the upper-left corner, grip ending
            // inside the card near the lower mint trim; ball floats above the head.
            float artwork = playHeight * 1.95f;
            float paddleY = .20f + (.5f - 31 / 256f) * artwork / playHeight;
            Place(paddle, new Vector2(.16f, paddleY), new Vector2(.5f, .5f), Vector2.zero, Vector2.one * artwork);
            Vector2 ballAnchor = new Vector2(.41f, 1.95f);
            Place(ball, ballAnchor, new Vector2(.5f, .5f), Vector2.zero, Vector2.one * (playHeight * .34f));
            // The ball approaches this contact point during the wind-up, then
            // follows the existing outgoing curve after the 0.15s strike.
            Vector2 contact = new Vector2(.42f, .30f) * (artwork / 2.2f);
            ballContactOffset = new Vector2((.16f + theme.paddleX.Evaluate(theme.impactTime) - ballAnchor.x) * playWidth + contact.x, (paddleY - ballAnchor.y) * playHeight + contact.y);
            flash.rectTransform.anchorMin = flash.rectTransform.anchorMax = ballAnchor;
            flash.rectTransform.anchoredPosition = ballContactOffset;
            bool compactTitle = touch || playWidth < 285;
            playTitle.rectTransform.anchorMin = new Vector2(compactTitle ? .35f : .06f, .25f);
            playTitle.rectTransform.anchorMax = new Vector2(compactTitle ? .97f : .94f, .82f);
            playTitle.rectTransform.offsetMin = playTitle.rectTransform.offsetMax = Vector2.zero;
            playTitle.fontSize = Mathf.Max(23, 18 * dp);
            playTitle.fontSizeMax = playTitle.fontSize;
            playHint.gameObject.SetActive(!touch);
            foreach (var label in nav.GetComponentsInChildren<TMP_Text>(true))
            {
                label.fontSize = Mathf.Max(12, 12 * dp);
                label.fontSizeMax = label.fontSize;
            }
            var name = profile.Find("TxtName").GetComponent<TMP_Text>();
            name.fontSizeMax = Mathf.Max(20, 15 * dp);
            var rank = profile.Find("TxtLevel").GetComponent<TMP_Text>();
            rank.fontSizeMax = Mathf.Max(12, 9 * dp);
            foreach (var text in wallet.GetComponentsInChildren<TMP_Text>(true))
            {
                text.enableAutoSizing = true;
                text.fontSizeMax = text.name == "Balance" ? Mathf.Max(21, 15 * dp) : Mathf.Max(10, 8.5f * dp);
                text.fontSize = text.fontSizeMax;
            }
            Place(drawerHandle, new Vector2(0, .56f), new Vector2(0, .5f), new Vector2(0, 0), new Vector2(touch ? target : 42, Mathf.Max(104, target * 1.4f)));
            if (character)
            {
                character.anchoredPosition = new Vector2(-pad, 0);
                character.sizeDelta = new Vector2(Mathf.Max(68, target), Mathf.Max(92, target * 2 + 6));
            }
            if (character)
                foreach (var text in character.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.enableAutoSizing = true;
                    text.fontSizeMax = Mathf.Max(14, 12 * dp);
                    text.fontSize = text.fontSizeMax;
                }
            if (atmosphere)
                Place(atmosphere, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-pad, bottom + dockHeight + 24), new Vector2(178, 32));
            if (music)
                ArrangeMusicWidget(music);
            social?.ConfigureDrawerLayout();
        }
        public void ArrangeMusicWidget(PickleVerse.Audio.MusicUI widget)
        {
            music = widget;
            if (!widget || !widget.mini || !widget.safeArea || !paddle || !play)
                return;
            Vector3 top = paddle.TransformPoint(new Vector3(0, paddle.rect.yMin + paddle.rect.height * 229 / 256f, 0));
            Vector3 ballTop = ball.TransformPoint(new Vector3(0, ball.rect.yMin + ball.rect.height * 203 / 256f, 0));
            Vector3 left = play.TransformPoint(new Vector3(play.rect.xMin, 0, 0));
            Vector3 localTop = widget.safeArea.InverseTransformPoint(top), localLeft = widget.safeArea.InverseTransformPoint(left);
            localTop.y = Mathf.Max(localTop.y, widget.safeArea.InverseTransformPoint(ballTop).y);
            var bounds = widget.safeArea.rect;
            widget.mini.anchorMin = widget.mini.anchorMax = Vector2.zero;
            widget.mini.pivot = Vector2.zero;
            widget.mini.anchoredPosition = new Vector2(Mathf.Clamp(localLeft.x - bounds.xMin, 0, Mathf.Max(0, bounds.width - widget.mini.rect.width)), localTop.y - bounds.yMin + 12);
        }
        static void Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = pivot;
            r.sizeDelta = size;
            r.anchoredPosition = position;
        }
    }
}

