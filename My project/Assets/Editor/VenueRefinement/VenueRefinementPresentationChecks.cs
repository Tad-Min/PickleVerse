using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using PickleVerse;
using PickleVerse.LocalMatch;
using PickleVerse.Rules;
using PickleVerse.Settings;
using Newtonsoft.Json;

// Local presentation fixture. It never signs in or sends authenticated game commands.
// The sign-in UI is removed only from the temporary Play session to inspect the scene.
public static class VenueRefinementPresentationChecks
{
    static readonly string Folder=Path.GetFullPath("../BlenderWork/VenueRefinement20261025");
    static readonly BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    static readonly List<object> checks=new List<object>(),performance=new List<object>();
    static readonly List<string> errors=new List<string>();
    static readonly List<float> frames=new List<float>();
    static Task task;static bool record;static int lastFrame;
    static BotMatch manual;static Action visual;
    static void Save(string status,object extra=null)=>File.WriteAllText(Folder+"/presentation-validation.json",JsonConvert.SerializeObject(new{status,checks,performance,errors,extra},Formatting.Indented));
    static void Check(string name,bool passed){checks.Add(new{name,passed});if(!passed)throw new Exception(name);}
    static void Log(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Assert||type==LogType.Exception)errors.Add(text);}
    static void Loaded(Scene s,LoadSceneMode mode)
    {
        if(s.name=="BotMatch")foreach(var root in s.GetRootGameObjects())foreach(var m in root.GetComponentsInChildren<BotMatch>(true)){m.enabled=false;if(m.opening)m.opening.enabled=false;}
    }
    static void Tick()
    {
        if(!Application.isPlaying||Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;
        if(manual){manual.StepSimulation(Mathf.Min(Time.deltaTime,.04f),Vector2.zero,false);visual?.Invoke();}
        if(record)frames.Add(Time.unscaledDeltaTime*1000);
    }
    static async Task Sample(string scene,string variant,int ms=6000)
    {
        await Task.Delay(1500);frames.Clear();record=true;await Task.Delay(ms);record=false;
        var values=frames.OrderBy(v=>v).ToArray();Check(scene+" "+variant+" produces rendered frames",values.Length>=60);
        performance.Add(new{scene,variant,frames=values.Length,meanFps=1000/values.Average(),p95Ms=values[(int)((values.Length-1)*.95)],maxMs=values.Last(),width=Screen.width,height=Screen.height});Save("running");
    }
    static async Task Picture(string name)
    {
        await Task.Delay(100);var image=ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Folder+"/Live-"+name+".png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);
    }
    static void Win(SinglesRules rules,int winner,int end)
    {
        for(int i=0;i<60&&rules.Winner<0;i++)
        {
            if(rules.GameFinished)rules.NextGame();rules.PrepareServe(end);rules.Fail(1-winner,Fault.Out);rules.CommitRally();
        }
        if(rules.Winner!=winner)throw new Exception("Local presentation score fixture failed");
    }
    public static object Start()
    {
        if(!Application.isPlaying)throw new Exception("Enter isolated Play mode");
        if(task!=null&&!task.IsCompleted)throw new Exception("Already running");
        checks.Clear();performance.Clear();errors.Clear();task=Run();return new{started=true,report=Folder+"/presentation-validation.json"};
    }
    static async Task Run()
    {
        Save("running");string saved=JsonUtility.ToJson(GameSettings.Current);
        Application.logMessageReceived+=Log;EditorApplication.update+=Tick;SceneManager.sceneLoaded+=Loaded;
        try
        {
            // Remove the UI-only gate from this in-memory test fixture. The
            // AccountService stays signed out, so there can be no reward/result writes.
            foreach(var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(component.GetType().FullName=="PickleVerse.FrontEnd.AccountScreen")UnityEngine.Object.Destroy(component.gameObject);
            await Task.Delay(100);Time.timeScale=1;
            var data=GameSettings.Current;data.fps=240;data.vsync=false;data.renderScale=1;data.msaa=2;SettingsRuntime.Apply(data);
            var view=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));view.Show();view.Focus();
            SceneManager.LoadScene("Menu");await Task.Delay(1600);Check("Menu loads actual lobby scene",SceneManager.GetActiveScene().name=="Menu");
            Check("Menu retains open sky",Camera.main&&Camera.main.clearFlags==CameraClearFlags.Skybox);await Sample("Menu","actual lobby");await Picture("Menu");
            foreach(CourtVenue venue in new[]{CourtVenue.Professional,CourtVenue.LowPoly})
            {
                SceneManager.LoadScene("BotMatch");await Task.Delay(600);var m=UnityEngine.Object.FindAnyObjectByType<BotMatch>();Check(venue+" actual BotMatch created",m&&m.Rules!=null);
                var choice=m.GetComponent<CourtVenueSelection>();Check(venue+" runtime venue switch",choice.Select(venue));await Task.Delay(150);
                var intro=m.opening;Check(venue+" actual entrance cinematic starts",intro.Play());
                foreach(float time in new[]{1.1f,2.8f,4.4f,5.4f,7.2f,8.5f})
                {
                    intro.Seek(time);if(!intro.Stage.Contains("entrance"))continue;var actor=intro.Stage=="Opponent entrance"?m.bot:m.human;
                    var p=Camera.main.WorldToViewportPoint(actor.transform.position+Vector3.up*.9f);
                    Check(venue+" entrance actor framed @"+time,p.z>0&&p.x>.02f&&p.x<.98f&&p.y>.02f&&p.y<.98f);
                    Check(venue+" entrance actor grounded @"+time,Mathf.Abs(actor.transform.position.y)<.001f);
                }
                await Picture(venue+"-Entrance");intro.Skip();m.autoPlayer=true;
                manual=m;visual=(Action)Delegate.CreateDelegate(typeof(Action),m,typeof(BotMatch).GetMethod("UpdateVisuals",Hidden));
                var lighting=m.GetComponent<MatchEnvironmentLighting>();
                foreach(bool night in new[]{false,true})
                {
                    lighting.SelectForMatch(night);Check(venue+" white court lights "+night,lighting.stadium.poleIllumination.All(l=>l.enabled&&l.color==Color.white&&l.intensity>0));
                    await Sample("BotMatch",venue+" "+(night?"night":"day"));await Picture(venue+(night?"-Night":"-Day"));
                }
                Check(venue+" local simulation strikes ball",m.TotalHits>0);manual=null;visual=null;
                await Task.Delay(3500);
                foreach(int winner in new[]{0,1})
                {
                    var rules=new SinglesRules(winner);Win(rules,winner,m.human.end);typeof(BotMatch).GetProperty("Rules").SetValue(m,rules);
                    typeof(BotMatch).GetProperty("Phase").SetValue(m,MatchPhase.MatchOver);
                    Check(venue+" outcome starts "+winner,m.victory.Begin());m.victory.Advance(4);await Task.Delay(250);
                    var p=Camera.main.WorldToViewportPoint(m.human.transform.position+Vector3.up*.9f);
                    Check(venue+" local outcome actor framed "+winner,m.victory.Ready&&p.z>0&&p.x>.05f&&p.x<.95f&&p.y>.05f&&p.y<.95f);
                    Check(venue+" Vietnamese outcome "+winner,m.victory.winnerTitle.text==(winner==0?"Thắng":"Thua"));await Picture(venue+(winner==0?"-Win":"-Loss"));m.victory.Cancel();
                }
            }
            Check("No runtime errors in local presentation fixture",errors.Count==0);
            Save("passed",new{scope="Actual Menu and BotMatch scenes, runtime venue switching, entry cameras, local rules/ball simulation and local outcome cameras. Signed-out Editor fixture; no authenticated matches, backend writes or Photon connection. Desktop hardware only."});
        }
        catch(Exception error){errors.Add(error.ToString());Save("failed");}
        finally
        {
            manual=null;visual=null;record=false;EditorApplication.update-=Tick;Application.logMessageReceived-=Log;SceneManager.sceneLoaded-=Loaded;
            JsonUtility.FromJsonOverwrite(saved,GameSettings.Current);SettingsRuntime.Apply(GameSettings.Current);
        }
    }
}
