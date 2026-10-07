using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
public static class MultiplayerAnimationProbe
{
    public static string Run()
    {
        var output=new List<object>();
        foreach(string gender in new[]{"Male","Female"})
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Characters/"+gender+".glb");
            var model=UnityEngine.Object.Instantiate(prefab);
            var hands=model.GetComponentsInChildren<Transform>();
            var right=hands.First(t=>t.name.EndsWith("RightHand"));
            var left=hands.First(t=>t.name.EndsWith("LeftHand"));
            foreach(string name in new[]{"Serve_FH_Underhand","Overhead_Smash","Dink_BH"})
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animation/Characters/"+gender+"/"+name+".anim");
                var rows=new List<object>();
                for(int frame=15;frame<=105;frame+=3)
                {
                    clip.SampleAnimation(model,frame/60f);
                    Vector3 r=right.position,l=left.position,p=right.TransformPoint(new Vector3(0,.14f,0));
                    rows.Add(new{frame,r=new{r.x,r.y,r.z},l=new{l.x,l.y,l.z},p=new{p.x,p.y,p.z}});
                }
                output.Add(new{gender,name,samples=rows});
            }
            UnityEngine.Object.DestroyImmediate(model);
        }
        File.WriteAllText("Temp/AnimationPoseSamples.json",JsonConvert.SerializeObject(output,Formatting.Indented));
        return "Saved Temp/AnimationPoseSamples.json";
    }
}
