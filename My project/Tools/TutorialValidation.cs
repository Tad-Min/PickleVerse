using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.LocalMatch;
using PickleVerse.Rules;

public static class TutorialValidation
{
    sealed class Memory:ITutorialProgressStore
    {readonly Dictionary<string,int> values=new Dictionary<string,int>();public int Read(string key)=>values.TryGetValue(key,out int v)?v:0;public void Write(string key,int value){values[key]=value;}}
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool ok){checks.Add(new{name,passed=ok});if(!ok)throw new Exception(name);}
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first.");checks.Clear();TutorialProgress.Configure(new Memory());
        int completed=PlayerPrefs.GetInt(TutorialProgress.CompletedKey,-1),skipped=PlayerPrefs.GetInt(TutorialProgress.SkippedKey,-1);
        try
        {
            SceneManager.LoadScene("Tutorial");await Task.Delay(150);var t=UnityEngine.Object.FindAnyObjectByType<TutorialDirector>();t.enabled=false;
            Check("Tutorial has independent runtime",UnityEngine.Object.FindObjectsByType<BotMatch>().Length==0&&t&&t.player&&t.coach);
            Check("Fresh player needs tutorial",TutorialProgress.NeedsIntroduction);
            var start=t.view.transform.position;t.Advance(1);Check("Court camera moves",Vector3.Distance(start,t.view.transform.position)>.005f);
            t.Advance(4);Check("Court overview ready",t.CanContinue&&t.courtLine.gameObject.activeSelf);t.Next();
            start=t.view.transform.position;t.Advance(1);Check("Kitchen camera transitions",Vector3.Distance(start,t.view.transform.position)>.2f);t.Advance(4);t.Next();
            var demo=new List<object>();
            for(int stage=2;stage<=6;stage++)
            {
                for(int i=0;i<1800&&!t.CanContinue;i++)t.Advance(1f/120);
                demo.Add(new{stage,t.Elapsed,fault=t.DemonstratedFault.ToString(),t.Feedback});
                Check("Rule demo completes "+stage,t.CanContinue&&t.LessonIndex==stage);t.Next();
            }
            Check("Movement requires action",t.Lesson.kind==TutorialKind.Movement&&!t.CanContinue);for(int i=0;i<40;i++)t.Advance(1f/120,0,Vector2.left,false);for(int i=0;i<35;i++)t.Advance(1f/120,0,Vector2.right,true);Check("Walk and sprint accepted",t.CanContinue);t.Next();
            for(int lesson=TutorialCurriculum.FirstShot;lesson<TutorialCurriculum.FirstShot+9;lesson++)
            {
                int mask=t.Lesson.mask;start=t.view.transform.position;t.Advance(.5f);
                t.Advance(1f/120,0);t.Advance(1f/120,mask);t.Advance(1f/120,mask&24);t.Advance(1f/120,0);
                Check("Correct input buffered "+mask,t.HasBufferedShot);
                for(int i=0;i<1500&&t.LessonIndex==lesson;i++)t.Advance(1f/120);
                Check("Actual paddle contact completes "+mask,t.LessonIndex==lesson+1&&t.ShotsCompleted==lesson-TutorialCurriculum.FirstShot+1);
            }
            Check("Completion recorded only after nine hits",t.Lesson.kind==TutorialKind.Complete&&TutorialProgress.Completed&&!TutorialProgress.NeedsIntroduction);
            Check("Real progress untouched",PlayerPrefs.GetInt(TutorialProgress.CompletedKey,-1)==completed&&PlayerPrefs.GetInt(TutorialProgress.SkippedKey,-1)==skipped);
            var result=new{passed=true,count=checks.Count,demo,checks};Directory.CreateDirectory("../UnityWork/Tutorial");File.WriteAllText("../UnityWork/Tutorial/validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally{TutorialProgress.Configure(null);SceneManager.LoadScene("Tutorial");}
    }
}
