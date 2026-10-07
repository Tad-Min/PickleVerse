using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PickleVerse.Multiplayer
{
    [Serializable] public struct PlayerFrame : INetworkSerializable, IEquatable<PlayerFrame>
    {
        public Vector3 position;
        public uint sequence;
        public Vector2 movement;
        public bool running;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        { serializer.SerializeValue(ref position); serializer.SerializeValue(ref sequence); serializer.SerializeValue(ref movement); serializer.SerializeValue(ref running); }
        public bool Equals(PlayerFrame other) => position==other.position && sequence==other.sequence && movement==other.movement && running==other.running;
    }
    public sealed class NetworkPlayer : NetworkBehaviour
    {
        public static readonly List<NetworkPlayer> All = new List<NetworkPlayer>();
        public NetworkVariable<FixedString64Bytes> Identity = new NetworkVariable<FixedString64Bytes>();
        public NetworkVariable<int> Team = new NetworkVariable<int>(), Index = new NetworkVariable<int>();
        public NetworkVariable<bool> Female = new NetworkVariable<bool>();
        public NetworkVariable<PlayerFrame> Frame = new NetworkVariable<PlayerFrame>();
        public Animator Animator { get; private set; }
        public Transform RightHand { get; private set; }
        public Transform LeftHand { get; private set; }
        public Transform PaddleContact { get; private set; }
        public Vector3 Velocity { get; private set; }
        public bool Connected = true;
        struct Move { public uint sequence; public Vector2 axes; public bool run; }
        readonly Queue<Move> received = new Queue<Move>();
        readonly List<Move> predicted = new List<Move>();
        readonly ShotChord chord = new ShotChord();
        uint sequence, lastReceived;
        double rateWindow; int receivedThisWindow;
        Vector3 previous;
        float swingUntil;
        string animationState;
        bool initialized;
        RoomMember initialMember; int initialIndex;
        public Vector3 ContactPosition => PaddleContact != null ? PaddleContact.position : transform.position + transform.forward*.7f + Vector3.up*.9f;
        public Vector3 WristPosition => RightHand != null ? RightHand.position : ContactPosition;
        public string Gender => Female.Value ? "Female" : "Male";

        public override void OnNetworkSpawn()
        {
            if(IsServer && initialMember!=null)
            {
                Identity.Value=new FixedString64Bytes(initialMember.id); Team.Value=initialMember.team; Index.Value=initialIndex;
                Female.Value=initialMember.gender=="Female"; Frame.Value=new PlayerFrame { position=transform.position };
            }
            All.Add(this); previous=transform.position; sequence=Frame.Value.sequence;
            BuildVisual(); Frame.OnValueChanged+=Reconcile;
            if(IsOwner) chord.ModifierMode=(ModifierMode)PlayerPrefs.GetInt("PKV_Modifier",0);
        }
        public override void OnNetworkDespawn() { All.Remove(this); Frame.OnValueChanged-=Reconcile; }
        public override void OnGainedOwnership() { sequence=Frame.Value.sequence; predicted.Clear(); chord.Reset(); }
        public void Initialize(RoomMember member,int index,Vector3 position)
        {
            initialMember=member; initialIndex=index; transform.position=position;
        }
        void BuildVisual()
        {
            if(initialized) return; initialized=true;
            var settings=MultiplayerApp.Instance.Settings;
            var visual=Instantiate(Female.Value ? settings.femaleModel : settings.maleModel,transform);
            visual.name="CharacterVisual"; visual.transform.localPosition=Vector3.zero; visual.transform.localRotation=Quaternion.identity;
            foreach(var c in visual.GetComponentsInChildren<Collider>()) c.enabled=false;
            Animator=visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
            Animator.runtimeAnimatorController=Female.Value?settings.femaleAnimator:settings.maleAnimator;
            Animator.applyRootMotion=false;
            RightHand=visual.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name.EndsWith("RightHand"));
            LeftHand=visual.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name.EndsWith("LeftHand"));
            if(RightHand!=null)
            {
                var paddle=Instantiate(settings.paddlePrefab,RightHand);
                paddle.name="Paddle"; paddle.transform.localPosition=new Vector3(0,.03f,.02f);
                paddle.transform.localRotation=Quaternion.Euler(0,0,90);
                foreach(var c in paddle.GetComponentsInChildren<Collider>()) c.enabled=false;
                foreach(var r in paddle.GetComponentsInChildren<Rigidbody>()) Destroy(r);
                PaddleContact=new GameObject("PaddleContact").transform; PaddleContact.SetParent(RightHand,false);
                PaddleContact.localPosition=new Vector3(0,.14f,0);
            }
            transform.rotation=Quaternion.Euler(0,Team.Value==0?0:180,0);
            PlayAnimation("Idle_Ready",1);
            var marker=GameObject.CreatePrimitive(PrimitiveType.Cylinder); marker.name="TeamMarker"; marker.transform.SetParent(transform,false);
            marker.transform.localPosition=new Vector3(0,.025f,0); marker.transform.localScale=new Vector3(.8f,.02f,.8f);
            Destroy(marker.GetComponent<Collider>());
            marker.GetComponent<Renderer>().sharedMaterial=Team.Value==0?settings.teamAMaterial:settings.teamBMaterial;
        }
        void Update()
        {
            if(!IsSpawned || MultiplayerApp.Instance==null) return;
            if(!IsOwner && !IsServer) transform.position=Vector3.Lerp(transform.position,Frame.Value.position,1-Mathf.Exp(-18*Time.deltaTime));
            if(Time.time>=swingUntil) Locomotion();
            if(!IsOwner || NetworkMatch.Instance==null || NetworkMatch.Instance.Paused.Value || NetworkMatch.Instance.Finished.Value) return;
            var keyboard=Keyboard.current; if(keyboard==null) { chord.Reset(); return; }
            chord.Modifier(keyboard.uKey.wasPressedThisFrame,keyboard.iKey.wasPressedThisFrame,keyboard.uKey.isPressed,keyboard.iKey.isPressed);
            int mask=(keyboard.jKey.isPressed?1:0)|(keyboard.kKey.isPressed?2:0)|(keyboard.lKey.isPressed?4:0);
            var intent=chord.Step(mask);
            if(intent.kind!=ShotKind.None) ShotServerRpc((int)intent.kind,intent.attack,intent.crossCourt);
        }
        void FixedUpdate()
        {
            if(!IsSpawned) return;
            var match=NetworkMatch.Instance;
            bool canMove=match!=null && !match.Paused.Value && !match.Finished.Value && Connected;
            if(IsOwner && canMove)
            {
                var keyboard=Keyboard.current;
                Vector2 axes=Vector2.zero; bool run=false;
                if(keyboard!=null && Application.isFocused)
                {
                    axes=new Vector2((keyboard.dKey.isPressed||keyboard.rightArrowKey.isPressed?1:0)-(keyboard.aKey.isPressed||keyboard.leftArrowKey.isPressed?1:0),
                        (keyboard.wKey.isPressed||keyboard.upArrowKey.isPressed?1:0)-(keyboard.sKey.isPressed||keyboard.downArrowKey.isPressed?1:0));
                    axes=Vector2.ClampMagnitude(axes,1); run=keyboard.leftShiftKey.isPressed||keyboard.rightShiftKey.isPressed||keyboard.spaceKey.isPressed;
                }
                var move=new Move { sequence=++sequence,axes=axes,run=run };
                if(!IsServer) { transform.position=Advance(transform.position,axes,run); predicted.Add(move); if(predicted.Count>150) predicted.RemoveAt(0); }
                MoveServerRpc(move.sequence,move.axes,move.run);
            }
            if(IsServer && canMove && received.Count>0)
            {
                var move=received.Dequeue();
                transform.position=Advance(transform.position,move.axes,move.run);
                Frame.Value=new PlayerFrame { position=transform.position,sequence=move.sequence,movement=move.axes,running=move.run };
            }
            Velocity=(transform.position-previous)/Time.fixedDeltaTime; previous=transform.position;
            if(IsServer && canMove) match?.Rules?.CheckMomentum(Team.Value,Index.Value,transform.position,Velocity);
        }
        public Vector3 Advance(Vector3 position,Vector2 movement,bool run)
        {
            float facing=PickleballRules.Facing(Team.Value);
            position+=new Vector3(movement.x*facing,0,movement.y*facing)*(run?4.6f:2.8f)*Time.fixedDeltaTime;
            position.x=Mathf.Clamp(position.x,-5.5f,5.5f);
            position.z=Team.Value==0?Mathf.Clamp(position.z,-9.5f,-.28f):Mathf.Clamp(position.z,.28f,9.5f);
            position.y=0;
            return position;
        }
        [ServerRpc] void MoveServerRpc(uint seq,Vector2 axes,bool run)
        {
            if(float.IsNaN(axes.x)||float.IsNaN(axes.y)||float.IsInfinity(axes.x)||float.IsInfinity(axes.y)||seq<=lastReceived) return;
            if(Time.realtimeSinceStartupAsDouble-rateWindow>=1) { rateWindow=Time.realtimeSinceStartupAsDouble; receivedThisWindow=0; }
            if(++receivedThisWindow>65) return;
            lastReceived=seq; if(received.Count>=6) received.Dequeue();
            received.Enqueue(new Move { sequence=seq,axes=Vector2.ClampMagnitude(axes,1),run=run });
        }
        [ServerRpc] void ShotServerRpc(int kind,bool attack,bool crossCourt)
        {
            if(kind<1||kind>(int)ShotKind.Punch) return;
            NetworkMatch.Instance?.TrySwing(this,new ShotIntent { kind=(ShotKind)kind,attack=attack,crossCourt=crossCourt });
        }
        void Reconcile(PlayerFrame old,PlayerFrame frame)
        {
            if(!IsOwner||IsServer) return;
            predicted.RemoveAll(p=>p.sequence<=frame.sequence);
            var reconciled=frame.position;
            foreach(var p in predicted) reconciled=Advance(reconciled,p.axes,p.run);
            transform.position=reconciled;
        }
        public void Teleport(Vector3 position)
        {
            if(!IsServer) return;
            received.Clear(); transform.position=position; previous=position;
            Frame.Value=new PlayerFrame { position=position,sequence=lastReceived };
            TeleportClientRpc(position,lastReceived);
        }
        [ClientRpc] void TeleportClientRpc(Vector3 position,uint acknowledged)
        { predicted.Clear(); sequence=Math.Max(sequence,acknowledged); transform.position=position; previous=position; chord.Reset(); }
        public void Swing(ShotKind kind,bool backhand,float duration)
        {
            var state=ShotAnimationLibrary.State(kind,backhand);
            var clip=Animator.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name==state);
            float speed=clip!=null?clip.length/duration:1;
            swingUntil=Time.time+duration; PlayAnimation(state,speed,true);
        }
        void Locomotion()
        {
            Vector2 move=IsOwner?LocalAxes():Frame.Value.movement;
            string state=move.sqrMagnitude<.01f?"Idle_Ready":move.y<-.2f?"Backpedal":Mathf.Abs(move.x)>Mathf.Abs(move.y)?(move.x>0?"Shuffle_Right":"Shuffle_Left"):Frame.Value.running?"Run_Forward":"Walk_Forward";
            PlayAnimation(state,1);
        }
        Vector2 LocalAxes()
        {
            var k=Keyboard.current; if(k==null) return Vector2.zero;
            return new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));
        }
        void PlayAnimation(string state,float speed,bool restart=false)
        {
            if(Animator==null || (!restart && animationState==state)) return;
            Animator.speed=speed; Animator.CrossFadeInFixedTime(state,.08f,0); animationState=state;
        }
        void OnApplicationFocus(bool focused) { if(!focused) chord.Reset(); }
        public void ResetInput() { chord.Reset(); received.Clear(); predicted.Clear(); }
    }
}
