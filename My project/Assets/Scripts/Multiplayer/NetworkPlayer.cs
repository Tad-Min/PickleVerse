using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using PickleVerse.LocalMatch;
using PickleVerse.Settings;
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
        public NetworkVariable<bool> Automated = new NetworkVariable<bool>();
        public NetworkVariable<FixedString64Bytes> PaddleSkin = new NetworkVariable<FixedString64Bytes>(new FixedString64Bytes(PickleVerse.Shop.ShopCatalog.DefaultPaddleId));
        public NetworkVariable<PlayerFrame> Frame = new NetworkVariable<PlayerFrame>();
        public MatchActor Actor { get; private set; }
        public Transform RightHand => Actor ? Actor.hand : null;
        public Transform LeftHand => Actor ? Actor.leftHand : null;
        public Transform PaddleContact => Actor ? Actor.paddleFace : null;
        public Vector3 Velocity => IsServer && Actor ? Actor.Velocity : IsOwner ? predictedVelocity : Frame.Value.velocity;
        public Vector3 ContactPosition => PaddleContact ? PaddleContact.position : transform.position + transform.forward * .7f + Vector3.up * .9f;
        public Vector3 WristPosition => RightHand ? RightHand.position : ContactPosition;
        public string Gender => Female.Value ? "Female" : "Male";
        public bool Connected = true;
        public bool IsLocalHuman => IsOwner && !Automated.Value;

        struct Move { public uint sequence; public Vector2 axes; public bool run, swinging; public float dt; }
        readonly List<Move> predicted = new List<Move>();
        LocalShotInput shotInput = new LocalShotInput();
        string inputFeedback="";
        int activeShotMask;
        int pendingShotMask;
        PickleVerse.LocalMatch.ShotIntent pendingShot;
        public string Selection => shotInput.Selection;
        public string SelectionKeys => shotInput.SelectionMask==0?"":GameControls.ShotKeys(shotInput.SelectionMask);
        public string ActiveShotKeys => Actor && Actor.Swinging?GameControls.ShotKeys(activeShotMask):"";
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
                Automated.Value = initialMember.bot;
                CourtEnd.Value = transform.position.z < 0 ? -1 : 1;
                Frame.Value = new PlayerFrame { position = transform.position };
            }
            All.Add(this); sequence = Frame.Value.sequence;
            BuildVisual();
            Frame.OnValueChanged += Reconcile;
            CourtEnd.OnValueChanged += EndChanged;
            PaddleSkin.OnValueChanged += SkinChanged;
            ApplyPaddleSkin();if(IsLocalHuman)PublishPaddleSkin();
        }
        public override void OnNetworkDespawn()
        {
            All.Remove(this); Frame.OnValueChanged -= Reconcile;
            CourtEnd.OnValueChanged -= EndChanged;
            PaddleSkin.OnValueChanged -= SkinChanged;
        }
        public override void OnGainedOwnership()
        { sequence = Frame.Value.sequence; ResetInput();if(IsSpawned&&IsLocalHuman)PublishPaddleSkin(); }

        async void PublishPaddleSkin()
        {
            try{var state=await PickleVerse.Shop.PlayerEconomy.Service.ReadAsync();if(this&&IsSpawned&&IsLocalHuman)SelectPaddleServerRpc(new FixedString64Bytes(state.equippedPaddle));}
            catch(Exception e){Debug.LogWarning("Paddle selection unavailable: "+e.Message);}
        }
        [ServerRpc] void SelectPaddleServerRpc(FixedString64Bytes skinId)
        {
            // Prototype inventory is local. A server economy can validate ownership here later.
            if(PickleVerse.Shop.PlayerEconomy.Catalog.Find(skinId.ToString())!=null)PaddleSkin.Value=skinId;
        }
        void SkinChanged(FixedString64Bytes previous,FixedString64Bytes current){ApplyPaddleSkin();}
        void ApplyPaddleSkin(){if(Actor&&Actor.paddle)PickleVerse.Shop.PaddleAppearance.Attach(Actor.paddle).Apply(PaddleSkin.Value.ToString());}

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
            Actor.playerId = Female.Value ? 0 : 1;
            Actor.Initialize();
            ResetActor(transform.position, CourtEnd.Value);
            Actor.RefreshPose();

        }
        bool CanMove()
        {
            var match = NetworkMatch.Instance;
            return match && match.Started.Value && !match.Paused.Value && !match.Finished.Value &&
                !match.BetweenGames.Value && !match.BetweenPoints.Value && !match.TimedOut.Value && Connected;
        }
        void Update()
        {
            if (!IsSpawned || !Actor || !MultiplayerApp.Instance) return;
            var match = NetworkMatch.Instance;
            if(MultiplayerCourtUI.Instance && MultiplayerCourtUI.Instance.cinematic && MultiplayerCourtUI.Instance.cinematic.OwnsPose) return;
            if(IsLocalHuman&&CanMove()&&!InputBlocked())
            {
                var local=LocalAxes();var axes=local*-CourtEnd.Value;bool run=LocalRun();
                if(IsServer){serverAxes=local;serverRun=run;}
                Actor.ObserveMovement(axes,run);
            }
            if (!IsServer)
            {
                if (!IsOwner)
                    transform.position = Vector3.Lerp(transform.position, Frame.Value.position, 1 - Mathf.Exp(-18 * Time.deltaTime));
                if (match && !match.Paused.Value)
                    Actor.RenderNetworkPose(Velocity, IsOwner ? LocalRun() : Frame.Value.running, Time.deltaTime, CanMove(),(IsLocalHuman?LocalAxes():Frame.Value.movement)*-CourtEnd.Value);
            }
            if (!IsLocalHuman) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && CanReadKeyboard() && !InputBlocked())
            {
                if (GameControls.Pressed(GameAction.Timeout)) TimeoutServerRpc();
                if (match && match.TimedOut.Value && GameControls.Pressed(GameAction.Continue)) ResumeTimeoutServerRpc();
            }
            if (!CanMove() || InputBlocked())
            { shotInput = new LocalShotInput(); return; }
            bool readKeys=keyboard!=null && CanReadKeyboard();
            int mask = (readKeys?GameControls.ShotMask:0) | (MultiplayerCourtUI.Instance?MultiplayerCourtUI.Instance.TouchShotMask:0), tap = readKeys?GameControls.TapMask:0;
            bool serve = match.WaitingServe.Value && match.ServerTeam.Value == Team.Value && match.ServerIndex.Value == Index.Value;
            bool cancel = readKeys && GameControls.Pressed(GameAction.CancelShot);
            if (tap != 0) TickShot(mask | tap, cancel, serve);
            TickShot(mask, cancel, serve);
        }
        void TickShot(int mask, bool cancel, bool serve)
        {
            if (shotInput.Tick(mask, cancel, serve, out var local))
            {
                pendingShotMask=shotInput.CommittedMask;pendingShot=local;
                var intent = BotMatchGameplayService.FromLocal(local);
                ShotServerRpc((int)intent.kind, intent.attack, intent.crossCourt,LocalAxes(),LocalRun());
            }
            if(!string.IsNullOrEmpty(shotInput.Feedback) && shotInput.Feedback!=inputFeedback)
            {
                inputFeedback=shotInput.Feedback;
                var ui=FindAnyObjectByType<MultiplayerCourtUI>();
                if(ui)ui.ShowAnnouncement("TỔ HỢP KHÔNG HỢP LỆ",inputFeedback,1.25f,AnnouncementTone.Fault);
            }
        }
        static bool CanReadKeyboard() => Application.isFocused ||
            InputSystem.settings.backgroundBehavior == InputSettings.BackgroundBehavior.IgnoreFocus;
        static bool InputBlocked() => SettingsOverlay.IsOpen || SettingsOverlay.Loading || MultiplayerCourtUI.Instance && MultiplayerCourtUI.Instance.InputBlocked;
        static bool LocalRun()
        {
            var keyboard = Keyboard.current;
            return !InputBlocked() && (MultiplayerCourtUI.Instance && MultiplayerCourtUI.Instance.TouchRun || keyboard != null && CanReadKeyboard() &&
                (GameControls.Held(GameAction.Run) || keyboard.spaceKey.isPressed && !Array.Exists(GameSettings.Current.keys, k=>k==(int)Key.Space)));
        }
        static Vector2 LocalAxes()
        {
            var keyboard = Keyboard.current;
            if (InputBlocked()) return Vector2.zero;
            Vector2 touch=MultiplayerCourtUI.Instance?MultiplayerCourtUI.Instance.TouchMovement:Vector2.zero;
            if (keyboard == null || !CanReadKeyboard()) return touch;
            return Vector2.ClampMagnitude(touch+new Vector2(
                (GameControls.Held(GameAction.Right) ? 1 : 0) - (GameControls.Held(GameAction.Left) ? 1 : 0),
                (GameControls.Held(GameAction.Forward) ? 1 : 0) - (GameControls.Held(GameAction.Backward) ? 1 : 0)), 1);
        }
        void FixedUpdate()
        {
            if (!IsSpawned || !IsLocalHuman || !Actor) return;
            if (!CanMove()) { predicted.Clear(); predictedVelocity = Vector3.zero; return; }
            var move = new Move { sequence = ++sequence, axes = LocalAxes(), run = LocalRun(), swinging=Actor.Swinging, dt = Time.fixedDeltaTime };
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
                move.swinging, CourtEnd.Value, move.dt, Actor.walkSpeed, Actor.runSpeed);
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
            var axes=moving?serverAxes*-CourtEnd.Value:Vector2.zero;
            Actor.Step(axes, moving && serverRun, dt, moving);
            // MatchActor owns its child root; publish its position on the NetworkObject root.
            var position = Actor.transform.position;
            transform.position = position; Actor.transform.localPosition = Vector3.zero;
            Frame.Value = new PlayerFrame { position = position, velocity = Actor.Velocity, sequence = lastReceived,
                movement = moving ? serverAxes : Vector2.zero, running = moving && serverRun };
        }
        public void SetAutomatedInput(Vector2 worldMovement,bool run)
        {
            if(!IsServer) return;
            serverAxes=Vector2.ClampMagnitude(worldMovement,1)*-CourtEnd.Value; serverRun=run;
        }
        [ServerRpc] void ShotServerRpc(int kind, bool attack, bool crossCourt,Vector2 axes,bool run)
        {
            if (kind < 1 || kind > (int)ShotKind.Punch || !CanMove() || !Actor || float.IsNaN(axes.x)||float.IsNaN(axes.y)||float.IsInfinity(axes.x)||float.IsInfinity(axes.y)) return;
            // Select the contact pose against the same movement snapshot as the shot.
            // Later movement packets update locomotion without cancelling this strike.
            serverAxes=Vector2.ClampMagnitude(axes,1);serverRun=run;
            Actor.ObserveMovement(serverAxes*-CourtEnd.Value,run);
            NetworkMatch.Instance?.TrySwing(this, new ShotIntent { kind = (ShotKind)kind, attack = attack, crossCourt = crossCourt });
        }
        [ServerRpc] void TimeoutServerRpc() { NetworkMatch.Instance?.RequestTimeout(this); }
        [ServerRpc] void ResumeTimeoutServerRpc() { NetworkMatch.Instance?.ResumeTimeout(this); }
        public void RequestTimeout() { if(IsOwner && IsSpawned)TimeoutServerRpc(); }
        public void ResumeTimeout() { if(IsOwner && IsSpawned)ResumeTimeoutServerRpc(); }
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
            Actor.RefreshPose();
            Frame.Value = new PlayerFrame { position = position, sequence = lastReceived };
            TeleportClientRpc(position, lastReceived, end);
        }
        [ClientRpc] void TeleportClientRpc(Vector3 position, uint acknowledged, int end)
        {
            if (IsServer) return;
            ResetInput(); sequence = Math.Max(sequence, acknowledged);
            ResetActor(position, end); Actor.RefreshPose();
        }
        public void BeginSwing(ShotIntent intent, Vector3 predictedBall, float contactDelay = -1)
        {
            if (!IsServer || !Actor || !Actor.Swing(BotMatchGameplayService.ToLocal(intent), predictedBall, contactDelay)) return;
            SetActiveShotKeys();
            SwingClientRpc((int)intent.kind, intent.attack, intent.crossCourt, predictedBall, Actor.ContactDelay, NetworkManager.ServerTime.Time);
        }
        [ClientRpc] void SwingClientRpc(int kind, bool attack, bool crossCourt, Vector3 predictedBall, float delay, double started)
        {
            if (IsServer || !Actor) return;
            if(Actor.Swing(BotMatchGameplayService.ToLocal(new ShotIntent { kind = (ShotKind)kind, attack = attack, crossCourt = crossCourt }), predictedBall, delay))
            { Actor.SynchronizeSwing((float)System.Math.Max(0,NetworkManager.ServerTime.Time-started)); SetActiveShotKeys(); }
        }
        void SetActiveShotKeys()
        {
            var shot=Actor.Intent;
            if(IsLocalHuman && pendingShotMask!=0 && pendingShot.kind==shot.kind && pendingShot.power==shot.power && pendingShot.cross==shot.cross)activeShotMask=pendingShotMask;
            else activeShotMask=(shot.kind==PickleVerse.LocalMatch.ShotKind.Drop?3:shot.kind==PickleVerse.LocalMatch.ShotKind.Smash?5:shot.kind==PickleVerse.LocalMatch.ShotKind.Reset?6:shot.kind==PickleVerse.LocalMatch.ShotKind.Dink?2:shot.kind==PickleVerse.LocalMatch.ShotKind.Volley?4:1)|(shot.power?8:0)|(shot.cross?16:0);
            pendingShotMask=0;
        }
        public void ConsumeContact(bool volley, Vector3? position = null, bool successful = true, Vector3 velocity = default)
        {
            if (!IsServer || !Actor) return;
            Vector3 contact = position ?? Actor.paddleFace.position;
            Actor.ConsumeContact(volley,contact,successful);
            ContactClientRpc(volley,contact,successful,velocity,NetworkManager.ServerTime.Time);
        }
        [ClientRpc] void ContactClientRpc(bool volley, Vector3 position, bool successful, Vector3 velocity, double time)
        {
            if (!IsServer && Actor)
            {
                Actor.ConsumeContact(volley,position,successful);
                if(successful)NetworkMatch.Instance?.PresentContact(position,velocity,time);
            }
        }
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
            activeShotMask=pendingShotMask=0;
            if(IsLocalHuman && MultiplayerCourtUI.Instance)MultiplayerCourtUI.Instance.ClearTouch();
            inputFeedback="";
            predicted.Clear(); predictedVelocity = Vector3.zero;
        }
    }
}
