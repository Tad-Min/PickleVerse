using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using PickleVerse.Multiplayer;
public static class MultiplayerInputProbe
{
    public static async Task<object> Serve()
    {
        var app=MultiplayerApp.Instance; var match=NetworkMatch.Instance;
        if(app.State!=FlowState.InMatch||!app.Network.IsHost) throw new Exception("Run this probe on the in-court Editor host.");
        var owner=NetworkPlayer.All.Single(p=>p.IsOwner);
        double deadline=Time.realtimeSinceStartupAsDouble+25;
        while((!match.Rules.waitingServe||owner.Team.Value!=match.Rules.servingTeam||owner.Index.Value!=match.Rules.serverIndex)&&Time.realtimeSinceStartupAsDouble<deadline) await Task.Delay(50);
        if(owner.Team.Value!=match.Rules.servingTeam||owner.Index.Value!=match.Rules.serverIndex) throw new Exception("Owner must be the current server.");
        var keyboard=InputSystem.AddDevice<Keyboard>("PickleVerseTestKeyboard");
        var background=InputSystem.settings.backgroundBehavior;
        try
        {
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus; keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.J)); await Task.Delay(150);
            if(owner.Animator.GetCurrentAnimatorStateInfo(0).IsName("Serve_FH_Underhand")) throw new Exception("Shot committed before key release");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); await Task.Delay(200);
            bool animated=owner.Animator.GetCurrentAnimatorStateInfo(0).IsName("Serve_FH_Underhand")||owner.Animator.GetNextAnimatorStateInfo(0).IsName("Serve_FH_Underhand");
            if(!animated) throw new Exception("J release did not trigger Serve_FH_Underhand");
            await Task.Delay(1150);
            if(match.Rules.strokeCount<1||match.Rules.waitingServe) throw new Exception("Animation did not contact ball for a legal serve: "+match.Rules.reason);
            var report=new{passed=true,owner.Gender,key="J press / release",animated,match.Rules.strokeCount,match.Rules.reason};
            File.WriteAllText("Docs/MultiplayerInputPlayTest.json",Newtonsoft.Json.JsonConvert.SerializeObject(report)); return report;
        }
        finally { InputSystem.RemoveDevice(keyboard); InputSystem.settings.backgroundBehavior=background; }
    }
}
