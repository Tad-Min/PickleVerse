using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using PickleVerse.Multiplayer;
public static class PlayMultiplayerNetworkProbe
{
    public static string Result="idle";
    public static void Start(){ Result="running"; Run(); }
    static async void Run()
    {
        Keyboard keyboard=null; var oldBackground=InputSystem.settings.backgroundBehavior;
        try
        {
            var app=MultiplayerApp.Instance;
            double deadline=Time.realtimeSinceStartupAsDouble+90;
            EditorApplication.update+=Pump;
            while(app.State!=FlowState.InMatch && Time.realtimeSinceStartupAsDouble<deadline) await Task.Delay(100);
            if(app.State!=FlowState.InMatch)throw new Exception("Did not enter court: "+app.Notice);
            var match=NetworkMatch.Instance;
            if(!match || !match.IsServer) throw new Exception("Requires Editor host in court");
            var owner=NetworkPlayer.All.Single(p=>p.IsOwner);
            while((!match.Rules.waitingServe || owner.Team!=match.Rules.servingTeam) && Time.realtimeSinceStartupAsDouble<deadline)await Task.Delay(50);
            if(!match.Rules.waitingServe || owner.Team!=match.Rules.servingTeam)throw new Exception("Owner was not current server");
            keyboard=InputSystem.AddDevice<Keyboard>("PlayMultiplayerProbe"); keyboard.MakeCurrent();
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); await Task.Delay(80);
            var before=owner.transform.position;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D,Key.LeftShift)); await Task.Delay(250);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); await Task.Delay(120);
            float dx=owner.transform.position.x-before.x;
            if(dx * -owner.Actor.end<.1f) throw new Exception("D movement did not follow own court facing: "+dx);
            if(NetworkPlayer.All.Where(p=>!p.IsOwner).Any(p=>p.Velocity.magnitude>.1f))throw new Exception("Keyboard moved remote player");
            owner.Teleport(before); owner.ResetInput(); await Task.Delay(80);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.J)); await Task.Delay(150);
            if(owner.Actor.Swinging)throw new Exception("Swing committed before key release");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); await Task.Delay(100);
            bool animated=owner.Actor.CurrentClip=="PKV_Serve_FH_Underhand";
            if(!animated)throw new Exception("J release did not use BotMatch serve clip: "+owner.Actor.CurrentClip);
            bool contact=false; deadline=Time.realtimeSinceStartupAsDouble+2;
            while(Time.realtimeSinceStartupAsDouble<deadline)
            {
                if(match.Rules.live && match.Rules.strokeCount>=1){contact=true;break;}
                await Task.Delay(20);
            }
            if(!contact)throw new Exception("BotMatch animation did not contact ball legally: "+match.Rules.reason);
            var report=new {passed=true,owner.Gender,dx,animated,contact,strokeCount=match.Rules.strokeCount,clip=owner.Actor.CurrentClip,
                players=NetworkPlayer.All.Count,male=NetworkPlayer.All.Count(p=>p.Gender=="Male"),female=NetworkPlayer.All.Count(p=>p.Gender=="Female")};
            Result=Newtonsoft.Json.JsonConvert.SerializeObject(report);
            File.WriteAllText("Docs/PlayMultiplayerInputTest.json",Result);
        }
        catch(Exception e){Result="FAIL "+e;Debug.LogException(e);}
        finally { EditorApplication.update-=Pump; if(keyboard!=null)InputSystem.RemoveDevice(keyboard);InputSystem.settings.backgroundBehavior=oldBackground; }
    }
    static void Pump(){EditorApplication.QueuePlayerLoopUpdate();}
}
