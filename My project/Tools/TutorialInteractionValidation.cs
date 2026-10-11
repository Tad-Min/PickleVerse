using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using PickleVerse.LocalMatch;
using PickleVerse.Multiplayer;

public static class TutorialInteractionValidation
{
    sealed class Memory:ITutorialProgressStore{readonly Dictionary<string,int> data=new Dictionary<string,int>();public int Read(string key)=>data.TryGetValue(key,out var value)?value:0;public void Write(string key,int value){data[key]=value;}}
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool value){checks.Add(new{name,passed=value});if(!value)throw new Exception(name);}
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first.");checks.Clear();TutorialProgress.Configure(new Memory());
        try
        {
            SceneManager.LoadScene("Menu");await Task.Delay(200);UnityEngine.Object.FindAnyObjectByType<MenuPlayMultiplayer>().Play();await Task.Delay(200);
            Check("Play goes directly to mode selection even for new players",SceneManager.GetActiveScene().name=="ModeSelection");
            UnityEngine.Object.FindAnyObjectByType<TutorialLauncher>().GetComponent<UnityEngine.UI.Button>().onClick.Invoke();await Task.Delay(200);
            var t=UnityEngine.Object.FindAnyObjectByType<TutorialDirector>();t.enabled=false;
            Check("Tutorial opens only after explicit mode choice",SceneManager.GetActiveScene().name=="Tutorial"&&t);
            Check("Court explanation has text and no shot cue",t.hud.body.text.Contains("13,41")&&!t.hud.keyPanel.activeSelf&&!t.depthLabel.activeSelf);
            Check("Only one active camera/listener/input",UnityEngine.Object.FindObjectsByType<Camera>().Count(c=>c.enabled)==1&&UnityEngine.Object.FindObjectsByType<AudioListener>().Count(l=>l.enabled)==1&&UnityEngine.Object.FindObjectsByType<EventSystem>().Count(e=>e.enabled)==1);
            t.SetPaused(true);float time=t.Elapsed;t.Advance(1);Check("Settings pause stops tutorial timeline",t.Elapsed==time);t.SetPaused(false);
            for(int stage=0;stage<7;stage++){for(int i=0;i<1800&&!t.CanContinue;i++)t.Advance(1f/120);t.Next();}
            for(int i=0;i<50;i++)t.Advance(1f/120,0,Vector2.left,true);t.Next();
            Check("Basic drive cue uses current binding",t.Lesson.mask==1&&t.hud.keys.text==PickleVerse.Settings.GameControls.ShotKeys(1));
            t.Advance(.02f,2);t.Advance(.02f,0);Check("Wrong shot does not pass lesson",!t.HasBufferedShot&&t.ShotsCompleted==0);
            for(int i=0;i<600;i++)t.Advance(1f/120);Check("No input cannot auto-complete",t.LessonIndex==8&&t.ShotsCompleted==0);
            t.Replay();t.hud.touchToggle.onClick.Invoke();Check("Touch cue explains screen buttons",t.hud.touchRoot.activeSelf&&t.hud.keys.text=="DRIVE");
            t.Advance(.02f,0);var key=t.touchControls.First(c=>c.shotMask==1);var pointer=new PointerEventData(EventSystem.current){pointerId=10};key.OnPointerDown(pointer);t.Advance(.02f,t.TouchShotMask);key.OnPointerUp(pointer);t.Advance(.02f,t.TouchShotMask);
            Check("Touch press/release produces same shot",t.HasBufferedShot);
            for(int i=0;i<1000&&t.LessonIndex==8;i++)t.Advance(1f/120);Check("Touch shot requires actual contact",t.LessonIndex==9&&t.ShotsCompleted==1);
            t.hud.skip.onClick.Invoke();Check("Skip asks explicitly and pauses",t.hud.skipDialog.activeSelf&&t.Paused);t.hud.stay.onClick.Invoke();Check("Stay resumes tutorial",!t.hud.skipDialog.activeSelf&&!t.Paused);
            t.hud.skip.onClick.Invoke();t.hud.confirmSkip.onClick.Invoke();await Task.Delay(200);Check("Explicit defer opens mode selection without completion",SceneManager.GetActiveScene().name=="ModeSelection"&&!TutorialProgress.NeedsIntroduction&&!TutorialProgress.Completed);
            UnityEngine.Object.FindAnyObjectByType<TutorialLauncher>().GetComponent<UnityEngine.UI.Button>().onClick.Invoke();await Task.Delay(200);Check("Mode selector can replay tutorial",SceneManager.GetActiveScene().name=="Tutorial");
            SceneManager.LoadScene("Menu");await Task.Delay(150);UnityEngine.Object.FindAnyObjectByType<MenuPlayMultiplayer>().Play();await Task.Delay(200);Check("Returning player goes to mode selection",SceneManager.GetActiveScene().name=="ModeSelection");
            var result=new{passed=true,count=checks.Count,checks};File.WriteAllText("../UnityWork/Tutorial/interaction-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally{TutorialProgress.Configure(null);SceneManager.LoadScene("Tutorial");}
    }
}
