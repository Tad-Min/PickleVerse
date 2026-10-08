using System;
using System.IO;
using System.Threading.Tasks;
using PickleVerse.FrontEnd;
using PickleVerse.LocalMatch;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HomeLobbyMatchTransition
{
    static async Task Wait(Func<bool> condition)
    {
        var until=DateTime.UtcNow.AddSeconds(8);
        while(!condition()){if(DateTime.UtcNow>until)throw new TimeoutException("Scene load timed out.");await Task.Delay(40);}
    }
    public static async Task<object> Run()
    {
        var home=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();
        if(!Application.isPlaying||home==null)throw new InvalidOperationException("Play Home first.");
        bool existedDifficulty=PlayerPrefs.HasKey("PKV.Bot.Difficulty"),existedNight=PlayerPrefs.HasKey("PKV.Bot.Night");
        int difficulty=PlayerPrefs.GetInt("PKV.Bot.Difficulty",1),night=PlayerPrefs.GetInt("PKV.Bot.Night",0);
        try
        {
            home.animateAtmosphere=false;home.EvaluateCycle(home.cycleSeconds*.55f);
            home.playButton.onClick.Invoke();
            await Wait(()=>SceneManager.GetActiveScene().name=="ModeSelection");await Task.Delay(200);
            var menu=UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>();menu.OpenBot();menu.SetDifficulty(1);menu.SelectDay();menu.StartBot();
            await Wait(()=>SceneManager.GetActiveScene().name=="BotMatch"&&UnityEngine.Object.FindAnyObjectByType<BotMatch>()!=null);await Task.Delay(200);
            var match=UnityEngine.Object.FindAnyObjectByType<BotMatch>();var lighting=match.GetComponent<MatchEnvironmentLighting>();
            bool correct=!lighting.IsNight&&lighting.sun.enabled&&RenderSettings.skybox==lighting.daySky&&match.botDifficulty==1;
            if(!correct)throw new InvalidOperationException("Home atmosphere leaked into the selected match environment.");
            var result=new{passed=true,homeWasNight=true,botMatchIsDay=true,difficulty=match.botDifficulty};
            Directory.CreateDirectory("../UnityWork/HomeLobby");File.WriteAllText("../UnityWork/HomeLobby/match-transition.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
            SceneManager.LoadScene("Menu");await Wait(()=>SceneManager.GetActiveScene().name=="Menu");return result;
        }
        finally
        {
            if(existedDifficulty)PlayerPrefs.SetInt("PKV.Bot.Difficulty",difficulty);else PlayerPrefs.DeleteKey("PKV.Bot.Difficulty");
            if(existedNight)PlayerPrefs.SetInt("PKV.Bot.Night",night);else PlayerPrefs.DeleteKey("PKV.Bot.Night");PlayerPrefs.Save();
        }
    }
}
