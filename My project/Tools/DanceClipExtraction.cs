using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

public static class DanceClipExtraction
{
    public const string SourceFolder="Assets/animaiton_dance";
    public sealed class Job
    {
        public string source,gender,name;
        public Job(string file,string gender,string name){source=SourceFolder+"/"+file+".glb";this.gender=gender;this.name=name;}
        public string Model=>"Assets/LocalMatchContent/Models/"+(gender=="female"?"Female":"Male")+".glb";
        public string Local=>"Assets/LocalMatchContent/Clips/"+gender+"/PKV_"+name+".anim";
        public string Characters=>"Assets/Animation/Characters/"+(gender=="female"?"Female":"Male")+"/"+name+".anim";
    }
    public static readonly Job[] Jobs={
        new Job("đồng_ý_vỗ_tay","male","Dance_Clap"),new Job("hóa_supersaigian","male","Dance_SuperSaiyan"),new Job("nhảy_suốt_đêm","male","Dance_AllNight"),
        new Job("đồng_ý_vỗ_tay","female","Dance_Clap"),new Job("nhảy_boom","female","Dance_Boom"),new Job("nhảy_suốt_đêm","female","Dance_AllNight")
    };
    static readonly string[] Axis={"x","y","z","w"};
    static string PathOf(Transform bone,Transform root)=>AnimationUtility.CalculateTransformPath(bone,root);
    static EditorCurveBinding Binding(string path,string property)=>EditorCurveBinding.FloatCurve(path,typeof(Transform),property);
    static Quaternion ReadRotation(AnimationCurve[] curves,int key,bool incoming=false,bool outgoing=false)
    {var v=curves.Select(c=>incoming?c.keys[key].inTangent:outgoing?c.keys[key].outTangent:c.keys[key].value).ToArray();return new Quaternion(v[0],v[1],v[2],v[3]);}
    static float Component(Quaternion q,int index)=>index==0?q.x:index==1?q.y:index==2?q.z:q.w;
    static float Component(Vector3 v,int index)=>index==0?v.x:index==1?v.y:v.z;

    public static object Run()
    {
        if(Application.isPlaying)throw new Exception("Stop Play first.");
        Directory.CreateDirectory("../UnityWork/DanceImport/SourceMotion");
        foreach(var job in Jobs)foreach(string path in new[]{job.Local,job.Characters})if(AssetDatabase.LoadAssetAtPath<AnimationClip>(path))throw new Exception("Output already exists; do not overwrite: "+path);
        foreach(string path in Jobs.SelectMany(j=>new[]{j.Local,j.Characters}).Select(Path.GetDirectoryName).Distinct())Directory.CreateDirectory(path);
        AssetDatabase.Refresh();
        var rows=new List<object>();
        foreach(var job in Jobs)
        {
            var sourceModel=AssetDatabase.LoadAssetAtPath<GameObject>(job.source);var targetModel=AssetDatabase.LoadAssetAtPath<GameObject>(job.Model);
            var source=AssetDatabase.LoadAllAssetsAtPath(job.source).OfType<AnimationClip>().Single();
            var bindings=AnimationUtility.GetCurveBindings(source);
            if(AnimationUtility.GetObjectReferenceCurveBindings(source).Length!=0||AnimationUtility.GetAnimationEvents(source).Length!=0)throw new Exception("Review embedded references/events first.");
            if(bindings.Any(b=>b.type!=typeof(Transform)||!sourceModel.transform.Find(b.path)||!targetModel.transform.Find(b.path)))throw new Exception("Unexpected curve binding: "+job.source);
            Archive(job.source,sourceModel,source);
            var clip=UnityEngine.Object.Instantiate(source);clip.name="PKV_"+job.name;clip.legacy=true;clip.wrapMode=WrapMode.ClampForever;
            var curves=bindings.Select(b=>AnimationUtility.GetEditorCurve(source,b)).ToArray();
            float bodyScale=targetModel.GetComponentsInChildren<Transform>().Single(t=>t.name=="mixamorig:Hips").localPosition.y/sourceModel.GetComponentsInChildren<Transform>().Single(t=>t.name=="mixamorig:Hips").localPosition.y;
            foreach(string path in bindings.Select(b=>b.path).Distinct())
            {
                var from=sourceModel.transform.Find(path);var to=targetModel.transform.Find(path);
                var rotationIndices=Enumerable.Range(0,4).Select(axis=>Array.FindIndex(bindings,b=>b.path==path&&b.propertyName=="m_LocalRotation."+Axis[axis])).ToArray();
                if(rotationIndices.All(i=>i>=0))
                {
                    var rotationCurves=rotationIndices.Select(i=>curves[i]).ToArray();
                    if(rotationCurves.Any(c=>c.length!=rotationCurves[0].length))throw new Exception("Quaternion component sampling differs.");
                    Quaternion correction=to.localRotation*Quaternion.Inverse(from.localRotation);
                    var keys=rotationCurves.Select(c=>c.keys).ToArray();
                    for(int k=0;k<keys[0].Length;k++)
                    {
                        Quaternion value=correction*ReadRotation(rotationCurves,k),incoming=correction*ReadRotation(rotationCurves,k,true),outgoing=correction*ReadRotation(rotationCurves,k,false,true);
                        for(int axis=0;axis<4;axis++){var key=keys[axis][k];key.value=Component(value,axis);key.inTangent=Component(incoming,axis);key.outTangent=Component(outgoing,axis);keys[axis][k]=key;}
                    }
                    for(int axis=0;axis<4;axis++)curves[rotationIndices[axis]]=new AnimationCurve(keys[axis]){preWrapMode=rotationCurves[axis].preWrapMode,postWrapMode=rotationCurves[axis].postWrapMode};
                }
                for(int axis=0;axis<3;axis++)
                {
                    int index=Array.FindIndex(bindings,b=>b.path==path&&b.propertyName=="m_LocalPosition."+Axis[axis]);if(index<0)continue;
                    float ratio=from.name=="mixamorig:Hips"?bodyScale:from.localPosition.magnitude>.000001f?to.localPosition.magnitude/from.localPosition.magnitude:1;
                    float sourceRest=Component(from.localPosition,axis),targetRest=Component(to.localPosition,axis);
                    var curve=curves[index];var keys=curve.keys;
                    for(int k=0;k<keys.Length;k++){var key=keys[k];key.value=targetRest+(key.value-sourceRest)*ratio;key.inTangent*=ratio;key.outTangent*=ratio;keys[k]=key;}
                    curves[index]=new AnimationCurve(keys){preWrapMode=curve.preWrapMode,postWrapMode=curve.postWrapMode};
                }
            }
            AnimationUtility.SetEditorCurves(clip,bindings,curves);
            var animatedPaths=bindings.Select(b=>b.path).ToHashSet();int resetBones=0;
            // Source motion has a simpler hand skeleton. Reset added finger joints to the target's
            // bind pose so an earlier gameplay clip cannot leave stale finger transforms in this dance.
            foreach(var bone in targetModel.GetComponentInChildren<SkinnedMeshRenderer>().bones)
            {
                string path=PathOf(bone,targetModel.transform);if(animatedPaths.Contains(path))continue;resetBones++;
                for(int i=0;i<3;i++){SetConstant(clip,path,"m_LocalPosition."+Axis[i],Component(bone.localPosition,i),source.length);SetConstant(clip,path,"m_LocalScale."+Axis[i],Component(bone.localScale,i),source.length);}
                for(int i=0;i<4;i++)SetConstant(clip,path,"m_LocalRotation."+Axis[i],Component(bone.localRotation,i),source.length);
            }
            clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.startTime=0;settings.stopTime=source.length;AnimationUtility.SetAnimationClipSettings(clip,settings);
            AssetDatabase.CreateAsset(clip,job.Local);
            var character=UnityEngine.Object.Instantiate(clip);character.name=job.name;character.legacy=false;AssetDatabase.CreateAsset(character,job.Characters);
            rows.Add(new{job.gender,source=job.source,name=job.name,duration=clip.length,sourceDuration=source.length,frameRate=clip.frameRate,bodyScale,resetBones,outputs=new[]{job.Local,job.Characters}});
        }
        AssetDatabase.SaveAssets();var report=new{created=12,rows};File.WriteAllText("../UnityWork/DanceImport/extraction.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
    }
    static void SetConstant(AnimationClip clip,string path,string property,float value,float duration)
    {AnimationUtility.SetEditorCurve(clip,Binding(path,property),new AnimationCurve(new Keyframe(0,value),new Keyframe(duration,value)));}
    static void Archive(string path,GameObject model,AnimationClip clip)
    {
        string output="../UnityWork/DanceImport/SourceMotion/"+Path.GetFileNameWithoutExtension(path)+".json";if(File.Exists(output))return;
        string hash;using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))hash=string.Concat(sha.ComputeHash(stream).Select(x=>x.ToString("x2")));
        var curves=AnimationUtility.GetCurveBindings(clip).Select(b=>new{b.path,b.propertyName,keys=AnimationUtility.GetEditorCurve(clip,b).keys.Select(k=>new{k.time,k.value,k.inTangent,k.outTangent,k.inWeight,k.outWeight,weightedMode=k.weightedMode.ToString()}).ToArray()}).ToArray();
        var rest=model.GetComponentsInChildren<Transform>(true).Where(t=>t!=model.transform).Select(t=>new{path=PathOf(t,model.transform),position=new[]{t.localPosition.x,t.localPosition.y,t.localPosition.z},rotation=new[]{t.localRotation.x,t.localRotation.y,t.localRotation.z,t.localRotation.w}}).ToArray();
        File.WriteAllText(output,Newtonsoft.Json.JsonConvert.SerializeObject(new{source=path,hash,clip.length,clip.frameRate,curves,rest}));
    }
    public static object Validate()
    {
        var reports=new List<object>();
        foreach(var job in Jobs)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(job.Model);var paths=new[]{job.Local,job.Characters};var clips=paths.Select(p=>AssetDatabase.LoadAssetAtPath<AnimationClip>(p)).ToArray();
            if(clips.Any(c=>!c)||!clips[0].legacy||clips[1].legacy)throw new Exception("Output clip/type missing: "+job.name);
            if(Mathf.Abs(clips[0].length-clips[1].length)>.0001f)throw new Exception("Duplicate lengths differ.");
            var bindings=AnimationUtility.GetCurveBindings(clips[0]);
            if(bindings.Any(b=>!model.transform.Find(b.path)))throw new Exception("Curve path missing in target.");
            if(paths.Any(p=>AssetDatabase.GetDependencies(p,true).Any(d=>d.StartsWith(SourceFolder+"/"))))throw new Exception("Output still depends on GLB source.");
            var obj=UnityEngine.Object.Instantiate(model);obj.hideFlags=HideFlags.HideAndDontSave;
            try
            {
                var bones=obj.GetComponentInChildren<SkinnedMeshRenderer>().bones;float maximumBoneStretch=0,maximumPositionError=0,maximumRotationError=0;float motion=0;
                var rest=bones.Select(t=>t.localPosition).ToArray();var first=bones.Select(t=>t.rotation).ToArray();var localPose=new Vector3[bones.Length];var rotation=new Quaternion[bones.Length];
                for(int sample=0;sample<=80;sample++)
                {
                    float time=clips[0].length*sample/80;clips[0].SampleAnimation(obj,time);
                    for(int i=0;i<bones.Length;i++)
                    {
                        if(bones[i].name!="mixamorig:Hips")maximumBoneStretch=Mathf.Max(maximumBoneStretch,Vector3.Distance(rest[i],bones[i].localPosition));
                        localPose[i]=bones[i].localPosition;rotation[i]=bones[i].localRotation;motion=Mathf.Max(motion,Quaternion.Angle(first[i],bones[i].rotation));
                        if(!float.IsFinite(localPose[i].x)||!float.IsFinite(rotation[i].w))throw new Exception("Non-finite animation pose.");
                    }
                    clips[1].SampleAnimation(obj,time);
                    for(int i=0;i<bones.Length;i++){maximumPositionError=Mathf.Max(maximumPositionError,Vector3.Distance(localPose[i],bones[i].localPosition));maximumRotationError=Mathf.Max(maximumRotationError,Quaternion.Angle(rotation[i],bones[i].localRotation));}
                }
                if(maximumBoneStretch>.0005f||maximumPositionError>.00001f||maximumRotationError>.08f||motion<5)throw new Exception("Retarget verification failed "+job.gender+" "+job.name+" stretch="+maximumBoneStretch+" position="+maximumPositionError+" angle="+maximumRotationError+" motion="+motion);
                reports.Add(new{job.gender,job.name,duration=clips[0].length,curves=bindings.Length,samples=81,maximumBoneStretch,maximumPositionError,maximumRotationError,motion,paths});
            }
            finally{UnityEngine.Object.DestroyImmediate(obj);}
        }
        var report=new{passed=true,files=12,reports};File.WriteAllText("../UnityWork/DanceImport/validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
    }
    public static object DeleteSources()
    {
        if(Application.isPlaying)throw new Exception("Stop Play first.");Validate();
        var holders=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/")&&!p.StartsWith(SourceFolder+"/")&&new[]{".unity",".prefab",".asset",".anim",".controller",".overrideController",".mat"}.Contains(Path.GetExtension(p).ToLowerInvariant())).ToArray();
        var dependencies=holders.Where(p=>AssetDatabase.GetDependencies(p,true).Any(d=>d.StartsWith(SourceFolder+"/"))).ToArray();
        if(dependencies.Length!=0)throw new Exception("Source GLB still referenced by: "+string.Join(", ",dependencies));
        var absolute=Path.GetFullPath(SourceFolder);if(!absolute.StartsWith(Path.GetFullPath(Application.dataPath)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Deletion target outside Assets.");
        long bytes=Directory.GetFiles(absolute,"*.glb").Sum(p=>new FileInfo(p).Length);
        bool deleted=AssetDatabase.DeleteAsset(SourceFolder);if(!deleted||Directory.Exists(absolute))throw new Exception("Source folder deletion failed.");
        var result=new{deleted,source=SourceFolder,removedGlbBytes=bytes,animationFiles=12};File.WriteAllText("../UnityWork/DanceImport/deletion.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
    }
}
