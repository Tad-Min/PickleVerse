using System;
using UnityEngine;
using PickleVerse.Rules;

namespace PickleVerse.LocalMatch
{
    [Serializable] public struct ClipEntry { public string name; public AnimationClip clip; public float contactPhase; public Vector3 contactOffset; }
    public sealed class MatchActor : MonoBehaviour
    {
        public int playerId;
        public int end = -1;
        public Transform visual, paddleFace, leftFoot, rightFoot;
        public GameObject paddle;
        public Transform upperArm, forearm, hand;
        public Transform leftHand;
        public Vector3 heldBallOffset = new Vector3(-.06f,.055f,0);
        public Vector3 HeldBallPosition => leftHand ? leftHand.TransformPoint(heldBallOffset) : transform.TransformPoint(new Vector3(-.30f,1.04f,.28f));
        public ClipEntry[] clips;
        public float walkSpeed = 3.3f, runSpeed = 5.8f;
        public Vector3 Velocity { get; private set; }
        public ShotIntent Intent { get; private set; }
        public bool Swinging { get; private set; }
        public bool ContactActive => Swinging && !contactConsumed && Mathf.Abs(swingTime - Intent.ContactDelay) <= .115f;
        public bool VolleyMomentum { get; private set; }
        public string CurrentClip => clipName;
        public float SwingTime => swingTime;
        public Vector3 PaddleVelocity {get;private set;}
        public float PaddleHeadTop
        {
            get {
                float top=float.NegativeInfinity;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=0;z<=1;z++)
                    top=Mathf.Max(top,paddle.transform.TransformPoint(new Vector3(x*.622f,y*.120f,z==0?-1.33f:0f)).y);
                return top;
            }
        }
        Animation animationPlayer;
        string clipName;
        float clipTime, swingTime, lastVolleyTime, balanceTime;
        bool contactConsumed;
        float sourceContactPhase;
        public bool InKitchen => Court.InKitchen(Feet(true)) || Court.InKitchen(Feet(false));
        public CourtPoint Feet(bool left)
        {
            var foot = left ? leftFoot : rightFoot;
            Vector3 p = foot ? foot.position : transform.position + transform.right * (left ? -.13f : .13f);
            return new CourtPoint(p.x, p.y, p.z);
        }
        public void Initialize()
        {
            PickleVerse.CharacterMotion.CharacterSecondaryMotion.Attach(visual.gameObject,playerId==0);
            var bones = visual.GetComponentsInChildren<Transform>(true);
            if (!leftHand) leftHand = Array.Find(bones, t => t.name == "mixamorig:LeftHand");
            animationPlayer = visual.GetComponent<Animation>();
            if (!animationPlayer) animationPlayer = visual.gameObject.AddComponent<Animation>();
            animationPlayer.playAutomatically = false;
            // Gameplay samples the complete pose, then applies grip/IK. Unity's automatic
            // legacy pass must not overwrite that pose later in the PlayerLoop.
            animationPlayer.enabled = false;
            foreach (var entry in clips) if (entry.clip) animationPlayer.AddClip(entry.clip, entry.name);
            Play("PKV_Idle_Ready");
        }
        public void RefreshPose() { Sample(0); }
        public void SampleEntrance(AnimationClip clip,float time)
        {
            if(!clip)return;
            clipName=clip.name;
            clip.SampleAnimation(visual.gameObject,Mathf.Repeat(time,clip.length));
        }
        public void ResetAt(Vector3 p, int newEnd)
        {
            end = newEnd; transform.position = p; transform.rotation = Quaternion.Euler(0, end < 0 ? 0 : 180, 0);
            Velocity = Vector3.zero; Swinging = VolleyMomentum = false; contactConsumed = false; balanceTime = 0;
            if (paddle) paddle.SetActive(true);
            if (animationPlayer) Play("PKV_Idle_Ready");
        }
        public bool Swing(ShotIntent shot, Vector3 predictedBall)
        {
            if (Swinging) return false;
            Intent = shot; Swinging = true; swingTime = 0; contactConsumed = false;
            bool backhand = Vector3.Dot(predictedBall - transform.position, transform.right) < -.05f;
            string suffix = backhand ? "_BH" : "_FH";
            string action = shot.kind == ShotKind.Serve ? "PKV_Serve_FH_Underhand" : shot.kind == ShotKind.Smash ? "PKV_Overhead_Smash" : shot.kind == ShotKind.Drop ? "PKV_ThirdShotDrop" + suffix : shot.kind == ShotKind.Dink ? "PKV_Dink" + suffix : shot.BeforeBounce ? "PKV_Volley" + suffix : "PKV_Groundstroke" + suffix;
            var profile = Array.Find(clips, c => c.name == action);
            sourceContactPhase = profile.contactPhase;
            Play(action); return true;
        }
        public Vector3 ContactOffset(ShotIntent shot, bool backhand = false)
        {
            string suffix=backhand?"_BH":"_FH";
            string key=shot.kind==ShotKind.Serve?"PKV_Serve_FH_Underhand":shot.kind==ShotKind.Smash?"PKV_Overhead_Smash":shot.kind==ShotKind.Drop?"PKV_ThirdShotDrop"+suffix:shot.kind==ShotKind.Dink?"PKV_Dink"+suffix:shot.BeforeBounce?"PKV_Volley"+suffix:"PKV_Groundstroke"+suffix;
            return Array.Find(clips,c=>c.name==key).contactOffset;
        }
        public void ConsumeContact(bool volley)
        {
            contactConsumed = true;
            if (volley) { VolleyMomentum = true; lastVolleyTime = 0; balanceTime = 0; }
        }
        public void Reaction(bool won, bool matchOver = false)
        {
            Swinging = false;
            if (won) {
                if (matchOver && Has("PKV_Dance_Win")) { Play("PKV_Dance_Win"); if (paddle) paddle.SetActive(false); }
                else Play("PKV_Idle_Ready");
            } else Play("PKV_Fault_Reaction");
        }
        public void SplitStep() { if (!Swinging && Velocity.sqrMagnitude < .2f) Play("PKV_SplitStep"); }
        public void Step(Vector2 movement, bool run, float dt, bool canMove = true)
        {
            Vector3 previousFace=paddleFace?paddleFace.position:Vector3.zero;
            if (canMove)
            {
                var p = transform.position; var velocity = Velocity;
                MatchMovement.Step(ref p, ref velocity, movement, run, Swinging, end, dt, walkSpeed, runSpeed);
                transform.position = p; Velocity = velocity;
            }
            else Velocity = Vector3.MoveTowards(Velocity, Vector3.zero, 24f * dt);
            AdvancePose(dt, run, canMove);
            if(paddleFace)PaddleVelocity=(paddleFace.position-previousFace)/Mathf.Max(.0001f,dt);
        }
        /// <summary>Remote replicas sample the same authored poses without moving their network root.</summary>
        public void RenderNetworkPose(Vector3 velocity, bool run, float dt)
        {
            Vector3 previousFace = paddleFace ? paddleFace.position : Vector3.zero;
            Velocity = velocity; AdvancePose(dt, run, true);
            if (paddleFace) PaddleVelocity = (paddleFace.position - previousFace) / Mathf.Max(.0001f, dt);
        }
        void AdvancePose(float dt, bool run, bool canMove)
        {
            if (VolleyMomentum)
            {
                lastVolleyTime += dt;
                // Control is re-established when follow-through is over and forward momentum stops.
                bool balanced = !Swinging && lastVolleyTime > .2f && Vector3.Dot(Velocity, -end * Vector3.forward) <= .08f;
                balanceTime = balanced ? balanceTime + dt : 0;
                if (balanceTime >= .14f) VolleyMomentum = false;
            }
            if (Swinging)
            {
                swingTime += dt;
                if (swingTime >= Intent.Duration) { Swinging = false; Play(VolleyMomentum ? "PKV_NVZ_MomentumRecovery" : "PKV_Idle_Ready"); }
            }
            else if (canMove && !IsReaction())
            {
                string locomotion = Velocity.magnitude < .15f ? "PKV_Idle_Ready" : Mathf.Abs(Velocity.x) > Mathf.Abs(Velocity.z) * 1.15f ? (Vector3.Dot(Velocity, transform.right) < 0 ? "PKV_Shuffle_Left" : "PKV_Shuffle_Right") : Velocity.z * -end < -.1f ? "PKV_Backpedal" : run ? "PKV_Run_Forward" : "PKV_Walk_Forward";
                if (clipName != "PKV_SplitStep" || clipTime + dt >= animationPlayer[clipName].length - .000001f || Velocity.magnitude >= .15f) Play(locomotion);
            }
            Sample(dt);
        }
        bool IsReaction() => clipName == "PKV_Fault_Reaction" || clipName == "PKV_Dance_Win";
        bool Has(string key) => animationPlayer && animationPlayer[key] != null;
        void Play(string key)
        {
            if (clipName == key && !Swinging) return;
            if (!Has(key)) key = "PKV_Idle_Ready";
            if (!Has(key)) return;
            animationPlayer.Stop(); clipName = key; clipTime = 0;
            var state = animationPlayer[key]; state.enabled = true; state.weight = 1; state.speed = 0; state.wrapMode = WrapMode.ClampForever;
        }
        void Sample(float dt)
        {
            if (!animationPlayer || !Has(clipName)) return;
            clipTime += dt;
            var state = animationPlayer[clipName];
            float duration = Swinging ? Intent.Duration : state.length;
            bool loop = clipName.Contains("Idle") || clipName.Contains("Walk") || clipName.Contains("Run") || clipName.Contains("Shuffle") || clipName.Contains("Backpedal");
            float time = Swinging ? swingTime : clipTime;
            float phase = Mathf.Min(time / Mathf.Max(.01f,duration),1f);
            if (Swinging && sourceContactPhase > 0)
            {
                float hit = Intent.ContactDelay / Intent.Duration;
                phase = phase < hit ? phase / hit * sourceContactPhase : sourceContactPhase + (phase-hit)/(1-hit)*(1-sourceContactPhase);
            }
            state.time = loop ? time % Mathf.Max(.01f, state.length) : phase * state.length;
            state.enabled = true;
            state.clip.SampleAnimation(visual.gameObject,state.time);
            if(Swinging && Intent.kind==ShotKind.Smash) OverheadPose(phase);
            if(Swinging && Intent.kind==ShotKind.Serve) ServeGrip(swingTime<=.8f?Mathf.SmoothStep(0,1,swingTime/.5f):Mathf.Max(0,1-(swingTime-.8f)/.7f));
        }
        public void ServeGrip(float weight)
        {
            if(!hand || !paddleFace)return;
            Vector3 axis=(transform.forward*.45f+Vector3.down*.85f).normalized;
            Quaternion rotation=Quaternion.FromToRotation(paddleFace.position-hand.position,axis)*hand.rotation;
            hand.rotation=Quaternion.Slerp(hand.rotation,rotation,weight);
        }
        void OverheadPose(float phase)
        {
            if(!upperArm || !forearm || !hand || !paddleFace)return;
            float gamePhase=Mathf.Clamp01(swingTime/Intent.Duration);
            float weight=Mathf.Sin(gamePhase*Mathf.PI);
            Vector3 desiredAxis=(Vector3.up*.25f+transform.forward*.15f).normalized;
            Quaternion wrist=Quaternion.FromToRotation(paddleFace.position-hand.position,desiredAxis)*hand.rotation;
            hand.rotation=Quaternion.Slerp(hand.rotation,wrist,weight);
            Vector3 target=transform.TransformPoint(new Vector3(.12f,2.02f,.35f))-(paddleFace.position-hand.position);
            Vector3 a=upperArm.position,b=forearm.position,c=hand.position;
            float first=Vector3.Distance(a,b),second=Vector3.Distance(b,c);
            Vector3 direction=(target-a).normalized;
            float distance=Mathf.Clamp(Vector3.Distance(a,target),Mathf.Abs(first-second)+.01f,first+second-.01f);
            target=a+direction*distance;
            float along=(first*first-second*second+distance*distance)/(2*distance);
            float outwards=Mathf.Sqrt(Mathf.Max(0,first*first-along*along));
            Vector3 bend=Vector3.ProjectOnPlane(transform.right,direction).normalized;
            Vector3 elbow=a+direction*along+bend*outwards;
            Quaternion shoulder=Quaternion.FromToRotation(b-a,elbow-a)*upperArm.rotation;
            upperArm.rotation=Quaternion.Slerp(upperArm.rotation,shoulder,weight);
            Quaternion lower=Quaternion.FromToRotation(hand.position-forearm.position,target-forearm.position)*forearm.rotation;
            forearm.rotation=Quaternion.Slerp(forearm.rotation,lower,weight);
            wrist=Quaternion.FromToRotation(paddleFace.position-hand.position,desiredAxis)*hand.rotation;
            hand.rotation=Quaternion.Slerp(hand.rotation,wrist,weight);
        }
    }
}
