using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using PickleVerse.LocalMatch;
using PickleVerse.Audio;
using PickleVerse.Progression;
using Newtonsoft.Json;

public static class HitGradeValidation
{
    const string Folder="../UnityWork/HitGrades20261019";
    static Task running;
    static void Save(string name,object value){Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/"+name+".json",JsonConvert.SerializeObject(value,Formatting.Indented));}
    public static object Start(){running=Run();return new{started=true};}
    public static object VisualStart(){running=Visual();return new{started=true};}
    static async Task Load(string name)
    {
        var task=SceneManager.LoadSceneAsync(name);while(!task.isDone)await Task.Delay(30);await Task.Delay(250);
    }
    static async Task Picture(string name)
    {await Task.Delay(60);ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder+"/"+name+".png"));await Task.Delay(150);}
    static async Task Visual()
    {
        var runtime=ProgressionRuntime.Instance;string original=runtime.Player.id,qa="HitVisualQA"+Guid.NewGuid().ToString("N").Substring(0,10);
        try
        {
            runtime.SwitchPlayer(qa,"Hit visual QA");await Load("BotMatch");
            var m=UnityEngine.Object.FindAnyObjectByType<BotMatch>();m.Continue();m.opening?.Skip();m.enabled=false;m.hud.enabled=false;
            m.GetComponent<MatchEnvironmentLighting>().Apply(false);m.hud.modal.SetActive(false);m.hud.announcements.Clear();
            var a=m.human;var shot=new ShotIntent(ShotKind.Drive);
            foreach(var grade in new[]{HitGrade.Perfect,HitGrade.Good})
            {
                a.ResetAt(new Vector3(.6f,0,-4.4f),-1);a.RefreshPose();Vector3 target=a.transform.TransformPoint(a.ContactOffset(shot));
                a.Swing(shot,target);for(int i=0;i<52;i++)a.Step(Vector2.zero,false,1f/120,false);
                Vector3 point=a.paddleFace.position;bool found=false;
                foreach(Vector3 offset in grade==HitGrade.Perfect?new[]{Vector3.zero}:new[]{Vector3.right*.25f,Vector3.left*.25f,Vector3.forward*.25f,Vector3.back*.25f})
                {point=a.paddleFace.position+offset;if(a.CanContact(point,point,.45f,out _)){found=true;break;}}
                if(!found)throw new Exception("Visual fixture missed "+grade);
                a.ConsumeContact(false,point);if(a.LastContactGrade!=grade)throw new Exception("Visual grade mismatch "+grade);
                m.ball.position=point;
                await Picture(grade.ToString().ToLowerInvariant());
            }
            var feedback=a.GetComponent<HitGradeFeedback>();feedback.Show(HitGrade.Perfect);feedback.Advance(.01f);
            long before=GC.GetAllocatedBytesForCurrentThread();var watch=System.Diagnostics.Stopwatch.StartNew();
            for(int i=0;i<1000;i++)feedback.Advance(.0001f);
            watch.Stop();long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Save("feedback-cost",new{updates=1000,managedBytes=allocated,milliseconds=watch.Elapsed.TotalMilliseconds,caption=feedback.Caption});
        }
        catch(Exception e){Save("visual-error",new{error=e.ToString()});Debug.LogException(e);}
        finally
        {
            await Load("Menu");runtime.SwitchPlayer(original,null);
            string root=Path.Combine(Application.persistentDataPath,"LocalPlayers");foreach(string suffix in new[]{".json",".json.bak",".lock"}){string file=Path.Combine(root,qa+suffix);if(File.Exists(file))File.Delete(file);}
        }
    }
    static async Task Run()
    {
        Save("runtime",new{status="running"});
        var runtime=ProgressionRuntime.Instance;string original=runtime.Player.id;
        string qa="HitGradeQA"+Guid.NewGuid().ToString("N").Substring(0,10);
        var random=UnityEngine.Random.state;
        try
        {
            runtime.SwitchPlayer(qa,"Hit grade QA");await Load("BotMatch");
            var match=UnityEngine.Object.FindAnyObjectByType<BotMatch>();
            match.opening?.Skip();match.Continue();match.enabled=false;match.hud.announcements.Clear();match.hud.modal.SetActive(false);
            var rows=new List<object>();int perfect=0,good=0,failures=0;
            foreach(string sex in new[]{"Female","Male"})
            {
                var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Multiplayer/BotMatch/"+sex+"Actor.prefab"));
                var a=go.GetComponent<MatchActor>();a.Initialize();var fx=a.GetComponent<PaddleImpactVfx>();
                try
                {
                    foreach(var kind in new[]{ShotKind.Drive,ShotKind.Dink,ShotKind.Drop,ShotKind.Volley,ShotKind.Reset,ShotKind.Smash})
                    foreach(bool backhand in kind==ShotKind.Smash?new[]{false}:new[]{false,true})
                    foreach(float dt in new[]{1f/120,1f/60,1f/30})
                    foreach(var movement in new[]{Vector2.zero,Vector2.right,Vector2.up})
                    foreach(bool quick in new[]{false,true})
                    {
                        a.ResetAt(new Vector3(0,0,-4.4f),-1);a.RefreshPose();a.ObserveMovement(movement,true);
                        var shot=new ShotIntent(kind,movement.x>0,movement.y>0);
                        float delay=quick?.125f:shot.ContactDelay;
                        Vector3 target=a.transform.TransformPoint(a.ContactOffset(shot,backhand))+PaddleContact.PredictMovement(a,delay);
                        Vector3 velocity=new Vector3(0,0,-6),ball=target-velocity*delay+Vector3.up*(.5f*BallFlight.Gravity*delay*delay);
                        a.Swing(shot,target,delay);int before=fx.PlayCount;
                        GameSfx sound=shot.power?GameSfx.HitStrong:GameSfx.HitNormal;
                        int soundBefore=SfxPlayer.PlayCounts[(int)sound];bool hit=false;float error=99;Vector3 start=a.transform.position;
                        for(int i=0;i<Mathf.CeilToInt((delay+.12f)/dt);i++)
                        {
                            Vector3 old=ball;BallFlight.Integrate(ref ball,ref velocity,dt);a.Step(movement,true,dt,true);
                            if(!a.CanContact(old,ball,.45f,out _))continue;
                            ball=a.ContactPoint;a.ConsumeContact(shot.BeforeBounce,ball);error=Vector3.Distance(ball,a.paddleFace.position);hit=true;
                            a.ConsumeContact(shot.BeforeBounce,ball);break;
                        }
                        if(a.LastContactGrade==HitGrade.Perfect)perfect++;else if(a.LastContactGrade==HitGrade.Good)good++;
                        bool moving=movement==Vector2.zero||a.Velocity.magnitude>.2f&&Vector3.Distance(start,a.transform.position)>.02f&&a.LocomotionClip!="PKV_Idle_Ready";
                        bool ok=hit&&error<.22f&&moving&&fx.PlayCount==before+1&&SfxPlayer.PlayCounts[(int)sound]==soundBefore+1&&!a.ContactActive;
                        if(!ok)failures++;
                        rows.Add(new{sex,kind=kind.ToString(),backhand,dt,movement=movement.ToString(),quick,hit,error,moving,grade=a.LastContactGrade.ToString(),ok});
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(go);}
            }
            Save("matrix",new{passed=failures==0,failures,cases=rows.Count,perfect,good,rows});

            var actor=match.human;actor.LocalHitFeedback=true;
            var checks=new List<object>();
            foreach(var grade in new[]{HitGrade.Perfect,HitGrade.Good})
            {
                match.hud.announcements.Clear();actor.ResetAt(new Vector3(.6f,0,-4.4f),-1);actor.RefreshPose();
                var shot=new ShotIntent(ShotKind.Drive);Vector3 target=actor.transform.TransformPoint(actor.ContactOffset(shot));
                actor.Swing(shot,target);actor.SynchronizeSwing(actor.ContactDelay);
                int hits=actor.GetComponent<PaddleImpactVfx>().PlayCount,sounds=SfxPlayer.PlayCounts[(int)GameSfx.HitNormal];
                actor.ConsumeContact(false,actor.paddleFace.position,true,grade);
                var feedback=actor.GetComponent<HitGradeFeedback>();
                actor.ConsumeContact(false,actor.paddleFace.position,true,grade);
                checks.Add(new{name=grade+" label and single impact",passed=feedback.Visible&&feedback.Caption==(grade==HitGrade.Perfect?"Hoàn hảo":"Tốt")&&actor.GetComponent<PaddleImpactVfx>().PlayCount==hits+1&&SfxPlayer.PlayCounts[(int)GameSfx.HitNormal]==sounds+1});
                feedback.Advance(.08f);
                await Picture(grade.ToString().ToLowerInvariant());
                feedback.Advance(2);checks.Add(new{name=grade+" expires",passed=!feedback.Visible});
            }
            actor.ResetAt(new Vector3(.6f,0,-4.4f),-1);actor.RefreshPose();actor.Swing(new ShotIntent(ShotKind.Drive),actor.transform.position+Vector3.up);
            actor.SynchronizeSwing(actor.ContactDelay);int shown=actor.GetComponent<HitGradeFeedback>().ShowCount;
            actor.ConsumeContact(false,actor.paddleFace.position,false);
            checks.Add(new{name="Fault has no grade",passed=actor.LastContactGrade==HitGrade.None&&actor.GetComponent<HitGradeFeedback>().ShowCount==shown});
            checks.Add(new{name="Bot has no local UI",passed=!match.bot.LocalHitFeedback&&!match.bot.GetComponent<HitGradeFeedback>()});
            // Regression: actual BotMatch rules, key combinations and launch paths.
            string contacts=PickleVerse.LocalMatch.Editor.BotMatchDiagnostics.ContactChecks();Save("bot-contacts",JsonConvert.DeserializeObject(contacts));
            Save("runtime",new{passed=failures==0&&checks.All(x=>(bool)x.GetType().GetProperty("passed").GetValue(x)),matrixCases=rows.Count,perfect,good,checks});
        }
        catch(Exception e){Save("runtime",new{passed=false,error=e.ToString()});Debug.LogException(e);}
        finally
        {
            await Load("Menu");runtime.SwitchPlayer(original,null);UnityEngine.Random.state=random;
            string root=Path.Combine(Application.persistentDataPath,"LocalPlayers");
            foreach(string suffix in new[]{".json",".json.bak",".lock"}){string file=Path.Combine(root,qa+suffix);if(File.Exists(file))File.Delete(file);}
        }
    }
}
