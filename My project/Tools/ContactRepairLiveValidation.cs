using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using PickleVerse.LocalMatch;
using PickleVerse.Settings;
using PickleVerse.Progression;
using Newtonsoft.Json;

[DefaultExecutionOrder(-10000)]
public sealed class ContactRepairDriver : MonoBehaviour
{
    public BotMatch bot;
    public TrainingDirector training;
    public TutorialDirector tutorial;
    public bool keyboardInput;
    public int mask;
    public Keyboard keyboard;
    bool issued;
    int trial=-1,heldFrame;
    public void ResetTrial(){issued=false;trial=-1;mask=0;heldFrame=-1;}
    void Update()
    {
        if(training)
        {
            if(training.Session!=null&&training.Session.trials!=trial){trial=training.Session.trials;issued=false;heldFrame=-1;}
            mask=0;
            if(training.Phase==TrainingPhase.Practice&&!training.PartnerTurn&&!issued)
            {
                bool serve=training.Lesson.skill==TrainingSkill.Serve;
                if(serve||training.Receiving&&training.ReleaseIn<=.1f)
                {
                    if(heldFrame<0)heldFrame=Time.frameCount;
                    mask=training.ExpectedMask;
                    if(Time.frameCount>heldFrame+1&&(serve||training.ReleaseIn<=.012f))
                    {mask=training.ExpectedMask&24;issued=true;}
                }
            }
            var goal=training.Phase==TrainingPhase.Recovery?training.RecoveryPosition:training.ReceivePosition;
            Vector3 delta=goal-training.player.transform.position;
            training.TouchMovement=Vector2.ClampMagnitude(new Vector2(delta.x,delta.z)*3,1);training.TouchRun=true;
            training.TouchShotMask=mask;
        }
        if(bot)bot.TouchShotMask=keyboardInput?0:mask;
        if(tutorial)tutorial.TouchShotMask=mask;
        if(keyboardInput&&keyboard!=null)
        {
            var actions=new[]{GameAction.Drive,GameAction.Dink,GameAction.Volley,GameAction.Power,GameAction.Cross};
            var keys=new List<Key>();for(int i=0;i<5;i++)if((mask&(1<<i))!=0)keys.Add(GameSettings.Current.KeyFor(actions[i]));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys.ToArray()));InputSystem.Update();keyboard.MakeCurrent();
        }
    }
    void OnDisable(){if(bot)bot.TouchShotMask=0;if(training){training.TouchShotMask=0;training.TouchMovement=Vector2.zero;}if(tutorial)tutorial.TouchShotMask=0;}
}

public static class ContactRepairLiveValidation
{
    const string Folder="../UnityWork/ContactRepair20261019";
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static Task running;
    static bool keyboardOnly;
    static void Save(string name,object value){Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/"+name+".json",JsonConvert.SerializeObject(value,Formatting.Indented));}
    static async Task Load(string scene){var op=SceneManager.LoadSceneAsync(scene);while(!op.isDone)await Task.Delay(20);await Task.Delay(200);}
    static async Task Frames(int count){int end=Time.frameCount+count;while(Time.frameCount<end)await Task.Delay(4);}
    static async Task<bool> Wait(Func<bool> ready,float seconds){var end=DateTime.UtcNow.AddSeconds(seconds);while(!ready()&&DateTime.UtcNow<end)await Task.Delay(8);return ready();}
    public static object Start(){running=Run();return new{started=true};}
    public static object KeyboardStart(){keyboardOnly=true;running=Run();return new{started=true};}
    public static object SmashStart(){running=Smash();return new{started=true};}
    static async Task Smash()
    {
        var runtime=ProgressionRuntime.Instance;string original=runtime.Player.id,qa="SmashContactQA"+Guid.NewGuid().ToString("N").Substring(0,8);ContactRepairDriver driver=null;
        try
        {
            runtime.SwitchPlayer(qa,"Smash contact QA");await Load("Training");var d=UnityEngine.Object.FindAnyObjectByType<TrainingDirector>();
            runtime.UpdateWallet(p=>{foreach(var lesson in TrainingCurriculum.Lessons)TrainingEngine.Record(TrainingEngine.Ensure(p),lesson.id).bestStars=1;});
            driver=new GameObject("Smash live input QA").AddComponent<ContactRepairDriver>();driver.training=d;d.Select("smash");d.StartLesson(true);
            bool done=await Wait(()=>d.Session.contacts>0||d.Session.trials>0,12);
            Save("training-smash",new{passed=done&&d.Session.contacts>0,contacts=d.Session.contacts,errors=d.Session.errors.Select(e=>e.error.ToString()).ToArray()});
            d.Catalogue();
        }
        catch(Exception e){Save("training-smash",new{passed=false,error=e.ToString()});Debug.LogException(e);}
        finally
        {
            if(driver){driver.enabled=false;UnityEngine.Object.Destroy(driver.gameObject);}await Load("Menu");runtime.SwitchPlayer(original,null);
            string root=Path.Combine(Application.persistentDataPath,"LocalPlayers");foreach(string suffix in new[]{".json",".json.bak",".lock"}){string path=Path.Combine(root,qa+suffix);if(File.Exists(path))File.Delete(path);}
        }
    }
    static void Report(object value)=>Save(keyboardOnly?"keyboard":"live",value);
    sealed class Memory:ITutorialProgressStore{readonly Dictionary<string,int> values=new Dictionary<string,int>();public int Read(string k)=>values.TryGetValue(k,out var v)?v:0;public void Write(string k,int v){values[k]=v;}}
    static async Task Run()
    {
        Report(new{status="running",stage="BotMatch"});
        var runtime=ProgressionRuntime.Instance;string original=runtime.Player.id,qa="LiveContactQA"+Guid.NewGuid().ToString("N").Substring(0,10);
        int priorFps=Application.targetFrameRate;var random=UnityEngine.Random.state;Keyboard keyboard=null;ContactRepairDriver driver=null;
        var results=new List<object>();
        try
        {
            runtime.SwitchPlayer(qa,"Live contact QA");await Load("BotMatch");
            var m=UnityEngine.Object.FindAnyObjectByType<BotMatch>();m.opening?.Skip();m.enabled=false;
            driver=new GameObject("Contact input QA").AddComponent<ContactRepairDriver>();driver.bot=m;
            keyboard=InputSystem.AddDevice<Keyboard>();driver.keyboard=keyboard;
            var initial=typeof(BotMatch).GetField("initialServer",Hidden);var prepare=typeof(BotMatch).GetMethod("PreparePoint",Hidden);
            foreach(int fps in new[]{30,60,144})
            foreach(var venue in new[]{PickleVerse.CourtVenue.Professional,PickleVerse.CourtVenue.LowPoly})
            foreach(int server in keyboardOnly?new[]{0}:new[]{0,1})
            foreach(int modifier in new[]{0,8,16})
            {
                Application.targetFrameRate=fps;m.enabled=false;driver.mask=0;await Frames(2);
                initial.SetValue(m,server);m.Restart();m.botDifficulty=2;m.autoPlayer=false;
                int end=(modifier==8?1:-1);m.human.end=end;m.bot.end=-end;prepare.Invoke(m,null);
                m.GetComponent<CourtVenueSelection>().Select(venue);
                typeof(BotMatch).GetField("focused",Hidden).SetValue(m,true);m.enabled=true;driver.keyboardInput=server==0;
                // Restart cancels/locks a held shot. Deliver a neutral frame before
                // pressing, just as a player does after the entrance/countdown.
                await Frames(2);
                bool began;
                if(server==0)
                {driver.mask=1|modifier;await Frames(3);driver.mask=modifier;await Frames(2);driver.mask=0;began=m.human.Swinging;}
                else began=m.RequestShot(1,new ShotIntent(ShotKind.Serve,modifier==8,modifier==16));
                bool served=await Wait(()=>m.Phase==MatchPhase.Rally||m.Rules.LastFault!=PickleVerse.Rules.Fault.None,2);
                var actor=m.Actor(server);bool passed=began&&served&&m.Rules.Stroke>=1&&m.Rules.LastFault==PickleVerse.Rules.Fault.None&&actor.LastContactGrade!=HitGrade.None;
                results.Add(new{test="Live serve",fps,venue=venue.ToString(),server,gender=server==0?"Female":"Male",end,modifier,began,passed,stroke=m.Rules.Stroke,fault=m.Rules.LastFault.ToString(),phase=m.Phase.ToString(),grade=actor.LastContactGrade.ToString()});
                Report(new{status="running",stage="BotMatch serves",results});
            }
            if(keyboardOnly){Report(new{passed=results.All(r=>(bool)r.GetType().GetProperty("passed").GetValue(r)),tests=results.Count,results});return;}
            driver.keyboardInput=false;driver.mask=0;m.enabled=false;m.Restart();m.botDifficulty=2;m.autoPlayer=true;
            Application.targetFrameRate=60;typeof(BotMatch).GetField("focused",Hidden).SetValue(m,true);m.enabled=true;
            await Task.Delay(22000);
            results.Add(new{test="Native frame rally smoke",passed=m.TotalHits>=4,hits=m.TotalHits,rallies=m.TotalRallies,longest=m.LongestRally,phase=m.Phase.ToString()});
            driver.enabled=false;UnityEngine.Object.Destroy(driver.gameObject);driver=null;InputSystem.RemoveDevice(keyboard);keyboard=null;
            Report(new{status="running",stage="Training",results});
            Application.targetFrameRate=60;await Load("Training");var training=UnityEngine.Object.FindAnyObjectByType<TrainingDirector>();
            runtime.UpdateWallet(p=>{foreach(var lesson in TrainingCurriculum.Lessons)TrainingEngine.Record(TrainingEngine.Ensure(p),lesson.id).bestStars=1;});
            driver=new GameObject("Training input QA").AddComponent<ContactRepairDriver>();driver.training=training;
            foreach(var lesson in TrainingCurriculum.Lessons)
            {
                driver.ResetTrial();training.Catalogue();training.Select(lesson.id);training.StartLesson(true);
                bool hit=await Wait(()=>training.Session.contacts>0||training.Session.trials>0,12);
                results.Add(new{test="Live training input",lesson=lesson.id,passed=hit&&training.Session.contacts>0,contacts=training.Session.contacts,errors=training.Session.errors.Select(e=>e.error.ToString()).ToArray(),grade=training.player.LastContactGrade.ToString()});
                Report(new{status="running",stage="Training "+lesson.id,results});
            }
            driver.enabled=false;training.Catalogue();UnityEngine.Object.Destroy(driver.gameObject);driver=null;
            TutorialProgress.Configure(new Memory());await Load("Tutorial");var tutorial=UnityEngine.Object.FindAnyObjectByType<TutorialDirector>();
            driver=new GameObject("Tutorial input QA").AddComponent<ContactRepairDriver>();driver.tutorial=tutorial;
            var begin=typeof(TutorialDirector).GetMethod("Begin",Hidden);
            for(int index=TutorialCurriculum.FirstShot;index<TutorialCurriculum.FirstShot+9;index++)
            {
                driver.mask=0;begin.Invoke(tutorial,new object[]{index,true});await Frames(3);
                int mask=tutorial.Lesson.mask;driver.mask=mask;await Frames(3);driver.mask=mask&24;await Frames(2);driver.mask=0;
                bool complete=await Wait(()=>tutorial.LessonIndex>index,9);
                results.Add(new{test="Live tutorial input",mask,passed=complete,grade=tutorial.player.LastContactGrade.ToString()});
                Report(new{status="running",stage="Tutorial "+mask,results});
            }
            Report(new{passed=results.All(r=>(bool)r.GetType().GetProperty("passed").GetValue(r)),tests=results.Count,results});
        }
        catch(Exception e){Report(new{passed=false,error=e.ToString(),results});Debug.LogException(e);}
        finally
        {
            if(driver){driver.enabled=false;UnityEngine.Object.Destroy(driver.gameObject);}if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            TutorialProgress.Configure(null);Application.targetFrameRate=priorFps;await Load("Menu");runtime.SwitchPlayer(original,null);UnityEngine.Random.state=random;
            string root=Path.Combine(Application.persistentDataPath,"LocalPlayers");foreach(string suffix in new[]{".json",".json.bak",".lock"}){string path=Path.Combine(root,qa+suffix);if(File.Exists(path))File.Delete(path);}
        }
    }
}
