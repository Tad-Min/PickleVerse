using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using PickleVerse.Rules;

namespace PickleVerse.LocalMatch
{
    public enum MatchPhase { Intro, PreServe, Rally, Settling, BetweenPoints, BetweenGames, Timeout, MatchOver }
    public sealed class BotMatch : MonoBehaviour
    {
        public MatchActor human, bot;
        public Transform ball, ballShadow, landingMarker, aimMarker;
        public MatchHud hud;
        [Range(0, 2)] public int botDifficulty = 1;
        [Range(.12f,.6f)] public float contactAssistRadius = .45f;
        public bool autoPlayer;
        public SinglesRules Rules { get; private set; }
        public MatchPhase Phase { get; private set; } = MatchPhase.Intro;
        public Vector3 BallPosition { get; private set; }
        public Vector3 BallVelocity { get; private set; }
        public float ServeElapsed { get; private set; }
        public string Message { get; private set; } = "Đấu đơn • Thắng 2/3 game • 11 điểm, cách 2";
        public string ShotFeedback { get; private set; } = "";
        public string Selection => input.Selection;
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
        Vector3 serveOrigin;
        public Vector2 TouchMovement { get; set; }
        public bool TouchRun { get; set; }
        public int TouchShotMask { get; set; }
        public MatchActor Actor(int id) => id == 0 ? human : bot;

        void Awake() { InitializeMatch(); }
        void OnEnable() { if(Application.isPlaying && Rules==null)InitializeMatch(); }
        void InitializeMatch()
        {
            if(!human || !bot)return;
            TotalHits=TotalRallies=LongestRally=0;RallyLog.Clear();Paused=false;
            human.end=-1;bot.end=1;
            human.Initialize(); bot.Initialize();
            initialServer = UnityEngine.Random.Range(0, 2);
            Rules = new SinglesRules(initialServer); PreparePoint(); Phase = MatchPhase.Intro;
            if (hud) hud.Bind(this);
        }
        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && focused)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) TogglePause();
                if (keyboard.enterKey.wasPressedThisFrame) Continue();
                if (keyboard.tKey.wasPressedThisFrame) Timeout();
                if (keyboard.f1Key.wasPressedThisFrame && hud) hud.ToggleHelp();
            }
            if (Paused || !focused) return;
            Vector2 move = TouchMovement; bool run = TouchRun; int mask = TouchShotMask;
            int tapMask=0;
            if (keyboard != null)
            {
                move += new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0), (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                run |= keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                mask |= (keyboard.jKey.isPressed ? 1 : 0) | (keyboard.kKey.isPressed ? 2 : 0) | (keyboard.lKey.isPressed ? 4 : 0) | (keyboard.uKey.isPressed ? 8 : 0) | (keyboard.iKey.isPressed ? 16 : 0);
                // Preserve a tap whose down/up both arrived between two rendered frames.
                tapMask=(keyboard.jKey.wasPressedThisFrame&&!keyboard.jKey.isPressed?1:0)|(keyboard.kKey.wasPressedThisFrame&&!keyboard.kKey.isPressed?2:0)|(keyboard.lKey.wasPressedThisFrame&&!keyboard.lKey.isPressed?4:0);
            }
            // WASD/touch forward always points into the court after an end change.
            move = Vector2.ClampMagnitude(move, 1); move *= -human.end;
            bool cancel=keyboard!=null&&keyboard.backspaceKey.wasPressedThisFrame;
            if(tapMask!=0)TickInput(mask|tapMask,cancel);
            TickInput(mask,cancel);
            accumulator += Mathf.Min(Time.deltaTime, .20f);
            const float tick = 1f / 120f;
            while (accumulator >= tick) { StepSimulation(tick, move, run); accumulator -= tick; }
            UpdateVisuals();
        }
        public void TickInput(int mask, bool cancel = false)
        {
            bool serve = Phase == MatchPhase.PreServe && Rules.Server == 0;
            if (Phase != MatchPhase.Rally && !serve) { input.Cancel(); input.Tick(mask, false, serve, out _); return; }
            if (input.Tick(mask, cancel, serve, out var shot)) RequestShot(0, shot);
            if (!string.IsNullOrEmpty(input.Feedback)) ShotFeedback = input.Feedback;
        }
        public bool RequestShot(int id, ShotIntent intent)
        {
            bool serving = Phase == MatchPhase.PreServe && id == Rules.Server;
            if ((Phase != MatchPhase.Rally && !serving) || Paused || Actor(id).Swinging) return false;
            if (serving != (intent.kind == ShotKind.Serve)) return false;
            Vector3 predicted = BallPosition + BallVelocity * intent.ContactDelay + Vector3.down * (BallFlight.Gravity * intent.ContactDelay * intent.ContactDelay * .5f);
            bool began = Actor(id).Swing(intent, predicted);
            if (began && serving) { releasedServe = false; serveOrigin = Actor(id).transform.position + Actor(id).transform.right * .3f + Actor(id).transform.forward * .5f + Vector3.up * .94f; }
            if (began && id == 0) ShotFeedback = intent + " • thả sớm để vợt đến bóng";
            return began;
        }
        public void StepSimulation(float dt, Vector2 humanMovement, bool running)
        {
            if (Paused || Phase == MatchPhase.Intro || Phase == MatchPhase.MatchOver) return;
            phaseTime += dt;
            opponent.difficulty = botDifficulty; testPlayer.difficulty = 2;
            opponent.Step(this, bot, dt, out var botMove, out var botRun);
            if (autoPlayer) testPlayer.Step(this, human, dt, out humanMovement, out running);
            bool motion = Phase == MatchPhase.PreServe || Phase == MatchPhase.Rally || Phase == MatchPhase.Settling;
            human.Step(humanMovement, running, dt, motion); bot.Step(botMove, botRun, dt, motion);
            CheckMomentum();
            if (Phase == MatchPhase.PreServe)
            {
                ServeElapsed += dt;
                var server = Actor(Rules.Server);
                if (server.Swinging && server.Intent.kind == ShotKind.Serve)
                {
                    if (server.SwingTime >= .5f && !releasedServe) { releasedServe = true; serveOrigin = server.transform.TransformPoint(server.ContactOffset(server.Intent)) + Vector3.up * (.5f*BallFlight.Gravity*.3f*.3f); }
                    if (releasedServe) { BallPosition = serveOrigin + Vector3.down * (.5f * BallFlight.Gravity * Mathf.Pow(server.SwingTime - .5f, 2)); }
                    if (server.SwingTime >= .8f && server.ContactActive && Vector3.Distance(BallPosition,server.paddleFace.position)<=contactAssistRadius)
                    {
                        // This is the authored underhand motion: no spin/toss imparted on release.
                        BallPosition = new Vector3(serveOrigin.x, BallPosition.y, serveOrigin.z);
                        var f = Rules.Serve(Rules.Server, server.Feet(true), server.Feet(false), ToPoint(BallPosition), ServeElapsed,server.PaddleVelocity.y>0,server.PaddleHeadTop<=server.hand.position.y+.015f);
                        if (f != Fault.None) BeginFault();
                        else { server.ConsumeContact(false); Launch(server, server.Intent); Phase = MatchPhase.Rally; phaseTime = 0; Message = "Nhận giao bóng: phải để bóng nảy"; }
                    }
                }
                else BallPosition = server.transform.position + server.transform.right * .25f + Vector3.up * .9f;
                if (ServeElapsed > 10f && Phase == MatchPhase.PreServe) { Rules.Fail(Rules.Server, Fault.ServeTimeout); BeginFault(); }
            }
            else if (Phase == MatchPhase.Rally)
            {
                Vector3 prior = BallPosition, p = BallPosition, v = BallVelocity;
                BallFlight.Integrate(ref p, ref v, dt);
                // Thin net uses a swept plane test; high balls and legal around-post shots pass.
                if (prior.z * p.z < 0 && Mathf.Abs(p.x) <= 3.35f)
                {
                    float t = prior.z / (prior.z - p.z); Vector3 crossing = Vector3.Lerp(prior, p, t);
                    float netTop = Court.NetHeight(crossing.x);
                    if (crossing.y - Court.BallRadius <= netTop)
                    {
                        // A tape graze may continue across; touching the net is not a let.
                        if (crossing.y >= netTop - .01f) { v.z *= .6f; v.x *= .6f; v.y *= .5f; }
                        else { p.z = Mathf.Sign(prior.z) * .04f; v.z *= -.16f; v.x *= .45f; v.y *= .35f; }
                    }
                }
                BallPosition = p; BallVelocity = v;
                TryContact(human, prior); if (Rules.Live) TryContact(bot, prior);
                if (Rules.Live && BallPosition.y <= Court.BallRadius)
                {
                    var bounce = BallPosition; bounce.y = Court.BallRadius;
                    Rules.Bounce(ToPoint(bounce));
                    if (!Rules.Live) BeginFault();
                    else { p = BallPosition; v = BallVelocity; BallFlight.Rebound(ref p, ref v); BallPosition = p; BallVelocity = v; Message = Rules.Stroke == 1 ? "Return sau nảy • J / K / J+K" : Rules.Stroke == 2 ? "Cú thứ ba: bên giao phải để bóng nảy" : "Bóng đã nảy • được đánh trong Kitchen"; }
                }
                if (Rules.Live)
                {
                    // A body contact is separate from paddle contact; never grants an automatic hit.
                    foreach (var actor in new[] { human, bot })
                    {
                        if (Rules.ExpectedHitter != actor.playerId) continue;
                        Vector3 body = actor.transform.position + Vector3.up * .85f;
                        if (BallPosition.y > .25f && BallPosition.y < 1.65f && DistanceSegment(prior, BallPosition, body) < .24f) { Rules.Fail(actor.playerId, Fault.BodyHit); BeginFault(); break; }
                    }
                    if (Mathf.Abs(BallPosition.x) > 7f || Mathf.Abs(BallPosition.z) > 12f) { Rules.Fail(Rules.Bounces > 0 ? Rules.ExpectedHitter : Rules.LastHitter, Fault.PermanentObject); BeginFault(); }
                }
            }
            else if (Phase == MatchPhase.Settling)
            {
                if (!human.VolleyMomentum && !bot.VolleyMomentum) FinishRally();
            }
            else if (Phase == MatchPhase.BetweenPoints && phaseTime >= 2.1f) PreparePoint();
            else if ((Phase == MatchPhase.BetweenGames && phaseTime >= 120f) || (Phase == MatchPhase.Timeout && phaseTime >= 60f)) Continue();
        }
        void TryContact(MatchActor actor, Vector3 previous)
        {
            if (!actor.ContactActive || Rules.ExpectedHitter != actor.playerId || BallPosition.z * actor.end <= .05f) return;
            ShotIntent shot = actor.Intent;
            if (shot.AfterBounce && Rules.Bounces == 0 || shot.BeforeBounce && Rules.Bounces != 0) return;
            if (shot.kind == ShotKind.Smash && BallPosition.y < 1.65f) return;
            Vector3 face = actor.paddleFace ? actor.paddleFace.position : actor.transform.position + actor.transform.forward * .5f + Vector3.up;
            // Small contact assist around the visible paddle; no ball teleport or guaranteed hit.
            float distance = DistanceSegment(previous, BallPosition, face);
            if (distance > contactAssistRadius || Vector3.Distance(BallPosition, actor.transform.position + Vector3.up * .9f) > 1.55f) return;
            bool volley = Rules.Bounces == 0;
            var result = Rules.Hit(actor.playerId, ToPoint(BallPosition), actor.InKitchen);
            actor.ConsumeContact(volley);
            if (result != Fault.None) { BeginFault(); return; }
            Launch(actor, shot);
            TotalHits++; LongestRally = Mathf.Max(LongestRally, Rules.Stroke);
            Actor(1 - actor.playerId).SplitStep();
            Message = Rules.Stroke <= 2 ? "Cú thứ ba phải nảy bên giao" : "Rally • Volley ngoài Kitchen / đánh sau nảy trong Kitchen";
            if (actor.playerId == 0) ShotFeedback = shot + " • " + (distance < .24f ? "Chạm tốt" : "Chạm lệch");
        }
        public Vector3 ShotTarget(MatchActor actor, ShotIntent shot)
        {
            int targetEnd = -actor.end;
            bool soft = shot.kind == ShotKind.Dink || shot.kind == ShotKind.Drop || shot.kind == ShotKind.Reset;
            float x = Mathf.Clamp(BallPosition.x, -2.25f, 2.25f);
            if (Mathf.Abs(x) < .25f) x = .3f;
            if (shot.cross) x = -Mathf.Sign(x) * 1.9f;
            float depth = soft ? 1.45f : shot.kind == ShotKind.Smash ? 4.1f : 5.05f;
            if (shot.kind == ShotKind.Serve) { int lane = (Rules.Points[Rules.Server] % 2 == 0 ? 1 : -1) * -actor.end; x = -lane * (shot.cross ? 2.05f : 1.35f); depth = shot.power ? 5.8f : 4.75f; }
            if (actor.playerId == 1 && shot.kind != ShotKind.Serve)
            {
                // Fixed aim uncertainty by difficulty, never targets using the future human input.
                float error = botDifficulty == 0 ? .30f : botDifficulty == 1 ? .16f : .08f;
                x += Mathf.Sin(TotalHits * 13.37f) * error; depth += Mathf.Cos(TotalHits * 7.11f) * error;
            }
            return new Vector3(x, Court.BallRadius, targetEnd * depth);
        }
        void Launch(MatchActor actor, ShotIntent shot)
        {
            var target = ShotTarget(actor, shot);
            bool soft = shot.kind == ShotKind.Dink || shot.kind == ShotKind.Drop || shot.kind == ShotKind.Reset;
            float apex = soft ? Mathf.Max(1.5f, BallPosition.y + .18f) : shot.kind == ShotKind.Smash ? BallPosition.y + .10f : shot.power ? Mathf.Max(1.65f, BallPosition.y + .13f) : Mathf.Max(2.25f, BallPosition.y + .3f);
            BallVelocity = BallFlight.Launch(BallPosition, target, apex);
            // A baseline drop needs more arc than a kitchen dink. Solve clearance from
            // the actual contact point, rather than assigning all soft shots one apex.
            float margin=shot.power?.045f:soft?.10f:.14f;
            for(int attempt=0;attempt<28;attempt++)
            {
                float crossingTime=-BallPosition.z/BallVelocity.z;
                float crossingX=BallPosition.x+BallVelocity.x*crossingTime;
                float crossingY=BallPosition.y+BallVelocity.y*crossingTime-.5f*BallFlight.Gravity*crossingTime*crossingTime;
                if(crossingTime<=0 || Mathf.Abs(crossingX)>3.35f || crossingY>=Court.NetHeight(crossingX)+Court.BallRadius+margin)break;
                apex+=.15f;BallVelocity=BallFlight.Launch(BallPosition,target,apex);
            }
        }
        public bool PredictContact(int end, bool requireBounce, out Vector3 point, out float delay, float height = .85f)
        {
            Vector3 p = BallPosition, v = BallVelocity; bool bounced = Rules.Bounces > 0;
            for (int i = 1; i <= 300; i++)
            {
                BallFlight.Integrate(ref p, ref v, .01f);
                if (p.y <= Court.BallRadius) { if (bounced) break; bounced = true; BallFlight.Rebound(ref p, ref v); }
                if (p.z * end > .5f && p.z * end < 9f && Mathf.Abs(p.y-height)<.10f && (!requireBounce || bounced)) { point = p; delay = i * .01f; return true; }
            }
            point = Vector3.zero; delay = 0; return false;
        }
        void CheckMomentum()
        {
            foreach (var actor in new[] { human, bot })
            {
                if (actor.VolleyMomentum && actor.InKitchen)
                {
                    Rules.Fail(actor.playerId, Fault.KitchenMomentum, true); BeginFault(); return;
                }
                if (Rules.Live && Mathf.Abs(actor.transform.position.z) < .31f)
                { Rules.Fail(actor.playerId, Fault.NetTouch); BeginFault(); return; }
            }
        }
        void BeginFault()
        {
            if (Phase == MatchPhase.Settling) return;
            Phase = MatchPhase.Settling; phaseTime = 0; input.Cancel(); TouchShotMask = 0;
            Message = FaultText(Rules.LastFault) + " • " + (Rules.FaultPlayer == 0 ? "Bạn" : "Bot") + " phạm lỗi";
        }
        void FinishRally()
        {
            int loser = Rules.FaultPlayer;
            Message = FaultText(Rules.LastFault) + " • " + (loser == 0 ? "Bạn" : "Bot") + " phạm lỗi";
            Rules.CommitRally(); TotalRallies++;
            RallyLog.Add(TotalRallies + ": " + Message + " | " + Rules.Points[0] + "-" + Rules.Points[1]);
            human.Reaction(loser != 0, Rules.Winner >= 0); bot.Reaction(loser != 1, Rules.Winner >= 0);
            phaseTime = 0;
            if (Rules.Winner >= 0) { Phase = MatchPhase.MatchOver; Message = Rules.Winner == 0 ? "Bạn thắng trận!" : "Bot thắng trận • Thử lại để luyện kỹ thuật"; }
            else if (Rules.GameFinished) { Phase = MatchPhase.BetweenGames; Message = "Hết game • Đổi sân • Enter để tiếp tục sớm"; }
            else
            {
                Phase = MatchPhase.BetweenPoints;
                if (Rules.EndChangeDue) { SwapEnds(); Message += " • Đổi sân tại 6 điểm ở game quyết định"; }
            }
        }
        void PreparePoint()
        {
            int lane = (Rules.Points[Rules.Server] % 2 == 0 ? 1 : -1) * -Actor(Rules.Server).end;
            Actor(Rules.Server).ResetAt(new Vector3(lane * 1.45f, 0, Actor(Rules.Server).end * 7.25f), Actor(Rules.Server).end);
            Actor(1 - Rules.Server).ResetAt(new Vector3(-lane * 1.45f, 0, Actor(1 - Rules.Server).end * 6.1f), Actor(1 - Rules.Server).end);
            Rules.PrepareServe(Actor(Rules.Server).end); Phase = MatchPhase.PreServe; phaseTime = ServeElapsed = 0;
            opponent.Reset(); testPlayer.Reset(); releasedServe = false; input.Cancel();
            Message = (Rules.Server == 0 ? "Bạn giao • giữ rồi thả J" : "Bot giao • chờ bóng nảy rồi trả") + " • Điểm gọi " + Rules.Points[Rules.Server] + "–" + Rules.Points[1 - Rules.Server];
            BallPosition = Actor(Rules.Server).transform.position + Vector3.up * .9f; BallVelocity = Vector3.zero;
        }
        void SwapEnds() { human.end *= -1; bot.end *= -1; }
        public void Continue()
        {
            if (Paused) { TogglePause(); return; }
            if (Phase == MatchPhase.Intro) PreparePoint();
            else if (Phase == MatchPhase.BetweenGames) { SwapEnds(); Rules.NextGame(); PreparePoint(); }
            else if (Phase == MatchPhase.Timeout) { Phase = MatchPhase.PreServe; ServeElapsed = phaseTime = 0; Message=(Rules.Server==0?"Bạn giao • giữ rồi thả J":"Bot giao • chờ bóng nảy rồi trả")+" • Điểm gọi "+Rules.Points[Rules.Server]+"–"+Rules.Points[1-Rules.Server]; }
        }
        public void Restart()
        {
            Paused = false; Rules = new SinglesRules(initialServer); human.end = -1; bot.end = 1;
            TotalHits = TotalRallies = LongestRally = 0; RallyLog.Clear(); PreparePoint();
        }
        public void TogglePause() { Paused = !Paused; input.Cancel(); TouchShotMask = 0; TouchMovement = Vector2.zero; }
        public void Timeout() { if (Phase == MatchPhase.PreServe && !human.Swinging && !bot.Swinging && Rules.TakeTimeout(0)) { Phase = MatchPhase.Timeout; phaseTime = 0; Message = "Time-out • tối đa 60 giây • Enter để tiếp tục"; input.Cancel(); } }
        public void SetDifficulty(int level) { botDifficulty = Mathf.Clamp(level, 0, 2); }
        void OnApplicationFocus(bool focus) { focused = focus; input.Cancel(); TouchShotMask = 0; TouchMovement = Vector2.zero; }
        void UpdateVisuals()
        {
            if (ball) { ball.position = BallPosition; ball.Rotate(new Vector3(80, 140, 40) * Time.deltaTime); }
            if (ballShadow) { ballShadow.position = new Vector3(BallPosition.x, .012f, BallPosition.z); ballShadow.localScale = Vector3.one * Mathf.Lerp(.12f, .24f, BallPosition.y / 3f); }
            if (landingMarker)
            {
                Vector3 p = BallPosition, v = BallVelocity;
                for (int i = 0; i < 300 && p.y > Court.BallRadius; i++) BallFlight.Integrate(ref p, ref v, .01f);
                landingMarker.position = new Vector3(p.x, .018f, p.z); landingMarker.gameObject.SetActive(Phase == MatchPhase.Rally && Rules.ExpectedHitter == 0);
            }
            if (aimMarker)
            {
                aimMarker.gameObject.SetActive(human.Swinging);
                if (human.Swinging) { var p = ShotTarget(human, human.Intent); p.y = .022f; aimMarker.position = p; }
            }
        }
        public string Snapshot() => JsonUtility.ToJson(new MatchSnapshot { phase = Phase.ToString(), score = Rules.Points[0] + "-" + Rules.Points[1], games = Rules.Games[0] + "-" + Rules.Games[1], hits = TotalHits, rallies = TotalRallies, longest = LongestRally, ball = BallPosition, humanClip = human.CurrentClip, botClip = bot.CurrentClip, message = Message });
        [Serializable] struct MatchSnapshot { public string phase, score, games, humanClip, botClip, message; public int hits, rallies, longest; public Vector3 ball; }
        static CourtPoint ToPoint(Vector3 p) => new CourtPoint(p.x, p.y, p.z);
        static float DistanceSegment(Vector3 a, Vector3 b, Vector3 p) { Vector3 d = b - a; return Vector3.Distance(p, a + d * Mathf.Clamp01(Vector3.Dot(p - a, d) / Mathf.Max(.000001f, d.sqrMagnitude))); }
        public static string FaultText(Fault fault)
        {
            switch (fault) {
                case Fault.ServeFoot: return "Lỗi chân giao bóng"; case Fault.ServeMotion: return "Giao bóng không hợp lệ"; case Fault.ServeTimeout: return "Quá 10 giây giao bóng";
                case Fault.ServeShort: return "Giao vào Kitchen / vạch Kitchen"; case Fault.ServeWrongCourt: return "Giao sai ô chéo"; case Fault.Out: return "OUT"; case Fault.OwnCourt: return "Bóng không qua lưới";
                case Fault.TwoBounce: return "Vi phạm hai lần nảy bắt buộc"; case Fault.DoubleBounce: return "Bóng nảy hai lần"; case Fault.KitchenVolley: return "Volley trong Kitchen"; case Fault.KitchenMomentum: return "Đà sau Volley chạm Kitchen";
                case Fault.BodyHit: return "Bóng chạm người"; case Fault.NetTouch: return "Chạm lưới"; case Fault.NetPlane: return "Đánh trước mặt phẳng lưới"; default: return fault.ToString();
            }
        }
    }
}
