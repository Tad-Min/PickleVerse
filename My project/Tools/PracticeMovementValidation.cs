using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.LocalMatch;
using PickleVerse.Progression;
using Newtonsoft.Json;

public static class PracticeMovementValidation
{
    const string Folder="../UnityWork/PracticeSmooth20261019";
    static Task running;static readonly List<object> checks=new List<object>();
    static void Save(object value){Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/movement.json",JsonConvert.SerializeObject(value,Formatting.Indented));}
    static void Check(string name,bool passed){checks.Add(new{name,passed});}
    static async Task Load(string scene){var op=SceneManager.LoadSceneAsync(scene);while(!op.isDone)await Task.Delay(20);await Task.Delay(200);}
    static bool Idle(MatchActor a)=>a.Velocity.sqrMagnitude<.0001f&&a.CurrentClip=="PKV_Idle_Ready";
    static bool Inside(MatchActor a){var p=a.transform.position;var r=MatchMovement.PracticeBounds(a.end);return p.x>=r.xMin-.001f&&p.x<=r.xMax+.001f&&p.z>=r.yMin-.001f&&p.z<=r.yMax+.001f;}
    sealed class Memory:ITutorialProgressStore{readonly Dictionary<string,int> values=new Dictionary<string,int>();public int Read(string k)=>values.TryGetValue(k,out var v)?v:0;public void Write(string k,int v){values[k]=v;}}
    public static object Start(){if(!Application.isPlaying)throw new Exception("Play Mode required");checks.Clear();running=Run();return new{started=true};}
    static async Task Run()
    {
        Save(new{status="running"});var runtime=ProgressionRuntime.Instance;string original=runtime.Player.id,qa="PracticeMove"+Guid.NewGuid().ToString("N").Substring(0,8);
        try
        {
            runtime.SwitchPlayer(qa,"Practice movement QA");await Load("Training");var d=UnityEngine.Object.FindAnyObjectByType<TrainingDirector>();d.enabled=false;
            runtime.UpdateWallet(p=>{foreach(var lesson in TrainingCurriculum.Lessons)TrainingEngine.Record(TrainingEngine.Ensure(p),lesson.id).bestStars=1;});
            var directions=new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right};
            foreach(var lesson in TrainingCurriculum.Lessons)
            {
                d.Select(lesson.id);
                foreach(var direction in directions)
                {
                    d.StartLesson(true);var area=MatchMovement.PracticeBounds(-1);
                    Vector3 start=new Vector3(direction.x>0?area.xMax-.2f:direction.x<0?area.xMin+.2f:0,0,direction.y>0?area.yMax-.2f:direction.y<0?area.yMin+.2f:-4);
                    d.player.ResetAt(start,-1);d.player.RefreshPose();
                    for(int i=0;i<40;i++)d.Advance(1f/30,0,direction,true);
                    Check(lesson.id+" hold into boundary "+direction,Inside(d.player)&&Idle(d.player));
                    var boundary=d.player.transform.position;
                    for(int i=0;i<20;i++)d.Advance(1f/60,0,-direction,true);
                    Check(lesson.id+" reverse away "+direction,Vector3.Distance(boundary,d.player.transform.position)>.3f&&d.player.CurrentClip!="PKV_Idle_Ready");
                    for(int i=0;i<25;i++)d.Advance(1f/60);
                    Check(lesson.id+" released input "+direction,Idle(d.player));
                }
                if(lesson.Doubles)
                {
                    d.StartLesson(true);d.player.ResetAt(new Vector3(-2.7f,0,-3),-1);d.player.RefreshPose();
                    for(int i=0;i<240;i++)d.Advance(1f/60);
                    Check(lesson.id+" partner keeps space at left edge",Mathf.Abs(d.player.transform.position.x-d.partner.transform.position.x)>.85f);
                }
                Save(new{status="running",stage="Training "+lesson.id,checks});await Task.Delay(1);
            }
            d.Catalogue();TutorialProgress.Configure(new Memory());await Load("Tutorial");var t=UnityEngine.Object.FindAnyObjectByType<TutorialDirector>();t.enabled=false;
            var begin=typeof(TutorialDirector).GetMethod("Begin",BindingFlags.Instance|BindingFlags.NonPublic);
            for(int index=0;index<TutorialCurriculum.Lessons.Length;index++)
            {
                begin.Invoke(t,new object[]{index,true});var kind=t.Lesson.kind;
                if(kind==TutorialKind.Movement)
                {
                    for(int i=0;i<90;i++)t.Advance(1f/30,0,Vector2.up,true);
                    Check("Tutorial held into net stops feet",Inside(t.player)&&Idle(t.player)&&t.player.transform.position.z<=-.85f+.001f);
                    var atNet=t.player.transform.position;
                    for(int i=0;i<30;i++)t.Advance(1f/60,0,Vector2.down,true);
                    Check("Tutorial turns away from net",Vector3.Distance(atNet,t.player.transform.position)>.5f);
                    for(int i=0;i<30;i++)t.Advance(1f/60);
                    Check("Tutorial release returns idle",Idle(t.player));Check("Movement tutorial completes from real movement",t.CanContinue);
                    begin.Invoke(t,new object[]{index,true});t.player.ResetAt(new Vector3(0,0,-.85f),-1);t.player.RefreshPose();
                    for(int i=0;i<90;i++)t.Advance(1f/60,0,Vector2.up,true);
                    Check("Blocked sprint does not count as learning to run",!t.CanContinue);
                }
                else if(kind==TutorialKind.Shot)
                {
                    int mask=t.Lesson.mask;t.Advance(1f/60);t.Advance(1f/60,mask);t.Advance(1f/60,mask&24);
                    for(int i=0;i<900&&t.LessonIndex==index;i++)t.Advance(1f/30);
                    Check("Tutorial shot "+mask+" completes",t.LessonIndex==index+1);
                }
                else if(kind!=TutorialKind.Complete)
                {
                    for(int i=0;i<1000&&!t.CanContinue;i++)t.Advance(1f/30);
                    Check("Tutorial demo "+kind+" completes",t.CanContinue);
                    if(kind==TutorialKind.Serve||kind==TutorialKind.KitchenVolley)
                    {
                        for(int i=0;i<240;i++)t.Advance(1f/60);
                        Vector3 position=t.player.transform.position;
                        for(int i=0;i<120;i++)t.Advance(1f/60);
                        Check("Tutorial auto movement stops after "+kind,Vector3.Distance(position,t.player.transform.position)<.02f&&Idle(t.player));
                    }
                }
                Save(new{status="running",stage="Tutorial "+kind,checks});await Task.Delay(1);
            }
            Check("Tutorial nine shots and completion saved",t.ShotsCompleted==9&&TutorialProgress.Completed);
            Save(new{passed=checks.All(c=>(bool)c.GetType().GetProperty("passed").GetValue(c)),count=checks.Count,checks});
        }
        catch(Exception e){Save(new{passed=false,error=e.ToString(),checks});Debug.LogException(e);}
        finally
        {
            TutorialProgress.Configure(null);await Load("Menu");runtime.SwitchPlayer(original,null);
            string root=Path.Combine(Application.persistentDataPath,"LocalPlayers");foreach(string suffix in new[]{".json",".json.bak",".lock"}){string path=Path.Combine(root,qa+suffix);if(File.Exists(path))File.Delete(path);}
        }
    }
}
