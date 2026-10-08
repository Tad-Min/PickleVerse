using System;
using TMPro;
using UnityEngine;

namespace PickleVerse.LocalMatch
{
    public sealed class BotMatchIntro : MonoBehaviour
    {
        public BotMatch match;
        public MatchCamera followCamera;
        public AnimationClip femaleWalk, maleWalk;
        public GameObject overlay;
        public RectTransform announcement;
        public TMP_Text caption;
        public UnityEngine.UI.Image topBar, bottomBar;
        public float establishingSeconds=.85f, entranceSeconds=4f, returnSeconds=1f, announcementSeconds=1.1f;
        public float femaleGroundOffset=.0153f, maleGroundOffset=.0298f;
        public bool IsPlaying { get; private set; }
        public float Elapsed { get; private set; }
        public float Duration => establishingSeconds+entranceSeconds*2+returnSeconds+announcementSeconds;
        public string Stage { get; private set; }="";
        Camera view;
        bool followWasEnabled;
        bool[] hudActive;
        GameObject[] hudObjects;
        PoseCache humanPose, botPose;

        sealed class PoseCache
        {
            public MatchActor actor; public AnimationClip clip;
            public Vector3 start,end;
            public float groundOffset;
            public Transform[] bones; public Vector3[] positions,scales; public Quaternion[] rotations;
            public PoseCache(MatchActor actor,AnimationClip clip,float groundOffset)
            {
                this.actor=actor;this.clip=clip;this.groundOffset=groundOffset;end=actor.transform.position;
                start=new Vector3(end.x,0,actor.end*9.45f);
                actor.ResetAt(end,actor.end);actor.RefreshPose();
                bones=actor.visual.GetComponentsInChildren<Transform>(true);
                positions=new Vector3[bones.Length];rotations=new Quaternion[bones.Length];scales=new Vector3[bones.Length];
                for(int i=0;i<bones.Length;i++){positions[i]=bones[i].localPosition;rotations[i]=bones[i].localRotation;scales[i]=bones[i].localScale;}
            }
            public void Walk(float time,float duration)
            {
                float phase=Mathf.Clamp01(time/duration);
                actor.transform.position=Vector3.Lerp(start,end,phase);
                // One gait cycle per second, with gentle pose blends at both ends.
                actor.SampleEntrance(clip,time*.85f);
                float weight=Mathf.Min(Mathf.SmoothStep(0,1,time/.25f),Mathf.SmoothStep(0,1,(duration-time)/.3f));
                for(int i=0;i<bones.Length;i++)
                {
                    bones[i].localPosition=Vector3.Lerp(positions[i],bones[i].localPosition,weight);
                    bones[i].localRotation=Quaternion.Slerp(rotations[i],bones[i].localRotation,weight);
                    bones[i].localScale=Vector3.Lerp(scales[i],bones[i].localScale,weight);
                }
                actor.visual.localPosition=positions[0]-Vector3.up*groundOffset*weight;
            }
            public void IdleAt(Vector3 position) {if(!actor || !actor.visual)return;actor.visual.localPosition=positions[0];actor.ResetAt(position,actor.end);actor.RefreshPose();}
        }
        public bool Play()
        {
            if(!match || !followCamera || !femaleWalk || !maleWalk || !overlay)return false;
            if(IsPlaying)Cancel();
            view=followCamera.GetComponent<Camera>();followWasEnabled=followCamera.enabled;followCamera.enabled=false;
            humanPose=new PoseCache(match.human,femaleWalk,femaleGroundOffset);botPose=new PoseCache(match.bot,maleWalk,maleGroundOffset);
            int count=match.hud.transform.childCount-1;hudObjects=new GameObject[count];hudActive=new bool[count];int index=0;
            foreach(Transform child in match.hud.transform)
            {
                if(child.gameObject==overlay)continue;
                hudObjects[index]=child.gameObject;hudActive[index]=child.gameObject.activeSelf;child.gameObject.SetActive(false);index++;
            }
            if(match.ball)match.ball.gameObject.SetActive(false);
            if(match.landingMarker)match.landingMarker.gameObject.SetActive(false);
            if(match.aimMarker)match.aimMarker.gameObject.SetActive(false);
            if(match.ballTrail){match.ballTrail.emitting=false;match.ballTrail.Clear();}
            match.BeginCinematic();IsPlaying=true;Elapsed=0;overlay.SetActive(true);overlay.transform.SetAsLastSibling();
            Evaluate(0);return true;
        }
        void Update() { if(IsPlaying)Advance(Mathf.Min(Time.unscaledDeltaTime,.1f)); }
        public void Advance(float delta)
        {
            if(!IsPlaying)return;
            Elapsed+=Mathf.Max(0,delta);
            if(Elapsed>=Duration){Finish();return;}
            Evaluate(Elapsed);
        }
        public void Seek(float seconds)
        {
            if(!IsPlaying && !Play())return;
            Elapsed=Mathf.Clamp(seconds,0,Duration-.0001f);Evaluate(Elapsed);
        }
        void Evaluate(float time)
        {
            announcement.gameObject.SetActive(false);topBar.gameObject.SetActive(true);bottomBar.gameObject.SetActive(true);
            if(time<establishingSeconds)
            {
                Stage="Establishing";caption.text="PICKLEVERSE";
                humanPose.IdleAt(humanPose.start);botPose.IdleAt(botPose.start);
                float t=Mathf.SmoothStep(0,1,time/establishingSeconds);
                view.transform.position=Vector3.Lerp(new Vector3(-6,9.8f,-12.2f),new Vector3(-3.8f,6.5f,-9.8f),t);
                view.transform.LookAt(new Vector3(0,.7f,0));view.fieldOfView=55;
                return;
            }
            time-=establishingSeconds;
            if(time<entranceSeconds)
            {
                Stage="Player entrance";caption.text="BẠN";
                humanPose.Walk(time,entranceSeconds);botPose.IdleAt(botPose.start);
                EntranceCamera(humanPose.actor,time/entranceSeconds);return;
            }
            time-=entranceSeconds;humanPose.IdleAt(humanPose.end);
            if(time<entranceSeconds)
            {
                Stage="Opponent entrance";caption.text="ĐỐI THỦ";
                botPose.Walk(time,entranceSeconds);EntranceCamera(botPose.actor,time/entranceSeconds);return;
            }
            time-=entranceSeconds;botPose.IdleAt(botPose.end);caption.text="";
            if(time<returnSeconds)
            {
                Stage="Return to gameplay";
                var actor=botPose.actor;
                Vector3 origin=actor.transform.TransformPoint(EntranceOffset(1));
                Quaternion rotation=Quaternion.LookRotation(actor.transform.position+Vector3.up*.92f-origin,Vector3.up);
                float t=Mathf.SmoothStep(0,1,time/returnSeconds);
                view.transform.SetPositionAndRotation(Vector3.Lerp(origin,followCamera.DesiredPosition,t),Quaternion.Slerp(rotation,followCamera.DesiredRotation,t));
                view.fieldOfView=Mathf.Lerp(43,followCamera.fieldOfView,t);return;
            }
            time-=returnSeconds;Stage="Match announcement";
            view.transform.SetPositionAndRotation(followCamera.DesiredPosition,followCamera.DesiredRotation);view.fieldOfView=followCamera.fieldOfView;
            topBar.gameObject.SetActive(false);bottomBar.gameObject.SetActive(false);announcement.gameObject.SetActive(true);
            float distance=((RectTransform)overlay.transform).rect.width*.5f+announcement.rect.width*.5f+40;
            float x=time<.22f?Mathf.Lerp(-distance,0,Mathf.SmoothStep(0,1,time/.22f)):time>announcementSeconds-.28f?Mathf.Lerp(0,distance,Mathf.SmoothStep(0,1,(time-announcementSeconds+.28f)/.28f)):0;
            announcement.anchoredPosition=new Vector2(x,0);
        }
        void EntranceCamera(MatchActor actor,float phase)
        {
            Vector3 offset=EntranceOffset(phase);
            view.transform.position=actor.transform.TransformPoint(offset);
            view.transform.LookAt(actor.transform.position+Vector3.up*.92f);view.fieldOfView=Mathf.Lerp(48,43,phase);
        }
        static Vector3 EntranceOffset(float phase)
        {
            float t=Mathf.SmoothStep(0,1,phase),angle=Mathf.Lerp(138,40,t)*Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(angle)*3.6f,Mathf.Lerp(3.1f,1.85f,t),Mathf.Cos(angle)*3.6f);
        }
        public void Skip() { if(IsPlaying)Finish(); }
        void Finish()
        {
            Cancel();match.EndCinematic();followCamera.SnapToPlayer();
        }
        public void Cancel()
        {
            if(!IsPlaying)return;IsPlaying=false;Stage="";
            humanPose.IdleAt(humanPose.end);botPose.IdleAt(botPose.end);
            if(overlay)overlay.SetActive(false);
            for(int i=0;i<hudObjects.Length;i++)if(hudObjects[i])hudObjects[i].SetActive(hudActive[i]);
            if(match && match.ball)match.ball.gameObject.SetActive(true);
            if(followCamera){followCamera.enabled=followWasEnabled;followCamera.SnapToPlayer();}
        }
        void OnDisable() { if(IsPlaying)Cancel(); }
    }
}
