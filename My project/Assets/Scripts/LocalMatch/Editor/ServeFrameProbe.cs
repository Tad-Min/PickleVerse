using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using PickleVerse.Rules;

namespace PickleVerse.LocalMatch.Editor
{
    // Runs across real PlayerLoop frames, including Unity's automatic animation pass.
    public static class ServeFrameProbe
    {
        [Serializable] public class Sample
        {
            public int frame;
            public float delta, elapsed, swing, velocityY, headAboveWrist, ballDistance;
            public string phase, fault;
        }
        [Serializable] public class Report
        {
            public int server;
            public float heldSeconds;
            public bool enteredRally;
            public string fault;
            public List<Sample> samples=new List<Sample>();
        }
        static BotMatch match;
        static Keyboard keyboard;
        static Report report;
        static string output;
        static float started;
        static bool released,pressed;
        static float pressedAt;
        static bool oldBackground;
        public static string Begin(int server, float hold, string name)
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Probe needs Play Mode");
            Stop();match=UnityEngine.Object.FindAnyObjectByType<BotMatch>();
            oldBackground=Application.runInBackground;Application.runInBackground=true;
            typeof(BotMatch).GetField("initialServer",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(match,server);
            typeof(BotMatch).GetField("focused",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(match,true);
            match.autoPlayer=false;match.enabled=true;match.Restart();
            report=new Report{server=server,heldSeconds=hold};released=pressed=false;
            output="../UnityWork/BotMatch/ServeCameraFix/"+name+".json";started=Time.unscaledTime;
            if(server==0){keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(keyboard,new KeyboardState());}
            EditorApplication.update+=Tick;return output;
        }
        static void Tick()
        {
            if(!match || report==null)return;
            EditorApplication.QueuePlayerLoopUpdate();
            typeof(BotMatch).GetField("focused",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(match,true);
            float time=Time.unscaledTime-started;
            if(keyboard!=null && !released)
            {
                // One neutral frame after Restart, then hold and release an actual input device.
                if(!pressed && time>.08f){InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.J));pressed=true;pressedAt=time;}
                else if(pressed && time-pressedAt>=report.heldSeconds){InputSystem.QueueStateEvent(keyboard,new KeyboardState());released=true;}
            }
            var actor=match.Actor(report.server);
            if(actor.Swinging || match.Phase!=MatchPhase.PreServe || match.ServeElapsed>9)
                report.samples.Add(new Sample{frame=Time.frameCount,delta=Time.deltaTime,elapsed=match.ServeElapsed,swing=actor.SwingTime,
                    velocityY=actor.PaddleVelocity.y,headAboveWrist=actor.PaddleHeadTop-actor.hand.position.y,
                    ballDistance=Vector3.Distance(match.BallPosition,actor.paddleFace.position),phase=match.Phase.ToString(),fault=match.Rules.LastFault.ToString()});
            if(match.Phase==MatchPhase.Rally || match.Rules.LastFault!=Fault.None || time>12)
            {
                report.enteredRally=match.Phase==MatchPhase.Rally;report.fault=match.Rules.LastFault.ToString();
                Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(report,true));
                match.enabled=false;Stop();
            }
        }
        static void Stop(){EditorApplication.update-=Tick;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);keyboard=null;Application.runInBackground=oldBackground;}
    }
}
