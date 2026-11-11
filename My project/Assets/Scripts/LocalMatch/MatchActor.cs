using System;
using UnityEngine;
using PickleVerse.Rules;
using PickleVerse.Audio;

namespace PickleVerse.LocalMatch
{
    [Serializable]
    public struct ClipEntry { public string name; public AnimationClip clip; public float contactPhase; public Vector3 contactOffset; }
    public sealed class MatchActor : MonoBehaviour
    {
        public int playerId;
        public int end = -1;
        public Transform visual, paddleFace, leftFoot, rightFoot;
        public GameObject paddle;
        public Transform upperArm, forearm, hand;
        public Transform leftHand;
        public Vector3 heldBallOffset = new Vector3(-.06f, .055f, 0);
        public Vector3 HeldBallPosition => leftHand ? leftHand.TransformPoint(heldBallOffset) : transform.TransformPoint(new Vector3(-.30f, 1.04f, .28f));
        public ClipEntry[] clips;
        public float walkSpeed = 3.3f, runSpeed = 5.8f;
        bool? femaleBody;
        /// <summary>
        /// True when this actor currently wears the female character. Scenes were authored with the
        /// female body on player 0 and the male body on player 1; ActorCharacter overrides this when
        /// it exchanges the bodies to follow the player's character choice.
        /// </summary>
        public bool IsFemale
        {
            get => femaleBody ?? playerId == 0;
            set => femaleBody = value;
        }
        public Vector3 Velocity { get; private set; }
        public ShotIntent Intent { get; private set; }
        public bool Swinging { get; private set; }
        public bool ContactActive => Swinging && !contactConsumed && swingTime >= ContactDelay - PaddleContact.TimingWindow && previousSwingTime <= ContactDelay + PaddleContact.TimingWindow;
        public HitGrade LastContactGrade { get; private set; }
        bool localHitFeedback;
        public bool LocalHitFeedback
        {
            get => localHitFeedback;
            set
            {
                localHitFeedback = value;
                if (value && Application.isPlaying && !gradeFeedback)
                {
                    gradeFeedback = gameObject.AddComponent<HitGradeFeedback>();
                    gradeFeedback.Prepare();
                }
                else if (!value && gradeFeedback)
                    gradeFeedback.Clear();
            }
        }
        public float ContactDelay { get; private set; }
        public float SwingDuration => ContactDelay + Intent.Duration - Intent.ContactDelay;
        public Vector3 PreviousPaddlePosition { get; private set; }
        public Vector3 ContactPoint { get; private set; }
        public bool VolleyMomentum { get; private set; }
        public string CurrentClip => clipName;
        public float SwingTime => swingTime;
        public string LocomotionClip => locomotionName;
        public Vector2 MovementIntent => movementIntent;
        public Rect? MovementBounds { get; private set; }
        public bool MovementRun { get; private set; }
        public Vector3 PaddleVelocity { get; private set; }
        public float PaddleHeadTop
        {
            get
            {
                float top = float.NegativeInfinity;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = 0; z <= 1; z++)
                            top = Mathf.Max(top, paddle.transform.TransformPoint(new Vector3(x * .622f, y * .120f, z == 0 ? -1.33f : 0f)).y);
                return top;
            }
        }
        Animation animationPlayer;
        string clipName;
        float clipTime, swingTime, lastVolleyTime, balanceTime;
        bool contactConsumed;
        float previousSwingTime;
        Vector3 previousBodyPosition;
        PaddleHit pendingHit;
        HitGradeFeedback gradeFeedback;
        float sourceContactPhase;
        Vector3 impactPoint;
        float impactTime;
        bool contactConfirmed;
        Vector3 overheadContactOffset;
        bool overheadCalibrated;
        PaddleImpactVfx impactVfx;
        ActorPoseMixer poseMixer;
        Vector2 movementIntent;
        string locomotionName = "PKV_Idle_Ready";
        float locomotionPhase, legWeight;
        readonly MovementFoley foley = new MovementFoley();
        public bool InKitchen => Court.InKitchen(Feet(true)) || Court.InKitchen(Feet(false));
        public CourtPoint Feet(bool left)
        {
            var foot = left ? leftFoot : rightFoot;
            Vector3 p = foot ? foot.position : transform.position + transform.right * (left ? -.13f : .13f);
            return new CourtPoint(p.x, p.y, p.z);
        }
        public void Initialize()
        {
            PickleVerse.CharacterMotion.CharacterSecondaryMotion.Attach(visual.gameObject, IsFemale);
            var bones = visual.GetComponentsInChildren<Transform>(true);
            if (!leftHand)
                leftHand = Array.Find(bones, t => t.name == "mixamorig:LeftHand");
            animationPlayer = visual.GetComponent<Animation>();
            if (!animationPlayer)
                animationPlayer = visual.gameObject.AddComponent<Animation>();
            animationPlayer.playAutomatically = false;
            // Gameplay samples the complete pose, then applies grip/IK. Unity's automatic
            // legacy pass must not overwrite that pose later in the PlayerLoop.
            animationPlayer.enabled = false;
            impactVfx = GetComponent<PaddleImpactVfx>();
            if (!impactVfx)
                impactVfx = gameObject.AddComponent<PaddleImpactVfx>();
            foreach (var entry in clips)
                if (entry.clip)
                    animationPlayer.AddClip(entry.clip, entry.name);
            if (paddleFace && Has("PKV_Overhead_Smash"))
            {
                // Measure the posed paddle on this rig, including the constrained overhead IK.
                // Male/female proportions differ; a nominal 2.02 m target was outside arm reach.
                Swing(new ShotIntent(ShotKind.Smash), transform.position + transform.forward);
                swingTime = ContactDelay;
                Sample(0);
                overheadContactOffset = transform.InverseTransformPoint(paddleFace.position);
                overheadCalibrated = true;
                Swinging = false;
            }
            Play("PKV_Idle_Ready");
            Sample(0);
            PreviousPaddlePosition = paddleFace ? paddleFace.position : transform.position;
            previousBodyPosition = transform.position;
            previousSwingTime = swingTime;
            poseMixer = new ActorPoseMixer(bones);
        }
        public void RefreshPose()
        {
            Sample(0);
        }
        public void SampleEntrance(AnimationClip clip, float time)
        {
            if (!clip)
                return;
            clipName = clip.name;
            poseMixer?.Reset();
            clip.SampleAnimation(visual.gameObject, Mathf.Repeat(time, clip.length));
        }
        public void SampleCelebration(AnimationClip clip, float time)
        {
            if (!clip || !visual)
                return;
            Swinging = VolleyMomentum = false;
            poseMixer?.Reset();
            clipName = clip.name;
            clip.SampleAnimation(visual.gameObject, Mathf.Clamp(time, 0, clip.length));
        }
        public void EntranceFootsteps(float dt)
        {
            foley.Step(transform.position, false, dt);
        }
        public void ResetAt(Vector3 p, int newEnd)
        {
            end = newEnd;
            transform.position = p;
            transform.rotation = Quaternion.Euler(0, end < 0 ? 0 : 180, 0);
            Velocity = Vector3.zero;
            Swinging = VolleyMomentum = false;
            contactConsumed = false;
            balanceTime = 0;
            movementIntent = Vector2.zero;
            MovementRun = false;
            legWeight = locomotionPhase = 0;
            locomotionName = "PKV_Idle_Ready";
            foley.Reset();
            if (paddle)
                paddle.SetActive(true);
            if (animationPlayer)
                Play("PKV_Idle_Ready");
            poseMixer?.Reset();
            PreviousPaddlePosition = paddleFace ? paddleFace.position : transform.position;
            previousBodyPosition = transform.position;
            previousSwingTime = 0;
            pendingHit = default;
            LastContactGrade = HitGrade.None;
            if (gradeFeedback)
                gradeFeedback.Clear();
            if (impactVfx)
                impactVfx.Clear();
        }
        public bool Swing(ShotIntent shot, Vector3 predictedBall, float contactDelay = -1)
        {
            if (Swinging)
                return false;
            Intent = shot;
            GetComponent<ActorTelemetry>()?.Input(shot);
            Swinging = true;
            swingTime = 0;
            contactConsumed = contactConfirmed = false;
            previousSwingTime = 0;
            pendingHit = default;
            previousBodyPosition = transform.position;
            PreviousPaddlePosition = paddleFace ? paddleFace.position : transform.position;
            ContactDelay = shot.kind == ShotKind.Serve || contactDelay < 0 ? shot.ContactDelay : Mathf.Clamp(contactDelay, .10f, shot.ContactDelay + .08f);
            // Some authored forehands cross the centre line, while backhand contacts
            // are almost centred. A left/right sign test selected the wrong drop clip.
            Vector3 localBall = transform.InverseTransformPoint(predictedBall - PaddleContact.PredictMovement(this, ContactDelay));
            bool backhand = (localBall - ContactOffset(shot, true)).sqrMagnitude < (localBall - ContactOffset(shot, false)).sqrMagnitude;
            string action = ActionName(shot, backhand);
            var profile = Profile(action);
            sourceContactPhase = profile.contactPhase;
            // Start from the legs currently on screen, including a gait that was
            // already running before this strike was requested.
            poseMixer?.BeginLegs(.06f);
            Play(action);
            return true;
        }
        public void ObserveMovement(Vector2 movement, bool run)
        {
            movementIntent = Vector2.ClampMagnitude(movement, 1);
            MovementRun = run;
            // Movement and the upper-body strike are concurrent actions. Never reset
            // the contact clock/window when a direction, sprint or touch input changes.
        }
        public bool CanContact(Vector3 previousBall, Vector3 ball, float assist, out float distance)
        {
            distance = float.PositiveInfinity;
            if (!ContactActive || !paddleFace)
                return false;
            pendingHit = default;
            if (!PaddleContact.TryEvaluate(previousBall, ball, PreviousPaddlePosition, paddleFace.position,
                previousBodyPosition, transform.position, transform.rotation, previousSwingTime, swingTime, ContactDelay,
                assist, Intent.kind == ShotKind.Smash, out pendingHit, Intent.kind == ShotKind.Serve))
                return false;
            distance = pendingHit.distance;
            ContactPoint = pendingHit.point;
            return true;
        }
        public Vector3 ContactOffset(ShotIntent shot, bool backhand = false)
        {
            if (shot.kind == ShotKind.Smash && overheadCalibrated)
                return overheadContactOffset;
            return Profile(ActionName(shot, backhand)).contactOffset;
        }
        ClipEntry Profile(string key)
        {
            foreach (var clip in clips)
                if (clip.name == key)
                    return clip;
            return default;
        }
        static string ActionName(ShotIntent shot, bool backhand) =>
            shot.kind == ShotKind.Serve ? "PKV_Serve_FH_Underhand" : shot.kind == ShotKind.Smash ? "PKV_Overhead_Smash" :
            shot.kind == ShotKind.Drop ? (backhand ? "PKV_ThirdShotDrop_BH" : "PKV_ThirdShotDrop_FH") :
            shot.kind == ShotKind.Dink ? (backhand ? "PKV_Dink_BH" : "PKV_Dink_FH") :
            shot.BeforeBounce ? (backhand ? "PKV_Volley_BH" : "PKV_Volley_FH") : (backhand ? "PKV_Groundstroke_BH" : "PKV_Groundstroke_FH");
        public void MissContact()
        {
            GetComponent<ActorTelemetry>()?.Outcome("Miss");
            contactConsumed = true;
            if (Intent.kind == ShotKind.Serve)
                SfxPlayer.Stop(GameSfx.Countdown);
        }
        public void ConsumeContact(bool volley, Vector3? position = null, bool successful = true, HitGrade? confirmedGrade = null)
        {
            if (contactConsumed)
                return;
            impactPoint = position ?? (paddleFace ? paddleFace.position : transform.position + Vector3.up);
            GetComponent<ActorTelemetry>()?.Contact(impactPoint, volley, successful);
            // Grade the uncorrected paddle, BEFORE the short contact IK pose is applied.
            LastContactGrade = successful ? confirmedGrade ?? (pendingHit.grade != HitGrade.None ? pendingHit.grade :
                PaddleContact.Grade(paddleFace ? Vector3.Distance(impactPoint, paddleFace.position) : float.PositiveInfinity, swingTime - ContactDelay)) : HitGrade.None;
            impactTime = swingTime;
            contactConsumed = contactConfirmed = true;
            // Resolve the visible pose at the same event that reverses the ball, then
            // retain a short contact pose before continuing the authored follow-through.
            if (Swinging)
                Sample(0);
            SfxPlayer.Play(Intent.kind == ShotKind.Serve ? GameSfx.Serve : Intent.power ? GameSfx.HitStrong : GameSfx.HitNormal, impactPoint);
            if (successful && impactVfx)
                impactVfx.Play(impactPoint, Intent, transform.forward, LastContactGrade);
            if (successful && LocalHitFeedback && LastContactGrade != HitGrade.None)
            {
                if (!gradeFeedback)
                    gradeFeedback = gameObject.AddComponent<HitGradeFeedback>();
                gradeFeedback.Show(LastContactGrade);
            }
            if (Intent.kind == ShotKind.Serve)
                SfxPlayer.Stop(GameSfx.Countdown);
            if (volley)
            {
                VolleyMomentum = true;
                lastVolleyTime = 0;
                balanceTime = 0;
            }
        }
        public void SynchronizeSwing(float elapsed)
        {
            if (!Swinging)
                return;
            swingTime = Mathf.Clamp(elapsed, 0, SwingDuration - .001f);
            Sample(0);
            previousSwingTime = swingTime;
            previousBodyPosition = transform.position;
            PreviousPaddlePosition = paddleFace.position;
        }
        public void Reaction(bool won, bool matchOver = false)
        {
            Swinging = false;
            if (won)
            {
                if (matchOver && Has("PKV_Dance_Win"))
                {
                    Play("PKV_Dance_Win");
                    if (paddle)
                        paddle.SetActive(false);
                }
                else Play("PKV_Idle_Ready");
            }
            else Play("PKV_Fault_Reaction");
        }
        public void SplitStep()
        {
            if (!Swinging && Velocity.sqrMagnitude < .2f)
                Play("PKV_SplitStep");
        }
        public void Step(Vector2 movement, bool run, float dt, bool canMove = true, Rect? movementBounds = null)
        {
            MovementBounds = movementBounds;
            Vector3 previousFace = paddleFace ? paddleFace.position : Vector3.zero;
            PreviousPaddlePosition = previousFace;
            previousBodyPosition = transform.position;
            previousSwingTime = swingTime;
            ObserveMovement(canMove ? movement : Vector2.zero, canMove && run);
            if (canMove)
            {
                var p = transform.position;
                var velocity = Velocity;
                MatchMovement.Step(ref p, ref velocity, movement, run, Swinging, end, dt, walkSpeed, runSpeed, movementBounds);
                transform.position = p;
                Velocity = velocity;
            }
            else Velocity = Vector3.MoveTowards(Velocity, Vector3.zero, 24f * dt);
            AdvancePose(dt, run, canMove);
            foley.Step(transform.position, run, dt, canMove);
            if (paddleFace)
                PaddleVelocity = (paddleFace.position - previousFace) / Mathf.Max(.0001f, dt);
        }
        /// <summary>Remote replicas sample the same authored poses without moving their network root.</summary>
        public void RenderNetworkPose(Vector3 velocity, bool run, float dt, bool canMove = true, Vector2 movement = default)
        {
            Vector3 previousFace = paddleFace ? paddleFace.position : Vector3.zero;
            PreviousPaddlePosition = previousFace;
            previousBodyPosition = transform.position;
            previousSwingTime = swingTime;
            movementIntent = canMove ? movement : Vector2.zero;
            MovementRun = run;
            Velocity = velocity;
            AdvancePose(dt, run, canMove);
            foley.Step(transform.position, run, dt, canMove);
            if (paddleFace)
                PaddleVelocity = (paddleFace.position - previousFace) / Mathf.Max(.0001f, dt);
        }
        void AdvancePose(float dt, bool run, bool canMove)
        {
            // Intent may remain pressed against a boundary. Only actual movement
            // should animate the feet; never run in place into the net or wall.
            Vector3 gait = canMove ? Velocity : Vector3.zero;
            string nextLocomotion = MovementClip(gait, run);
            if (Swinging && nextLocomotion != locomotionName)
                poseMixer?.BeginLegs(.10f);
            locomotionName = nextLocomotion;
            bool moving = canMove && locomotionName != "PKV_Idle_Ready";
            legWeight = Mathf.MoveTowards(legWeight, moving ? 1 : 0, dt / .06f);
            if (moving && Has(locomotionName))
            {
                float speed = Mathf.Clamp(Velocity.magnitude / (run ? runSpeed : walkSpeed), .25f, 1.6f);
                locomotionPhase = Mathf.Repeat(locomotionPhase + dt * speed / Mathf.Max(.01f, animationPlayer[locomotionName].length), 1);
            }
            if (VolleyMomentum)
            {
                lastVolleyTime += dt;
                // Control is re-established when follow-through is over and forward momentum stops.
                bool balanced = !Swinging && lastVolleyTime > .2f && Vector3.Dot(Velocity, -end * Vector3.forward) <= .08f;
                balanceTime = balanced ? balanceTime + dt : 0;
                if (balanceTime >= .14f)
                    VolleyMomentum = false;
            }
            if (Swinging)
            {
                swingTime += dt;
                float endTime = contactConfirmed ? impactTime + .035f + Intent.Duration - Intent.ContactDelay : SwingDuration;
                if (swingTime >= endTime)
                {
                    if (!contactConsumed) GetComponent<ActorTelemetry>()?.Outcome("Miss");
                    Swinging = false;
                    Play(moving ? locomotionName : VolleyMomentum ? "PKV_NVZ_MomentumRecovery" : "PKV_Idle_Ready");
                }
            }
            else if (!IsReaction() || clipName == "PKV_Fault_Reaction" && (moving || clipTime >= animationPlayer[clipName].length))
            {
                if (clipName != "PKV_SplitStep" || clipTime + dt >= animationPlayer[clipName].length - .000001f || gait.magnitude >= .15f)
                    Play(locomotionName);
            }
            Sample(dt);
        }
        string MovementClip(Vector3 direction, bool run) => direction.magnitude < .15f ? "PKV_Idle_Ready" :
            Mathf.Abs(direction.x) > Mathf.Abs(direction.z) * 1.15f ? (Vector3.Dot(direction, transform.right) < 0 ? "PKV_Shuffle_Left" : "PKV_Shuffle_Right") :
            direction.z * -end < -.1f ? "PKV_Backpedal" : run ? "PKV_Run_Forward" : "PKV_Walk_Forward";
        bool IsReaction() => clipName == "PKV_Fault_Reaction" || clipName == "PKV_Dance_Win";
        bool Has(string key) => animationPlayer && animationPlayer[key] != null;
        void Play(string key)
        {
            if (clipName == key && !Swinging)
                return;
            if (!Has(key))
                key = "PKV_Idle_Ready";
            if (!Has(key))
                return;
            // Finish the entry blend before even the earliest legal contact frame.
            poseMixer?.Begin(Swinging ? Mathf.Min(.055f, Mathf.Max(.02f, ContactDelay - PaddleContact.TimingWindow)) : .10f);
            animationPlayer.Stop();
            clipName = key;
            clipTime = 0;
            var state = animationPlayer[key];
            state.enabled = true;
            state.weight = 1;
            state.speed = 0;
            state.wrapMode = WrapMode.ClampForever;
        }
        void Sample(float dt)
        {
            if (!animationPlayer || !Has(clipName))
                return;
            clipTime += dt;
            var state = animationPlayer[clipName];
            float duration = Swinging ? SwingDuration : state.length;
            bool loop = clipName.Contains("Idle") || clipName.Contains("Walk") || clipName.Contains("Run") || clipName.Contains("Shuffle") || clipName.Contains("Backpedal");
            float time = Swinging ? swingTime : clipTime;
            float phase = Mathf.Min(time / Mathf.Max(.01f, duration), 1f);
            if (Swinging && sourceContactPhase > 0)
            {
                float after = contactConfirmed ? Mathf.Max(0, swingTime - impactTime - .035f) : Mathf.Max(0, swingTime - ContactDelay);
                phase = !contactConfirmed && swingTime < ContactDelay
                    ? swingTime / ContactDelay * sourceContactPhase
                    : sourceContactPhase + after / (Intent.Duration - Intent.ContactDelay) * (1 - sourceContactPhase);
                phase = Mathf.Clamp01(phase);
            }
            state.time = loop ? (clipName == locomotionName && locomotionName != "PKV_Idle_Ready" ? locomotionPhase * state.length : time % Mathf.Max(.01f, state.length)) : phase * state.length;
            state.enabled = true;
            bool movingStrike = Swinging && legWeight > .001f && poseMixer != null && Has(locomotionName);
            if (movingStrike)
            {
                var gait = animationPlayer[locomotionName];
                gait.clip.SampleAnimation(visual.gameObject, locomotionPhase * gait.length);
                poseMixer.CaptureLegs(dt);
            }
            state.clip.SampleAnimation(visual.gameObject, state.time);
            if (movingStrike)
                poseMixer.ApplyLegs(legWeight);
            poseMixer?.Blend(dt);
            if (Swinging && Intent.kind == ShotKind.Smash)
                OverheadPose(phase);
            if (Swinging && Intent.kind == ShotKind.Serve)
                ServeGrip(swingTime <= .8f ? Mathf.SmoothStep(0, 1, swingTime / .5f) : Mathf.Max(0, 1 - (swingTime - .8f) / .7f));
            if (Swinging && contactConfirmed)
                AlignPaddle(impactPoint, Mathf.Clamp01(1 - Mathf.Max(0, swingTime - impactTime - .035f) / .12f));
        }
        void AlignPaddle(Vector3 point, float weight)
        {
            if (!upperArm || !forearm || !hand || !paddleFace || weight <= 0)
                return;
            Quaternion wrist = hand.rotation;
            Vector3 a = upperArm.position, b = forearm.position, c = hand.position;
            Vector3 target = c + Vector3.ClampMagnitude(point - paddleFace.position, PaddleContact.MaxAssist) * weight;
            float first = Vector3.Distance(a, b), second = Vector3.Distance(b, c);
            Vector3 direction = (target - a).normalized;
            float distance = Mathf.Clamp(Vector3.Distance(a, target), Mathf.Abs(first - second) + .001f, first + second - .001f);
            Vector3 bend = Vector3.ProjectOnPlane(b - a, direction).normalized;
            if (bend.sqrMagnitude < .01f)
                bend = Vector3.ProjectOnPlane(transform.right, direction).normalized;
            float along = (first * first - second * second + distance * distance) / (2 * distance);
            Vector3 elbow = a + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, first * first - along * along));
            upperArm.rotation = Quaternion.FromToRotation(b - a, elbow - a) * upperArm.rotation;
            forearm.rotation = Quaternion.FromToRotation(hand.position - forearm.position, a + direction * distance - forearm.position) * forearm.rotation;
            hand.rotation = wrist;
        }
        public void ServeGrip(float weight)
        {
            if (!hand || !paddleFace)
                return;
            Vector3 axis = (transform.forward * .45f + Vector3.down * .85f).normalized;
            Quaternion rotation = Quaternion.FromToRotation(paddleFace.position - hand.position, axis) * hand.rotation;
            hand.rotation = Quaternion.Slerp(hand.rotation, rotation, weight);
        }
        void OverheadPose(float phase)
        {
            if (!upperArm || !forearm || !hand || !paddleFace)
                return;
            float weight = swingTime <= ContactDelay ? Mathf.SmoothStep(0, 1, swingTime / ContactDelay) : Mathf.Max(0, 1 - (swingTime - ContactDelay) / (Intent.Duration - Intent.ContactDelay));
            Vector3 desiredAxis = (Vector3.up * .25f + transform.forward * .15f).normalized;
            Quaternion wrist = Quaternion.FromToRotation(paddleFace.position - hand.position, desiredAxis) * hand.rotation;
            hand.rotation = Quaternion.Slerp(hand.rotation, wrist, weight);
            Vector3 target = transform.TransformPoint(new Vector3(.12f, 2.02f, .35f)) - (paddleFace.position - hand.position);
            Vector3 a = upperArm.position, b = forearm.position, c = hand.position;
            float first = Vector3.Distance(a, b), second = Vector3.Distance(b, c);
            Vector3 direction = (target - a).normalized;
            float distance = Mathf.Clamp(Vector3.Distance(a, target), Mathf.Abs(first - second) + .01f, first + second - .01f);
            target = a + direction * distance;
            float along = (first * first - second * second + distance * distance) / (2 * distance);
            float outwards = Mathf.Sqrt(Mathf.Max(0, first * first - along * along));
            Vector3 bend = Vector3.ProjectOnPlane(transform.right, direction).normalized;
            Vector3 elbow = a + direction * along + bend * outwards;
            Quaternion shoulder = Quaternion.FromToRotation(b - a, elbow - a) * upperArm.rotation;
            upperArm.rotation = Quaternion.Slerp(upperArm.rotation, shoulder, weight);
            Quaternion lower = Quaternion.FromToRotation(hand.position - forearm.position, target - forearm.position) * forearm.rotation;
            forearm.rotation = Quaternion.Slerp(forearm.rotation, lower, weight);
            wrist = Quaternion.FromToRotation(paddleFace.position - hand.position, desiredAxis) * hand.rotation;
            hand.rotation = Quaternion.Slerp(hand.rotation, wrist, weight);
        }
        void OnDrawGizmosSelected()
        {
            var old = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.color = new Color(.3f, 1, .85f, .7f);
            Gizmos.DrawWireCube(Swinging && Intent.kind == ShotKind.Smash ? new Vector3(0, 1.9f, PaddleContact.FrontCenter.z) : PaddleContact.FrontCenter, PaddleContact.FrontHalfSize * 2);
            Gizmos.matrix = Matrix4x4.identity;
            if (paddleFace)
            {
                Gizmos.color = new Color(1, .65f, .12f);
                Gizmos.DrawWireSphere(paddleFace.position, PaddleContact.PaddleRadius);
            }
            Gizmos.matrix = old;
        }
    }
}
