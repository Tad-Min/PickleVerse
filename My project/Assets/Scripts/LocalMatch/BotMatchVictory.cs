using PickleVerse.CharacterMotion;
using PickleVerse.Settings;
using TMPro;
using UnityEngine;

namespace PickleVerse.LocalMatch
{
    // The match simulation stops at MatchOver. Show the local player's result only
    // after the referee's complete announcement queue has finished.
    public sealed class BotMatchVictory : MonoBehaviour
    {
        public BotMatch match;
        public MatchCamera followCamera;
        public AnimationClip femaleWin, maleSuperSaiyan, maleClap;
        public CanvasGroup resultOpacity;
        public TMP_Text winnerTitle;
        public Material winTitleMaterial, lossTitleMaterial;
        public UnityEngine.UI.Image defeatShade;
        public RectTransform sheenMask, sheenText;
        public Light portraitLight;
        public GameObject[] gameplayHud = System.Array.Empty<GameObject>();
        public float cameraSeconds = 1.25f;
        public Transform nearPresentation,farPresentation;
        public bool Active { get; private set; }
        public bool Ready { get; private set; }
        public MatchActor Winner { get; private set; }
        public MatchActor PresentedActor { get; private set; }
        public bool PlayerWon { get; private set; }
        public bool TransitionStarted { get; private set; }
        public float DanceElapsed { get; private set; }
        public AnimationClip CurrentClip { get; private set; }

        Camera view;
        bool female, followWasEnabled, ballWasActive, paddleWasActive;
        bool[] hudWasActive;
        Vector3 actorPosition, visualPosition, visualScale, cameraPosition, stagePosition;
        Quaternion actorRotation, visualRotation, cameraRotation;
        float cameraFov, elapsed;
        Transform[] bones;
        Vector3[] positions, scales;
        Quaternion[] rotations;
        AnimationClip defeatAnimation;

        void Update()
        {
            if (!Active || !match) return;
            if (match.Phase != MatchPhase.MatchOver) { Cancel(); return; }
            if (match.Paused || SettingsOverlay.IsOpen || SettingsOverlay.Loading) return;
            Advance(Mathf.Min(Time.unscaledDeltaTime, .1f));
        }

        public bool Begin()
        {
            if (Active) return true;
            if (!match || match.Rules == null || match.Rules.Winner < 0 || !followCamera || !femaleWin || !maleSuperSaiyan || !maleClap) return false;
            Winner = match.Actor(match.Rules.Winner);
            PresentedActor = match.human;
            PlayerWon = match.Rules.Winner == PresentedActor.playerId;
            view = followCamera.GetComponent<Camera>();
            var secondary = PresentedActor.visual.GetComponent<CharacterSecondaryMotion>();
            female = secondary && secondary.profile ? System.Array.Exists(secondary.profile.joints, j => j.part == SecondaryPart.Ponytail || j.part == SecondaryPart.Skirt) : PresentedActor == match.human;
            actorPosition = PresentedActor.transform.position; actorRotation = PresentedActor.transform.rotation;
            visualPosition = PresentedActor.visual.localPosition; visualRotation = PresentedActor.visual.localRotation; visualScale = PresentedActor.visual.localScale;
            cameraPosition = view.transform.position; cameraRotation = view.transform.rotation; cameraFov = view.fieldOfView;
            // Freeze the current gameplay view without moving it during the banners.
            followWasEnabled = followCamera.enabled; followCamera.enabled = false;
            var anchor=PresentedActor.end<0?nearPresentation:farPresentation;
            stagePosition = anchor?anchor.position:new Vector3(0, actorPosition.y, PresentedActor.end * 4.8f);
            defeatAnimation = null;
            foreach (var entry in PresentedActor.clips) if (entry.name == "PKV_Fault_Reaction") { defeatAnimation = entry.clip; break; }
            bones = PresentedActor.visual.GetComponentsInChildren<Transform>(true);
            positions = new Vector3[bones.Length]; scales = new Vector3[bones.Length]; rotations = new Quaternion[bones.Length];
            match.TouchMovement = Vector2.zero; match.TouchRun = false; match.TouchShotMask = 0;
            Active = true; Ready = TransitionStarted = false; elapsed = DanceElapsed = 0; CurrentClip = null;
            if (resultOpacity) { resultOpacity.alpha = 0; resultOpacity.blocksRaycasts = false; resultOpacity.interactable = false; }
            if (match.ballTrail) { match.ballTrail.emitting = false; match.ballTrail.Clear(); }
            Advance(0);
            return true;
        }

        void StartTransition()
        {
            TransitionStarted = true;
            hudWasActive = new bool[gameplayHud.Length];
            for (int i = 0; i < gameplayHud.Length; i++) if (gameplayHud[i]) { hudWasActive[i] = gameplayHud[i].activeSelf; gameplayHud[i].SetActive(false); }
            ballWasActive = match.ball && match.ball.gameObject.activeSelf;
            if (match.ball) match.ball.gameObject.SetActive(false);
            if (match.ballTrail) { match.ballTrail.emitting = false; match.ballTrail.Clear(); }
            if (match.landingMarker) match.landingMarker.gameObject.SetActive(false);
            if (match.aimMarker) match.aimMarker.gameObject.SetActive(false);
            paddleWasActive = PresentedActor.paddle && PresentedActor.paddle.activeSelf;
            if (PresentedActor.paddle) PresentedActor.paddle.SetActive(false);
            if (portraitLight) { portraitLight.enabled = true; portraitLight.intensity = PlayerWon ? 3.5f : .8f; }
            SetResultStyle();
        }

        void SetResultStyle()
        {
            if (winnerTitle)
            {
                winnerTitle.text = PlayerWon ? "Thắng" : "Thua";
                var material = PlayerWon ? winTitleMaterial : lossTitleMaterial;
                if (material) winnerTitle.fontSharedMaterial = material;
                winnerTitle.color = Color.white;
                winnerTitle.enableVertexGradient = true;
                var top = PlayerWon ? new Color(1,.97f,.82f) : new Color(.57f,.59f,.62f);
                var bottom = PlayerWon ? new Color(.87f,.59f,.23f) : new Color(.25f,.28f,.32f);
                winnerTitle.colorGradient = new VertexGradient(top,top,bottom,bottom);
            }
            if (sheenText) sheenText.GetComponent<TMP_Text>().text = "Thắng";
            if (sheenMask) sheenMask.gameObject.SetActive(PlayerWon);
            if (defeatShade) defeatShade.gameObject.SetActive(!PlayerWon);
        }

        public void Advance(float delta)
        {
            if (!Active || !PresentedActor || !view || match.Paused) return;
            if (!TransitionStarted)
            {
                if (match.hud.announcements && match.hud.announcements.Busy) return;
                StartTransition();
                delta = 0; // The first transition frame still shows the held gameplay view.
            }
            delta = Mathf.Max(0, delta); elapsed += delta;
            float blend = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / Mathf.Max(.01f, cameraSeconds)));
            // Keep the hero and camera on one side of the net, with clear space for the title.
            PresentedActor.transform.SetPositionAndRotation(Vector3.Lerp(actorPosition, stagePosition, blend), Quaternion.Slerp(actorRotation, Quaternion.Euler(0, PresentedActor.end < 0 ? 0 : 180, 0), blend));
            Quaternion facing = Quaternion.Euler(0, PresentedActor.end < 0 ? 0 : 180, 0);
            Vector3 targetCamera = stagePosition + facing * new Vector3(.2f, 1.55f, 4.2f);
            Quaternion targetRotation = Quaternion.LookRotation(stagePosition + Vector3.up * .43f - targetCamera);
            view.transform.SetPositionAndRotation(Vector3.Lerp(cameraPosition, targetCamera, blend), Quaternion.Slerp(cameraRotation, targetRotation, blend));
            // A wider lens on narrow screens keeps raised hands in frame.
            view.fieldOfView = Mathf.Lerp(cameraFov, view.aspect < 1.5f ? 49 : 45, blend);
            if (portraitLight)
            {
                portraitLight.transform.position = view.transform.position + Vector3.up * .6f;
                portraitLight.transform.LookAt(PresentedActor.transform.position + Vector3.up * .95f);
            }
            bool announced = !match.hud.announcements || !match.hud.announcements.Busy;
            bool startedThisFrame = false;
            if (!Ready && blend >= 1 && announced)
            {
                Ready = true; DanceElapsed = 0; startedThisFrame = true;
                if (resultOpacity) { resultOpacity.blocksRaycasts = true; resultOpacity.interactable = true; }
                var secondary = PresentedActor.visual.GetComponent<CharacterSecondaryMotion>(); if (secondary) secondary.ResetMotion();
            }
            if (!Ready)
            {
                foreach (var entry in PresentedActor.clips) if (entry.name == "PKV_Idle_Ready") { Sample(entry.clip, 0, null); break; }
                return;
            }
            if (!startedThisFrame) DanceElapsed += delta;
            AnimationClip clip, next; float time;
            if (!PlayerWon) { clip = defeatAnimation; next = null; time = clip ? Mathf.Min(DanceElapsed, clip.length) : 0; }
            else if (female) { clip = next = femaleWin; time = Mathf.Repeat(DanceElapsed, Mathf.Max(.01f, clip.length)); }
            else if (DanceElapsed < maleSuperSaiyan.length) { clip = maleSuperSaiyan; next = maleClap; time = DanceElapsed; }
            else { clip = next = maleClap; time = Mathf.Repeat(DanceElapsed - maleSuperSaiyan.length, Mathf.Max(.01f, clip.length)); }
            Sample(clip, time, next); CurrentClip = clip;
            if (resultOpacity) resultOpacity.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01(DanceElapsed / .35f));
            if (PlayerWon) AnimateSheen(DanceElapsed);
            else if (defeatShade) defeatShade.color = new Color(.018f,.024f,.036f,.24f + .025f * Mathf.Sin(DanceElapsed * .8f));
        }

        void Sample(AnimationClip clip, float time, AnimationClip next)
        {
            if (!clip) return;
            PresentedActor.SampleCelebration(clip, time);
            float seam = Mathf.Min(.3f, clip.length * .12f);
            if (next && time > clip.length - seam)
            {
                for (int i = 0; i < bones.Length; i++) { positions[i] = bones[i].localPosition; rotations[i] = bones[i].localRotation; scales[i] = bones[i].localScale; }
                next.SampleAnimation(PresentedActor.visual.gameObject, 0);
                float weight = Mathf.SmoothStep(0, 1, (time - clip.length + seam) / seam);
                for (int i = 0; i < bones.Length; i++)
                {
                    bones[i].localPosition = Vector3.Lerp(positions[i], bones[i].localPosition, weight);
                    bones[i].localRotation = Quaternion.Slerp(rotations[i], bones[i].localRotation, weight);
                    bones[i].localScale = Vector3.Lerp(scales[i], bones[i].localScale, weight);
                }
            }
            PresentedActor.visual.SetLocalPositionAndRotation(visualPosition, visualRotation); PresentedActor.visual.localScale = visualScale;
        }

        void AnimateSheen(float seconds)
        {
            if (!winnerTitle || !sheenMask || !sheenText) return;
            float width = winnerTitle.rectTransform.rect.width;
            float phase = Mathf.Repeat(seconds, 2.4f);
            sheenMask.gameObject.SetActive(phase < .8f);
            float x = Mathf.Lerp(-width * .5f - 70, width * .5f + 70, Mathf.Clamp01(phase / .8f));
            sheenMask.anchoredPosition = new Vector2(x, 0);
            sheenText.sizeDelta = winnerTitle.rectTransform.rect.size;
            sheenText.anchoredPosition = new Vector2(-x, 0);
        }

        public void Cancel()
        {
            if (!Active) return;
            Active = Ready = false;
            if (TransitionStarted && PresentedActor)
            {
                PresentedActor.transform.SetPositionAndRotation(actorPosition, actorRotation);
                PresentedActor.visual.SetLocalPositionAndRotation(visualPosition, visualRotation); PresentedActor.visual.localScale = visualScale;
                if (PresentedActor.paddle) PresentedActor.paddle.SetActive(paddleWasActive);
            }
            if (TransitionStarted && match && match.ball) match.ball.gameObject.SetActive(ballWasActive);
            if (TransitionStarted) for (int i = 0; i < gameplayHud.Length; i++) if (gameplayHud[i]) gameplayHud[i].SetActive(hudWasActive[i]);
            if (portraitLight) portraitLight.enabled = false;
            if (resultOpacity) { resultOpacity.alpha = 0; resultOpacity.blocksRaycasts = false; resultOpacity.interactable = false; }
            if (match && match.hud && match.hud.resultActions) match.hud.resultActions.SetActive(false);
            if (followCamera) { followCamera.enabled = followWasEnabled; if (followWasEnabled) followCamera.SnapToPlayer(); }
            TransitionStarted = false; Winner = PresentedActor = null; CurrentClip = null;
        }

        void OnDisable() { Cancel(); }
    }
}
