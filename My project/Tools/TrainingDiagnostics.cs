using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PickleVerse.LocalMatch;
using PickleVerse.Progression;
using UnityEngine;

public static class TrainingDiagnostics
{
    public static object All()
    {
        var results=TrainingCurriculum.Lessons.Select(d=>Run(d.id)).ToArray();
        return new{count=results.Length,results};
    }
    public static object Run(string lessonId="drive")
    {
        if(!Application.isPlaying)throw new Exception("Play Training first.");
        var d=UnityEngine.Object.FindAnyObjectByType<TrainingDirector>();var runtime=ProgressionRuntime.Instance;
        string original=runtime.Player.id;bool paused=UnityEditor.EditorApplication.isPaused;
        string id="TrainingQA"+Guid.NewGuid().ToString("N").Substring(0,10);var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        try
        {
            UnityEditor.EditorApplication.isPaused=true;
            runtime.SwitchPlayer(id,"Training QA");runtime.UpdateWallet(p=>{foreach(var lesson in TrainingCurriculum.Lessons)TrainingEngine.Record(TrainingEngine.Ensure(p),lesson.id).bestStars=1;});
            d.SetPaused(false);d.Select(lessonId);d.StartLesson();int frames=0,shots=0;bool issued=false;int trial=-1;
            while(d.Phase!=TrainingPhase.Finished&&frames++<24000)
            {
                if(d.Session.trials!=trial){trial=d.Session.trials;issued=false;}
                var goal=d.Phase==TrainingPhase.Recovery?d.RecoveryPosition:d.ReceivePosition;
                Vector3 delta=goal-d.player.transform.position;Vector2 move=new Vector2(delta.x,delta.z);move=move.normalized*Mathf.Clamp01(move.magnitude*3);
                bool incoming=(bool)typeof(TrainingDirector).GetField("incoming",flags).GetValue(d);
                int mask=0,tap=0;
                if(d.Phase==TrainingPhase.Practice&&!d.PartnerTurn&&!issued&&(lessonId=="serve"||incoming&&d.ReleaseIn<=.012f))
                {tap=d.ExpectedMask;mask=d.ExpectedMask&24;issued=true;shots++;}
                d.Advance(1f/60,mask,move,true,tap);
            }
            var result=new{lesson=lessonId,frames,shots,phase=d.Phase.ToString(),successes=d.Session.successes,trials=d.Session.trials,stars=d.Session.stars,d.Session.passed,errors=d.Session.errors.Select(e=>new{error=e.error.ToString(),e.count}).ToArray(),d.Feedback,position=d.player.transform.position.ToString()};
            Directory.CreateDirectory("../UnityWork/Training");File.WriteAllText("../UnityWork/Training/diagnostic-"+lessonId+".json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally
        {
            d.Catalogue();runtime.SwitchPlayer(original,null);UnityEditor.EditorApplication.isPaused=paused;
            // Only remove this diagnostic's files, after releasing the profile lease.
            string root=Path.Combine(Application.persistentDataPath,"LocalPlayers");
            foreach(var suffix in new[]{".json",".json.bak",".lock"}){string path=Path.Combine(root,id+suffix);if(File.Exists(path))File.Delete(path);}
        }
    }
}
