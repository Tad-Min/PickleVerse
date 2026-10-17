using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.LocalMatch;
using PickleVerse.Progression;

public static class MovementBlendValidation
{
    const string Root="../UnityWork/MovingStrike/",Qa="MovingStrikeQA20261015";
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool ok){checks.Add(new{name,passed=ok});if(!ok)throw new Exception(name);}
    public static object Prepare()
    {
        var r=ProgressionRuntime.Instance;Directory.CreateDirectory(Root);
        if(r.Player.id!=Qa){File.WriteAllText(Root+"original-profile.txt",r.Player.id);r.SwitchPlayer(Qa,"Moving Strike QA");}
        return new{qa=r.Player.id==Qa};
    }
    static Vector2 Movement(int pattern,int tick,int ticks,int end)
    {
        int change=Mathf.Max(2,ticks/3);
        Vector2 local=pattern==0?(tick<change?Vector2.zero:Vector2.right):
            pattern==1?(tick<change?Vector2.up:Vector2.left):
            pattern==2?Vector2.up:pattern==3?(tick<change?Vector2.up:Vector2.zero):
            tick<change?Vector2.up:Vector2.down;
        return local*-end;
    }
    public static object Run()
    {
        if(!Application.isPlaying)throw new Exception("Play Menu first");checks.Clear();
        long bytes=0;double milliseconds=0;float maxContactError=0,maxTurnAngle=0;
        try
        {
            foreach(string sex in new[]{"Female","Male"})
            {
                var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Multiplayer/BotMatch/"+sex+"Actor.prefab"));
                var a=go.GetComponent<MatchActor>();a.Initialize();
                try
                {
                    foreach(int end in new[]{-1,1})
                    foreach(var kind in new[]{ShotKind.Drive,ShotKind.Dink,ShotKind.Drop,ShotKind.Volley,ShotKind.Reset,ShotKind.Smash,ShotKind.Serve})
                    foreach(float dt in new[]{1f/120,1f/60,1f/30})
                    {
                        a.ResetAt(new Vector3(0,0,end*4),end);a.RefreshPose();var shot=new ShotIntent(kind);
                        a.Swing(shot,a.transform.TransformPoint(a.ContactOffset(shot)));string clip=a.CurrentClip;
                        int ticks=Mathf.CeilToInt(shot.ContactDelay/dt);bool uninterrupted=true;float elapsed=0;
                        for(int i=0;i<ticks;i++)
                        {
                            Vector2 move=i<ticks/4?Vector2.zero:i<ticks/2?Vector2.right:i<ticks*3/4?Vector2.down:Vector2.left;
                            bool run=i>=ticks/2;var before=a.leftFoot.localRotation;
                            a.Step(move*-end,run,dt);elapsed+=dt;
                            uninterrupted&=a.Swinging&&a.CurrentClip==clip&&Mathf.Abs(a.SwingTime-elapsed)<.0001f;
                            if(i==ticks/4&&dt<.009f)maxTurnAngle=Math.Max(maxTurnAngle,Quaternion.Angle(before,a.leftFoot.localRotation));
                        }
                        Check(sex+" "+end+" "+kind+" movement/turn/sprint at "+Mathf.RoundToInt(1/dt)+" Hz",uninterrupted&&a.ContactActive&&a.Velocity.sqrMagnitude>0);
                    }
                    foreach(int end in new[]{-1,1})
                    foreach(var kind in new[]{ShotKind.Drive,ShotKind.Dink,ShotKind.Drop,ShotKind.Volley,ShotKind.Reset,ShotKind.Smash})
                    for(int pattern=0;pattern<5;pattern++)
                    foreach(bool quick in new[]{false,true})
                    {
                        a.ResetAt(new Vector3(0,0,end*4),end);a.RefreshPose();var shot=new ShotIntent(kind);
                        float delay=quick?.10f:shot.ContactDelay;
                        int ticks=Mathf.CeilToInt(delay*120);float time=ticks/120f;
                        var initial=Movement(pattern,0,ticks,end);a.ObserveMovement(initial,false);
                        a.Swing(shot,a.transform.TransformPoint(a.ContactOffset(shot)),delay);
                        Vector3 root=a.transform.position,speed=a.Velocity;
                        for(int i=0;i<ticks;i++)MatchMovement.Step(ref root,ref speed,Movement(pattern,i,ticks,end),pattern==2&&i>=ticks/3,true,end,1f/120,a.walkSpeed,a.runSpeed);
                        bool backhand=a.CurrentClip.Contains("Backhand");Vector3 target=root+a.transform.TransformDirection(a.ContactOffset(shot,backhand));
                        Vector3 incoming=-a.transform.forward*6,ball=target-incoming*time+Vector3.up*(.5f*BallFlight.Gravity*time*time);
                        bool hit=false;float error=99;int effects=a.GetComponent<PaddleImpactVfx>().PlayCount;
                        for(int i=0;i<ticks+12;i++)
                        {
                            Vector3 before=ball;BallFlight.Integrate(ref ball,ref incoming,1f/120);
                            a.Step(Movement(pattern,i,ticks,end),pattern==2&&i>=ticks/3,1f/120);
                            if(!a.CanContact(before,ball,.3f,out _))continue;
                            a.ConsumeContact(shot.BeforeBounce,a.ContactPoint);error=Vector3.Distance(a.ContactPoint,a.paddleFace.position);
                            a.ConsumeContact(shot.BeforeBounce,a.ContactPoint);hit=true;break;
                        }
                        maxContactError=Math.Max(maxContactError,hit?error:0);
                        Check(sex+" "+end+" "+kind+" physical contact during movement pattern "+pattern+" quick="+quick,hit&&error<.22f&&a.Swinging&&a.GetComponent<PaddleImpactVfx>().PlayCount==effects+1);
                        if(shot.BeforeBounce)Check(sex+" "+kind+" momentum survives movement "+pattern,a.VolleyMomentum);
                    }
                    foreach(int end in new[]{-1,1})
                    foreach(var kind in new[]{ShotKind.Drive,ShotKind.Dink,ShotKind.Drop,ShotKind.Volley,ShotKind.Reset,ShotKind.Smash,ShotKind.Serve})
                    {
                        a.ResetAt(new Vector3(0,0,end*4),end);a.RefreshPose();var shot=new ShotIntent(kind);
                        a.Swing(shot,a.transform.TransformPoint(a.ContactOffset(shot)));string clip=a.CurrentClip;
                        Vector3 root=a.transform.position;var before=a.upperArm.localRotation;
                        for(int i=0;i<8;i++)a.RenderNetworkPose(Vector3.right*2,i>=4,1f/60,true,(i<3?Vector2.zero:i<6?Vector2.right:Vector2.up)*-end);
                        Check(sex+" "+end+" remote "+kind+" keeps strike, gait and network root",a.Swinging&&a.CurrentClip==clip&&a.LocomotionClip=="PKV_Run_Forward"&&a.transform.position==root&&Quaternion.Angle(before,a.upperArm.localRotation)>.1f);
                    }
                    a.ResetAt(Vector3.back*5,-1);a.RefreshPose();
                    for(int i=0;i<120;i++)a.Step(Vector2.up,false,1f/120);
                    var walkingFoot=a.leftFoot.localRotation;a.Swing(new ShotIntent(ShotKind.Drive),a.paddleFace.position+Vector3.forward*.6f);a.Step(Vector2.up,false,1f/120);
                    Check(sex+" strike starts from a long-running gait without stale leg buffers",a.Swinging&&Quaternion.Angle(walkingFoot,a.leftFoot.localRotation)<20);
                    a.ResetAt(Vector3.back*4,-1);a.RefreshPose();a.Swing(new ShotIntent(ShotKind.Drive),a.paddleFace.position);
                    for(int i=0;i<15;i++)a.Step(Vector2.right,false,1f/120);
                    var layered=a.leftFoot.localRotation;string selected=a.CurrentClip;var authored=Array.Find(a.clips,c=>c.name==selected);
                    authored.clip.SampleAnimation(a.visual.gameObject,authored.clip.length*authored.contactPhase*a.SwingTime/a.ContactDelay);
                    Check(sex+" walking leg layer differs from planted swing",Quaternion.Angle(layered,a.leftFoot.localRotation)>.2f&&a.LocomotionClip=="PKV_Shuffle_Right");a.RefreshPose();
                    for(int i=0;i<180;i++)a.Step(Vector2.right,true,1f/120);
                    Check(sex+" follow-through completes into locomotion",!a.Swinging&&a.CurrentClip==a.LocomotionClip);
                    a.ResetAt(Vector3.back*7.3f,-1);a.RefreshPose();a.Swing(new ShotIntent(ShotKind.Serve),a.HeldBallPosition);
                    bool released=false;Vector3 origin=Vector3.zero,p=Vector3.zero,v=Vector3.zero;
                    for(int i=0;i<88;i++){a.Step(i<50?Vector2.zero:Vector2.right,false,1f/120);ServeBallMotion.Step(a,ref released,ref origin,ref p,ref v);}
                    Check(sex+" moving during serve retains swing and released ball",a.Swinging&&released&&p.y<origin.y&&v.y<0);
                    var mixer=new ActorPoseMixer(a.visual.GetComponentsInChildren<Transform>(true));mixer.BeginLegs(.1f);mixer.CaptureLegs(.01f);mixer.ApplyLegs(1);
                    var timer=new System.Diagnostics.Stopwatch();long start=GC.GetAllocatedBytesForCurrentThread();timer.Start();
                    for(int i=0;i<2000;i++){mixer.BeginLegs(.1f);mixer.CaptureLegs(.016f);mixer.ApplyLegs(.6f);}
                    timer.Stop();bytes+=GC.GetAllocatedBytesForCurrentThread()-start;milliseconds+=timer.Elapsed.TotalMilliseconds;
                }
                finally{UnityEngine.Object.DestroyImmediate(go);}
            }
            return new{passed=true,count=checks.Count,maxTurnAngle,maxContactError,poseBufferManagedBytes=bytes,poseBufferMilliseconds=milliseconds,iterations=4000};
        }
        finally{File.WriteAllText(Root+"rig-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{checks,maxTurnAngle,maxContactError,bytes,milliseconds},Newtonsoft.Json.Formatting.Indented));}
    }
    public static async Task<object> Training(){SceneManager.LoadScene("Training");await Task.Delay(250);return "Training ready";}
    public static async Task<object> Restore()
    {
        SceneManager.LoadScene("Menu");await Task.Delay(200);
        var r=ProgressionRuntime.Instance;string original=File.ReadAllText(Root+"original-profile.txt").Trim();
        if(r.Player.id!=Qa&&r.Player.id!=original)throw new Exception("Unexpected profile");r.SwitchPlayer(original,null);
        string path=Path.Combine(Application.persistentDataPath,"LocalPlayers",Qa);
        foreach(string suffix in new[]{".json",".json.bak",".lock"})if(File.Exists(path+suffix))File.Delete(path+suffix);
        return new{restored=r.Player.id==original};
    }
}

