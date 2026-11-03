using System;
using System.Collections.Generic;
using Fusion;
using Newtonsoft.Json.Linq;

using UnityEngine;
using UnityEngine.InputSystem;
using PickleVerse.LocalMatch;
using PickleVerse.Settings;
using LocalShotInput = PickleVerse.LocalMatch.ShotInput;

namespace PickleVerse.Multiplayer
{
    [Serializable]
    public struct PlayerFrame : INetworkStruct, IEquatable<PlayerFrame>
    {
        public Vector3 position, velocity;
        public uint sequence;
        public ushort motionEpoch;
        public Vector2 movement;
        public NetworkBool running;
        public bool Equals(PlayerFrame other) => position == other.position && velocity == other.velocity &&
            sequence == other.sequence && motionEpoch == other.motionEpoch && movement == other.movement && (bool)running == (bool)other.running;
    }

    public sealed partial class NetworkPlayer : NetworkBehaviour, IPhotonMessageTarget
    {
        public static readonly List<NetworkPlayer> All = new List<NetworkPlayer>();
        [Networked]
        [Capacity(64)]
        public string Identity { get; set; }
        [Networked]
        public int Team { get; set; }
        [Networked]
        public int Index { get; set; }
        [Networked]
        public int CourtEnd { get; set; } = -1;
        [Networked]
        public bool Female { get; set; }
        [Networked]
        public bool Automated { get; set; }
        [Networked]
        [Capacity(64)]
        public string PaddleSkin { get; set; } = "paddle.default";
        [Networked]
        public PlayerFrame Frame { get; set; }
        [Networked]
        public PlayerRef ControllerPeer { get; set; }
        public bool IsServer => HasStateAuthority;
        public bool IsSpawned => Object && Object.IsValid;
        public bool IsOwner => IsSpawned && ControllerPeer == Runner.LocalPlayer;
        PlayerFrame renderedFrame;
        int renderedEnd;
        string renderedSkin;
        bool renderedOwner;
        public MatchActor Actor { get; private set; }
        public Transform RightHand => Actor ? Actor.hand : null;
        public Transform LeftHand => Actor ? Actor.leftHand : null;
        public Transform PaddleContact => Actor ? Actor.paddleFace : null;
        public Vector3 Velocity => IsServer && Actor ? Actor.Velocity : IsOwner ? predictedVelocity : Frame.velocity;
        public Vector3 ContactPosition => PaddleContact ? PaddleContact.position : transform.position + transform.forward * .7f + Vector3.up * .9f;
        public Vector3 WristPosition => RightHand ? RightHand.position : ContactPosition;
        public string Gender => Female ? "Female" : "Male";
        public bool Connected = true;
        public bool IsLocalHuman => IsOwner && !Automated;

        struct Move { public uint sequence; public Vector2 axes; public bool run, swinging; public float dt; }
        readonly List<Move> predicted = new List<Move>();
        LocalShotInput shotInput = new LocalShotInput();
        string inputFeedback = "";
        int activeShotMask;
        int pendingShotMask;
        PickleVerse.LocalMatch.ShotIntent pendingShot;
        public string Selection => shotInput.Selection;
        public string SelectionKeys => shotInput.SelectionMask == 0 ? "" : GameControls.ShotKeys(shotInput.SelectionMask);
        public string ActiveShotKeys => Actor && Actor.Swinging ? GameControls.ShotKeys(activeShotMask) : "";
        Vector3 predictedVelocity;
        Vector2 serverAxes;
        bool serverRun;
        uint sequence, lastReceived;
        readonly MovementInputPlayback inputPlayback = new MovementInputPlayback();
        PhotonCommandChannel playbackChannel;
        Func<uint, MovementInputFrame?> readPlaybackInput;
        double rateWindow;
        int receivedThisWindow;
        RoomMember initialMember;
        int initialIndex;

        public void Initialize(RoomMember member, int index, Vector3 position)
        {
            initialMember = member;
            initialIndex = index;
            transform.position = position;
        }

        public override void Spawned()
        {
            if (IsServer && initialMember != null)
            {
                Identity = initialMember.id;
                PaddleSkin = initialMember.paddleId;
                Team = initialMember.team;
                Index = initialIndex;
                Female = initialMember.gender == "Female";
                Automated = initialMember.bot;
                CourtEnd = transform.position.z < 0 ? -1 : 1;
                Frame = new PlayerFrame { position = transform.position };
            }
            All.Add(this);
            sequence = Frame.sequence;
            inputPlayback.Reset(Frame.sequence);
            BuildVisual();



            ApplyPaddleSkin();
        }
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            All.Remove(this);


        }
        public override void Render()
        {
            if (!renderedFrame.Equals(Frame))
            {
                Reconcile(renderedFrame, Frame);
                renderedFrame = Frame;
            }
            if (renderedEnd != CourtEnd)
            {
                EndChanged(renderedEnd, CourtEnd);
                renderedEnd = CourtEnd;
            }
            if (renderedSkin != PaddleSkin)
            {
                renderedSkin = PaddleSkin;
                ApplyPaddleSkin();
            }
            if (renderedOwner != IsOwner)
            {
                renderedOwner = IsOwner;
                sequence = Frame.sequence;
                ResetInput();
                if (Actor)
                    Actor.LocalHitFeedback = IsLocalHuman;
            }
        }
        void ApplyPaddleSkin()
        {
#if !UNITY_SERVER
            if (Actor && Actor.paddle)
                PickleVerse.Shop.PaddleAppearance.Attach(Actor.paddle).Apply(PaddleSkin);
#endif
        }

        void BuildVisual()
        {
            var settings = MultiplayerApp.Instance.Settings;
            var prefab = Female ? settings.femaleActorPrefab : settings.maleActorPrefab;
            if (!prefab)
                throw new InvalidOperationException("Missing BotMatch actor prefab in MultiplayerSettings.");
            var instance = Instantiate(prefab, transform, false);
            instance.name = "CharacterVisual";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            Actor = instance.GetComponent<MatchActor>();
            if (!Actor)
                throw new InvalidOperationException("The multiplayer actor prefab must contain MatchActor.");
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true))
                Destroy(body);
            foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
                animator.enabled = false;
            Actor.playerId = Female ? 0 : 1;
            Actor.Initialize();
            Actor.LocalHitFeedback = IsLocalHuman;
            ResetActor(transform.position, CourtEnd);
            Actor.RefreshPose();

        }
        bool CanMove()
        {
            var match = NetworkMatch.Instance;
            return match && match.Started && !match.Paused && !match.Finished &&
                !match.BetweenGames && !match.BetweenPoints && !match.TimedOut && Connected;
        }
        void Update()
        {
            if (!IsSpawned || !Actor || !MultiplayerApp.Instance)
                return;
            var match = NetworkMatch.Instance;
            if (MultiplayerCourtUI.Instance && MultiplayerCourtUI.Instance.cinematic && MultiplayerCourtUI.Instance.cinematic.OwnsPose)
                return;
            if (IsLocalHuman && CanMove() && !InputBlocked())
            {
                var local = LocalAxes();
                var axes = local * -CourtEnd;
                bool run = LocalRun();
                if (IsServer)
                {
                    serverAxes = local;
                    serverRun = run;
                }
                Actor.ObserveMovement(axes, run);
            }
            if (!IsServer)
            {
                if (!IsOwner)
                    transform.position = Frame.position;
                if (match && !match.Paused)
                    Actor.RenderNetworkPose(Velocity, IsOwner ? LocalRun() : Frame.running, Time.deltaTime, CanMove(), (IsLocalHuman ? LocalAxes() : Frame.movement) * -CourtEnd);
            }
            if (!IsLocalHuman)
                return;
            var keyboard = Keyboard.current;
            if (keyboard != null && CanReadKeyboard() && !InputBlocked())
            {
                if (GameControls.Pressed(GameAction.Timeout))
                    TimeoutServerRpc();
                if (match && match.TimedOut && GameControls.Pressed(GameAction.Continue))
                    ResumeTimeoutServerRpc();
            }
            if (!CanMove() || InputBlocked())
            {
                shotInput = new LocalShotInput();
                return;
            }
            bool readKeys = keyboard != null && CanReadKeyboard();
            int mask = (readKeys ? GameControls.ShotMask : 0) | (MultiplayerCourtUI.Instance ? MultiplayerCourtUI.Instance.TouchShotMask : 0), tap = readKeys ? GameControls.TapMask : 0;
            bool serve = match.WaitingServe && match.ServerTeam == Team && match.ServerIndex == Index;
            bool cancel = readKeys && GameControls.Pressed(GameAction.CancelShot);
            if (tap != 0)
                TickShot(mask | tap, cancel, serve);
            TickShot(mask, cancel, serve);
        }
        public bool TouchShotsEnabled => IsSpawned && IsLocalHuman && Actor && CanMove() && !InputBlocked();
        public bool TouchServing => IsSpawned && NetworkMatch.Instance && NetworkMatch.Instance.IsSpawned && NetworkMatch.Instance.WaitingServe &&
            NetworkMatch.Instance.ServerTeam == Team && NetworkMatch.Instance.ServerIndex == Index;
        public void CommitTouchShot(int mask)
        {
            if (!TouchShotsEnabled)
                return;
            shotInput.Cancel();
            shotInput.Tick(0, false, TouchServing, out _);
            TickShot(mask, false, TouchServing);
            TickShot(mask & 24, false, TouchServing);
        }
        void TickShot(int mask, bool cancel, bool serve)
        {
            if (shotInput.Tick(mask, cancel, serve, out var local))
            {
                pendingShotMask = shotInput.CommittedMask;
                pendingShot = local;
                var intent = BotMatchGameplayService.FromLocal(local);
                ShotServerRpc((int)intent.kind, intent.attack, intent.crossCourt, LocalAxes(), LocalRun());
            }
            if (!string.IsNullOrEmpty(shotInput.Feedback) && shotInput.Feedback != inputFeedback)
            {
                inputFeedback = shotInput.Feedback;
                var ui = FindAnyObjectByType<MultiplayerCourtUI>();
                if (ui)
                    ui.ShowAnnouncement("TỔ HỢP KHÔNG HỢP LỆ", inputFeedback, 1.25f, AnnouncementTone.Fault);
            }
        }
        static bool CanReadKeyboard() => Application.isFocused ||
            InputSystem.settings.backgroundBehavior == InputSettings.BackgroundBehavior.IgnoreFocus;
        static bool InputBlocked() => SettingsOverlay.IsOpen || SettingsOverlay.Loading || MultiplayerCourtUI.Instance && MultiplayerCourtUI.Instance.InputBlocked;
        static bool LocalRun()
        {
            var keyboard = Keyboard.current;
            return !InputBlocked() && (MultiplayerCourtUI.Instance && MultiplayerCourtUI.Instance.TouchRun || keyboard != null && CanReadKeyboard() &&
                (GameControls.Held(GameAction.Run) || keyboard.spaceKey.isPressed && !Array.Exists(GameSettings.Current.keys, k => k == (int)Key.Space)));
        }
        static Vector2 LocalAxes()
        {
            var keyboard = Keyboard.current;
            if (InputBlocked())
                return Vector2.zero;
            Vector2 touch = MultiplayerCourtUI.Instance ? MultiplayerCourtUI.Instance.TouchMovement : Vector2.zero;
            if (keyboard == null || !CanReadKeyboard())
                return touch;
            return Vector2.ClampMagnitude(touch + new Vector2(
                (GameControls.Held(GameAction.Right) ? 1 : 0) - (GameControls.Held(GameAction.Left) ? 1 : 0),
                (GameControls.Held(GameAction.Forward) ? 1 : 0) - (GameControls.Held(GameAction.Backward) ? 1 : 0)), 1);
        }
        internal void CollectLocalInput(float deltaTime)
        {
            if (!IsSpawned || !IsLocalHuman || !Actor)
                return;
            if (!CanMove())
            {
                predicted.Clear();
                predictedVelocity = Vector3.zero;
                return;
            }
            var move = new Move { sequence = ++sequence, axes = LocalAxes(), run = LocalRun(), swinging = Actor.Swinging, dt = deltaTime };
            if (!IsServer)
            {
                var position = transform.position;
                Predict(ref position, ref predictedVelocity, move);
                transform.position = position;
                predicted.Add(move);
                if (predicted.Count > 150)
                    predicted.RemoveAt(0);
            }
            NetworkQualityDiagnostics.RecordInput(move.sequence, Frame.sequence, predicted.Count);
            PhotonSession.Find(Runner)?.Channel?.Input(move.sequence, move.axes, move.run);
        }
        public DedicatedInput CaptureDedicatedInput()
        {
            CollectLocalInput(Runner.DeltaTime);
            return new DedicatedInput { Movement = CanMove() && !InputBlocked() ? LocalAxes() : Vector2.zero,
                Running = CanMove() && !InputBlocked() && LocalRun(), Sequence = sequence };
        }
        void Predict(ref Vector3 position, ref Vector3 velocity, Move move)
        {
            // Match the authority's 120 Hz movement integration, rather than one large render/network step.
            float remaining = move.dt;
            while (remaining > .000001f)
            {
                float step = Mathf.Min(1f / 120f, remaining);
                MatchMovement.Step(ref position, ref velocity, move.axes * -CourtEnd, move.run,
                    move.swinging, CourtEnd, step, Actor.walkSpeed, Actor.runSpeed);
                remaining -= step;
            }
        }
        void MoveFrom(uint seq, Vector2 axes, bool run)
        {
            if (float.IsNaN(axes.x) || float.IsNaN(axes.y) || float.IsInfinity(axes.x) || float.IsInfinity(axes.y) || seq <= lastReceived)
                return;
            if (Time.realtimeSinceStartupAsDouble - rateWindow >= 1)
            {
                rateWindow = Time.realtimeSinceStartupAsDouble;
                receivedThisWindow = 0;
            }
            if (++receivedThisWindow > 65)
                return;
            lastReceived = seq;
            serverAxes = CanMove() ? Vector2.ClampMagnitude(axes, 1) : Vector2.zero;
            serverRun = CanMove() && run;
        }
        public void Simulate(float dt, bool canMove)
        {
            if (!IsServer || !Actor)
                return;
            var channel = PhotonCommandChannel.For(Runner, ControllerPeer);
            if (PhotonSession.Find(Runner)?.IsDedicated == true && !Automated && Runner.TryGetInputForPlayer<DedicatedInput>(ControllerPeer, out var input))
                MoveFrom(input.Sequence, input.Movement, input.Running);
            bool moving = canMove && Connected;
            if (channel && !Automated && PhotonSession.Find(Runner)?.IsDedicated != true)
            {
                if (playbackChannel != channel)
                {
                    playbackChannel = channel; readPlaybackInput = channel.ReadInput;
                    inputPlayback.Reset(lastReceived);
                }
                if (channel.InputSequence > inputPlayback.Acknowledged + PhotonCommandChannel.InputHistorySize)
                {
                    // A gap beyond retained history is an explicit discontinuity, never a false ACK.
                    inputPlayback.Reset(channel.InputSequence - PhotonCommandChannel.InputHistorySize);
                    Frame = new PlayerFrame { position = Frame.position, velocity = Frame.velocity,
                        sequence = inputPlayback.Acknowledged, motionEpoch = unchecked((ushort)(Frame.motionEpoch + 1)) };
                }
                float budget = dt;
                while (budget > .000001f)
                {
                    bool available = inputPlayback.Take(budget, Runner.DeltaTime, readPlaybackInput, out var bufferedInput, out float step);
                    serverAxes = available ? Vector2.ClampMagnitude(bufferedInput.axes, 1) : Vector2.zero;
                    serverRun = available && bufferedInput.running;
                    Actor.Step(moving ? serverAxes * -CourtEnd : Vector2.zero, moving && serverRun, step, moving);
                    budget -= step;
                }
                lastReceived = inputPlayback.Acknowledged;
            }
            else Actor.Step(moving ? serverAxes * -CourtEnd : Vector2.zero, moving && serverRun, dt, moving);
            // MatchActor owns its child root; publish its position on the NetworkObject root.
            var position = Actor.transform.position;
            transform.position = position;
            Actor.transform.localPosition = Vector3.zero;
            Frame = new PlayerFrame
            {
                motionEpoch = Frame.motionEpoch,
                position = position,
                velocity = Actor.Velocity,
                sequence = lastReceived,
                movement = moving ? serverAxes : Vector2.zero,
                running = moving && serverRun
            };
        }
        public void SetAutomatedInput(Vector2 worldMovement, bool run)
        {
            if (!IsServer)
                return;
            serverAxes = Vector2.ClampMagnitude(worldMovement, 1) * -CourtEnd;
            serverRun = run;
        }
        void ShotFrom(int kind, bool attack, bool crossCourt, Vector2 axes, bool run)
        {
            if (kind < 1 || kind > (int)ShotKind.Punch || !CanMove() || !Actor || float.IsNaN(axes.x) || float.IsNaN(axes.y) || float.IsInfinity(axes.x) || float.IsInfinity(axes.y))
                return;
            // Select the contact pose against the same movement snapshot as the shot.
            // Later movement packets update locomotion without cancelling this strike.
            serverAxes = Vector2.ClampMagnitude(axes, 1);
            serverRun = run;
            Actor.ObserveMovement(serverAxes * -CourtEnd, run);
            NetworkMatch.Instance?.TrySwing(this, new ShotIntent { kind = (ShotKind)kind, attack = attack, crossCourt = crossCourt });
        }
        void TimeoutFrom()
        {
            NetworkMatch.Instance?.RequestTimeout(this);
        }
        void ResumeTimeoutFrom()
        {
            NetworkMatch.Instance?.ResumeTimeout(this);
        }
        public void RequestTimeout()
        {
            if (IsOwner && IsSpawned)
                TimeoutServerRpc();
        }
        public void ResumeTimeout()
        {
            if (IsOwner && IsSpawned)
                ResumeTimeoutServerRpc();
        }
        void Reconcile(PlayerFrame old, PlayerFrame frame)
        {
            if (!IsOwner || IsServer || !Actor)
                return;
            if (old.motionEpoch != frame.motionEpoch)
            {
                ResetInput(); visualCorrection = Vector3.zero;
                sequence = Math.Max(sequence, frame.sequence);
                transform.position = frame.position; predictedVelocity = frame.velocity;
                return;
            }
            if (frame.sequence < old.sequence) return;
            predicted.RemoveAll(move => move.sequence <= frame.sequence);
            var position = frame.position;
            var velocity = frame.velocity;
            foreach (var move in predicted)
                Predict(ref position, ref velocity, move);
            var correction = transform.position - position;
            visualCorrection = Vector3.ClampMagnitude(visualCorrection + correction, 1.5f);
            NetworkQualityDiagnostics.RecordCorrection(correction.magnitude, predicted.Count, sequence, frame.sequence);
            transform.position = position;
            predictedVelocity = velocity;
        }
        void EndChanged(int old, int end)
        {
            transform.rotation = Quaternion.Euler(0, end < 0 ? 0 : 180, 0);
            if (Actor)
            {
                Actor.end = end;
                Actor.transform.localRotation = Quaternion.identity;
            }
        }
        void ResetActor(Vector3 position, int end)
        {
            transform.position = position;
            transform.rotation = Quaternion.Euler(0, end < 0 ? 0 : 180, 0);
            if (!Actor)
                return;
            Actor.ResetAt(position, end);
            Actor.transform.localPosition = Vector3.zero;
            Actor.transform.localRotation = Quaternion.identity;
        }
        public void Teleport(Vector3 position)
        {
            if (!IsServer)
                return;
            int end = position.z < 0 ? -1 : 1;
            CourtEnd = end;
            ResetInput();
            inputPlayback.Reset(lastReceived);
            ResetActor(position, end);
            Actor.RefreshPose();
            Frame = new PlayerFrame { position = position, sequence = lastReceived, motionEpoch = unchecked((ushort)(Frame.motionEpoch + 1)) };
            TeleportClientRpc(position, lastReceived, end);
        }
        void TeleportEffect(Vector3 position, uint acknowledged, int end)
        {
            if (IsServer)
                return;
            ResetInput();
            sequence = Math.Max(sequence, acknowledged);
            ResetActor(position, end);
            Actor.RefreshPose();
        }
        public void BeginSwing(ShotIntent intent, Vector3 predictedBall, float contactDelay = -1)
        {
            if (!IsServer || !Actor || !Actor.Swing(BotMatchGameplayService.ToLocal(intent), predictedBall, contactDelay))
                return;
            SetActiveShotKeys();
            SwingClientRpc((int)intent.kind, intent.attack, intent.crossCourt, predictedBall, Actor.ContactDelay, Runner.SimulationTime);
        }
        void SwingEffect(int kind, bool attack, bool crossCourt, Vector3 predictedBall, float delay, double started)
        {
            if (IsServer || !Actor)
                return;
            if (Actor.Swing(BotMatchGameplayService.ToLocal(new ShotIntent { kind = (ShotKind)kind, attack = attack, crossCourt = crossCourt }), predictedBall, delay))
            {
                Actor.SynchronizeSwing((float)System.Math.Max(0, Runner.SimulationTime - started));
                SetActiveShotKeys();
            }
        }
        void SetActiveShotKeys()
        {
            var shot = Actor.Intent;
            if (IsLocalHuman && pendingShotMask != 0 && pendingShot.kind == shot.kind && pendingShot.power == shot.power && pendingShot.cross == shot.cross)
                activeShotMask = pendingShotMask;
            else activeShotMask = (shot.kind == PickleVerse.LocalMatch.ShotKind.Drop ? 3 : shot.kind == PickleVerse.LocalMatch.ShotKind.Smash ? 5 : shot.kind == PickleVerse.LocalMatch.ShotKind.Reset ? 6 : shot.kind == PickleVerse.LocalMatch.ShotKind.Dink ? 2 : shot.kind == PickleVerse.LocalMatch.ShotKind.Volley ? 4 : 1) | (shot.power ? 8 : 0) | (shot.cross ? 16 : 0);
            pendingShotMask = 0;
        }
        public void ConsumeContact(bool volley, Vector3? position = null, bool successful = true, Vector3 velocity = default)
        {
            if (!IsServer || !Actor)
                return;
            Vector3 contact = position ?? Actor.paddleFace.position;
            Actor.ConsumeContact(volley, contact, successful);
            ContactClientRpc(volley, contact, successful, velocity, Runner.SimulationTime, (byte)Actor.LastContactGrade);
        }
        void ContactEffect(bool volley, Vector3 position, bool successful, Vector3 velocity, double time, byte grade)
        {
            if (!IsServer && Actor)
            {
                Actor.ConsumeContact(volley, position, successful, (HitGrade)grade);
                if (successful)
                    NetworkMatch.Instance?.PresentContact(position, velocity, time);
            }
        }
        public void SplitStep()
        {
            if (!IsServer || !Actor)
                return;
            Actor.SplitStep();
            SplitStepClientRpc();
        }
        void SplitStepEffect()
        {
            if (!IsServer && Actor)
                Actor.SplitStep();
        }
        public void React(bool won, bool matchOver = false)
        {
            if (!IsServer || !Actor)
                return;
            Actor.Reaction(won, matchOver);
            ReactClientRpc(won, matchOver);
        }
        void ReactEffect(bool won, bool matchOver)
        {
            if (!IsServer && Actor)
                Actor.Reaction(won, matchOver);
        }
        void OnApplicationFocus(bool focused)
        {
            if (!focused)
                shotInput = new LocalShotInput();
        }
        public void ResetInput()
        {
            visualCorrection = Vector3.zero;
            shotInput = new LocalShotInput();
            serverAxes = Vector2.zero;
            serverRun = false;
            activeShotMask = pendingShotMask = 0;
            if (IsLocalHuman && MultiplayerCourtUI.Instance)
                MultiplayerCourtUI.Instance.ClearTouch();
            inputFeedback = "";
            predicted.Clear();
            predictedVelocity = Vector3.zero;
        }
    }
}
