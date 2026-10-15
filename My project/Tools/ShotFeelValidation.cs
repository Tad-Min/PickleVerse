using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PickleVerse.LocalMatch;
using PickleVerse.Audio;

public static class ShotFeelValidation
{
    public static object Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first");
        var rows=new List<object>();int failures=0;float worst=0;
        foreach(string sex in new[]{"Female","Male"})
        {
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Multiplayer/BotMatch/"+sex+"Actor.prefab"));
            var a=go.GetComponent<MatchActor>();a.Initialize();var fx=a.GetComponent<PaddleImpactVfx>();
            try
            {
                foreach(var kind in new[]{ShotKind.Drive,ShotKind.Dink,ShotKind.Drop,ShotKind.Volley,ShotKind.Reset,ShotKind.Smash})
                foreach(bool backhand in kind==ShotKind.Smash?new[]{false}:new[]{false,true})
                foreach(float step in new[]{1f/120,1f/60,1f/30})
                foreach(bool quick in new[]{false,true})
                {
                    a.ResetAt(new Vector3(0,0,-3),-1);a.RefreshPose();var shot=new ShotIntent(kind,kind==ShotKind.Smash);
                    float delay=quick?.125f:shot.ContactDelay;
                    Vector3 target=a.transform.TransformPoint(a.ContactOffset(shot,backhand));
                    Vector3 v=new Vector3(0,0,-6),p=target-v*delay+Vector3.up*(.5f*BallFlight.Gravity*delay*delay);
                    a.Swing(shot,target,delay);int before=fx.PlayCount;bool hit=false;float error=99,time=0;
                    for(int i=0;i<Mathf.CeilToInt((delay+.12f)/step);i++)
                    {
                        Vector3 old=p;BallFlight.Integrate(ref p,ref v,step);a.Step(Vector2.zero,false,step,false);
                        if(!a.CanContact(old,p,.45f,out _))continue;
                        p=a.ContactPoint;a.ConsumeContact(shot.BeforeBounce,p);time=a.SwingTime;
                        error=Vector3.Distance(p,a.paddleFace.position);hit=true;
                        a.ConsumeContact(shot.BeforeBounce,p);break;
                    }
                    bool ok=hit&&error<.16f&&fx.PlayCount==before+1&&!a.ContactActive;
                    if(!ok)failures++;if(hit)worst=Math.Max(worst,error);
                    rows.Add(new{sex,kind=kind.ToString(),backhand,step,quick,hit,error,time,delay,ok});
                }
                a.ResetAt(Vector3.back*3,-1);a.RefreshPose();a.Swing(new ShotIntent(ShotKind.Drive),new Vector3(1,1,-2));
                int count=fx.PlayCount;for(int i=0;i<160;i++)a.Step(Vector2.zero,false,1f/120,false);
                bool miss=fx.PlayCount==count;if(!miss)failures++;rows.Add(new{sex,missHasNoVfx=miss});
                a.ResetAt(new Vector3(1,0,-7.3f),-1);a.RefreshPose();var serve=new ShotIntent(ShotKind.Serve);
                a.Swing(serve,a.HeldBallPosition);bool released=false,served=false;Vector3 origin=Vector3.zero,ball=a.HeldBallPosition,velocity=Vector3.zero;float serveError=99;
                for(int i=0;i<130;i++)
                {
                    a.Step(Vector2.zero,false,1f/120,false);ServeBallMotion.Step(a,ref released,ref origin,ref ball,ref velocity);
                    if(a.SwingTime<.8f||!a.ContactActive||Vector3.Distance(ball,a.paddleFace.position)>.45f)continue;
                    a.ConsumeContact(false,ball);serveError=Vector3.Distance(ball,a.paddleFace.position);served=true;break;
                }
                bool serveOk=served&&serveError<.16f;if(!serveOk)failures++;rows.Add(new{sex,serveOk,serveError});
                a.ResetAt(Vector3.back*3,-1);a.RefreshPose();var drive=new ShotIntent(ShotKind.Drive);
                Vector3 incomingVelocity=new Vector3(0,-1,-6),ideal=a.transform.TransformPoint(a.ContactOffset(drive));
                Vector3 incoming=ideal-incomingVelocity*.15f+Vector3.up*(.5f*BallFlight.Gravity*.15f*.15f);
                float adaptive=PaddleContact.Anticipation(a,drive,incoming,incomingVelocity,out var predicted);
                bool adaptiveOk=Mathf.Abs(adaptive-.15f)<.03f&&Vector3.Distance(predicted,ideal)<.15f;
                if(!adaptiveOk)failures++;rows.Add(new{sex,adaptiveOk,adaptive});
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        var result=new{passed=failures==0,failures,worstPoseError=worst,checks=rows.Count,rows};
        Directory.CreateDirectory("../UnityWork/ShotFeel");File.WriteAllText("../UnityWork/ShotFeel/contact-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        return new{result.passed,result.failures,result.worstPoseError,result.checks,failed=rows.Where(r=>r.ToString().Contains("ok = False")).Take(20).ToArray()};
    }
}
