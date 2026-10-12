using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using PickleVerse.FrontEnd;
using PickleVerse.LocalMatch;
using PickleVerse.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TrainingFlowValidation
{
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool ok){checks.Add(new{name,passed=ok});if(!ok)throw new Exception(name);}
    static async Task Scene(string name){float until=Time.realtimeSinceStartup+20;while(SceneManager.GetActiveScene().name!=name){if(Time.realtimeSinceStartup>until)throw new Exception("Scene timeout "+name);await Task.Delay(40);}await Task.Delay(250);}
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first.");checks.Clear();
        var runtime=ProgressionRuntime.Instance;string original=runtime.Player.id;string qa="TrainingQA"+Guid.NewGuid().ToString("N").Substring(0,10);bool paused=UnityEditor.EditorApplication.isPaused;
        try
        {
            runtime.SwitchPlayer(qa,"Training QA");runtime.UpdateWallet(p=>{p.coins=1000;p.gems=9;});
            SceneManager.LoadScene("ModeSelection");await Scene("ModeSelection");
            var modes=UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>();var button=modes.modePage.transform.Find("TrainingMode").GetComponent<UnityEngine.UI.Button>();
            Check("Training card is enabled and wired",button.interactable&&button.onClick.GetPersistentMethodName(0)=="OpenTraining");button.onClick.Invoke();await Scene("Training");
            var d=UnityEngine.Object.FindAnyObjectByType<TrainingDirector>();d.SetPaused(false);
            Check("Training opens its lesson catalogue",d.Phase==TrainingPhase.Catalogue&&d.hud.catalogue.gameObject.activeSelf&&d.hud.content.childCount==12);
            Check("Arena copied with four lights and no missing scripts",GameObject.Find("Arena").GetComponentsInChildren<Renderer>(true).Length==28&&d.GetComponent<MatchEnvironmentLighting>().floodlights.Length==4&&d.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Sum(t=>UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))==0);
            d.hud.content.Find("Lesson drive/Open").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();await Task.Delay(200);
            Check("Lesson opens detailed coach dialogue",d.Phase==TrainingPhase.Briefing&&d.hud.briefing.gameObject.activeSelf&&d.hud.lessonBody.text.Contains("150 Tokenpad"));
            Vector3 cam=d.view.transform.position;d.Advance(1.4f);Check("Briefing moves the camera",Vector3.Distance(cam,d.view.transform.position)>.2f);
            d.hud.start.onClick.Invoke();d.SetPaused(false);UnityEditor.EditorApplication.isPaused=true;
            Check("Start button opens real practice",d.Phase==TrainingPhase.Practice&&d.Session!=null&&d.hud.practice.gameObject.activeSelf);
            long balance=runtime.Player.coins;
            for(int i=0;i<24000&&d.Phase!=TrainingPhase.Finished;i++)d.Advance(1f/60);
            Check("Missing balls gives a failed assessment and no reward",d.Phase==TrainingPhase.Finished&&!d.Session.passed&&runtime.Player.coins==balance&&d.Session.errors.Any(e=>e.error==TrainingError.Late));
            d.hud.retry.onClick.Invoke();Check("Retry creates a new tracked attempt",d.Phase==TrainingPhase.Practice&&runtime.Player.training.lessons.Single().retries==1);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;int currentTrial=-1;bool issued=false;
            for(int i=0;i<24000&&d.Phase!=TrainingPhase.Finished;i++)
            {
                if(currentTrial!=d.Session.trials){currentTrial=d.Session.trials;issued=false;}
                bool incoming=(bool)typeof(TrainingDirector).GetField("incoming",flags).GetValue(d);
                int tap=0;if(incoming&&!issued&&d.ReleaseIn<=.012f){tap=d.ExpectedMask;issued=true;}
                d.Advance(1f/60,d.ExpectedMask&24,Vector2.zero,false,tap);
            }
            Check("Successful retry earns 150 Tokenpad and XP",d.Session.passed&&d.Session.stars==3&&runtime.Player.coins==balance+150&&runtime.Player.totalXp==5);
            long awarded=runtime.Player.coins;runtime.UpdateWallet(p=>TrainingEngine.Complete(p,d.Session));Check("Replaying result does not award twice",runtime.Player.coins==awarded&&runtime.Player.gems==9);
            d.Select("drive");d.StartLesson(true);d.Session.trials=3;d.Session.successes=2;d.SaveActive();string checkpoint=d.Session.id;
            d.Catalogue();d.Select("drive");d.StartLesson();Check("Current lesson resumes its saved checkpoint",d.Session.id==checkpoint&&d.Session.trials==3&&d.Session.successes==2);
            d.SetPaused(true);double time=d.Session.seconds;d.Advance(2);Check("Pause freezes practice",d.Session.seconds==time);d.SetPaused(false);d.LeaveToMenu();
            UnityEditor.EditorApplication.isPaused=false;await Scene("Menu");
            var progress=UnityEngine.Object.FindAnyObjectByType<PlayerProgressionMenu>();progress.progressionButton.onClick.Invoke();await Task.Delay(150);
            Check("Progression nav opens training progress",progress.modal.gameObject.activeSelf&&progress.title.text.Contains("LUYỆN TẬP")&&progress.content.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text.Contains("Làm lại")));
            Check("Progress shows mistakes and completion stats",progress.content.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text.Contains("Đánh hụt"))&&progress.summaryText.text.Contains("drive")==false);
            progress.bonusButton.onClick.Invoke();await Scene("Training");d=UnityEngine.Object.FindAnyObjectByType<TrainingDirector>();d.SetPaused(false);
            Check("Continue from progression routes to the pending lesson",d.Phase==TrainingPhase.Briefing&&d.Lesson.id=="drive");d.hud.start.onClick.Invoke();Check("Continue keeps completed trial count",d.Session.id==checkpoint&&d.Session.trials==3);
            d.Catalogue();Check("Catalogue remains navigable",d.hud.catalogue.gameObject.activeSelf);
            var result=new{passed=true,count=checks.Count,checks};Directory.CreateDirectory("../UnityWork/Training");File.WriteAllText("../UnityWork/Training/flow-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally
        {
            var d=UnityEngine.Object.FindAnyObjectByType<TrainingDirector>();if(d)d.Catalogue();runtime.SwitchPlayer(original,null);UnityEditor.EditorApplication.isPaused=paused;
            string root=Path.Combine(Application.persistentDataPath,"LocalPlayers");foreach(var suffix in new[]{".json",".json.bak",".lock"}){string path=Path.Combine(root,qa+suffix);if(File.Exists(path))File.Delete(path);}
        }
    }
}
