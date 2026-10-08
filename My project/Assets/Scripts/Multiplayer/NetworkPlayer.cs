using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using PickleVerse.LocalMatch;

namespace PickleVerse.Multiplayer
{
    [Serializable] public struct PlayerFrame : INetworkSerializable, IEquatable<PlayerFrame>
    {
        public Vector3 position, velocity;
        public uint sequence;
        public Vector2 movement;
        public bool running;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        { s.SerializeValue(ref position); s.SerializeValue(ref velocity); s.SerializeValue(ref sequence); s.SerializeValue(ref movement); s.SerializeValue(ref running); }
        public bool Equals(PlayerFrame o) => position == o.position && velocity == o.velocity && sequence == o.sequence && movement == o.movement && running == o.running;
    }
    public sealed class NetworkPlayer : NetworkBehaviour
    {
        public static readonly List<NetworkPlayer> All = new List<NetworkPlayer>();
        public NetworkVariable<FixedString64Bytes> Identity = new NetworkVariable<FixedString64Bytes>();
        public NetworkVariable<int> Team = new NetworkVariable<int>(), Index = new NetworkVariable<int>();
        public NetworkVariable<bool> Female = new NetworkVariable<bool>();
        public NetworkVariable<int> CourtEnd = new NetworkVariable<int>(-1);
        public NetworkVariable<PlayerFrame> Frame = new NetworkVariable<PlayerFrame>();
        public MatchActor Actor { get; private set; }
        public Animator Animator => Actor ? Actor.visual.GetComponent<Animator>() : null;
        public Transform RightHand => Actor ? Actor.hand : null;
        public Transform LeftHand { get; private set; }
        public Transform PaddleContact => Actor ? Actor.paddleFace : null;
        public Vector3 Velocity => Actor ? Actor.Velocity : Vector3.zero;
        public Vector3 ContactPosition => Actor.paddleFace.position;
        public Vector3 WristPosition => Actor.hand.position;
        public string Gender => Female.Value ? "Female" : "Male";
        public bool Connected = true;
        struct Move { public uint sequence; public Vector2 axes; public bool run; }
        readonly Queue<Move> received = new Queue<Move>();
        readonly List<Move> predicted = new List<Move>();
        readonly ShotInput input = new ShotInput();
        uint sequence, lastReceived;
        double rateWindow, lastMoveTime;
        int receivedThisWindow;
        Move current;
        Vector3 predictedVelocity;
        RoomMember initialMember;
        int initialIndex;

        public override void OnNetworkSpawn()
        {
            if (IsServer && initialMember != null)
            {
                Identity.Value = new FixedString64Bytes(initialMember.id); Team.Value = initialMember.team;
                Index.Value = initialIndex; Female.Value = initialMember.gender == "Female";
                CourtEnd.Value = NetworkMatch.Instance.PlayerEnd(initialMember.team);
                Frame.Value = new PlayerFrame { position = transform.position };
            }
            All.Add(this); sequence = Frame.Value.sequence;
            var settings = MultiplayerApp.Instance.Settings;
            var prefab = Female.Value ? settings.femaleActorPrefab : settings.maleActorPrefab;
            if (!prefab) throw new InvalidOperationException("Thiếu prefab MatchActor từ BotMatch.");
            Actor = Instantiate(prefab, transform).GetComponent<MatchActor>();
            Actor.name = "CharacterActor"; Actor.playerId = Team.Value * 2 + Index.Value;
            // NGO may spawn players before the match object on a client. The end
            // travels with the player, including reconnects after changing ends.
            int end=CourtEnd.Value;
            transform.rotation = Quaternion.Euler(0, end < 0 ? 0 : 180, 0);
            Actor.Initialize(); Actor.ResetAt(Frame.Value.position, end);
            Actor.transform.localPosition = Vector3.zero;
            Frame.OnValueChanged += Reconcile;
        }
        public override void OnNetworkDespawn() { All.Remove(this); Frame.OnValueChanged -= Reconcile; }
        public override void OnGainedOwnership() { sequence = Frame.Value.sequence; predicted.Clear(); input.Cancel(); }
        public void Initialize(RoomMember member, int index, Vector3 position)
        { initialMember = member; initialIndex = index; transform.position = position; }
        void Update()
        {
            if (!IsSpawned || !Actor || !NetworkMatch.Instance) return;
            var match = NetworkMatch.Instance;
            if (!IsServer)
            {
                if (!IsOwner) transform.position = Vector3.Lerp(transform.position, Frame.Value.position, 1 - Mathf.Exp(-18 * Time.deltaTime));
                Actor.RenderNetworkPose(IsOwner ? predictedVelocity : Frame.Value.velocity, Frame.Value.running,
                    match.Paused.Value ? 0 : Time.deltaTime);
            }
            if (!IsOwner || match.Paused.Value || match.Finished.Value || match.BetweenGames.Value || match.BetweenPoints.Value || !match.Started.Value || !Application.isFocused)
            { input.Cancel(); return; }
            var k = Keyboard.current;
            if (k == null) { input.Cancel(); return; }
            bool serving = match.WaitingServe.Value && match.ServerTeam.Value == Team.Value && match.ServerIndex.Value == Index.Value;
            int mask = (k.jKey.isPressed ? 1 : 0) | (k.kKey.isPressed ? 2 : 0) | (k.lKey.isPressed ? 4 : 0) |
                (k.uKey.isPressed ? 8 : 0) | (k.iKey.isPressed ? 16 : 0);
            int taps = (k.jKey.wasPressedThisFrame && !k.jKey.isPressed ? 1 : 0) |
                (k.kKey.wasPressedThisFrame && !k.kKey.isPressed ? 2 : 0) | (k.lKey.wasPressedThisFrame && !k.lKey.isPressed ? 4 : 0);
            if (taps != 0) SubmitInput(mask | taps, serving, k.backspaceKey.wasPressedThisFrame);
            SubmitInput(mask, serving, k.backspaceKey.wasPressedThisFrame);
        }
        void SubmitInput(int mask, bool serving, bool cancel)
        {
            if (input.Tick(mask, cancel, serving, out var shot))
            {
                var intent = BotMatchGameplayService.FromLocal(shot);
                ShotServerRpc((int)intent.kind, intent.attack, intent.crossCourt);
            }
        }
        void FixedUpdate()
        {
            if (!IsSpawned || !Actor) return;
            var match = NetworkMatch.Instance;
            bool move = match && match.Started.Value && !match.Paused.Value && !match.Finished.Value && !match.BetweenGames.Value && !match.BetweenPoints.Value && Connected;
            if (IsOwner && move)
            {
                var k = Keyboard.current; Vector2 axes = Vector2.zero; bool run = false;
                if (k != null && Application.isFocused)
                {
                    axes = new Vector2((k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0),
                        (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0));
                    axes = Vector2.ClampMagnitude(axes, 1); run = k.leftShiftKey.isPressed || k.rightShiftKey.isPressed || k.spaceKey.isPressed;
                }
                var command = new Move { sequence = ++sequence, axes = axes, run = run };
                if (!IsServer)
                {
                    var p = transform.position;
                    Predict(ref p, ref predictedVelocity, command); transform.position = p;
                    predicted.Add(command); if (predicted.Count > 150) predicted.RemoveAt(0);
                }
                MoveServerRpc(command.sequence, axes, run);
            }
            if (IsServer && received.Count > 0) { current = received.Dequeue(); lastMoveTime = Time.realtimeSinceStartupAsDouble; }
        }
        public void Simulate(float dt, bool canMove)
        {
            if (!IsServer || !Actor) return;
            var axes = Time.realtimeSinceStartupAsDouble - lastMoveTime < .2 ? current.axes : Vector2.zero;
            Actor.Step(axes * -Actor.end, current.run, dt, canMove && Connected);
            var position = Actor.transform.position; transform.position = position; Actor.transform.localPosition = Vector3.zero;
            Frame.Value = new PlayerFrame { position = position, velocity = Actor.Velocity, sequence = current.sequence, movement = axes, running = current.run };
        }
        void Predict(ref Vector3 position, ref Vector3 velocity, Move command)
        { MatchMovement.Step(ref position, ref velocity, command.axes * -Actor.end, command.run, Actor.Swinging, Actor.end, Time.fixedDeltaTime, Actor.walkSpeed, Actor.runSpeed); }
        public Vector3 Advance(Vector3 position, Vector2 movement, bool run)
        { var v = predictedVelocity; Predict(ref position, ref v, new Move { axes = movement, run = run }); return position; }
        [ServerRpc] void MoveServerRpc(uint seq, Vector2 axes, bool run)
        {
            if (float.IsNaN(axes.x) || float.IsNaN(axes.y) || float.IsInfinity(axes.x) || float.IsInfinity(axes.y) || seq <= lastReceived) return;
            if (Time.realtimeSinceStartupAsDouble - rateWindow >= 1) { rateWindow = Time.realtimeSinceStartupAsDouble; receivedThisWindow = 0; }
            if (++receivedThisWindow > 65) return;
            lastReceived = seq; if (received.Count >= 6) received.Dequeue();
            received.Enqueue(new Move { sequence = seq, axes = Vector2.ClampMagnitude(axes, 1), run = run });
        }
        [ServerRpc] void ShotServerRpc(int kind, bool attack, bool crossCourt)
        {
            if (kind < 1 || kind > (int)ShotKind.Punch) return;
            NetworkMatch.Instance?.TrySwing(this, new ShotIntent { kind = (ShotKind)kind, attack = attack, crossCourt = crossCourt });
        }
        void Reconcile(PlayerFrame old, PlayerFrame frame)
        {
            if (!IsOwner || IsServer) return;
            predicted.RemoveAll(p => p.sequence <= frame.sequence);
            var position = frame.position; var velocity = frame.velocity;
            foreach (var command in predicted) Predict(ref position, ref velocity, command);
            transform.position = position; predictedVelocity = velocity;
        }
        public void Teleport(Vector3 position)
        {
            if (!IsServer) return;
            received.Clear(); current = new Move { sequence = lastReceived }; transform.position = position;
            int end=NetworkMatch.Instance.PlayerEnd(Team.Value); CourtEnd.Value=end; transform.rotation=Quaternion.Euler(0,end<0?0:180,0);
            Actor.ResetAt(position, end); Actor.transform.localPosition = Vector3.zero;
            Frame.Value = new PlayerFrame { position = position, sequence = lastReceived };
            TeleportClientRpc(position, lastReceived, end);
        }
        [ClientRpc] void TeleportClientRpc(Vector3 position, uint acknowledged, int end)
        {
            predicted.Clear(); sequence = Math.Max(sequence, acknowledged); transform.position = position; predictedVelocity = Vector3.zero;
            transform.rotation=Quaternion.Euler(0,end<0?0:180,0);
            Actor.ResetAt(position, end); Actor.transform.localPosition = Vector3.zero; input.Cancel();
        }
        public void BeginSwing(ShotIntent shot, Vector3 predictedBall)
        {
            if (IsServer) Actor.Swing(BotMatchGameplayService.ToLocal(shot), predictedBall);
            BeginSwingClientRpc((int)shot.kind, shot.attack, shot.crossCourt, predictedBall);
        }
        [ClientRpc] void BeginSwingClientRpc(int kind, bool power, bool cross, Vector3 predictedBall)
        { if (!IsServer) Actor.Swing(BotMatchGameplayService.ToLocal(new ShotIntent { kind = (ShotKind)kind, attack = power, crossCourt = cross }), predictedBall); }
        public void React(bool won, bool matchOver)
        { if (IsServer) Actor.Reaction(won, matchOver); ReactionClientRpc(won, matchOver); }
        [ClientRpc] void ReactionClientRpc(bool won, bool matchOver) { if (!IsServer) Actor.Reaction(won, matchOver); }
        public void ConsumeContact(bool volley) { Actor.ConsumeContact(volley); ContactClientRpc(volley); }
        [ClientRpc] void ContactClientRpc(bool volley) { if (!IsServer) Actor.ConsumeContact(volley); }
        public void SplitStep() { Actor.SplitStep(); SplitStepClientRpc(); }
        [ClientRpc] void SplitStepClientRpc() { if (!IsServer) Actor.SplitStep(); }
        void OnApplicationFocus(bool focus) { if (!focus) input.Cancel(); }
        public void ResetInput() { input.Cancel(); received.Clear(); predicted.Clear(); current = new Move { sequence = lastReceived }; }
    }
}
