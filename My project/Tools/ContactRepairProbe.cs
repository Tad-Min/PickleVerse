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
using PickleVerse.Progression;
using Newtonsoft.Json;

public static class ContactRepairProbe
{
    const string Folder="../UnityWork/ContactRepair20261019";
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static Task running;
    static void Save(string name,object value){Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/"+name+".json",JsonConvert.SerializeObject(value,Formatting.Indented));}
    public static object Start(){running=Run();return "Started";}
    static async Task Run()
    {
        var runtime=ProgressionRuntime.Instance;string original=runtime.Player.id,qa="ContactProbe"+Guid.NewGuid().ToString("N").Substring(0,10);
        var rows=new List<object>();Keyboard keyboard=null;
        try
        {
            runtime.SwitchPlayer(qa,"Contact probe");SceneManager.LoadScene("BotMatch");await Task.Delay(500);
            var m=UnityEngine.Object.FindAnyObjectByType<BotMatch>();typeof(BotMatch).GetField("initialServer",Hidden).SetValue(m,0);
            m.RestartWithIntro();await Task.Delay(13000);m.opening.Skip();
            typeof(BotMatch).GetField("focused",Hidden).SetValue(m,true);
            keyboard=InputSystem.AddDevice<Keyboard>();
            m.TickInput(0);m.TickInput(17);await Task.Delay(180);m.TickInput(16);m.TickInput(0);
            var end=DateTime.UtcNow.AddSeconds(2);
            while(DateTime.UtcNow<end)
            {
                var a=m.human;float swept=PaddleContact.Distance((Vector3)typeof(BotMatch).GetProperty("BallPosition").GetValue(m),m.BallPosition,a.PreviousPaddlePosition,a.paddleFace.position);
                rows.Add(new{phase=m.Phase.ToString(),m.ServeElapsed,ball=m.BallPosition.ToString("F4"),velocity=m.BallVelocity.ToString("F4"),m.ShotFeedback,fault=m.Rules.LastFault.ToString(),a.SwingTime,a.ContactDelay,a.ContactActive,a.Swinging,a.CurrentClip,
                    root=a.transform.position.ToString("F4"),localBall=a.transform.InverseTransformPoint(m.BallPosition).ToString("F4"),localFace=a.transform.InverseTransformPoint(a.paddleFace.position).ToString("F4"),distance=Vector3.Distance(m.BallPosition,a.paddleFace.position),swept,
                    hand=a.transform.InverseTransformPoint(a.HeldBallPosition).ToString("F4"),visual=a.visual.localPosition.ToString("F4"),
                    grade=a.LastContactGrade.ToString()});
                await Task.Delay(8);
            }
            Save("reproduction",new{phase=m.Phase.ToString(),m.Rules.Stroke,fault=m.Rules.LastFault.ToString(),rows});
        }
        catch(Exception e){Save("reproduction",new{error=e.ToString(),rows});Debug.LogException(e);}
        finally
        {
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);SceneManager.LoadScene("Menu");await Task.Delay(300);runtime.SwitchPlayer(original,null);
            string root=Path.Combine(Application.persistentDataPath,"LocalPlayers");foreach(string suffix in new[]{".json",".json.bak",".lock"}){string f=Path.Combine(root,qa+suffix);if(File.Exists(f))File.Delete(f);}
        }
    }
}
