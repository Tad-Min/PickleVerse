using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PickleVerse.LocalMatch;

public static class ShotPoseProbe
{
    public static object Run()
    {
        var rows=new List<object>();
        foreach(string sex in new[]{"Female","Male"})
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Multiplayer/BotMatch/"+sex+"Actor.prefab");
            var go=UnityEngine.Object.Instantiate(prefab);var a=go.GetComponent<MatchActor>();a.Initialize();
            try
            {
                foreach(var kind in new[]{ShotKind.Drive,ShotKind.Dink,ShotKind.Drop,ShotKind.Volley,ShotKind.Reset,ShotKind.Smash,ShotKind.Serve})
                foreach(bool backhand in kind==ShotKind.Serve||kind==ShotKind.Smash?new[]{false}:new[]{false,true})
                {
                    a.ResetAt(Vector3.zero,-1);a.RefreshPose();var shot=new ShotIntent(kind);
                    var offset=a.ContactOffset(shot,backhand);a.Swing(shot,new Vector3(backhand?-1:1,1,0));
                    a.Step(Vector2.zero,false,shot.ContactDelay,false);
                    rows.Add(new{sex,kind,backhand,clip=a.CurrentClip,face=a.paddleFace.position,offset,error=Vector3.Distance(a.paddleFace.position,offset),wrist=a.hand.position,top=a.PaddleHeadTop,paddleScale=a.paddle.transform.lossyScale});
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        var text=Newtonsoft.Json.JsonConvert.SerializeObject(rows,Newtonsoft.Json.Formatting.Indented,new Newtonsoft.Json.JsonSerializerSettings{ReferenceLoopHandling=Newtonsoft.Json.ReferenceLoopHandling.Ignore});
        System.IO.Directory.CreateDirectory("../UnityWork/ShotFeel");System.IO.File.WriteAllText("../UnityWork/ShotFeel/pose-current.json",text);return rows.Select(r=>r.ToString()).ToArray();
    }
}
