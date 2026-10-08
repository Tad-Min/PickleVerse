using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PickleVerse.FrontEnd;
using PickleVerse.LocalMatch;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class ModeSelectionValidation
{
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool passed)
    {
        checks.Add(new{name,passed});
        if(!passed)throw new InvalidOperationException("Validation failed: "+name);
    }
    static async Task Wait(Func<bool> condition,int milliseconds=6000)
    {
        var until=DateTime.UtcNow.AddMilliseconds(milliseconds);
        while(!condition()) {if(DateTime.UtcNow>until)throw new TimeoutException("Scene/phase did not become ready.");await Task.Delay(40);}
    }
    static void Click(UnityEngine.UI.Button button)
    {
        if(!button)throw new InvalidOperationException("Missing button.");
        ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
    }
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Open Menu and enter Play first.");
        checks.Clear();
        bool hadDifficulty=PlayerPrefs.HasKey("PKV.Bot.Difficulty"),hadNight=PlayerPrefs.HasKey("PKV.Bot.Night");
        int oldDifficulty=PlayerPrefs.GetInt("PKV.Bot.Difficulty",1),oldNight=PlayerPrefs.GetInt("PKV.Bot.Night",0);
        Directory.CreateDirectory("../UnityWork/ModeSelection");
        try
        {
            Check("Starts at Home Menu",SceneManager.GetActiveScene().name=="Menu");
            var play=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include).Single(b=>b.name=="BtnPlay");Click(play);
            await Wait(()=>SceneManager.GetActiveScene().name=="ModeSelection" && UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>()!=null);await Task.Delay(250);
            var ui=UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>();
            Check("Home Play opens mode-selection scene",ui.modePage.activeSelf&&!ui.IsBotSetup);
            var inactive=ui.GetComponentsInChildren<UnityEngine.UI.Button>(true).Where(b=>b.name=="MultiplayerMode"||b.name=="TrainingMode").ToArray();
            Check("Multiplayer and Training stay placeholders",inactive.Length==2&&inactive.All(b=>!b.interactable));
            foreach(var button in inactive)Click(button);
            Check("Disabled modes cannot navigate",SceneManager.GetActiveScene().name=="ModeSelection"&&!ui.IsBotSetup);
            Click(ui.botButton);await Task.Delay(250);
            Check("Bot card opens setup page",ui.IsBotSetup&&ui.botPage.activeSelf&&!ui.modePage.activeSelf);
            for(int i=0;i<3;i++)
            {
                Click(ui.difficultyButtons[i]);
                Check("Difficulty button "+i,ui.SelectedDifficulty==i&&ui.difficultySelected[i].gameObject.activeSelf&&ui.difficultySelected.Count(t=>t.gameObject.activeSelf)==1);
            }
            Click(ui.environmentButtons[0]);Check("Day selection updates court preview",!ui.SelectedNight&&ui.courtPreview.texture==ui.dayPreview&&ui.environmentSelected[0].gameObject.activeSelf);
            Click(ui.environmentButtons[1]);Check("Night selection updates court preview",ui.SelectedNight&&ui.courtPreview.texture==ui.nightPreview&&ui.environmentSelected[1].gameObject.activeSelf);
            var back=ui.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name=="Back");Click(back);
            Check("Back returns to modes",!ui.IsBotSetup&&ui.modePage.activeSelf);
            Click(ui.botButton);Check("Setup retains pending choices",ui.SelectedNight&&ui.SelectedDifficulty==2);
            Click(ui.startButton);Click(ui.startButton);
            await Wait(()=>SceneManager.GetActiveScene().name=="BotMatch" && UnityEngine.Object.FindAnyObjectByType<BotMatch>()!=null);await Task.Delay(250);
            var match=UnityEngine.Object.FindAnyObjectByType<BotMatch>();var light=match.GetComponent<MatchEnvironmentLighting>();
            Check("Hard/Night passed to match",match.botDifficulty==2&&light.IsNight&&light.selectedNight);
            Check("Night uses four floodlights and no sun",light.floodlights.Length==4&&light.floodlights.All(l=>l.enabled)&&!light.sun.enabled);
            Check("Launch begins existing cinematic automatically",match.Phase==MatchPhase.Cinematic&&match.opening.IsPlaying);
            Check("BotMatch has no difficulty/environment pickers",match.hud.GetComponentsInChildren<Transform>(true).All(t=>!t.name.StartsWith("BotLevel")&&t.name!="EnvironmentChoice"));
            await Wait(()=>match.Phase==MatchPhase.PreServe,45000);
            Check("Cinematic returns to playable serve",match.ball.gameObject.activeSelf&&match.Rules!=null&&!match.opening.IsPlaying);
            match.TogglePause();await Task.Delay(100);Click(match.hud.modeButton);
            await Wait(()=>SceneManager.GetActiveScene().name=="ModeSelection" && UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>()!=null);await Task.Delay(250);
            ui=UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>();Click(ui.botButton);
            Check("Preferences survive returning from a match",ui.SelectedDifficulty==2&&ui.SelectedNight);
            Click(ui.difficultyButtons[0]);Click(ui.environmentButtons[0]);Click(ui.startButton);
            await Wait(()=>SceneManager.GetActiveScene().name=="BotMatch" && UnityEngine.Object.FindAnyObjectByType<BotMatch>()!=null);await Task.Delay(250);
            match=UnityEngine.Object.FindAnyObjectByType<BotMatch>();light=match.GetComponent<MatchEnvironmentLighting>();
            Check("Easy/Day passed to match",match.botDifficulty==0&&!light.IsNight&&light.sun.enabled&&light.floodlights.All(l=>!l.enabled));
            match.opening.Skip();await Task.Delay(100);
            match.RestartWithIntro();await Task.Delay(100);
            Check("Replay keeps selected settings and cinematic",match.botDifficulty==0&&!light.IsNight&&match.Phase==MatchPhase.Cinematic);
            match.opening.Skip();match.TogglePause();await Task.Delay(100);Click(match.hud.modeButton);
            await Wait(()=>SceneManager.GetActiveScene().name=="ModeSelection");await Task.Delay(250);
            ui=UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>();
            Check("Exactly one EventSystem",UnityEngine.Object.FindObjectsByType<EventSystem>().Length==1);
            Check("No Missing Script in mode-selection scene",SceneManager.GetActiveScene().GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Transform>(true).Sum(t=>UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)))==0);
            var result=new{passed=true,count=checks.Count,checks=checks.ToArray()};
            File.WriteAllText("../UnityWork/ModeSelection/validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
            return result;
        }
        finally
        {
            if(hadDifficulty)PlayerPrefs.SetInt("PKV.Bot.Difficulty",oldDifficulty);else PlayerPrefs.DeleteKey("PKV.Bot.Difficulty");
            if(hadNight)PlayerPrefs.SetInt("PKV.Bot.Night",oldNight);else PlayerPrefs.DeleteKey("PKV.Bot.Night");PlayerPrefs.Save();
        }
    }
}
