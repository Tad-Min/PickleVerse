using System;
using System.Linq;
using PickleVerse.LocalMatch;
using TMPro;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    // Presentation moves only the visual child. Network positions and court rules remain authoritative.
    [DefaultExecutionOrder(500)]
    public sealed class MultiplayerCinematic : MonoBehaviour
    {
        public AnimationClip femaleWalk, maleWalk, femaleWin, maleSuperSaiyan, maleClap;
        public const float IntroDuration = 9.5f;
        public bool PlayingIntro { get; private set; }
        public bool ShowingOutcome { get; private set; }
        public bool OutcomeReady { get; private set; }
        public bool PlayerWon { get; private set; }
        public int PresentedCount => presented?.Length ?? 0;
        public bool OwnsPose => PlayingIntro || ShowingOutcome;
        MultiplayerCourtUI ui;
        Camera view; CourtCamera follow; StadiumEnvironment stadium;
        ActorPose[] ourTeam, theirTeam;
        RectTransform overlay, safeArea, sheenMask, sheenText;
        TMP_Text caption, title;
        UnityEngine.UI.Button next;
        UnityEngine.UI.Image shade;
        GameObject[] hud; bool[] hudActive;
        ActorPose[] actors, presented;
        bool finishedIntro, followEnabled;
        Vector3 originCamera; Quaternion originRotation; float originFov, outcomeTime;
        sealed class ActorPose
        {
            public NetworkPlayer player; public MatchActor actor; public Vector3 destination, visualPosition, visualScale; public Quaternion rotation, visualRotation;
            public bool paddleActive; public AnimationClip walk; readonly int capturedEnd;
            public ActorPose(NetworkPlayer p, AnimationClip walk)
            {
                player = p;
                actor = p.Actor;
                this.walk = walk;
                capturedEnd = p.CourtEnd;
                destination = actor.transform.position;
                rotation = actor.transform.rotation;
                visualPosition = actor.visual.localPosition;
                visualRotation = actor.visual.localRotation;
                visualScale = actor.visual.localScale;
                paddleActive = actor.paddle && actor.paddle.activeSelf;
            }
            public void Sample(AnimationClip clip, float time, bool celebration = false)
            {
                if (celebration)
                    actor.SampleCelebration(clip, time);
                else actor.SampleEntrance(clip, time);
                actor.visual.SetLocalPositionAndRotation(visualPosition, visualRotation);
                actor.visual.localScale = visualScale;
            }
            public AnimationClip Clip(string name) => actor.clips.FirstOrDefault(c => c.name == name).clip;
            public void Restore()
            {
                if (!actor)
                    return;
                actor.ResetAt(destination, player && player.IsSpawned ? player.CourtEnd : capturedEnd);
                actor.transform.rotation = rotation;
                actor.visual.SetLocalPositionAndRotation(visualPosition, visualRotation);
                actor.visual.localScale = visualScale;
                if (actor.paddle)
                    actor.paddle.SetActive(paddleActive);
                actor.RefreshPose();
            }
        }
        void Awake()
        {
            ui = GetComponent<MultiplayerCourtUI>();
            Build();
        }
        void Build()
        {
            var font = ui.modalTitle ? ui.modalTitle.font : TMP_Settings.defaultFontAsset;
            overlay = MatchPresentationUi.Rect("CourtCinematic", transform, Vector2.zero, Vector2.one);
            shade = MatchPresentationUi.Image("DefeatShade", overlay, Vector2.zero, Vector2.one, new Color(.012f, .018f, .035f, .22f));
            safeArea = MatchPresentationUi.Rect("SafeArea", overlay, Vector2.zero, Vector2.one);
            MatchPresentationUi.Image("TopLetterbox", overlay, new Vector2(0, .91f), Vector2.one, new Color(.008f, .02f, .03f, .9f));
            MatchPresentationUi.Image("BottomLetterbox", overlay, Vector2.zero, new Vector2(1, .07f), new Color(.008f, .02f, .03f, .9f));
            caption = MatchPresentationUi.Label("Caption", safeArea, font, "", 23, new Vector2(.15f, .92f), new Vector2(.85f, .99f), MatchPresentationUi.Gold);
            title = MatchPresentationUi.Label("Outcome", safeArea, font, "", 76, new Vector2(.24f, .17f), new Vector2(.76f, .33f), Color.white);
            sheenMask = MatchPresentationUi.Rect("ShineMask", title.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            sheenMask.sizeDelta = new Vector2(52, 120);
            var maskImage = sheenMask.gameObject.AddComponent<UnityEngine.UI.Image>();
            maskImage.raycastTarget = false;
            maskImage.color = Color.white;
            var mask = sheenMask.gameObject.AddComponent<UnityEngine.UI.Mask>();
            mask.showMaskGraphic = false;
            var sheen = MatchPresentationUi.Label("Shine", sheenMask, font, "Thắng", 76, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Color(1, 1, .94f));
            sheenText = sheen.rectTransform;
            next = MatchPresentationUi.Button("Continue", safeArea, font, "THOÁT TRẬN  →", new Vector2(.36f, .075f), new Vector2(.64f, .15f), true);
            next.onClick.AddListener(() =>
            {
                if (OutcomeReady)
                    ui.resultScreen.Open();
            });
            overlay.gameObject.SetActive(false);
        }
        void Capture()
        {
            view = Camera.main;
            stadium = FindAnyObjectByType<StadiumEnvironment>();
            if (!view)
                return;
            follow = view.GetComponent<CourtCamera>();
            followEnabled = follow && follow.enabled;
            if (follow)
                follow.enabled = false;
            hud = transform.Cast<Transform>().Where(t => t != overlay && t.name != "MultiplayerSummary" && (!ui.announcements || t != ui.announcements.panel)).Select(t => t.gameObject).ToArray();
            hudActive = hud.Select(h => h.activeSelf).ToArray();
            foreach (var h in hud)
                h.SetActive(false);
            overlay.gameObject.SetActive(true);
            overlay.SetAsLastSibling();
        }
        void LateUpdate()
        {
            if (PickleVerse.Settings.SceneLoading.IsBlocking)
                return;
            var match = NetworkMatch.Instance;
            if (!match || !match.IsSpawned || !ui)
                return;
            if (match.IntroStart > 0 && !match.Started && !finishedIntro)
            {
                if (!PlayingIntro)
                {
                    if (NetworkPlayer.All.Count < match.Roster.Count || NetworkPlayer.All.Any(p => !p.Actor))
                        return;
                    actors = NetworkPlayer.All.Select(p => new ActorPose(p, p.Female ? femaleWalk : maleWalk)).ToArray();
                    Capture();
                    int localTeam = actors.First(p => p.player.IsLocalHuman).player.Team;
                    ourTeam = actors.Where(p => p.player.Team == localTeam).ToArray();
                    theirTeam = actors.Where(p => p.player.Team != localTeam).ToArray();
                    if (match.Body)
                        match.Body.gameObject.SetActive(false);
                    PlayingIntro = true;
                    title.gameObject.SetActive(false);
                    next.gameObject.SetActive(false);
                    shade.gameObject.SetActive(false);
                }
                EvaluateIntro(Mathf.Max(0, (float)(match.Runner.SimulationTime - match.IntroStart)));
            }
            else if (PlayingIntro)
                EndIntro();
            if (match.Finished && !ShowingOutcome && ui.EndAnnouncementFinished && (!ui.announcements || !ui.announcements.Busy))
                BeginOutcome(match);
            if (ShowingOutcome)
                EvaluateOutcome(Mathf.Min(Time.unscaledDeltaTime, .1f));
            if (overlay.gameObject.activeSelf)
                MatchPresentationUi.SafeArea(safeArea);
        }
        void EvaluateIntro(float time)
        {
            if (!view)
                return;
            if (time >= IntroDuration)
            {
                EndIntro();
                return;
            }
            var local = actors.First(p => p.player.IsLocalHuman);
            int team = local.player.Team;
            float stage = time < .8f ? 0 : time < 4.6f ? 1 : time < 8.4f ? 2 : 3;
            caption.text = stage == 0 ? "PICKLEVERSE / " + (actors.Length == 4 ? "ĐẤU ĐÔI 2v2" : "ĐẤU ĐƠN 1v1") : stage == 1 ? "ĐỘI CỦA BẠN" : stage == 2 ? "ĐỘI ĐỐI THỦ" : "SẴN SÀNG VÀO TRẬN";
            foreach (var p in actors)
            {
                bool ours = p.player.Team == team;
                float elapsed = time - (ours ? .8f : 4.6f);
                float t = Mathf.Clamp01(elapsed / 3.8f);
                var anchor = p.player.CourtEnd < 0 ? stadium?.nearEntrance : stadium?.farEntrance;
                var start = anchor ? anchor.position : new Vector3(0, 0, p.player.CourtEnd * 10.8f);
                start.x = p.destination.x;
                p.actor.transform.position = Vector3.Lerp(start, p.destination, t);
                p.actor.transform.rotation = p.rotation;
                p.Sample(elapsed > 0 && t < 1 ? p.walk : p.Clip("PKV_Idle_Ready"), elapsed > 0 && t < 1 ? elapsed * .85f : 0);
                if (t > 0 && t < 1)
                    p.actor.EntranceFootsteps(Time.unscaledDeltaTime);
            }
            if (stage == 0)
            {
                view.transform.position = new Vector3(-5, 9.8f, local.player.CourtEnd * 12.2f);
                view.transform.LookAt(Vector3.up * .7f);
                view.fieldOfView = 55;
            }
            else if (stage < 3)
            {
                var group = stage == 1 ? ourTeam : theirTeam;
                Vector3 center = group.Aggregate(Vector3.zero, (s, p) => s + p.actor.transform.position) / group.Length;
                var facing = Quaternion.Euler(0, group[0].player.CourtEnd < 0 ? 0 : 180, 0);
                float t = Mathf.Clamp01((time - (stage == 1 ? .8f : 4.6f)) / 3.8f);
                // Wider framing keeps both doubles teammates visible throughout their walk.
                var position = center + facing * new Vector3(Mathf.Lerp(2.2f, -.4f, t), 2.3f, group.Length > 1 ? 7.2f : 4.7f);
                view.transform.position = stadium ? stadium.KeepCameraInside(position) : position;
                view.transform.LookAt(center + Vector3.up * .85f);
                view.fieldOfView = 48;
            }
            else
            {
                if (follow)
                    follow.SnapToPlayer();
            }
        }
        void EndIntro()
        {
            foreach (var p in actors ?? Array.Empty<ActorPose>())
                p.Restore();
            if (NetworkMatch.Instance && NetworkMatch.Instance.Body)
                NetworkMatch.Instance.Body.gameObject.SetActive(true);
            PlayingIntro = false;
            finishedIntro = true;
            overlay.gameObject.SetActive(false);
            RestoreHud();
            if (follow)
            {
                follow.enabled = followEnabled;
                follow.SnapToPlayer();
            }
        }
        void BeginOutcome(NetworkMatch match)
        {
            var local = NetworkPlayer.All.FirstOrDefault(p => p.IsLocalHuman);
            if (!local || !local.Actor)
                return;
            presented = NetworkPlayer.All.Where(p => p.Team == local.Team && p.Actor).OrderBy(p => p.Index).Select(p => new ActorPose(p, null)).ToArray();
            PlayerWon = match.WinningTeam == local.Team;
            Capture();
            if (!view)
                return;
            originCamera = view.transform.position;
            originRotation = view.transform.rotation;
            originFov = view.fieldOfView;
            ShowingOutcome = true;
            outcomeTime = 0;
            OutcomeReady = false;
            if (match.Body)
                match.Body.gameObject.SetActive(false);
            foreach (var p in presented)
                if (p.actor.paddle)
                    p.actor.paddle.SetActive(false);
            title.gameObject.SetActive(true);
            title.text = PlayerWon ? "Thắng" : "Thua";
            title.enableVertexGradient = true;
            var top = PlayerWon ? new Color(1, .97f, .82f) : new Color(.55f, .57f, .60f);
            var bottom = PlayerWon ? new Color(.88f, .59f, .23f) : new Color(.24f, .27f, .31f);
            title.colorGradient = new VertexGradient(top, top, bottom, bottom);
            sheenMask.gameObject.SetActive(PlayerWon);
            shade.gameObject.SetActive(!PlayerWon);
            caption.text = presented.Length > 1 ? "ĐỘI CỦA BẠN" : "KẾT QUẢ CỦA BẠN";
            next.gameObject.SetActive(false);
        }
        void EvaluateOutcome(float delta)
        {
            outcomeTime += delta;
            float blend = Mathf.SmoothStep(0, 1, Mathf.Clamp01(outcomeTime / 1.25f));
            int end = presented[0].player.CourtEnd;
            var facing = Quaternion.Euler(0, end < 0 ? 0 : 180, 0);
            var anchor = end < 0 ? stadium?.nearResult : stadium?.farResult;
            var center = anchor ? anchor.position : new Vector3(0, 0, end * 4.8f);
            for (int i = 0; i < presented.Length; i++)
            {
                var p = presented[i];
                var stage = center + Vector3.right * (presented.Length == 1 ? 0 : (i == 0 ? -1.15f : 1.15f));
                p.actor.transform.SetPositionAndRotation(Vector3.Lerp(p.destination, stage, blend), Quaternion.Slerp(p.rotation, facing, blend));
                float dance = Mathf.Max(0, outcomeTime - 1.25f);
                AnimationClip clip;
                if (blend < 1)
                    clip = p.Clip("PKV_Idle_Ready");
                else if (!PlayerWon)
                {
                    clip = p.Clip("PKV_Fault_Reaction");
                    dance = clip ? Mathf.Min(dance, clip.length) : 0;
                }
                else if (p.player.Female)
                {
                    clip = femaleWin;
                    dance = clip ? Mathf.Repeat(dance, clip.length) : 0;
                }
                else if (maleSuperSaiyan && dance < maleSuperSaiyan.length)
                    clip = maleSuperSaiyan;
                else
                {
                    clip = maleClap;
                    dance = clip ? Mathf.Repeat(dance - (maleSuperSaiyan ? maleSuperSaiyan.length : 0), clip.length) : 0;
                }
                p.Sample(clip, dance, true);
            }
            float distance = presented.Length > 1 ? 7.2f : 5.8f;
            if (view.aspect < 1.4f)
                distance *= 1.4f / view.aspect;
            // Lift the doubles camera above the net so its top tape cannot cross the result title.
            var target = center + facing * new Vector3(.1f, presented.Length > 1 ? 3.1f : 1.85f, distance);
            var rotation = Quaternion.LookRotation(center + Vector3.up * .65f - target);
            view.transform.SetPositionAndRotation(Vector3.Lerp(originCamera, target, blend), Quaternion.Slerp(originRotation, rotation, blend));
            view.fieldOfView = Mathf.Lerp(originFov, 48, blend);
            if (blend >= 1 && !OutcomeReady)
            {
                OutcomeReady = true;
                next.gameObject.SetActive(true);
                UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(next.gameObject);
            }
            if (PlayerWon)
            {
                float width = title.rectTransform.rect.width, phase = Mathf.Repeat(outcomeTime - 1.25f, 2.4f), x = Mathf.Lerp(-width * .5f - 60, width * .5f + 60, Mathf.Clamp01(phase / .8f));
                sheenMask.gameObject.SetActive(phase < .8f && OutcomeReady);
                sheenMask.anchoredPosition = new Vector2(x, 0);
                sheenText.sizeDelta = title.rectTransform.rect.size;
                sheenText.anchoredPosition = new Vector2(-x, 0);
            }
            else shade.color = new Color(.012f, .018f, .035f, .22f + .025f * Mathf.Sin(outcomeTime * .8f));
            if (ui.resultScreen.IsVisible)
                overlay.SetAsFirstSibling();
        }
        void RestoreHud()
        {
            if (hud != null)
                for (int i = 0; i < hud.Length; i++)
                    if (hud[i])
                        hud[i].SetActive(hudActive[i]);
        }
        void OnDisable()
        {
            foreach (var p in actors ?? Array.Empty<ActorPose>())
                p.Restore();
            foreach (var p in presented ?? Array.Empty<ActorPose>())
                p.Restore();
            if (follow)
                follow.enabled = followEnabled;
            PlayingIntro = ShowingOutcome = false;
        }
    }
}
