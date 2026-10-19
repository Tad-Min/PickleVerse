using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.Settings;
using PickleVerse.LocalMatch;
using PickleVerse.FrontEnd;
using PickleVerse.Shop;
using PickleVerse.Progression;
using Newtonsoft.Json;

public static class LoadingFlowValidation
{
    const string Folder="../UnityWork/Loading20261019";static Task running;static readonly List<object> checks=new List<object>();
    static void Save(object result)=>File.WriteAllText(Folder+"/local-flow.json",JsonConvert.SerializeObject(result,Formatting.Indented));
    static void Check(string name,bool passed){checks.Add(new{name,passed});if(!passed)throw new Exception(name);}
    static async Task Wait(Func<bool> predicate,int ms=30000){double until=Time.realtimeSinceStartupAsDouble+ms/1000.0;while(!predicate()&&Time.realtimeSinceStartupAsDouble<until)await Task.Delay(8);if(!predicate())throw new Exception("Wait timed out");}
    static async Task Load(string scene){var op=SceneManager.LoadSceneAsync(scene);await Wait(()=>op.isDone);await Task.Delay(250);}
    public static object Start(){if(!Application.isPlaying)throw new Exception("Play required");checks.Clear();running=Run();return "Loading flow checks started";}
    static async Task Watch(string destination)
    {
        bool visible=false,zero=false,full=false;float previous=0;int samples=0,blockedFrames=0;var steps=new List<object>();
        await Wait(()=>SceneLoading.IsBlocking);
        while(SceneLoading.IsBlocking)
        {
            var view=SceneLoading.View;if(view)
            {
                visible=true;float value=view.DisplayedProgress;zero|=value<.02f;full|=value>=.9999f;
                if(value<previous-.0001f)throw new Exception("Loading progress reversed");previous=value;samples++;
                if(!SceneLoading.LocalRenderReady&&SceneLoading.Progress>=1)throw new Exception("100% before render readiness");
                var intro=UnityEngine.Object.FindAnyObjectByType<BotMatchIntro>();if(intro&&intro.IsPlaying&&intro.Elapsed>.001f)throw new Exception("Intro ran behind loading screen");
                blockedFrames++;if(samples%12==0)steps.Add(new{value,ready=SceneLoading.LocalRenderReady,scene=SceneManager.GetActiveScene().name});
            }
            await Task.Delay(8);if(samples>4000)throw new Exception("Loading did not complete");
        }
        Check(destination+" shows real loading screen",visible&&zero&&full&&samples>5);
        Check(destination+" activates correct scene",SceneManager.GetActiveScene().name==destination);
        await Task.Delay(300);Check(destination+" removes loading canvas",!SceneLoading.View&&UnityEngine.Object.FindObjectsByType<LoadingScreenView>().Length==0);
        File.WriteAllText(Folder+"/flow-"+destination+".json",JsonConvert.SerializeObject(new{samples,blockedFrames,steps},Formatting.Indented));
    }
    static async Task ModesFromLobby(){await Load("Menu");LobbyNavigation.OpenModes();await Wait(()=>SceneManager.GetActiveScene().name=="ModeSelection"&&!LobbyNavigation.IsBusy);await Task.Delay(100);}
    static async Task Run()
    {
        Directory.CreateDirectory(Folder);Save(new{status="running"});var runtime=ProgressionRuntime.Instance;string original=runtime.Player.id,qa="LoadingQA"+Guid.NewGuid().ToString("N").Substring(0,8);
        if(!File.Exists(Folder+"/previous-profile.txt"))File.WriteAllText(Folder+"/previous-profile.txt",original);
        try
        {
            runtime.SwitchPlayer(qa,"Loading QA");var game=UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));game.Show();game.Focus();
            await ModesFromLobby();var mode=UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>();mode.OpenBot();mode.StartBot();await Watch("BotMatch");
            var opening=UnityEngine.Object.FindAnyObjectByType<BotMatchIntro>();Check("Bot cinematic resumes after reveal",opening&&opening.IsPlaying&&opening.Elapsed>0&&opening.Elapsed<2);opening.Skip();
            await ModesFromLobby();mode=UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>();mode.OpenTraining();await Watch("Training");
            Check("Training preserves suspended lobby",SceneManager.GetSceneByName("Menu").isLoaded);var training=UnityEngine.Object.FindAnyObjectByType<TrainingDirector>();Check("Training opens catalogue",training.Phase==TrainingPhase.Catalogue);
            training.LeaveToMenu();await Wait(()=>SceneManager.GetActiveScene().name=="Menu"&&!LobbyNavigation.IsBusy);Check("Return reuses one lobby",SceneManager.sceneCount==1);
            await ModesFromLobby();var tutorial=UnityEngine.Object.FindAnyObjectByType<TutorialLauncher>();tutorial.Replay();await Watch("Tutorial");Check("Tutorial starts at first lesson",UnityEngine.Object.FindAnyObjectByType<TutorialDirector>().LessonIndex==0);
            await Load("ModeSelection");SceneLoading.BeginExternal("MultiplayerCourt");await Wait(()=>SceneLoading.View);SceneLoading.ExternalProgress(.54f,"Đang tải sân và nhân vật");await Task.Delay(1300);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder+"/loading-desktop.png"));await Task.Delay(160);
            Check("External loading cannot reach 100 before handshake",SceneLoading.Progress<1);SceneLoading.CancelExternal();await Task.Delay(100);Check("Canceled connection clears loading overlay",!SceneLoading.IsBlocking&&!SceneLoading.View);
            Save(new{passed=true,count=checks.Count,checks});
        }
        catch(Exception e){Save(new{passed=false,error=e.ToString(),checks});Debug.LogException(e);}
        finally
        {
            SceneLoading.CancelExternal();await Load("Menu");runtime.SwitchPlayer(original,null);
            string root=Path.Combine(Application.persistentDataPath,"LocalPlayers");foreach(string suffix in new[]{".json",".json.bak",".lock"}){string path=Path.Combine(root,qa+suffix);if(File.Exists(path))File.Delete(path);}
        }
    }
}
