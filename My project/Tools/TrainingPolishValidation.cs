using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PickleVerse.Audio;
using PickleVerse.LocalMatch;
using PickleVerse.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TrainingPolishValidation
{
    static readonly List<object> checks=new List<object>();
    static bool successCaptured;
    static void Check(string name,bool ok){checks.Add(new{name,passed=ok});if(!ok)throw new Exception(name);}
    static async Task Wait(Func<bool> ready){float until=Time.realtimeSinceStartup+12;while(!ready()){if(Time.realtimeSinceStartup>until)throw new Exception("Music timeout");await Task.Delay(60);}}
    static void Complete(TrainingDirector d)
    {
        int trial=-1;bool issued=false;int frames=0;
        while(d.Phase!=TrainingPhase.Finished&&frames++<24000)
        {
            if(d.Session.trials!=trial){trial=d.Session.trials;issued=false;}
            int tap=0;
            if(d.Phase==TrainingPhase.Practice&&!d.PartnerTurn&&!issued&&(d.Lesson.skill==TrainingSkill.Serve||d.Receiving&&d.ReleaseIn<=.012f)){tap=d.ExpectedMask;issued=true;}
            var delta=(d.Phase==TrainingPhase.Recovery?d.RecoveryPosition:d.ReceivePosition)-d.player.transform.position;
            Vector2 move=new Vector2(delta.x,delta.z);move=move.normalized*Mathf.Clamp01(move.magnitude*3);
            d.Advance(1f/60,d.ExpectedMask&24,move,false,tap);
            if(d.hud.announcements.ActiveTitle=="THÀNH CÔNG"&&d.hud.announcements.Elapsed>.4f&&!successCaptured){Capture(d.view,"success");successCaptured=true;}
        }
        File.WriteAllText("../UnityWork/TrainingPolish/physical-"+d.Lesson.id+".json",JsonUtility.ToJson(d.Session,true));
        Check("Physical "+d.Lesson.id+" completes with three stars",d.Phase==TrainingPhase.Finished&&d.Session.passed&&d.Session.stars==3);
    }
    public static async Task<object> Run()
    {
        if(!Application.isPlaying||SceneManager.GetActiveScene().name!="Training")throw new Exception("Play Training first.");
        checks.Clear();successCaptured=false;Directory.CreateDirectory("../UnityWork/TrainingPolish");
        var runtime=ProgressionRuntime.Instance;var d=UnityEngine.Object.FindAnyObjectByType<TrainingDirector>();
        string original=runtime.Player.id,qa="TrainingQA"+Guid.NewGuid().ToString("N").Substring(0,10);
        bool pause=UnityEditor.EditorApplication.isPaused,musicPaused=MusicPlayer.Instance&&MusicPlayer.Instance.UserPaused;
        var sounds=new List<GameSfx>();Action<GameSfx,Vector3> listener=(sound,position)=>sounds.Add(sound);
        try
        {
            UnityEditor.EditorApplication.isPaused=false;runtime.SwitchPlayer(qa,"Training QA");runtime.UpdateWallet(p=>{p.coins=1000;p.gems=9;});
            d.Catalogue();d.SetPaused(false);if(MusicPlayer.Instance.UserPaused)MusicPlayer.Instance.TogglePause();
            await Wait(()=>MusicPlayer.Instance.Playing);
            Check("Direct Training entry has catalogue music",MusicPlayer.Instance.Allowed&&MusicPlayer.Instance.Playing);
            Check("Training has no music widget",!UnityEngine.Object.FindAnyObjectByType<MusicUI>().mini.gameObject.activeInHierarchy);
            d.Select("serve");Check("Briefing keeps background music",MusicPlayer.Instance.Allowed);
            d.StartLesson();Check("Exercise stops all music voices without user pause",!MusicPlayer.Instance.Allowed&&!MusicPlayer.Instance.UserPaused&&!MusicPlayer.Instance.GetComponentsInChildren<AudioSource>().Any(s=>s.isPlaying));
            UnityEditor.EditorApplication.isPaused=true;d.SetPaused(false);d.Advance(1f/60);SfxPlayer.Emitted+=listener;Complete(d);
            Check("Serve contacts use serve sound once per hit",sounds.Count(s=>s==GameSfx.Serve)==8&&sounds.Count(s=>s==GameSfx.HitNormal||s==GameSfx.HitStrong||s==GameSfx.FaultWhistle)==0);
            Check("First mastery rewards 150 Tokenpad and 5 XP",runtime.Player.coins==1150&&runtime.Player.totalXp==5&&d.Session.rewardTokenpad==150);
            Check("Result retry is labelled review",d.hud.retry.GetComponentInChildren<TMPro.TMP_Text>().text=="ÔN LẠI");
            Capture(d.view,"first-result");d.Catalogue();Canvas.ForceUpdateCanvases();
            var row=d.hud.content.Cast<Transform>().Single(t=>t.name=="Lesson serve"&&t.gameObject.activeSelf);Check("Mastered lesson shows review and no reward",row.Find("Open/Label").GetComponent<TMPro.TMP_Text>().text=="ÔN LẠI"&&row.Find("Info").GetComponent<TMPro.TMP_Text>().text.Contains("Ôn lại không thưởng"));
            d.Select("serve");Check("Review briefing states no Tokenpad or XP",d.hud.lessonBody.text.Contains("không nhận thưởng")&&d.hud.start.GetComponentInChildren<TMPro.TMP_Text>().text=="BẮT ĐẦU ÔN LẠI");
            d.StartLesson();sounds.Clear();Complete(d);
            Check("Full three-star review awards nothing",d.Session.review&&d.Session.rewardTokenpad==0&&d.Session.rewardXp==0&&runtime.Player.coins==1150&&runtime.Player.totalXp==5&&runtime.Player.gems==9);
            Check("Review retains assessment and mastery history",runtime.Player.training.lessons.Single().completions==2&&runtime.Player.training.lessons.Single().accuracyHistory.Count==2);
            Check("Review result does not claim reward",d.hud.resultBody.text.Contains("Ôn lại không nhận thưởng")&&!d.hud.resultBody.text.Contains("Đã nhận"));
            Capture(d.view,"review-result");d.Catalogue();
            d.Select("drive");d.StartLesson();sounds.Clear();d.Advance(1f/60,0,Vector2.zero,false,2);d.Advance(.4f);
            var a=d.hud.announcements;
            Check("Wrong technique shows centered reassurance",a.Busy&&a.ActiveTitle.Contains("THỬ LẠI")&&a.detail.text.Contains("Không sao bạn ơi, rồi bạn cũng quen thôi."));
            Check("Encouragement glyphs are baked in the static Vietnamese font",a.detail.font.HasCharacter('ũ')&&a.detail.font.atlasPopulationMode==TMPro.AtlasPopulationMode.Static);
            Check("Wrong-input coaching does not replace hit with referee whistle",sounds.Count==0);
            int before=d.Session.trials;double seconds=d.Session.seconds;float elapsed=a.Elapsed;d.SetPaused(true);d.Advance(2);
            Check("Pause freezes exercise and sweep",d.Session.seconds==seconds&&a.Elapsed==elapsed&&!a.panel.gameObject.activeSelf);
            d.SetPaused(false);Check("Resume restores sweep",a.panel.gameObject.activeSelf);Capture(d.view,"retry-feedback");
            d.Advance(.5f);Check("Next feed waits until sweep exits",d.Session.trials==before&&d.Phase==TrainingPhase.Recovery);
            for(int i=0;i<180&&d.Phase==TrainingPhase.Recovery;i++)d.Advance(1f/60);
            Check("Sweep completes before next practice ball",!a.Busy&&d.Phase==TrainingPhase.Practice&&d.Session.trials==before+1);
            d.Select("drive");d.StartLesson(true);sounds.Clear();Complete(d);
            Check("Drive contacts match BotMatch normal sound",sounds.Count(s=>s==GameSfx.HitNormal)==16&&sounds.Count(s=>s==GameSfx.FaultWhistle)==0);
            runtime.UpdateWallet(p=>{foreach(var id in new[]{"dink","volley"})TrainingEngine.Record(p.training,id).bestStars=1;});
            d.Select("modifiers");d.StartLesson();sounds.Clear();Complete(d);
            Check("Power uses strong sound; cross and feeder use normal sound",sounds.Count(s=>s==GameSfx.HitStrong)==5&&sounds.Count(s=>s==GameSfx.HitNormal)==15&&sounds.Count(s=>s==GameSfx.Serve||s==GameSfx.FaultWhistle)==0);
            d.Catalogue();d.Advance(1);UnityEditor.EditorApplication.isPaused=false;await Wait(()=>MusicPlayer.Instance.Playing);
            Canvas.ForceUpdateCanvases();Capture(d.view,"catalogue-review");
            Check("Returning to catalogue resumes music",MusicPlayer.Instance.Allowed&&MusicPlayer.Instance.Playing);
            MusicPlayer.Instance.TogglePause();Check("Listener music pause survives exercise and return",MusicPlayer.Instance.UserPaused);
            d.Select("serve");d.StartLesson();d.Catalogue();Check("Training silence never changes user's pause preference",MusicPlayer.Instance.UserPaused&&!MusicPlayer.Instance.Playing);
            Check("Single listener and original shared impact clips",UnityEngine.Object.FindObjectsByType<AudioListener>().Count(l=>l.enabled)==1&&Resources.Load<SfxLibrary>("Audio/Match SFX").Clip(GameSfx.HitNormal).name=="smash-normal"&&Resources.Load<SfxLibrary>("Audio/Match SFX").Clip(GameSfx.HitStrong).name=="smash-strong");
            var result=new{passed=true,count=checks.Count,checks};File.WriteAllText("../UnityWork/TrainingPolish/validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally
        {
            SfxPlayer.Emitted-=listener;d.Catalogue();runtime.SwitchPlayer(original,null);d.Catalogue();
            if(MusicPlayer.Instance&&MusicPlayer.Instance.UserPaused!=musicPaused)MusicPlayer.Instance.TogglePause();
            UnityEditor.EditorApplication.isPaused=pause;
            string root=Path.Combine(Application.persistentDataPath,"LocalPlayers");
            foreach(var suffix in new[]{".json",".json.bak",".lock"}){string path=Path.Combine(root,qa+suffix);if(File.Exists(path))File.Delete(path);}
        }
    }
    static void Capture(Camera camera,string name)
    {
        var target=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
        try{camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("../UnityWork/TrainingPolish/"+name+".png",image.EncodeToPNG());}
        finally{camera.targetTexture=previousTarget;RenderTexture.active=previousActive;UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
    }
}
