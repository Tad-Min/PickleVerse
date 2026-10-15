using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.LocalMatch;

public static class ShotFeelGameValidation
{
    public static async Task<object> Bot()
    {
        if(!Application.isPlaying)throw new Exception("Play first");
        if(PickleVerse.Progression.ProgressionRuntime.Instance.Player.id!="ShotFeelQA20261015")throw new Exception("QA profile required");
        SceneManager.LoadScene("BotMatch");await Task.Delay(300);
        var m=UnityEngine.Object.FindAnyObjectByType<BotMatch>();m.enabled=false;
        var contacts=PickleVerse.LocalMatch.Editor.BotMatchDiagnostics.ContactChecks();
        m.Restart();m.enabled=false;m.autoPlayer=true;m.botDifficulty=2;
        for(int i=0;i<240*120&&m.Phase!=MatchPhase.MatchOver;i++)
        {
            m.hud.announcements.Advance(1f/120);
            if(m.Phase==MatchPhase.BetweenGames&&!m.hud.announcements.Busy)m.Continue();
            m.StepSimulation(1f/120,Vector2.zero,false);
        }
        var smoke=new{m.TotalHits,m.TotalRallies,m.LongestRally,snapshot=m.Snapshot(),log=m.RallyLog.ToArray()};
        m.enabled=false;
        var result=new{contacts=Newtonsoft.Json.JsonConvert.DeserializeObject(contacts),smoke};
        File.WriteAllText("../UnityWork/ShotFeel/bot-gameplay.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        SceneManager.LoadScene("Menu");return result;
    }
    public static async Task<object> Training()
    {
        SceneManager.LoadScene("Training");await Task.Delay(300);return "Training loaded; use TrainingDiagnostics.All";
    }
}
