using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using PickleVerse.Rules;
using PickleVerse.Settings;
using PickleVerse.Audio;

namespace PickleVerse.LocalMatch
{
    public sealed class TutorialDirector : MonoBehaviour, ISettingsHost, IMatchTouchShotReceiver
    {
        public MatchActor player, coach;
        public Transform ball;
        public TrailRenderer trail;
        public Camera view;
        public TutorialHud hud;
        public LineRenderer courtLine, kitchenNear, kitchenFar;
        public GameObject dimensions, depthLabel;
        public Transform landingRing;
        public TouchControl[] touchControls;
        public int LessonIndex { get; private set; }
        public TutorialLesson Lesson => TutorialCurriculum.Lessons[LessonIndex];
        public float Elapsed { get; private set; }
        public int ShotsCompleted { get; private set; }
        public bool Paused { get; private set; }
        public bool IsGameplay => false;
        public bool IsPaused => Paused;
        public bool CanSignOut => false;
        public int TouchShotMask { get; set; }
        public bool TouchServing => false;
        public bool TouchShotsEnabled => Lesson.kind == TutorialKind.Shot && !Paused && successTimer <= 0 &&
            !SettingsOverlay.IsOpen && !SettingsOverlay.Loading && !SceneLoading.IsBlocking;
        public void CommitTouchShot(int mask)
        {
            if (!TouchShotsEnabled)
                return;
            input.Cancel();
            input.Tick(0, false, false, out _);
            Input(mask);
            Input(mask & 24);
        }
        public Vector2 TouchMovement { get; set; }
        public bool TouchRun { get; set; }
        public string Feedback { get; private set; } = "";
        string LessonKeys => hud && hud.touchRoot && hud.touchRoot.activeSelf ? TouchShotGesture.Hint(Lesson.mask) : GameControls.ShotKeys(Lesson.mask);
        public Fault DemonstratedFault { get; private set; }
        public bool CanContinue => Lesson.kind == TutorialKind.Complete || Lesson.kind == TutorialKind.Court || Lesson.kind == TutorialKind.Kitchen ? Elapsed >= Lesson.minimum :
            Lesson.kind == TutorialKind.Movement ? moved && ran : Lesson.kind == TutorialKind.Shot ? false : demoDone && Elapsed >= Lesson.minimum;
        public bool HasBufferedShot => buffered;
        public float FeedRemaining => flight ? arc.duration - flightTime : 0;
        readonly ShotInput input = new ShotInput();
        SinglesRules rules;
        TutorialFeed arc;
        Vector3 ballPosition, ballVelocity, cameraStart, cameraEnd, lookEnd, movementOrigin;
        Quaternion rotationStart, rotationEnd;
        float startFov, endFov, flightTime, wait, successTimer, runTime;
        bool flight, bounced, returning, buffered, hit, launched, demoDone, moved, ran, netBlocked;
        int receiver;
        int resumeFrame = -1;
        Vector3[] perimeter;
        ShotIntent expected;
        static readonly Color Mint = new Color(.35f, 1, .85f), Warning = new Color(1, .32f, .24f);

        void Awake()
        {
            MatchRenderBudget.ConfigurePractice(gameObject);
            perimeter = new Vector3[5];
            courtLine.GetPositions(perimeter);
            player.Initialize();
            coach.Initialize();
            player.LocalHitFeedback = true;
            PickleVerse.Shop.EquippedPaddle.Attach(player.paddle);
            foreach (var control in touchControls)
                if (control)
                    control.Input = this;
            MobileTouchPad.Install(hud.touchRoot, this);
            hud.Bind(this);
            Begin(0, true);
        }
        void Update()
        {
            if (SettingsOverlay.IsOpen || SettingsOverlay.Loading || Paused || SceneLoading.IsBlocking || resumeFrame == Time.frameCount)
                return;
            if (GameControls.Pressed(GameAction.Settings))
            {
                SettingsOverlay.Open(this);
                return;
            }
            if (GameControls.Pressed(GameAction.Continue) && CanContinue)
                Next();
            Vector2 movement = TouchMovement;
            if (Keyboard.current != null)
            {
                if (GameControls.Held(GameAction.Forward))
                    movement.y++;
                if (GameControls.Held(GameAction.Backward))
                    movement.y--;
                if (GameControls.Held(GameAction.Right))
                    movement.x++;
                if (GameControls.Held(GameAction.Left))
                    movement.x--;
            }
            Advance(Mathf.Min(Time.deltaTime, .05f), GameControls.ShotMask | TouchShotMask, Vector2.ClampMagnitude(movement, 1), GameControls.Held(GameAction.Run) || TouchRun, GameControls.TapMask);
        }
        public void Advance(float dt, int mask = 0, Vector2 movement = default, bool run = false, int tap = 0)
        {
            if (Paused || dt <= 0)
                return;
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt * 60));
            float step = dt / steps;
            int lesson = LessonIndex;
            for (int i = 0; i < steps; i++)
            {
                StepLesson(step, mask, movement, run, i == 0 ? tap : 0);
                if (LessonIndex != lesson)
                    break;
            }
            RenderLesson();
            hud.Refresh();
            CameraStep();
        }
        void StepLesson(float dt, int mask, Vector2 movement, bool run, int tap)
        {
            if (Paused || dt <= 0)
                return;
            Elapsed += dt;
            if (Lesson.kind == TutorialKind.Complete)
            {
                player.Step(Vector2.zero, false, dt, false);
                coach.Step(Vector2.zero, false, dt, false);
                return;
            }
            if (Lesson.kind == TutorialKind.Movement)
            {
                player.Step(movement, run, dt, true, MatchMovement.PracticeBounds(player.end));
                coach.Step(Vector2.zero, false, dt, false);
                moved |= Vector3.Distance(player.transform.position, movementOrigin) > .55f;
                if (player.Velocity.sqrMagnitude > .25f && run)
                    runTime += dt;
                ran |= runTime > .18f;
                Feedback = !moved ? "Di chuyển một đoạn ngắn trong sân." : !ran ? "Đúng rồi. Bây giờ thử chạy một đoạn." : "Đã biết di chuyển và chạy. Tiếp tục để tập đánh bóng.";
                if (movement.sqrMagnitude > .1f && player.Velocity.sqrMagnitude < .01f)
                    Feedback = moved && ran ? "Đã tới giới hạn sân. Thả phím hoặc đổi hướng; bạn có thể tiếp tục." : "Đã tới giới hạn sân. Đổi hướng để tiếp tục thử di chuyển và chạy.";
            }
            else
            {
                Vector2 coachMove = Vector2.zero, playerMove = Lesson.kind == TutorialKind.Shot ? movement : Vector2.zero;
                if (Lesson.kind == TutorialKind.Serve && launched && receiver == 0 && !demoDone)
                    coachMove = new Vector2(0, -Mathf.Clamp01((coach.transform.position.z - 5.55f) * 4));
                if (Lesson.kind == TutorialKind.KitchenVolley && Elapsed > .65f && !demoDone)
                    playerMove = new Vector2(0, Mathf.Clamp01((-1.25f - player.transform.position.z) * 4));
                player.Step(playerMove, Lesson.kind == TutorialKind.Shot && run, dt, Lesson.kind == TutorialKind.Shot || playerMove != Vector2.zero, MatchMovement.PracticeBounds(player.end));
                coach.Step(coachMove, false, dt, coachMove != Vector2.zero, MatchMovement.PracticeBounds(coach.end));
                if (Lesson.kind == TutorialKind.Shot)
                {
                    if (tap != 0)
                        Input(mask | tap);
                    Input(mask);
                    if (successTimer > 0)
                    {
                        successTimer -= dt;
                        if (successTimer <= 0)
                        {
                            ShotsCompleted++;
                            Begin(LessonIndex + 1);
                            return;
                        }
                    }
                }
                if (Lesson.kind != TutorialKind.Court && Lesson.kind != TutorialKind.Kitchen)
                    Simulation(dt);
            }
        }
        void RenderLesson()
        {
            if (courtLine.gameObject.activeSelf)
            {
                float pulse = .055f + Mathf.Sin(Elapsed * 2) * .008f;
                courtLine.widthMultiplier = pulse;
                float drawing = Lesson.kind == TutorialKind.Court ? Mathf.Clamp01(Elapsed / 2.7f) * 4 : 4;
                for (int i = 0; i < 5; i++)
                {
                    int corner = Mathf.Min(i, Mathf.FloorToInt(drawing));
                    courtLine.SetPosition(i, corner < 4 && i > corner ? Vector3.Lerp(perimeter[corner], perimeter[corner + 1], drawing - corner) : perimeter[corner]);
                }
            }
        }
        void Input(int mask)
        {
            if (successTimer > 0 || !input.Tick(mask, GameControls.Pressed(GameAction.CancelShot), false, out var shot))
                return;
            if (!TutorialCurriculum.Matches(Lesson.mask, input.CommittedMask, shot))
            {
                Feedback = "Bài này cần " + LessonKeys + ". Giữ lựa chọn đến khi thả nút đánh.";
                return;
            }
            buffered = true;
            Feedback = "Đã chọn đúng cú đánh. Đón bóng đang tới.";
        }
        void Simulation(float dt)
        {
            if (demoDone && !flight && !returning)
                return;
            wait -= dt;
            if (!launched && !coach.Swinging && wait <= 0)
            {
                if (Lesson.kind == TutorialKind.Serve)
                    coach.Swing(new ShotIntent(ShotKind.Serve), player.transform.position);
                else coach.Swing(new ShotIntent(ShotKind.Drive), player.transform.position);
            }
            if (!launched && coach.Swinging && coach.ContactActive && coach.SwingTime >= coach.ContactDelay)
            {
                coach.ConsumeContact(false);
                launched = true;
                Vector3 target = Contact(player, expected);
                if (Lesson.kind == TutorialKind.KitchenVolley)
                    target = new Vector3(player.transform.position.x, .75f, -.70f);
                arc = TutorialFeed.Create(coach.paddleFace.position, target, expected.AfterBounce || Lesson.kind == TutorialKind.DoubleBounce || Lesson.kind == TutorialKind.Serve);
                flight = true;
                flightTime = 0;
                bounced = false;
                receiver = 0;
                ball.gameObject.SetActive(true);
                ballPosition = arc.origin;
                SetTrail(true);
                if (Lesson.kind == TutorialKind.Serve)
                    rules.Serve(1, coach.Feet(true), coach.Feet(false), Point(ballPosition), 1);
                else rules = TutorialCurriculum.RallyReady();
                landingRing.position = new Vector3(arc.hasBounce ? arc.bouncePoint.x : target.x, .028f, arc.hasBounce ? arc.bouncePoint.z : target.z);
                landingRing.gameObject.SetActive(true);
            }
            if (!launched && coach.Swinging)
            {
                ball.gameObject.SetActive(true);
                ballPosition = coach.HeldBallPosition;
            }
            if (flight)
            {
                Vector3 previousBall = ballPosition;
                flightTime += dt;
                var actor = receiver == 0 ? player : coach;
                if (arc.hasBounce && !bounced && flightTime >= arc.bounceTime)
                {
                    bounced = true;
                    rules.Bounce(Point(arc.bouncePoint));
                    if (Lesson.kind == TutorialKind.Serve)
                        Feedback = receiver == 0 ? "NẢY 1 • Bên nhận đã chờ bóng nảy." : "NẢY 2 • Bên giao cũng phải chờ bóng nảy.";
                }
                ballPosition = arc.At(flightTime);
                bool automatic = Lesson.kind != TutorialKind.Shot && Lesson.kind != TutorialKind.DoubleBounce;
                ShotIntent intent = receiver == 1 ? new ShotIntent(ShotKind.Drive) : expected;
                if ((automatic || buffered) && !actor.Swinging && !hit && arc.duration - flightTime <= intent.ContactDelay + .012f)
                    actor.Swing(intent, arc.At(arc.duration));
                if (!hit && Mathf.Abs(flightTime - arc.duration) < .13f && actor.CanContact(previousBall, ballPosition, PaddleContact.MaxAssist, out _))
                {
                    bool volley = !bounced;
                    if ((intent.AfterBounce && bounced || intent.BeforeBounce && !bounced) && (automatic || buffered))
                    {
                        ballPosition = actor.ContactPoint;
                        var fault = rules.Hit(actor.playerId, Point(ballPosition), actor.InKitchen);
                        actor.ConsumeContact(volley, ballPosition, fault == Fault.None);
                        hit = true;
                        flight = false;
                        if (fault != Fault.None)
                        {
                            FaultFeedback(fault);
                            ballVelocity = Vector3.down;
                            returning = true;
                        }
                        else if (Lesson.kind == TutorialKind.Serve && receiver == 0)
                        {
                            arc = TutorialFeed.Create(actor.paddleFace.position, Contact(coach, new ShotIntent(ShotKind.Drive)), true);
                            flight = true;
                            flightTime = 0;
                            bounced = false;
                            receiver = 1;
                            hit = false;
                        }
                        else if (Lesson.kind == TutorialKind.Serve)
                        {
                            demoDone = true;
                            Feedback = "HAI LẦN NẢY ĐÃ XONG • Từ đây có thể vô lê ngoài vùng cấm vô lê.";
                            returning = true;
                            ballVelocity = ShotTrajectory.Launch(intent, ballPosition, new Vector3(1.2f, Court.BallRadius, -4));
                        }
                        else if (Lesson.kind == TutorialKind.Shot)
                        {
                            Feedback = "ĐÚNG RỒI • " + LessonKeys;
                            successTimer = 1.8f;
                            returning = true;
                            ballVelocity = ShotTrajectory.Launch(intent, ballPosition, ShotTrajectory.Target(actor, intent, ballPosition, actor.transform.position.x));
                        }
                        else
                        {
                            returning = true;
                            if (Lesson.kind == TutorialKind.Net)
                            {
                                Vector3 target = new Vector3(ballPosition.x, .45f, 0);
                                float time = .85f;
                                ballVelocity = (target - ballPosition) / time;
                                ballVelocity.y += .5f * BallFlight.Gravity * time;
                            }
                            else ballVelocity = ShotTrajectory.Launch(intent, ballPosition, new Vector3(4.1f, Court.BallRadius, 4));
                        }
                    }
                }
                if (flight && flightTime > arc.duration + .32f && Lesson.kind == TutorialKind.Shot)
                {
                    flight = false;
                    returning = true;
                    ballVelocity = arc.rebound;
                    Feedback = "Chưa trúng bóng. Thử lại với " + LessonKeys + "; có thể bấm sớm khi bóng đang tới.";
                }
                if (flight && ballPosition.y <= Court.BallRadius && bounced && flightTime > arc.bounceTime + .1f)
                {
                    ballPosition.y = Court.BallRadius;
                    var fault = rules.Bounce(Point(ballPosition));
                    flight = false;
                    returning = false;
                    SetTrail(false);
                    if (Lesson.kind == TutorialKind.DoubleBounce)
                        FaultFeedback(fault);
                    else if (Lesson.kind == TutorialKind.Shot)
                        ResetFeed();
                }
            }
            else if (returning)
            {
                var before = ballPosition;
                BallFlight.Integrate(ref ballPosition, ref ballVelocity, dt);
                if (Lesson.kind == TutorialKind.Net && !netBlocked && before.z < 0 && ballPosition.z >= 0)
                {
                    netBlocked = true;
                    ballPosition.z = -.06f;
                    ballVelocity = new Vector3(0, -.7f, -.6f);
                    FaultFeedback(Fault.OwnCourt);
                }
                if (ballPosition.y <= Court.BallRadius)
                {
                    ballPosition.y = Court.BallRadius;
                    returning = false;
                    SetTrail(false);
                    if (Lesson.kind == TutorialKind.Out)
                        FaultFeedback(rules.Bounce(Point(ballPosition)));
                    else if (Lesson.kind == TutorialKind.Shot && successTimer <= 0)
                        ResetFeed();
                }
            }
            ball.position = ballPosition;
        }
        void ResetFeed()
        {
            flight = returning = launched = hit = bounced = buffered = false;
            wait = .75f;
            input.Cancel();
            player.ResetAt(player.transform.position, -1);
            coach.ResetAt(coach.transform.position, 1);
        }
        void FaultFeedback(Fault fault)
        {
            DemonstratedFault = fault;
            demoDone = true;
            SfxPlayer.Play(GameSfx.FaultWhistle);
            player.Reaction(false);
            coach.Reaction(true);
            Feedback = Lesson.kind == TutorialKind.Net ? "LỖI • BÓNG KHÔNG QUA LƯỚI" : fault == Fault.Out ? "OUT • BÓNG NGOÀI BIÊN" : fault == Fault.DoubleBounce ? "LỖI • BÓNG NẢY HAI LẦN" : "LỖI • VOLLEY TRONG KITCHEN";
            if (Lesson.kind == TutorialKind.KitchenVolley)
            {
                SetLineColor(kitchenNear, Warning);
                SetLineColor(kitchenFar, Warning);
            }
            hud.Refresh();
        }
        void Begin(int index, bool snap = false)
        {
            LessonIndex = index;
            Elapsed = 0;
            wait = .65f;
            successTimer = runTime = 0;
            Feedback = "";
            DemonstratedFault = Fault.None;
            flight = returning = buffered = hit = launched = bounced = demoDone = moved = ran = netBlocked = false;
            TouchShotMask = 0;
            TouchMovement = Vector2.zero;
            TouchRun = false;
            input.Cancel();
            expected = Lesson.kind == TutorialKind.Shot ? TutorialCurriculum.Intent(Lesson.mask) : Lesson.kind == TutorialKind.KitchenVolley ? new ShotIntent(ShotKind.Volley) : new ShotIntent(ShotKind.Drive);
            float depth = Lesson.kind == TutorialKind.Shot ? (expected.kind == ShotKind.Drive ? -4.9f : -3.1f) : Lesson.kind == TutorialKind.KitchenVolley ? -2.8f : Lesson.kind == TutorialKind.DoubleBounce ? -3.4f : -5.0f;
            player.ResetAt(new Vector3(1.1f, 0, depth), -1);
            coach.ResetAt(new Vector3(-1.25f, 0, Lesson.kind == TutorialKind.Serve ? 7.3f : 4.2f), 1);
            player.RefreshPose();
            coach.RefreshPose();
            movementOrigin = player.transform.position;
            rules = Lesson.kind == TutorialKind.Serve ? new SinglesRules(1) : TutorialCurriculum.RallyReady();
            if (Lesson.kind == TutorialKind.Serve)
                rules.PrepareServe(1);
            ballPosition = coach.HeldBallPosition;
            ball.gameObject.SetActive(false);
            landingRing.gameObject.SetActive(false);
            SetTrail(false);
            bool kitchen = Lesson.kind == TutorialKind.Kitchen || Lesson.kind == TutorialKind.KitchenVolley;
            courtLine.gameObject.SetActive(Lesson.kind == TutorialKind.Court || Lesson.kind == TutorialKind.Out);
            kitchenNear.gameObject.SetActive(kitchen);
            kitchenFar.gameObject.SetActive(kitchen);
            dimensions.SetActive(Lesson.kind == TutorialKind.Court);
            depthLabel.SetActive(Lesson.kind == TutorialKind.Kitchen);
            SetLineColor(kitchenNear, Mint);
            SetLineColor(kitchenFar, Mint);
            SetCameraTarget();
            cameraStart = view.transform.position;
            rotationStart = view.transform.rotation;
            startFov = view.fieldOfView;
            if (snap)
            {
                view.transform.SetPositionAndRotation(cameraEnd, rotationEnd);
                view.fieldOfView = endFov;
                cameraStart = cameraEnd;
                rotationStart = rotationEnd;
                startFov = endFov;
            }
            if (Lesson.kind == TutorialKind.Complete)
            {
                if (ShotsCompleted == TutorialCurriculum.ShotCount)
                    tutorialSave = SaveTutorialAsync(false);
                player.Reaction(true, true);
            }
            hud.ShowLesson();
        }
        void SetCameraTarget()
        {
            endFov = 52;
            lookEnd = new Vector3(0, .2f, 0);
            switch (Lesson.kind)
            {
                case TutorialKind.Court: cameraEnd = new Vector3(-7.1f, 13, -11.4f); endFov = 52; break;
                case TutorialKind.Kitchen: cameraEnd = new Vector3(-3.8f, 7, -5.0f); endFov = 48; break;
                case TutorialKind.Serve: cameraEnd = new Vector3(-7.8f, 9, -1.5f); endFov = 60; break;
                case TutorialKind.Net: cameraEnd = new Vector3(-4.8f, 4.2f, -6); lookEnd = new Vector3(.3f, .5f, -1.6f); break;
                case TutorialKind.Out: cameraEnd = new Vector3(6.5f, 10, -7); lookEnd = new Vector3(1.2f, 0, 1.8f); break;
                case TutorialKind.DoubleBounce: cameraEnd = new Vector3(-2.5f, 5, -8); lookEnd = new Vector3(1, .2f, -3.9f); endFov = 48; break;
                case TutorialKind.KitchenVolley: cameraEnd = new Vector3(-3.4f, 5, -4.8f); lookEnd = new Vector3(1, .5f, -.5f); endFov = 48; break;
                case TutorialKind.Movement: cameraEnd = new Vector3(0, 5.3f, -10.8f); lookEnd = new Vector3(0, .5f, -.8f); break;
                case TutorialKind.Shot:
                    cameraEnd = new Vector3(player.transform.position.x - .65f + (Lesson.mask & 16) / 32f + (LessonIndex - TutorialCurriculum.FirstShot) * .10f, expected.kind == ShotKind.Drive ? 5.3f : 4.7f, player.transform.position.z - 4.25f);
                    lookEnd = new Vector3(.65f, .7f, expected.kind == ShotKind.Drive ? -.5f : .3f); endFov = 56 + (Lesson.mask & 8); break;
                default: cameraEnd = new Vector3(-1.4f, 3.2f, -8); lookEnd = player.transform.position + Vector3.up * .85f; endFov = 50; break;
            }
            rotationEnd = Quaternion.LookRotation(lookEnd - cameraEnd);
        }
        void CameraStep()
        {
            float t = Mathf.SmoothStep(0, 1, Elapsed / 1.1f);
            Vector3 drift = Vector3.zero;
            if (Lesson.kind == TutorialKind.Court)
                drift = new Vector3(Mathf.Sin(Elapsed * .18f) * .65f, 0, Mathf.Cos(Elapsed * .18f) * .3f);
            else if (Lesson.kind == TutorialKind.Kitchen)
                drift = new Vector3(Mathf.Sin(Elapsed * .16f) * .22f, 0, 0);
            else if (Lesson.kind == TutorialKind.Shot || Lesson.kind == TutorialKind.Movement)
                drift = new Vector3(Mathf.Sin(Elapsed * .16f) * .06f, 0, 0);
            view.transform.SetPositionAndRotation(Vector3.Lerp(cameraStart, cameraEnd + drift, t), Quaternion.Slerp(rotationStart, rotationEnd, t));
            view.fieldOfView = Mathf.Lerp(startFov, endFov, t);
        }
        void SetTrail(bool emit)
        {
            if (!trail)
                return;
            trail.emitting = emit;
            trail.Clear();
        }
        static Vector3 Contact(MatchActor actor, ShotIntent intent) => actor.transform.TransformPoint(actor.ContactOffset(intent));
        static CourtPoint Point(Vector3 p) => new CourtPoint(p.x, p.y, p.z);
        static void SetLineColor(LineRenderer line, Color color)
        {
            line.startColor = line.endColor = color;
        }
        System.Threading.Tasks.Task tutorialSave = System.Threading.Tasks.Task.CompletedTask;
        string tutorialSaveError;
        async System.Threading.Tasks.Task SaveTutorialAsync(bool skip)
        {
            if (this)
                Feedback = "Đang lưu tiến trình trực tuyến…";
            try
            {
                if (skip)
                    await TutorialProgress.SkipAsync();
                else await TutorialProgress.CompleteAsync();
                tutorialSaveError = null;
                if (this)
                    Feedback = "Đã lưu tiến trình trực tuyến.";
            }
            catch (Exception error)
            {
                tutorialSaveError = error.Message;
                if (this)
                    Feedback = "Chưa lưu được: " + error.Message + " • Bấm tiếp tục để thử lại.";
                Debug.LogWarning("Chưa lưu tiến trình hướng dẫn: " + error.Message);
            }
        }
        public async void Next()
        {
            if (!CanContinue)
                return;
            if (Lesson.kind != TutorialKind.Complete)
            {
                Begin(LessonIndex + 1);
                return;
            }
            await tutorialSave;
            if (tutorialSaveError != null)
            {
                tutorialSave = SaveTutorialAsync(false);
                await tutorialSave;
            }
            if (this && tutorialSaveError == null)
                SceneManager.LoadScene("ModeSelection");
        }
        public void Replay()
        {
            Begin(LessonIndex);
        }
        public async void ConfirmSkip()
        {
            tutorialSave = SaveTutorialAsync(true);
            await tutorialSave;
            if (this && tutorialSaveError == null)
                SceneManager.LoadScene("ModeSelection");
        }
        public void SetPaused(bool paused)
        {
            if (Paused && !paused)
                resumeFrame = Time.frameCount;
            Paused = paused;
            TouchShotMask = 0;
            TouchMovement = Vector2.zero;
            TouchRun = false;
            input.Cancel();
            if (paused)
                SfxPlayer.StopAll();
        }
        public async void LeaveToMenu()
        {
            await tutorialSave;
            if (this)
                SceneManager.LoadScene("Menu");
        }
        public System.Threading.Tasks.Task SignOutAsync() => System.Threading.Tasks.Task.CompletedTask;
    }
}
