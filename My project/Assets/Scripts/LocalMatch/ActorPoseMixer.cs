using System;
using UnityEngine;

namespace PickleVerse.LocalMatch
{
    /// <summary>Reusable bone pose buffers for the manually sampled legacy clips.</summary>
    public sealed class ActorPoseMixer
    {
        readonly Transform[] bones,legs;
        readonly Transform spine;
        readonly Vector3[] fromPositions,legPositions,legFromPositions;
        readonly Quaternion[] fromRotations,legRotations,legFromRotations;
        float elapsed,duration,legElapsed,legDuration;
        bool blending,legBlending;
        public ActorPoseMixer(Transform[] hierarchy)
        {
            bones=Array.FindAll(hierarchy,t=>t.name.StartsWith("mixamorig:",StringComparison.Ordinal));
            var hips=Array.Find(bones,t=>t.name=="mixamorig:Hips");
            var left=Array.Find(bones,t=>t.name=="mixamorig:LeftUpLeg");
            var right=Array.Find(bones,t=>t.name=="mixamorig:RightUpLeg");
            spine=Array.Find(bones,t=>t.name=="mixamorig:Spine");
            legs=Array.FindAll(bones,t=>t==hips||left&&t.IsChildOf(left)||right&&t.IsChildOf(right));
            fromPositions=new Vector3[bones.Length];fromRotations=new Quaternion[bones.Length];
            legPositions=new Vector3[legs.Length];legRotations=new Quaternion[legs.Length];
            legFromPositions=new Vector3[legs.Length];legFromRotations=new Quaternion[legs.Length];
        }
        public void Begin(float seconds)
        {
            for(int i=0;i<bones.Length;i++){fromPositions[i]=bones[i].localPosition;fromRotations[i]=bones[i].localRotation;}
            elapsed=0;duration=seconds;blending=true;
        }
        public void Reset(){blending=legBlending=false;}
        public void BeginLegs(float seconds)
        {
            // A turn can happen during a strike, when the full-body clip stays the
            // same. Crossfade the gait independently, without rewinding that strike.
            for(int i=0;i<legs.Length;i++){legFromPositions[i]=legs[i].localPosition;legFromRotations[i]=legs[i].localRotation;}
            legElapsed=0;legDuration=seconds;legBlending=true;
        }
        public void CaptureLegs(float dt=0)
        {
            legElapsed+=dt;
            float weight=legBlending?Mathf.SmoothStep(0,1,Mathf.Clamp01(legElapsed/Mathf.Max(.001f,legDuration))):1;
            for(int i=0;i<legs.Length;i++)
            {
                legPositions[i]=Vector3.Lerp(legFromPositions[i],legs[i].localPosition,weight);
                legRotations[i]=Quaternion.Slerp(legFromRotations[i],legs[i].localRotation,weight);
            }
            if(legElapsed>=legDuration)legBlending=false;
        }
        public void ApplyLegs(float weight)
        {
            Quaternion upper=spine?spine.rotation:Quaternion.identity;
            for(int i=0;i<legs.Length;i++)
            {
                legs[i].localPosition=Vector3.Lerp(legs[i].localPosition,legPositions[i],weight);
                legs[i].localRotation=Quaternion.Slerp(legs[i].localRotation,legRotations[i],weight);
            }
            // Preserve the authored racket/torso orientation when the hips follow the gait.
            if(spine)spine.rotation=upper;
        }
        public void Blend(float dt)
        {
            if(!blending)return;
            elapsed+=dt;float weight=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/Mathf.Max(.001f,duration)));
            for(int i=0;i<bones.Length;i++)
            {
                bones[i].localPosition=Vector3.Lerp(fromPositions[i],bones[i].localPosition,weight);
                bones[i].localRotation=Quaternion.Slerp(fromRotations[i],bones[i].localRotation,weight);
            }
            if(elapsed>=duration)blending=false;
        }
    }
}
