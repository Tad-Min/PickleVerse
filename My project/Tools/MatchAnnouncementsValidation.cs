using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PickleVerse.LocalMatch;
using PickleVerse.Rules;
using UnityEngine;

public static class MatchAnnouncementsValidation
{
    static readonly List<object> checks=new List<object>();
    static readonly BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    static void Check(string name,bool passed){checks.Add(new{name,passed});if(!passed)throw new InvalidOperationException(name);}
    static void Hud(BotMatch m){typeof(MatchHud).GetMethod("Update",Hidden).Invoke(m.hud,null);}
    static void Reset(BotMatch m,int server=0)
    {
        typeof(BotMatch).GetField("initialServer",Hidden).SetValue(m,server);
        m.Restart();m.autoPlayer=false;Hud(m);
    }
    static void Lose(BotMatch m,int loser,Fault fault=Fault.Out)
    {
        m.Rules.Fail(loser,fault);
        typeof(BotMatch).GetMethod("BeginFault",Hidden).Invoke(m,null);
        m.StepSimulation(1f/120,Vector2.zero,false);Hud(m);
    }

    public static async System.Threading.Tasks.Task<object> AutomaticTransition()
    {
        var m=UnityEngine.Object.FindAnyObjectByType<BotMatch>();
        Reset(m);m.Rules.Points[0]=10;m.Rules.Points[1]=3;int end=m.human.end;
        Lose(m,1);
        UnityEditor.EditorApplication.isPaused=false;
        var began=DateTime.UtcNow;var deadline=began.AddSeconds(30);
        while(m.Phase!=MatchPhase.PreServe)
        {
            if(DateTime.UtcNow>deadline)throw new TimeoutException("Automatic game transition did not finish.");
            await System.Threading.Tasks.Task.Delay(40);
        }
        bool passed=m.Rules.Games[0]==1&&m.Rules.GameNumber==2&&m.human.end==-end&&!m.hud.modal.activeSelf;
        UnityEditor.EditorApplication.isPaused=true;
        var result=new{passed,elapsed=(DateTime.UtcNow-began).TotalSeconds,game=m.Rules.GameNumber,gameWins=m.Rules.Games[0],end=m.human.end};
        if(!passed)throw new InvalidOperationException("Automatic next-game state incorrect.");
        File.WriteAllText("../UnityWork/MatchAnnouncements/automatic-transition.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
    }

    public static object Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Enter Play in BotMatch first.");
        UnityEditor.EditorApplication.isPaused=true;
        checks.Clear();var m=UnityEngine.Object.FindAnyObjectByType<BotMatch>();var n=m.hud.announcements;
        Check("Announcements attached",n&&n.panel&&n.opacity&&n.headline&&n.detail);
        foreach(int opponentPoints in new[]{0,3})
        {
            Reset(m);m.Rules.Points[0]=10;m.Rules.Points[1]=opponentPoints;int end=m.human.end;
            Lose(m,1);
            Check("11-"+opponentPoints+" is one game, not match",m.Rules.Games[0]==1&&m.Rules.Games[1]==0&&m.Rules.Winner<0&&m.Phase==MatchPhase.BetweenGames);
            Check("Between-game modal hidden "+opponentPoints,!m.hud.modal.activeSelf&&!m.hud.resultActions.activeSelf);
            Check("Final rally fault is announced "+opponentPoints,n.ActiveTitle=="OUT"&&!m.hud.status.transform.parent.gameObject.activeSelf);
            n.Advance(1.21f);
            Check("Game-end banner before changing ends "+opponentPoints,n.ActiveTitle=="GAME 1 KẾT THÚC"&&m.human.end==end&&m.Rules.Points[0]==11);
            m.Continue();
            Check("Enter cannot skip end-change sequence "+opponentPoints,m.Phase==MatchPhase.BetweenGames&&m.human.end==end);
            n.Advance(1.61f);Hud(m);
            Check("Change-ends banner stages next game "+opponentPoints,n.ActiveTitle=="ĐỔI BÊN"&&m.human.end==-end&&m.bot.end==end&&m.Rules.GameNumber==2&&m.Rules.Points[0]==0&&m.Rules.Points[1]==0);
            Check("Serve clock held during change ends "+opponentPoints,m.Phase==MatchPhase.BetweenGames&&m.ServeElapsed==0);
            var camera=m.hud.GetComponent<Canvas>().worldCamera.GetComponent<MatchCamera>();
            Check("Camera faces new player end "+opponentPoints,Vector3.Distance(camera.transform.position,camera.DesiredPosition)<.001f);
            n.Advance(1.41f);Hud(m);
            Check("Game 2 starts automatically "+opponentPoints,m.Phase==MatchPhase.PreServe&&!n.Busy&&m.Rules.Games[0]==1&&!m.hud.modal.activeSelf);
            Check("New game changes initial service "+opponentPoints,m.Rules.Server==1);
        }

        Reset(m,1);m.Rules.Points[1]=10;int humanEnd=m.human.end;Lose(m,0);n.Advance(4.3f);Hud(m);
        Check("Bot winning a game also changes ends",m.Rules.Games[1]==1&&m.Rules.Winner<0&&m.Phase==MatchPhase.PreServe&&m.human.end==-humanEnd);

        Reset(m);m.Rules.Points[0]=10;m.Rules.Points[1]=10;Lose(m,1);
        Check("11-10 does not finish game",!m.Rules.GameFinished&&m.Rules.Games[0]==0&&m.Phase==MatchPhase.BetweenPoints);
        n.Advance(1.3f);m.StepSimulation(2.2f,Vector2.zero,false);Lose(m,1);
        Check("12-10 schedules next game",m.Rules.GameFinished&&m.Rules.Games[0]==1&&m.Rules.Winner<0);
        n.Advance(4.3f);
        Check("12-10 resumes game 2",m.Phase==MatchPhase.PreServe&&m.Rules.GameNumber==2);

        Reset(m);m.Rules.Games[0]=1;m.Rules.Points[0]=10;humanEnd=m.human.end;Lose(m,1);n.Advance(1.21f);Hud(m);
        Check("Match victory uses central banner",m.Rules.Winner==0&&m.Phase==MatchPhase.MatchOver&&n.ActiveTitle=="GAME, MATCH!"&&!m.hud.modal.activeSelf);
        n.Advance(2.2f);if(m.victory){m.victory.Advance(0);m.victory.Advance(m.victory.cameraSeconds);}Hud(m);
        Check("Final victory shows the winner presentation and actions",m.hud.resultActions.activeSelf&&!m.hud.modal.activeSelf&&m.human.end==humanEnd&&m.Rules.Games[0]==2&&(!m.victory||m.victory.Ready));

        Reset(m,1);m.Rules.Games[0]=m.Rules.Games[1]=1;m.Rules.Points[1]=10;Lose(m,0);n.Advance(1.21f);
        Check("Bot match victory has correct winner text",m.Rules.Winner==1&&n.detail.text.Contains("BOT THẮNG TRẬN")&&n.detail.text.Contains("1 - 2"));

        Reset(m);m.Rules.Games[0]=m.Rules.Games[1]=1;m.Rules.Points[0]=5;m.Rules.Points[1]=4;m.Rules.Timeouts[0]=1;humanEnd=m.human.end;
        Lose(m,1);n.Advance(1.21f);n.Advance(1.41f);
        Check("Deciding game changes ends at six without resetting score",m.Phase==MatchPhase.PreServe&&m.human.end==-humanEnd&&m.Rules.GameNumber==3&&m.Rules.Points[0]==6&&m.Rules.Points[1]==4&&m.Rules.Timeouts[0]==1);

        Reset(m);Lose(m,0,Fault.KitchenVolley);n.Advance(.3f);
        Check("Kitchen fault uses centered reason",n.ActiveTitle=="FAULT / LỖI"&&n.detail.text.Contains("Volley trong Kitchen"));
        float elapsed=n.Elapsed;m.TogglePause();n.Advance(3);
        Check("Pause freezes notice and callbacks",Mathf.Abs(n.Elapsed-elapsed)<.00001f&&n.Busy);
        m.TogglePause();n.Advance(1.2f);

        Reset(m);m.Rules.Points[0]=10;Lose(m,1);m.Restart();n.Advance(10);
        Check("Restart cancels queued end changes",!n.Busy&&m.Rules.GameNumber==1&&m.Rules.Games[0]==0&&m.human.end==-1&&m.Phase==MatchPhase.PreServe);

        n.Enqueue("ĐỔI BÊN","CHANGE ENDS",AnnouncementTone.ChangeEnds,1);
        Check("Banner enters from left",n.panel.anchoredPosition.x<0);
        n.Advance(.25f);Check("Banner holds at center",Mathf.Abs(n.panel.anchoredPosition.x)<.001f&&Mathf.Abs(n.opacity.alpha-1)<.001f);
        n.Advance(.6f);Check("Banner exits to right",n.panel.anchoredPosition.x>0);
        n.Advance(.2f);Check("Banner hides after exit",!n.Busy&&!n.panel.gameObject.activeSelf);
        Check("Notifications do not block input",!n.opacity.blocksRaycasts&&n.panel.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).All(g=>!g.raycastTarget));
        Reset(m);m.hud.announcements.Enqueue("GAME 1 KẾT THÚC","Bạn thắng game • 11 - 3\nSố game thắng 1 - 0",AnnouncementTone.Success,1.6f);n.Advance(.4f);Hud(m);
        Check("Vietnamese banner text fits and has glyphs",n.headline.font.HasCharacters(n.headline.text)&&n.detail.font.HasCharacters(n.detail.text));
        var result=new{passed=true,count=checks.Count,checks=checks.ToArray()};Directory.CreateDirectory("../UnityWork/MatchAnnouncements");
        File.WriteAllText("../UnityWork/MatchAnnouncements/validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
    }
}
