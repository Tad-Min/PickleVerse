using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using PickleVerse.Rules;
using PickleVerse.Settings;
using PickleVerse.Audio;

namespace PickleVerse.LocalMatch
{
    public enum MatchPhase { Intro, PreServe, Rally, Settling, BetweenPoints, BetweenGames, Timeout, MatchOver, Cinematic }
    public sealed partial class BotMatch : MonoBehaviour, ISettingsHost, IMatchTouchShotReceiver
    {
        public MatchActor human, bot;
        public Transform ball, ballShadow, landingMarker, aimMarker;
        public TrailRenderer ballTrail;
        public MatchHud hud;
        public BotMatchIntro opening;
        public BotMatchVictory victory;
        [Range(0, 3)]
        public int botDifficulty = 1;
        BhTreeBot behaviorBot;
        public string BotLabel => botDifficulty == 3 ? "BhTree · " + (behaviorBot ? $"{behaviorBot.Elo} Elo\nPhản xạ {behaviorBot.ReactionSeconds:0.00}s · Sai số {behaviorBot.AimError:0.00}m\n{behaviorBot.Branch}" : "đang chuẩn bị") : new[] { "Dễ", "Vừa", "Khó" }[botDifficulty];
        [Range(.12f, .6f)]
        public float contactAssistRadius = .45f;
        public bool autoPlayer;
        public SinglesRules Rules { get; private set; }
        public MatchPhase Phase { get; private set; } = MatchPhase.Intro;
        public Vector3 BallPosition { get; private set; }
        public Vector3 BallVelocity { get; private set; }
        public float ServeElapsed { get; private set; }
        public float TimeoutRemaining => Phase == MatchPhase.Timeout ? Mathf.Max(0, 60 - phaseTime) : 0;
        public string Message { get; private set; } = "Đấu đơn • Thắng 2/3 ván • 11 điểm, cách 2";
        public string ShotFeedback { get; private set; } = "";
        public string Selection => input.Selection;
        public string SelectionKeys => GameControls.ShotKeys(input.SelectionMask);
        public string ActiveShotKeys { get; private set; } = "";
        public bool Paused { get; private set; }
        public int TotalHits { get; private set; }
        public int TotalRallies { get; private set; }
        public int LongestRally { get; private set; }
        public readonly List<string> RallyLog = new List<string>();
        readonly ShotInput input = new ShotInput();
        readonly LocalBot opponent = new LocalBot(731), testPlayer = new LocalBot(197);
        float accumulator, phaseTime;
        int initialServer;
        bool releasedServe, focused = true;
        readonly ServeCountdownCue countdown = new ServeCountdownCue();
        Vector3 serveOrigin;
        public Vector2 TouchMovement { get; set; }
        public bool TouchRun { get; set; }
        public int TouchShotMask { get; set; }
        public bool TouchServing => Phase == MatchPhase.PreServe && Rules != null && Rules.Server == 0;
        public bool TouchShotsEnabled => !Paused && !SettingsOverlay.IsOpen && !SettingsOverlay.Loading && !SceneLoading.IsBlocking &&
            (Phase == MatchPhase.Rally || TouchServing);
        public void CommitTouchShot(int mask)
        {
            if (!TouchShotsEnabled)
                return;
            input.Cancel();
            input.Tick(0, false, TouchServing, out _);
            TickInput(mask);
            TickInput(mask & 24);
        }
        public MatchActor Actor(int id) => id == 0 ? human : bot;

        void Awake()
        {
            InitializeMatch();
        }
        async void Start()
        {
            try
            {
                await beginningProgression;
            }
            catch
            {
                return;
            }
            if (!this)
                return;
            if (BotMatchLaunchSettings.ConsumeLaunch() || !GetComponent<MatchEnvironmentLighting>())
                if (!opening || !opening.Play())
                    PreparePoint();
        }
        void OnEnable()
        {
            if (Application.isPlaying && Rules == null)
                InitializeMatch();
        }
        void InitializeMatch()
        {
            if (!human || !bot)
                return;
            botDifficulty = BotMatchLaunchSettings.Difficulty;
            TotalHits = TotalRallies = LongestRally = 0;
            RallyLog.Clear();
            Paused = false;
            human.end = -1;
            bot.end = 1;
            human.Initialize();
            bot.Initialize();
            human.LocalHitFeedback = true;
            if (human.paddle)
                PickleVerse.Shop.EquippedPaddle.Attach(human.paddle);
            initialServer = UnityEngine.Random.Range(0, 2);
            Rules = new SinglesRules(initialServer);
            PreparePoint();
            Phase = MatchPhase.Intro;
            if (hud)
                hud.Bind(this);
            BeginProgressionMatch();
        }
        void Update()
        {
            if (Application.isPlaying && (startingProgression || progressionResult == null))
                return;
            if (SettingsOverlay.IsOpen || SettingsOverlay.Loading || SceneLoading.IsBlocking)
                return;
            var keyboard = Keyboard.current;
            if (focused && GameControls.Pressed(GameAction.Settings))
            {
                SettingsOverlay.Open(this);
                return;
            }
            if (Phase == MatchPhase.Cinematic)
            {
                if (focused && keyboard != null && (GameControls.Pressed(GameAction.Continue) || keyboard.spaceKey.wasPressedThisFrame))
                    opening.Skip();
                return;
            }
            if (Phase == MatchPhase.MatchOver)
                return;
            if (keyboard != null && focused)
            {
                if (GameControls.Pressed(GameAction.Continue))
                    Continue();
                if (GameControls.Pressed(GameAction.Timeout))
                    Timeout();
                if (GameControls.Pressed(GameAction.Help) && hud)
                    hud.ToggleHelp();
            }
            if (Paused || !focused)
                return;
            Vector2 move = TouchMovement;
            bool run = TouchRun;
            int mask = TouchShotMask;
            int tapMask = 0;
            if (keyboard != null)
            {
                move += new Vector2((GameControls.Held(GameAction.Right) ? 1 : 0) - (GameControls.Held(GameAction.Left) ? 1 : 0), (GameControls.Held(GameAction.Forward) ? 1 : 0) - (GameControls.Held(GameAction.Backward) ? 1 : 0));
                run |= GameControls.Held(GameAction.Run);
                mask |= GameControls.ShotMask;
                // Preserve a tap whose down/up both arrived between two rendered frames.
                tapMask = GameControls.TapMask;
            }
            // WASD/touch forward always points into the court after an end change.
            move = Vector2.ClampMagnitude(move, 1);
            move *= -human.end;
            bool cancel = GameControls.Pressed(GameAction.CancelShot);
            if (!autoPlayer)
                human.ObserveMovement(move, run);
            if (tapMask != 0)
                TickInput(mask | tapMask, cancel);
            TickInput(mask, cancel);
            accumulator += Mathf.Min(Time.deltaTime, .20f);
            const float tick = 1f / 120f;
            while (accumulator >= tick)
            {
                StepSimulation(tick, move, run);
                accumulator -= tick;
            }
            UpdateVisuals();
        }
        public void TickInput(int mask, bool cancel = false)
        {
            bool serve = Phase == MatchPhase.PreServe && Rules.Server == 0;
            if (Phase != MatchPhase.Rally && !serve)
            {
                input.Cancel();
                input.Tick(mask, false, serve, out _);
                return;
            }
            if (input.Tick(mask, cancel, serve, out var shot) && RequestShot(0, shot))
                ActiveShotKeys = GameControls.ShotKeys(input.CommittedMask);
            if (!string.IsNullOrEmpty(input.Feedback) && ShotFeedback != input.Feedback)
            {
                ShotFeedback = input.Feedback;
                hud?.announcements?.Enqueue("TỔ HỢP KHÔNG HỢP LỆ", input.Feedback, AnnouncementTone.Fault);
            }
        }
        public bool RequestShot(int id, ShotIntent intent)
        {
            bool serving = Phase == MatchPhase.PreServe && id == Rules.Server;
            if ((Phase != MatchPhase.Rally && !serving) || Paused || Actor(id).Swinging)
                return false;
            if (serving != (intent.kind == ShotKind.Serve))
                return false;
            float contactDelay = PaddleContact.Anticipation(Actor(id), intent, BallPosition, BallVelocity, out var predicted);
            Actor(id).GetComponent<ActorTelemetry>()?.Context(Rules.Stroke,TotalRallies,Rules.Bounces);
            bool began = Actor(id).Swing(intent, predicted, contactDelay);
            if (began && serving)
            {
                releasedServe = false;
                serveOrigin = Actor(id).transform.position + Actor(id).transform.right * .3f + Actor(id).transform.forward * .5f + Vector3.up * .94f;
            }
            if (began && id == 0)
            {
                ShotFeedback = intent + " • thả sớm để vợt đến bóng";
                int keys = intent.kind == ShotKind.Drop ? 3 : intent.kind == ShotKind.Smash ? 5 : intent.kind == ShotKind.Reset ? 6 : intent.kind == ShotKind.Dink ? 2 : intent.kind == ShotKind.Volley ? 4 : 1;
                ActiveShotKeys = GameControls.ShotKeys(keys | (intent.power ? 8 : 0) | (intent.cross ? 16 : 0));
            }
            return began;
        }
        public void StepSimulation(float dt, Vector2 humanMovement, bool running)
        {
            if (Paused || Phase == MatchPhase.Intro || Phase == MatchPhase.Cinematic || Phase == MatchPhase.MatchOver)
                return;
            phaseTime += dt;
            opponent.difficulty = botDifficulty;
            testPlayer.difficulty = 2;
            Vector2 botMove;
            bool botRun;
            if (botDifficulty == 3 && behaviorBot)
                behaviorBot.Step(dt, out botMove, out botRun);
            else opponent.Step(this, bot, dt, out botMove, out botRun);
            if (autoPlayer)
                testPlayer.Step(this, human, dt, out humanMovement, out running);
            bool motion = Phase == MatchPhase.PreServe || Phase == MatchPhase.Rally || Phase == MatchPhase.Settling;
            human.Step(humanMovement, running, dt, motion);
            bot.Step(botMove, botRun, dt, motion);
            CheckMomentum();
            if (Phase == MatchPhase.PreServe)
            {
                ServeElapsed += dt;
                countdown.Step(ServeElapsed);
                var server = Actor(Rules.Server);
                var previousServePosition = BallPosition;
                var servePosition = BallPosition;
                var serveVelocity = BallVelocity;
                if (BotMissedContact(server))
                    BallFlight.Integrate(ref servePosition, ref serveVelocity, dt);
                else ServeBallMotion.Step(server, ref releasedServe, ref serveOrigin, ref servePosition, ref serveVelocity);
                BallPosition = servePosition;
                BallVelocity = serveVelocity;
                if (BotMissedContact(server) && BallPosition.y <= Court.BallRadius)
                {
                    Rules.Fail(Rules.Server, Fault.ServeMiss);
                    BeginFault();
                }
                if (Phase == MatchPhase.PreServe && server.Swinging && server.Intent.kind == ShotKind.Serve)
                {
                    if (server.SwingTime >= .8f && server.CanContact(previousServePosition, BallPosition, contactAssistRadius, out _) && TryRankedBotContact(server))
                    {
                        // This is the authored underhand motion: no spin/toss imparted on release.
                        BallPosition = new Vector3(serveOrigin.x, BallPosition.y, serveOrigin.z);
                        var f = Rules.Serve(Rules.Server, server.Feet(true), server.Feet(false), ToPoint(BallPosition), ServeElapsed, server.PaddleVelocity.y > 0, server.PaddleHeadTop <= server.hand.position.y + .015f);
                        if (f != Fault.None)
                            BeginFault();
                        else
                        {
                            server.ConsumeContact(false, BallPosition);
                            Launch(server, server.Intent);
                            Phase = MatchPhase.Rally;
                            phaseTime = 0;
                            Message = "Nhận giao bóng: phải để bóng nảy";
                        }
                    }
                }
                if (ServeElapsed > 10f && Phase == MatchPhase.PreServe)
                {
                    Rules.Fail(Rules.Server, Fault.ServeTimeout);
                    BeginFault();
                }
            }
            else if (Phase == MatchPhase.Rally)
            {
                Vector3 prior = BallPosition, p = BallPosition, v = BallVelocity;
                BallFlight.Integrate(ref p, ref v, dt);
                // Thin net uses a swept plane test; high balls and legal around-post shots pass.
                if (prior.z * p.z < 0 && Mathf.Abs(p.x) <= Court.NetHalfSpan)
                {
                    float t = prior.z / (prior.z - p.z);
                    Vector3 crossing = Vector3.Lerp(prior, p, t);
                    float netTop = Court.NetHeight(crossing.x);
                    if (Rules.LastHitter == 0) human.GetComponent<ActorTelemetry>()?.NetClearance(crossing.y-Court.BallRadius-netTop);
                    if (crossing.y - Court.BallRadius <= netTop)
                    {
                        // A tape graze may continue across; touching the net is not a let.
                        if (crossing.y >= netTop - .01f)
                        {
                            v.z *= .6f;
                            v.x *= .6f;
                            v.y *= .5f;
                        }
                        else
                        {
                            p.z = Mathf.Sign(prior.z) * .04f;
                            v.z *= -.16f;
                            v.x *= .45f;
                            v.y *= .35f;
                        }
                    }
                }
                BallPosition = p;
                BallVelocity = v;
                TryContact(human, prior);
                if (Rules.Live)
                    TryContact(bot, prior);
                if (Rules.Live && BallPosition.y <= Court.BallRadius)
                {
                    var bounce = BallPosition;
                    bounce.y = Court.BallRadius;
                    if (Rules.LastHitter == 0) human.GetComponent<ActorTelemetry>()?.Landing(bounce);
                    Rules.Bounce(ToPoint(bounce));
                    if (!Rules.Live)
                        BeginFault();
                    else
                    {
                        progressStrokes.AcceptFlight();
                        p = BallPosition;
                        v = BallVelocity;
                        BallFlight.Rebound(ref p, ref v);
                        BallPosition = p;
                        BallVelocity = v;
                        Message = Rules.Stroke == 1 ? "Trả bóng sau nảy • " + GameControls.ShotKeys(1) + " / " + GameControls.ShotKeys(2) + " / " + GameControls.ShotKeys(3) : Rules.Stroke == 2 ? "Cú thứ ba: bên giao phải để bóng nảy" : "Bóng đã nảy • được đánh trong vùng cấm vô lê";
                    }
                }
                if (Rules.Live)
                {
                    // A body contact is separate from paddle contact; never grants an automatic hit.
                    foreach (var actor in new[] { human, bot })
                    {
                        if (Rules.ExpectedHitter != actor.playerId)
                            continue;
                        Vector3 body = actor.transform.position + Vector3.up * .85f;
                        if (BallPosition.y > .25f && BallPosition.y < 1.65f && DistanceSegment(prior, BallPosition, body) < .24f)
                        {
                            Rules.Fail(actor.playerId, Fault.BodyHit);
                            BeginFault();
                            break;
                        }
                    }
                    if (Mathf.Abs(BallPosition.x) > 7f || Mathf.Abs(BallPosition.z) > 12f)
                    {
                        Rules.Fail(Rules.Bounces > 0 ? Rules.ExpectedHitter : Rules.LastHitter, Fault.PermanentObject);
                        BeginFault();
                    }
                }
            }
            else if (Phase == MatchPhase.Settling)
            {
                if (!human.VolleyMomentum && !bot.VolleyMomentum)
                    FinishRally();
            }
            else if (Phase == MatchPhase.BetweenPoints && phaseTime >= 2.1f && !AnnouncementBusy)
                PreparePoint();
            else if (Phase == MatchPhase.BetweenGames && !Announcements && phaseTime >= 3.4f)
                Continue();
            else if (Phase == MatchPhase.Timeout && phaseTime >= 60f)
                Continue();
        }
        void TryContact(MatchActor actor, Vector3 previous)
        {
            if (!actor.ContactActive || Rules.ExpectedHitter != actor.playerId || BallPosition.z * actor.end <= .05f)
                return;
            ShotIntent shot = actor.Intent;
            if (shot.AfterBounce && Rules.Bounces == 0 || shot.BeforeBounce && Rules.Bounces != 0)
                return;
            if (shot.kind == ShotKind.Smash && BallPosition.y < 1.65f)
                return;
            // Swept paddle and front assist hitboxes share the same swing clock.
            if (!actor.CanContact(previous, BallPosition, contactAssistRadius, out float distance))
                return;
            if (!TryRankedBotContact(actor))
                return;
            BallPosition = actor.ContactPoint;
            bool volley = Rules.Bounces == 0;
            var result = Rules.Hit(actor.playerId, ToPoint(BallPosition), actor.InKitchen);
            actor.ConsumeContact(volley, BallPosition, result == Fault.None);
            if (result != Fault.None)
            {
                BeginFault();
                return;
            }
            progressStrokes.AcceptFlight();
            Launch(actor, shot);
            TotalHits++;
            LongestRally = Mathf.Max(LongestRally, Rules.Stroke);
            Actor(1 - actor.playerId).SplitStep();
            Message = Rules.Stroke <= 2 ? "Cú thứ ba phải nảy bên giao" : "Pha bóng • Vô lê ngoài vùng cấm vô lê / đánh sau nảy trong vùng cấm vô lê";
            if (actor.playerId == 0)
                ShotFeedback = shot + " • " + (actor.LastContactGrade == HitGrade.Perfect ? "Hoàn hảo" : "Tốt");
        }
        public Vector3 ShotTarget(MatchActor actor, ShotIntent shot)
        {
            float serverX = (Rules.Points[Rules.Server] % 2 == 0 ? 1 : -1) * -Actor(Rules.Server).end;
            var target = ShotTrajectory.Target(actor, shot, BallPosition, serverX);
            float x = target.x, depth = Mathf.Abs(target.z);
            if (actor.playerId == 1 && shot.kind != ShotKind.Serve)
            {
                // Fixed aim uncertainty by difficulty, never targets using the future human input.
                float error = botDifficulty == 3 && behaviorBot ? behaviorBot.AimError : botDifficulty == 0 ? .30f : botDifficulty == 1 ? .16f : .08f;
                x += Mathf.Sin(TotalHits * 13.37f) * error;
                depth += Mathf.Cos(TotalHits * 7.11f) * error;
            }
            return new Vector3(x, Court.BallRadius, -actor.end * depth);
        }
        void Launch(MatchActor actor, ShotIntent shot)
        {
            replay?.Event(shot.kind == ShotKind.Serve ? "Serve" : "Hit", actor.playerId + ":" + shot.kind);
            var target = ShotTarget(actor, shot);
            progressStrokes.Launch(actor.playerId, shot.kind == ShotKind.Serve, Rules.Stroke);
            BallVelocity = ShotTrajectory.Launch(shot, BallPosition, target);
        }
        public bool PredictContact(int end, bool requireBounce, out Vector3 point, out float delay, float height = .85f, bool beforeBounce = false)
        {
            Vector3 p = BallPosition, v = BallVelocity;
            bool bounced = Rules.Bounces > 0;
            if (beforeBounce && bounced)
            {
                point = Vector3.zero;
                delay = 0;
                return false;
            }
            for (int i = 1; i <= 300; i++)
            {
                BallFlight.Integrate(ref p, ref v, .01f);
                if (p.y <= Court.BallRadius)
                {
                    if (bounced || beforeBounce)
                        break;
                    bounced = true;
                    BallFlight.Rebound(ref p, ref v);
                }
                if (p.z * end > .5f && p.z * end < 9f && Mathf.Abs(p.y - height) < .10f && (!requireBounce || bounced))
                {
                    point = p;
                    delay = i * .01f;
                    return true;
                }
            }
            point = Vector3.zero;
            delay = 0;
            return false;
        }
        void CheckMomentum()
        {
            foreach (var actor in new[] { human, bot })
            {
                if (actor.VolleyMomentum && actor.InKitchen)
                {
                    Rules.Fail(actor.playerId, Fault.KitchenMomentum, true);
                    BeginFault();
                    return;
                }
                if (Rules.Live && Mathf.Abs(actor.transform.position.z) < .31f)
                {
                    Rules.Fail(actor.playerId, Fault.NetTouch);
                    BeginFault();
                    return;
                }
            }
        }
        void BeginFault()
        {
            if (Phase == MatchPhase.Settling)
                return;
            replay?.Event("Fault", Rules.FaultPlayer + ":" + Rules.LastFault);
            if (Rules.FaultPlayer == 0)
            {
                string fault = Rules.LastFault.ToString();
                if (Rules.LastFault == Fault.ServeTimeout || Rules.LastFault == Fault.ServeMiss || Rules.LastFault == Fault.DoubleBounce)
                    human.GetComponent<ActorTelemetry>()?.MissedOpportunity(Rules.LastFault != Fault.DoubleBounce,Rules.Stroke,TotalRallies);
                human.GetComponent<ActorTelemetry>()?.Outcome(fault == "ServeTimeout" ? "LateServe" : fault == "KitchenVolley" ? "Kitchen" : fault == "KitchenMomentum" ? "Momentum" : fault == "ServeFoot" ? "ServeFeet" : fault == "NetPlane" ? "Net" : fault);
            }
            human.GetComponent<ActorTelemetry>()?.Checkpoint();
            Phase = MatchPhase.Settling;
            phaseTime = 0;
            input.Cancel();
            TouchShotMask = 0;
            countdown.Reset();
            SfxPlayer.Play(GameSfx.FaultWhistle);
            Message = FaultText(Rules.LastFault) + " • " + (Rules.FaultPlayer == 0 ? "Bạn" : "Bot") + " phạm lỗi";
        }
        void FinishRally()
        {
            CoachingQuickToast.Show(gameObject, PickleVerse.Progression.QuickCoachingRules.Tip(coachingSession?.shots, quickCoachingStart));
            int loser = Rules.FaultPlayer;
            Fault fault = Rules.LastFault;
            int endedGame = Rules.GameNumber;
            int priorServer = Rules.Server;
            Message = FaultText(Rules.LastFault) + " • " + (loser == 0 ? "Bạn" : "Bot") + " phạm lỗi";
            int ownBefore = Rules.Points[0];
            Rules.CommitRally();
            TotalRallies++;
            if (Rules.GameFinished)
                replay?.Event("GameEnd", Rules.Points[0] + ":" + Rules.Points[1]);
            if (progressionResult != null)
                progressionResult.points += Rules.Points[0] - ownBefore;
            if (Rules.GameFinished)
                CaptureProgressionGame();
            RallyLog.Add(TotalRallies + ": " + Message + " | " + Rules.Points[0] + "-" + Rules.Points[1]);
            human.Reaction(loser != 0, Rules.Winner >= 0);
            bot.Reaction(loser != 1, Rules.Winner >= 0);
            phaseTime = 0;
            string outcome = priorServer == 1 - loser ? (loser == 1 ? "Bạn ghi điểm" : "Máy ghi điểm") : "Đổi quyền giao bóng";
            Announcements?.Enqueue(fault == Fault.Out ? "NGOÀI SÂN" : "LỖI", FaultText(fault) + " • " + (loser == 0 ? "Bạn" : "Bot") + " phạm lỗi\n" + outcome, loser == 0 ? AnnouncementTone.Fault : AnnouncementTone.Neutral, 1.2f);
            if (Rules.Winner >= 0)
            {
                SfxPlayer.Play(GameSfx.GameWhistle);
                Phase = MatchPhase.MatchOver;
                Message = Rules.Winner == 0 ? "Bạn thắng trận!" : "Máy thắng trận";
                CompleteProgressionMatch(false);
                Announcements?.Enqueue("KẾT THÚC TRẬN", (Rules.Winner == 0 ? "BẠN THẮNG TRẬN" : "MÁY THẮNG TRẬN") + " • " + Rules.Games[0] + " - " + Rules.Games[1] + "\nGame " + endedGame + ": " + Rules.Points[0] + " - " + Rules.Points[1], Rules.Winner == 0 ? AnnouncementTone.Success : AnnouncementTone.Fault, 2.1f);
                if (victory)
                    victory.Begin();
            }
            else if (Rules.GameFinished)
            {
                SfxPlayer.Play(GameSfx.GameWhistle);
                Phase = MatchPhase.BetweenGames;
                Message = "";
                Announcements?.Enqueue("VÁN " + endedGame + " KẾT THÚC", (loser == 1 ? "Bạn thắng ván" : "Máy thắng ván") + " • " + Rules.Points[0] + " - " + Rules.Points[1] + "\nSố ván thắng " + Rules.Games[0] + " - " + Rules.Games[1], loser == 1 ? AnnouncementTone.Success : AnnouncementTone.Neutral, 1.6f);
                Announcements?.Enqueue("ĐỔI BÊN", "ĐỔI BÊN • Chuẩn bị ván " + (endedGame + 1), AnnouncementTone.ChangeEnds, 1.4f, () => StageEndChange(true), FinishEndChange);
            }
            else
            {
                Phase = MatchPhase.BetweenPoints;
                if (Rules.EndChangeDue)
                {
                    if (Announcements)
                        Announcements.Enqueue("ĐỔI BÊN", "ĐỔI BÊN • Mốc 6 điểm trong ván quyết định", AnnouncementTone.ChangeEnds, 1.4f, () => StageEndChange(false), FinishEndChange);
                    else SwapEnds();
                }
            }
        }
        MatchAnnouncements Announcements => hud ? hud.announcements : null;
        bool AnnouncementBusy => Announcements && Announcements.Busy;
        void StageEndChange(bool nextGame)
        {
            if (Phase != MatchPhase.BetweenGames && Phase != MatchPhase.BetweenPoints)
                return;
            if (!nextGame)
                SfxPlayer.Play(GameSfx.GameWhistle);
            var transition = Phase;
            SwapEnds();
            if (nextGame)
                Rules.NextGame();
            PreparePoint();
            Phase = transition;
            TouchMovement = Vector2.zero;
            TouchRun = false;
            var camera = hud ? hud.GetComponent<Canvas>().worldCamera : null;
            if (camera && camera.TryGetComponent<MatchCamera>(out var follow))
                follow.SnapToPlayer();
        }
        void FinishEndChange()
        {
            if (Phase != MatchPhase.BetweenGames && Phase != MatchPhase.BetweenPoints)
                return;
            Phase = MatchPhase.PreServe;
            ServeElapsed = phaseTime = 0;
            accumulator = 0;
        }
        void PreparePoint()
        {
            quickCoachingStart = coachingSession?.shots.Count ?? 0;
            CoachingQuickToast.Show(gameObject, "");
            progressStrokes.NewRally();
            rankedBotContact.Reset();
            int lane = (Rules.Points[Rules.Server] % 2 == 0 ? 1 : -1) * -Actor(Rules.Server).end;
            Actor(Rules.Server).ResetAt(new Vector3(lane * 1.45f, 0, Actor(Rules.Server).end * 7.25f), Actor(Rules.Server).end);
            Actor(1 - Rules.Server).ResetAt(new Vector3(-lane * 1.45f, 0, Actor(1 - Rules.Server).end * 6.1f), Actor(1 - Rules.Server).end);
            Rules.PrepareServe(Actor(Rules.Server).end);
            Phase = MatchPhase.PreServe;
            phaseTime = ServeElapsed = 0;
            countdown.Reset();
            opponent.Reset();
            testPlayer.Reset();
            behaviorBot?.ResetPoint();
            replay?.Event("NewRally");
            releasedServe = false;
            input.Cancel();
            Message = (Rules.Server == 0 ? "Bạn giao • giữ rồi thả " + GameSettings.KeyName(GameSettings.Current.KeyFor(GameAction.Drive)) : "Máy giao • chờ bóng nảy rồi trả") + " • Điểm gọi " + Rules.Points[Rules.Server] + "–" + Rules.Points[1 - Rules.Server];
            ActiveShotKeys = ShotFeedback = "";
            human.RefreshPose();
            bot.RefreshPose();
            BallPosition = Actor(Rules.Server).HeldBallPosition;
            BallVelocity = Vector3.zero;
            if (ballTrail)
            {
                ballTrail.emitting = false;
                ballTrail.Clear();
            }
        }
        void SwapEnds()
        {
            human.end *= -1;
            bot.end *= -1;
            replay?.Event("EndChange");
        }
        public void Continue()
        {
            if (Paused)
            {
                TogglePause();
                return;
            }
            if (Phase == MatchPhase.Intro)
            {
                if (!opening || !opening.Play())
                    PreparePoint();
            }
            else if (Phase == MatchPhase.BetweenGames && !Announcements)
            {
                StageEndChange(true);
                FinishEndChange();
            }
            else if (Phase == MatchPhase.Timeout)
            {
                Phase = MatchPhase.PreServe;
                ServeElapsed = phaseTime = 0;
                Message = (Rules.Server == 0 ? "Bạn giao • giữ rồi thả " + GameSettings.KeyName(GameSettings.Current.KeyFor(GameAction.Drive)) : "Máy giao • chờ bóng nảy rồi trả") + " • Điểm gọi " + Rules.Points[Rules.Server] + "–" + Rules.Points[1 - Rules.Server];
            }
        }
        public async void Restart()
        {
            if (startingProgression || progressionNavigation)
                return;
            progressionNavigation = true;
            try
            {
                CompleteProgressionMatch(true);
                try
                {
                    await FlushProgressionAsync();
                }
                catch
                {
                    return;
                }
                if (!this)
                    return;
                if (victory)
                    victory.Cancel();
                Announcements?.Clear();
                if (opening && opening.IsPlaying)
                    opening.Cancel();
                Paused = false;
                Rules = new SinglesRules(initialServer);
                human.end = -1;
                bot.end = 1;
                TotalHits = TotalRallies = LongestRally = 0;
                RallyLog.Clear();
                BeginProgressionMatch();
                try
                {
                    await beginningProgression;
                }
                catch
                {
                    return;
                }
                if (!this)
                    return;
                PreparePoint();
                if (restartIntro)
                {
                    restartIntro = false;
                    Phase = MatchPhase.Intro;
                    if (!opening || !opening.Play())
                        PreparePoint();
                }
            }
            finally
            {
                progressionNavigation = false;
            }
        }
        bool restartIntro, progressionNavigation;
        public void RestartWithIntro()
        {
            restartIntro = true;
            Restart();
        }
        public void BeginCinematic()
        {
            Announcements?.Clear();
            Phase = MatchPhase.Cinematic;
            Paused = false;
            accumulator = 0;
            input.Cancel();
            TouchShotMask = 0;
            TouchMovement = Vector2.zero;
        }
        public void EndCinematic()
        {
            PreparePoint();
            UpdateVisuals();
        }
        public void TogglePause()
        {
            Paused = !Paused;
            input.Cancel();
            TouchShotMask = 0;
            TouchMovement = Vector2.zero;
            if (Paused)
                SfxPlayer.StopAll();
        }
        public void Timeout()
        {
            if (Phase == MatchPhase.PreServe && !human.Swinging && !bot.Swinging && Rules.TakeTimeout(0))
            {
                Phase = MatchPhase.Timeout;
                phaseTime = 0;
                countdown.Reset();
                Message = "Hội ý • tối đa 60 giây • Enter để tiếp tục";
                input.Cancel();
            }
        }
        public void SetDifficulty(int level)
        {
            botDifficulty = Mathf.Clamp(level, 0, 3);
        }
        public bool IsGameplay => true;
        public bool IsPaused => Paused;
        public bool CanSignOut => false;
        public void SetPaused(bool paused)
        {
            if (Paused != paused)
                TogglePause();
        }
        public async void LeaveToMenu()
        {
            if (progressionNavigation)
                return;
            progressionNavigation = true;
            try
            {
                CompleteProgressionMatch(true);
                try
                {
                    await FlushProgressionAsync();
                }
                catch
                {
                    return;
                }
                if (!this)
                    return;
                PickleVerse.Progression.RankedBotLaunch.Clear();
                UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
            }
            finally
            {
                progressionNavigation = false;
            }
        }
        public System.Threading.Tasks.Task SignOutAsync() => System.Threading.Tasks.Task.CompletedTask;
        void OnApplicationFocus(bool focus)
        {
            focused = focus;
            input.Cancel();
            TouchShotMask = 0;
            TouchMovement = Vector2.zero;
        }
        void UpdateVisuals()
        {
            bool flying = (Phase == MatchPhase.Rally && Rules.Live || Phase == MatchPhase.PreServe && releasedServe) && BallPosition.y > Court.BallRadius + .02f && BallVelocity.sqrMagnitude > .04f;
            if (ballTrail && !flying)
            {
                ballTrail.emitting = false;
                ballTrail.Clear();
            }
            if (ball)
            {
                ball.position = BallPosition;
                if (flying)
                    ball.Rotate(new Vector3(80, 140, 40) * Time.deltaTime);
            }
            if (ballTrail)
                ballTrail.emitting = flying && !Paused;
            if (ballShadow)
                ballShadow.gameObject.SetActive(false);
            if (landingMarker)
            {
                Vector3 p = BallPosition, v = BallVelocity;
                for (int i = 0; i < 300 && p.y > Court.BallRadius; i++)
                    BallFlight.Integrate(ref p, ref v, .01f);
                landingMarker.position = new Vector3(p.x, .018f, p.z);
                landingMarker.gameObject.SetActive(Phase == MatchPhase.Rally && Rules.ExpectedHitter == 0);
            }
            if (aimMarker)
            {
                aimMarker.gameObject.SetActive(human.Swinging);
                if (human.Swinging)
                {
                    var p = ShotTarget(human, human.Intent);
                    p.y = .022f;
                    aimMarker.position = p;
                }
            }
        }
        public string Snapshot() => JsonUtility.ToJson(new MatchSnapshot { phase = Phase.ToString(), score = Rules.Points[0] + "-" + Rules.Points[1], games = Rules.Games[0] + "-" + Rules.Games[1], hits = TotalHits, rallies = TotalRallies, longest = LongestRally, ball = BallPosition, humanClip = human.CurrentClip, botClip = bot.CurrentClip, message = Message });
        [Serializable]
        struct MatchSnapshot { public string phase, score, games, humanClip, botClip, message; public int hits, rallies, longest; public Vector3 ball; }
        static CourtPoint ToPoint(Vector3 p) => new CourtPoint(p.x, p.y, p.z);
        static float DistanceSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 d = b - a;
            return Vector3.Distance(p, a + d * Mathf.Clamp01(Vector3.Dot(p - a, d) / Mathf.Max(.000001f, d.sqrMagnitude)));
        }
        public static string FaultText(Fault fault)
        {
            switch (fault)
            {
                case Fault.ServeFoot: return "Lỗi chân giao bóng";
                case Fault.ServeMotion: return "Giao bóng không hợp lệ";
                case Fault.ServeTimeout: return "Quá 10 giây giao bóng";
                case Fault.ServeMiss: return "Giao bóng hụt";
                case Fault.ServeShort: return "Giao vào vùng cấm vô lê / vạch vùng cấm vô lê";
                case Fault.ServeWrongCourt: return "Giao sai ô chéo";
                case Fault.Out: return "NGOÀI SÂN";
                case Fault.OwnCourt: return "Bóng không qua lưới";
                case Fault.TwoBounce: return "Vi phạm hai lần nảy bắt buộc";
                case Fault.DoubleBounce: return "Bóng nảy hai lần";
                case Fault.KitchenVolley: return "Vô lê trong vùng cấm vô lê";
                case Fault.KitchenMomentum: return "Đà sau Vô lê chạm vùng cấm vô lê";
                case Fault.BodyHit: return "Bóng chạm người";
                case Fault.NetTouch: return "Chạm lưới";
                case Fault.NetPlane: return "Đánh trước mặt phẳng lưới";
                default: return fault.ToString();
            }
        }
    }
}
