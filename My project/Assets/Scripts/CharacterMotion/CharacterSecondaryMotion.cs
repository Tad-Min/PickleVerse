using System;
using UnityEngine;

namespace PickleVerse.CharacterMotion
{
    // Visual-only secondary bones. Original animation bones and gameplay colliders
    // remain untouched; this runs after either manual sampling or Animator evaluation.
    [DefaultExecutionOrder(10000)]
    public sealed class CharacterSecondaryMotion : MonoBehaviour
    {
        public CharacterMotionProfile profile;
        public bool simulate=true;
        public float teleportDistance=.8f;
        public float LastPonyAngle { get; private set; }
        public float LastHairAngle { get; private set; }
        public float LastSkirtAngle { get; private set; }
        public bool Initialized => nodes!=null;
        Transform[] nodes;
        Transform head,hips,spine,leftThigh,leftKnee,rightThigh,rightKnee;
        Vector3[] points,previous;
        Vector3 previousRoot;
        Quaternion previousRootRotation;
        bool needsReset=true;
        SkinnedMeshRenderer renderer;
        float collisionGrace;
        float previousStepDelta;
        Matrix4x4 hipsToMesh,meshToHips;

        public static CharacterSecondaryMotion Attach(GameObject visual,bool female)
        {
            var component=visual.GetComponent<CharacterSecondaryMotion>();
            if(!component)component=visual.AddComponent<CharacterSecondaryMotion>();
            var asset=Resources.Load<CharacterMotionProfile>("CharacterMotion/"+(female?"FemaleMotion":"MaleMotion"));
            if(asset)component.Initialize(asset);
            return component;
        }
        public void Initialize(CharacterMotionProfile settings)
        {
            if(nodes!=null)return;
            profile=settings;var skin=GetComponentInChildren<SkinnedMeshRenderer>();
            if(!skin || !profile || !profile.mesh)return;
            renderer=skin;
            if(skin.sharedMesh.vertexCount!=profile.originalVertexCount)throw new InvalidOperationException("Secondary motion mesh does not match character.");
            var original=skin.bones;
            if(original.Length<profile.originalBoneCount)throw new InvalidOperationException("Secondary motion skeleton does not match character.");
            for(int i=0;i<profile.originalBoneCount;i++)if(original[i].name!=profile.originalBoneNames[i])throw new InvalidOperationException("Secondary motion bone order differs: "+original[i].name);
            nodes=new Transform[profile.joints.Length];points=new Vector3[nodes.Length];previous=new Vector3[nodes.Length];
            var all=new Transform[profile.originalBoneCount+nodes.Length];Array.Copy(original,all,profile.originalBoneCount);
            for(int i=0;i<nodes.Length;i++)
            {
                var definition=profile.joints[i];var parent=definition.parent>=0?nodes[definition.parent]:Array.Find(original,b=>b.name==definition.anchor);
                if(!parent)throw new InvalidOperationException("Missing secondary anchor: "+definition.anchor);
                var existing=parent.Find("PKV_Secondary_"+definition.name);
                nodes[i]=existing?existing:new GameObject("PKV_Secondary_"+definition.name).transform;
                nodes[i].SetParent(parent,false);nodes[i].localPosition=definition.localPosition;nodes[i].localRotation=Quaternion.identity;nodes[i].localScale=Vector3.one;
                all[profile.originalBoneCount+i]=nodes[i];
            }
            skin.sharedMesh=profile.mesh;skin.bones=all;skin.updateWhenOffscreen=true;
            var bounds=skin.localBounds;bounds.Expand(.45f);skin.localBounds=bounds;
            head=Array.Find(original,b=>b.name=="mixamorig:Head");hips=Array.Find(original,b=>b.name=="mixamorig:Hips");spine=Array.Find(original,b=>b.name=="mixamorig:Spine2");
            int hipsIndex=Array.IndexOf(original,hips);
            meshToHips=profile.mesh.bindposes[hipsIndex];hipsToMesh=meshToHips.inverse;
            if(head && profile.foreheadMesh && profile.foreheadMaterial)
            {
                var fringe=head.Find("PKV_SmoothFringe");
                if(!fringe){fringe=new GameObject("PKV_SmoothFringe",typeof(MeshFilter),typeof(MeshRenderer)).transform;fringe.SetParent(head,false);}
                fringe.GetComponent<MeshFilter>().sharedMesh=profile.foreheadMesh;
                fringe.GetComponent<MeshRenderer>().sharedMaterial=profile.foreheadMaterial;
            }
            leftThigh=Array.Find(original,b=>b.name=="mixamorig:LeftUpLeg");leftKnee=Array.Find(original,b=>b.name=="mixamorig:LeftLeg");
            rightThigh=Array.Find(original,b=>b.name=="mixamorig:RightUpLeg");rightKnee=Array.Find(original,b=>b.name=="mixamorig:RightLeg");
            ResetMotion();
        }
        void Start() {if(profile && nodes==null)Initialize(profile);}
        void OnEnable() {needsReset=true;}
        void LateUpdate() {if(Application.isPlaying)Step(Time.deltaTime);}
        public void ResetMotion()
        {
            if(nodes==null)return;
            for(int i=0;i<nodes.Length;i++)
            {
                nodes[i].localPosition=profile.joints[i].localPosition;nodes[i].localRotation=Quaternion.identity;
                points[i]=previous[i]=nodes[i].TransformPoint(profile.joints[i].endOffset);
            }
            previousRoot=transform.position;previousRootRotation=transform.rotation;needsReset=false;
            previousStepDelta=0;collisionGrace=0;
            if(renderer)foreach(var joint in profile.joints)if(joint.blendShapeX>=0){renderer.SetBlendShapeWeight(joint.blendShapeX,0);renderer.SetBlendShapeWeight(joint.blendShapeZ,0);}
        }
        public void Step(float delta)
        {
            if(nodes==null || delta<=0)return;
            if(needsReset || Vector3.Distance(previousRoot,transform.position)>teleportDistance || Quaternion.Angle(previousRootRotation,transform.rotation)>70 || delta>.20f)ResetMotion();
            if(Vector3.Distance(previousRoot,transform.position)/delta>.12f)collisionGrace=.35f;
            else collisionGrace=Mathf.Max(0,collisionGrace-delta);
            previousRoot=transform.position;previousRootRotation=transform.rotation;
            for(int i=0;i<nodes.Length;i++){nodes[i].localPosition=profile.joints[i].localPosition;nodes[i].localRotation=Quaternion.identity;}
            LastPonyAngle=LastHairAngle=LastSkirtAngle=0;
            if(!simulate){ResetMotion();return;}
            int steps=Mathf.Clamp(Mathf.CeilToInt(delta*120),1,8);float dt=Mathf.Min(delta,.067f)/steps;
            for(int step=0;step<steps;step++)
            {
                // Preserve velocity when the frame/substep duration changes.
                float timeRatio=previousStepDelta>0?dt/previousStepDelta:1;
                for(int i=0;i<nodes.Length;i++)
                {
                var joint=profile.joints[i];var node=nodes[i];
                Quaternion rest=node.parent.rotation;
                Vector3 pivot=node.position,direction=rest*joint.endOffset;float length=direction.magnitude;
                Vector3 target=pivot+direction;
                Vector3 momentum=(points[i]-previous[i])*timeRatio*Mathf.Exp(-joint.damping*dt);
                Vector3 next=points[i]+momentum+(target-points[i])*(joint.stiffness*dt*dt)+Physics.gravity*(joint.gravityScale*dt*dt);
                previous[i]=points[i];
                Vector3 bent=next-pivot;
                if(bent.sqrMagnitude<.0000001f)bent=direction;
                bent=Vector3.RotateTowards(direction.normalized,bent.normalized,joint.maximumAngle*Mathf.Deg2Rad,0)*length;
                next=pivot+bent;
                next=Collide(next,joint.part);
                bent=next-pivot;
                // Leg contact may briefly spread the skirt farther than its normal sway.
                float limit=joint.part==SecondaryPart.Skirt?Mathf.Max(joint.maximumAngle,32):joint.maximumAngle;
                bent=Vector3.RotateTowards(direction.normalized,bent.normalized,limit*Mathf.Deg2Rad,0)*length;
                points[i]=pivot+bent;
                node.rotation=Quaternion.FromToRotation(direction,bent)*rest;
                float angle=Vector3.Angle(direction,bent);
                if(joint.part==SecondaryPart.Ponytail)LastPonyAngle=Mathf.Max(LastPonyAngle,angle);
                else if(joint.part==SecondaryPart.ShortHair)LastHairAngle=Mathf.Max(LastHairAngle,angle);
                else LastSkirtAngle=Mathf.Max(LastSkirtAngle,angle);
                }
                previousStepDelta=dt;
            }
            // Add small skirt displacements to the original animated skin instead of
            // replacing thigh weights; the original silhouette remains exact at rest.
            for(int i=0;i<nodes.Length;i++)
            {
                var joint=profile.joints[i];if(joint.blendShapeX<0)continue;
                Vector3 target=hips.TransformPoint(meshToHips.MultiplyPoint3x4(joint.meshEnd));
                Vector3 displacement=hips.InverseTransformDirection(points[i]-target);
                // Convert hips-local offsets back to the mesh's bind-space axes.
                displacement=hipsToMesh.MultiplyVector(displacement);
                renderer.SetBlendShapeWeight(joint.blendShapeX,Mathf.Clamp(displacement.x/.03f*100,-100,100));
                renderer.SetBlendShapeWeight(joint.blendShapeZ,Mathf.Clamp(displacement.z/.03f*100,-100,100));
            }
        }
        Vector3 Collide(Vector3 point,SecondaryPart part)
        {
            if(part==SecondaryPart.ShortHair)return point;
            if(part==SecondaryPart.Ponytail)
            {
                if(head)point=PushOutside(point,head.position+transform.up*.10f,head.position+transform.up*.10f,.13f);
                if(hips&&spine)point=PushOutside(point,hips.position,spine.position,.145f);
            }
            else
            {
                if(collisionGrace<=0)return point;
                if(leftThigh&&leftKnee)point=PushOutside(point,leftThigh.position,leftKnee.position,.075f);
                if(rightThigh&&rightKnee)point=PushOutside(point,rightThigh.position,rightKnee.position,.075f);
            }
            return point;
        }
        static Vector3 PushOutside(Vector3 point,Vector3 a,Vector3 b,float radius)
        {
            Vector3 ab=b-a;float along=ab.sqrMagnitude>.000001f?Mathf.Clamp01(Vector3.Dot(point-a,ab)/ab.sqrMagnitude):0;
            Vector3 nearest=a+ab*along,difference=point-nearest;
            if(difference.sqrMagnitude>=radius*radius)return point;
            return nearest+(difference.sqrMagnitude>.000001f?difference.normalized:Vector3.back)*radius;
        }
    }
}
