using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using PickleVerse.LocalMatch;
using LocalShotInput = PickleVerse.LocalMatch.ShotInput;

namespace PickleVerse.Multiplayer
{
    [Serializable] public struct PlayerFrame : INetworkSerializable, IEquatable<PlayerFrame>
    {
        public Vector3 position, velocity;
        public uint sequence;
        public Vector2 movement;
        public bool running;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref position); serializer.SerializeValue(ref velocity);
            serializer.SerializeValue(ref sequence); serializer.SerializeValue(ref movement);
            serializer.SerializeValue(ref running);
        }
        public bool Equals(PlayerFrame other) => position == other.position && velocity == other.velocity &&
            sequence == other.sequence && movement == other.movement && running == other.running;
    }

    public sealed class NetworkPlayer : NetworkBehaviour
    {
        public static readonly List<NetworkPlayer> All = new List<NetworkPlayer>();
        public NetworkVariable<FixedString64Bytes> Identity = new NetworkVariable<FixedString64Bytes>();
        public NetworkVariable<int> Team = new NetworkVariable<int>(), Index = new NetworkVariable<int>();
        // Replicate the physical end independently of the match object's spawn order.
        public NetworkVariable<int> CourtEnd = new NetworkVariable<int>(-1);
        public NetworkVariable<bool> Female = new NetworkVariable<bool>();
        public NetworkVariable<PlayerFrame> Frame = new NetworkVariable<PlayerFrame>();
        public MatchActor Actor { get; private set; }
        public Animator Animator { get; private set; }
        public Transform RightHand => Actor ? Actor.hand : null;
        public Transform LeftHand => Actor ? Actor.leftHand : null;
        public Transform PaddleContact => Actor ? Actor.paddleFace : null;
        public Vector3 Velocity => IsServer && Actor ? Actor.Velocity : IsOwner ? predictedVelocity : Frame.Value.velocity;
        public Vector3 ContactPosition => PaddleContact ? PaddleContact.position : transform.position + transform.forward * .7f + Vector3.up * .9f;
        public Vector3 WristPosition => RightHand ? RightHand.position : ContactPosition;
        public string Gender => Female.Value ? "Female" : "Male";
        public bool Connected = true;

        struct Move { public uint sequence; public Vector2 axes; public bool run; public float dt; }
        readonly List<Move> predicted = new List<Move>();
        LocalShotInput shotInput = new LocalShotInput();
        Vector3 predictedVelocity;
        Vector2 serverAxes;
        bool serverRun;
        uint sequence, lastReceived;
        double rateWindow;
        int receivedThisWindow;
        RoomMember initialMember;
        int initialIndex;

        public void Initialize(RoomMember member, int index, Vector3 position)
        { initialMember = member; initialIndex = index; transform.position = position; }

        public override void OnNetworkSpawn()
        {
            if (IsServer && initialMember != null)
            {
                Identity.Value = new FixedString64Bytes(initialMember.id);
                Team.Value = initialMember.team; Index.Value = initialIndex;
                Female.Value = initialMember.gender == "Female";
                CourtEnd.Value = transform.position.z < 0 ? -1 : 1;
                Frame.Value = new PlayerFrame { position = transform.position };
            }
            All.Add(this); sequence = Frame.Value.sequence;
            BuildVisual();
            Frame.OnValueChanged += Reconcile;
            CourtEnd.OnValueChanged += EndChanged;
        }
        public override void OnNetworkDespawn()
        {
            All.Remove(this); Frame.OnValueChanged -= Reconcile;
            CourtEnd.OnValueChanged -= EndChanged;
        }
        public override void OnGainedOwnership()
        { sequence = Frame.Value.sequence; ResetInput(); }

        void BuildVisual()
        {
            var settings = MultiplayerApp.Instance.Settings;
            var prefab = Female.Value ? settings.femaleActorPrefab : settings.maleActorPrefab;
            if (!prefab) throw new InvalidOperationException("Missing BotMatch actor prefab in MultiplayerSettings.");
            var instance = Instantiate(prefab, transform, false);
            instance.name = "CharacterVisual";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            Actor = instance.GetComponent<MatchActor>();
            if (!Actor) throw new InvalidOperationException("The multiplayer actor prefab must contain MatchActor.");
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true)) Destroy(body);
            foreach (var animator in instance.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            Animator = Actor.visual.GetComponent<Animator>();
            Actor.playerId = Female.Value ? 0 : 1;
            Actor.Initialize();
            ResetActor(transform.position, CourtEnd.Value);
            Actor.RefreshPose();

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "TeamMarker"; marker.transform.SetParent(transform, false);
            marker.transform.localPosition = new Vector3(0, .025f, 0);
            marker.transform.localScale = new Vector3(.8f, .02f, .8f);
            Destroy(marker.GetComponent<Collider>());
            marker.GetComponent<Renderer>().sharedMaterial = Team.Value == 0 ? settings.teamAMaterial : settings.teamBMaterial;
        }
        bool CanMove()
        {
            var match = NetworkMatch.Instance;
            return match && match.Started.Value && !match.Paused.Value && !match.Finished.Value &&
                !match.BetweenGames.Value && !match.BetweenPoints.Value && Connected;
        }
        void Update()
        {
            if (!IsSpawned || !Actor || !MultiplayerApp.Instance) return;
            var match = NetworkMatch.Instance;
            if (!IsServer)
            {
                if (!IsOwner)
                    transform.position = Vector3.Lerp(transform.position, Frame.Value.position, 1 - Mathf.Exp(-18 * Time.deltaTime));
                if (match && !match.Paused.Value)
                    Actor.RenderNetworkPose(Velocity, IsOwner ? LocalRun() : Frame.Value.running, Time.deltaTime);
            }
            if (!IsOwner) return;
            var keyboard = Keyboard.current;
            if (!CanMove() || keyboard == null || !Application.isFocused)
            { shotInput = new LocalShotInput(); return; }
            int mask = (Held(keyboard.jKey) ? 1 : 0) | (Held(keyboard.kKey) ? 2 : 0) |
                (Held(keyboard.lKey) ? 4 : 0) | (Held(keyboard.uKey) ? 8 : 0) | (Held(keyboard.iKey) ? 16 : 0);
            bool serve = match.WaitingServe.Value && match.ServerTeam.Value == Team.Value && match.ServerIndex.Value == Index.Value;
            if (shotInput.Tick(mask, keyboard.backspaceKey.wasPressedThisFrame, serve, out var local))
            {
                var intent = BotMatchGameplayService.FromLocal(local);
                ShotServerRpc((int)intent.kind, intent.attack, intent.crossCourt);
            }
        }
        static bool Held(UnityEngine.InputSystem.Controls.KeyControl key) => key.isPressed || key.wasPressedThisFrame;
        static bool LocalRun()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && Application.isFocused &&
                (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed || keyboard.spaceKey.isPressed);
        }
        static Vector2 LocalAxes()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !Application.isFocused) return Vector2.zero;
            return Vector2.ClampMagnitude(new Vector2(
                (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0)), 1);
        }
        void FixedUpdate()
        {
            if (!IsSpawned || !IsOwner || !Actor) return;
            if (!CanMove()) { predicted.Clear(); predictedVelocity = Vector3.zero; return; }
            var move = new Move { sequence = ++sequence, axes = LocalAxes(), run = LocalRun(), dt = Time.fixedDeltaTime };
            if (!IsServer)
            {
                var position = transform.position;
                Predict(ref position, ref predictedVelocity, move);
                transform.position = position;
                predicted.Add(move); if (predicted.Count > 150) predicted.RemoveAt(0);
            }
            MoveServerRpc(move.sequence, move.axes, move.run);
        }
        void Predict(ref Vector3 position, ref Vector3 velocity, Move move)
        {
            MatchMovement.Step(ref position, ref velocity, move.axes * -CourtEnd.Value, move.run,
                Actor.Swinging, CourtEnd.Value, move.dt, Actor.walkSpeed, Actor.runSpeed);
        }
        [ServerRpc] void MoveServerRpc(uint seq, Vector2 axes, bool run)
        {
            if (float.IsNaN(axes.x) || float.IsNaN(axes.y) || float.IsInfinity(axes.x) || float.IsInfinity(axes.y) || seq <= lastReceived) return;
            if (Time.realtimeSinceStartupAsDouble - rateWindow >= 1) { rateWindow = Time.realtimeSinceStartupAsDouble; receivedThisWindow = 0; }
            if (++receivedThisWindow > 65) return;
            lastReceived = seq;
            serverAxes = CanMove() ? Vector2.ClampMagnitude(axes, 1) : Vector2.zero;
            serverRun = CanMove() && run;
        }
        public void Simulate(float dt, bool canMove)
        {
            if (!IsServer || !Actor) return;
            bool moving = canMove && Connected;
            Actor.Step(moving ? serverAxes * -CourtEnd.Value : Vector2.zero, moving && serverRun, dt, moving);
            // MatchActor owns its child root; publish its position on the NetworkObject root.
            var position = Actor.transform.position;
            transform.position = position; Actor.transform.localPosition = Vector3.zero;
            Frame.Value = new PlayerFrame { position = position, velocity = Actor.Velocity, sequence = lastReceived,
                movement = moving ? serverAxes : Vector2.zero, running = moving && serverRun };
        }
        [ServerRpc] void ShotServerRpc(int kind, bool attack, bool crossCourt)
        {
            if (kind < 1 || kind > (int)ShotKind.Punch) return;
            NetworkMatch.Instance?.TrySwing(this, new ShotIntent { kind = (ShotKind)kind, attack = attack, crossCourt = crossCourt });
        }
        void Reconcile(PlayerFrame old, PlayerFrame frame)
        {
            if (!IsOwner || IsServer || !Actor) return;
            predicted.RemoveAll(move => move.sequence <= frame.sequence);
            var position = frame.position; var velocity = frame.velocity;
            foreach (var move in predicted) Predict(ref position, ref velocity, move);
            transform.position = position; predictedVelocity = velocity;
        }
        void EndChanged(int old, int end)
        {
            transform.rotation = Quaternion.Euler(0, end < 0 ? 0 : 180, 0);
            if (Actor) { Actor.end = end; Actor.transform.localRotation = Quaternion.identity; }
        }
        void ResetActor(Vector3 position, int end)
        {
            transform.position = position;
            transform.rotation = Quaternion.Euler(0, end < 0 ? 0 : 180, 0);
            if (!Actor) return;
            Actor.ResetAt(position, end);
            Actor.transform.localPosition = Vector3.zero;
            Actor.transform.localRotation = Quaternion.identity;
        }
        public void Teleport(Vector3 position)
        {
            if (!IsServer) return;
            int end = position.z < 0 ? -1 : 1;
            CourtEnd.Value = end; ResetInput(); ResetActor(position, end);
            Frame.Value = new PlayerFrame { position = position, sequence = lastReceived };
            TeleportClientRpc(position, lastReceived, end);
        }
        [ClientRpc] void TeleportClientRpc(Vector3 position, uint acknowledged, int end)
        {
            if (IsServer) return;
            ResetInput(); sequence = Math.Max(sequence, acknowledged);
            ResetActor(position, end); Actor.RefreshPose();
        }
        public void BeginSwing(ShotIntent intent, Vector3 predictedBall)
        {
            if (!IsServer || !Actor || !Actor.Swing(BotMatchGameplayService.ToLocal(intent), predictedBall)) return;
            SwingClientRpc((int)intent.kind, intent.attack, intent.crossCourt, predictedBall);
        }
        [ClientRpc] void SwingClientRpc(int kind, bool attack, bool crossCourt, Vector3 predictedBall)
        {
            if (IsServer || !Actor) return;
            Actor.Swing(BotMatchGameplayService.ToLocal(new ShotIntent { kind = (ShotKind)kind, attack = attack, crossCourt = crossCourt }), predictedBall);
        }
        public void ConsumeContact(bool volley)
        { if (!IsServer || !Actor) return; Actor.ConsumeContact(volley); ContactClientRpc(volley); }
        [ClientRpc] void ContactClientRpc(bool volley)
        { if (!IsServer && Actor) Actor.ConsumeContact(volley); }
        public void SplitStep()
        { if (!IsServer || !Actor) return; Actor.SplitStep(); SplitStepClientRpc(); }
        [ClientRpc] void SplitStepClientRpc()
        { if (!IsServer && Actor) Actor.SplitStep(); }
        public void React(bool won, bool matchOver = false)
        { if (!IsServer || !Actor) return; Actor.Reaction(won, matchOver); ReactClientRpc(won, matchOver); }
        [ClientRpc] void ReactClientRpc(bool won, bool matchOver)
        { if (!IsServer && Actor) Actor.Reaction(won, matchOver); }
        void OnApplicationFocus(bool focused) { if (!focused) shotInput = new LocalShotInput(); }
        public void ResetInput()
        {
            shotInput = new LocalShotInput(); serverAxes = Vector2.zero; serverRun = false;
            predicted.Clear(); predictedVelocity = Vector3.zero;
        }
    }
}
