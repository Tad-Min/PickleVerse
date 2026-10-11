using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PickleVerse.LocalMatch.Editor
{
    public static class BotMatchDiagnostics
    {
        [Serializable] public class Audit
        {
            public string scene;
            public int cameras, lights, eventSystems, humanClips, botClips, humanBones, botBones;
            public bool jumpExcluded, referencesValid;
            public string[] humanAnimations, botAnimations;
        }
        public static string Inspect()
        {
            var match = UnityEngine.Object.FindAnyObjectByType<BotMatch>();
            var a = new Audit {
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                cameras = UnityEngine.Object.FindObjectsByType<Camera>().Length,
                lights = UnityEngine.Object.FindObjectsByType<Light>().Length,
                eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>().Length,
                humanClips = match.human.clips.Length, botClips = match.bot.clips.Length,
                humanBones = match.human.visual.GetComponentInChildren<SkinnedMeshRenderer>().bones.Length,
                botBones = match.bot.visual.GetComponentInChildren<SkinnedMeshRenderer>().bones.Length,
                humanAnimations = match.human.clips.Select(c=>c.name).ToArray(), botAnimations=match.bot.clips.Select(c=>c.name).ToArray(),
                jumpExcluded = !match.human.clips.Concat(match.bot.clips).Any(c=>c.name.Contains("Jump")),
                referencesValid = match.human.clips.Concat(match.bot.clips).All(c=>c.clip && c.clip.legacy) && match.ball && match.hud && match.human.paddleFace && match.bot.paddleFace
            };
            string json = JsonUtility.ToJson(a,true); Save("scene-audit.json",json); return json;
        }
        [Serializable] class SmokeResult { public int seconds, hits, rallies, longest; public string snapshot; public string[] log; }
        public static string Smoke(int seconds = 60)
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Smoke test needs Play Mode.");
            var match = UnityEngine.Object.FindAnyObjectByType<BotMatch>();
            match.enabled = false; match.autoPlayer = true; match.Restart();
            for (int i=0; i<seconds*120 && match.Phase!=MatchPhase.MatchOver;i++)
            {
                if (match.Phase==MatchPhase.BetweenGames) match.Continue();
                match.StepSimulation(1f/120f,Vector2.zero,false);
            }
            var result = new SmokeResult { seconds=seconds,hits=match.TotalHits,rallies=match.TotalRallies,longest=match.LongestRally,snapshot=match.Snapshot(),log=match.RallyLog.ToArray() };
            string json = JsonUtility.ToJson(result,true); Save("smoke-"+seconds+"s.json",json);
            match.autoPlayer = false; match.Restart(); match.enabled = true; return json;
        }
        public static string PoseAudit()
        {
            var match = UnityEngine.Object.FindAnyObjectByType<BotMatch>();
            var rows = new List<string>();
            foreach(var actor in new[]{match.human,match.bot})
            {
                foreach(var entry in actor.clips)
                {
                    float contact = entry.name.Contains("Serve") ? .5333f : entry.name.Contains("Volley") ? .3889f : entry.name.Contains("Dink") ? .4583f : entry.name.Contains("Drop") ? .4444f : entry.name.Contains("Smash") ? .5f : .4333f;
                    entry.clip.SampleAnimation(actor.visual.gameObject,entry.clip.length*contact);
                    rows.Add(actor.playerId + " " + entry.name + " face="+actor.paddleFace.position.ToString("F3")+" feet="+actor.leftFoot.position.ToString("F3")+"/"+actor.rightFoot.position.ToString("F3"));
                }
                actor.clips.Single(c=>c.name=="PKV_Idle_Ready").clip.SampleAnimation(actor.visual.gameObject,0);
            }
            var value=string.Join("\n",rows); Save("pose-audit.txt",value); return value;
        }
        [Serializable] class ContactResult {public int mask;public string shot,clip,fault;public int hits;public bool passed;public string face;}
        [Serializable] class ContactReport {public ContactResult[] results;public bool passed;}
        public static string ContactChecks()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Contact checks need Play Mode");
            var match=UnityEngine.Object.FindAnyObjectByType<BotMatch>();match.enabled=false;
            var tests=new[]{1,2,4,3,5,6,9,10,12,17,18,20};var rows=new List<ContactResult>();
            foreach(int mask in tests)
            {
                match.Restart();match.autoPlayer=false;
                var rules=new PickleVerse.Rules.SinglesRules(0);rules.PrepareServe(-1);
                Func<float,float,PickleVerse.Rules.CourtPoint> p=(x,z)=>new PickleVerse.Rules.CourtPoint(x,.8f,z);
                rules.Serve(0,p(1,-7.2f),p(1.4f,-7.2f),p(1.2f,-7f),1);
                rules.Bounce(p(-1,4));rules.Hit(1,p(-1,4),false);
                var parser=new ShotInput();parser.Tick(mask,false,false,out _);parser.Tick(mask&24,false,false,out var intent);
                bool volley=intent.BeforeBounce || intent.kind==ShotKind.Smash;
                rules.Bounce(p(1,-4));
                if(volley){rules.Hit(0,p(1,-4),false);rules.Hit(1,p(-1,4),false);}
                typeof(BotMatch).GetProperty("Rules").SetValue(match,rules);
                Set(match,"Phase",MatchPhase.Rally);
                match.human.ResetAt(new Vector3(0,0,-3.3f),-1);match.bot.ResetAt(new Vector3(0,0,7.5f),1);
                Vector3 destination=match.human.transform.TransformPoint(match.human.ContactOffset(intent));
                Vector3 velocity=new Vector3(0,0,-6);
                Vector3 origin=destination-velocity*intent.ContactDelay+Vector3.up*(.5f*BallFlight.Gravity*intent.ContactDelay*intent.ContactDelay);
                Set(match,"BallPosition",origin);Set(match,"BallVelocity",velocity);
                match.TickInput(0);match.TickInput(mask);match.TickInput(mask&24);
                string clip=match.human.CurrentClip;
                for(int i=0;i<Mathf.CeilToInt((intent.ContactDelay+.15f)*120);i++)match.StepSimulation(1f/120f,Vector2.zero,false);
                rows.Add(new ContactResult{mask=mask,shot=intent.ToString(),clip=clip,fault=rules.LastFault.ToString(),hits=match.TotalHits,passed=match.TotalHits==1&&rules.LastFault==PickleVerse.Rules.Fault.None,face=match.human.paddleFace.position.ToString("F3")});
            }
            var report=new ContactReport{results=rows.ToArray(),passed=rows.All(r=>r.passed)};string json=JsonUtility.ToJson(report,true);Save("contact-checks.json",json);
            match.Restart();match.enabled=true;return json;
        }
        static void Set(BotMatch match,string property,object value) {typeof(BotMatch).GetField("<"+property+">k__BackingField",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(match,value);}
        public static string InputDevices()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Input checks need Play Mode");
            var match=UnityEngine.Object.FindAnyObjectByType<BotMatch>();match.enabled=false;match.autoPlayer=false;
            var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            var update=typeof(BotMatch).GetMethod("Update",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            var focus=typeof(BotMatch).GetField("focused",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);object oldFocus=focus.GetValue(match);focus.SetValue(match,true);
            Action<UnityEngine.InputSystem.Key[]> keys=k=>{UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(k));UnityEngine.InputSystem.InputSystem.Update();update.Invoke(match,null);};
            float walk=0,run=0;bool held=false,combo=false,cancel=false;
            int movementFrames=Mathf.CeilToInt(.75f/Mathf.Max(.0001f,Time.deltaTime));
            try {
                match.Restart();var start=match.human.transform.position;for(int i=0;i<movementFrames;i++)keys(new[]{UnityEngine.InputSystem.Key.W});walk=Vector3.Distance(start,match.human.transform.position);
                keys(Array.Empty<UnityEngine.InputSystem.Key>());match.Restart();start=match.human.transform.position;for(int i=0;i<movementFrames;i++)keys(new[]{UnityEngine.InputSystem.Key.W,UnityEngine.InputSystem.Key.LeftShift});run=Vector3.Distance(start,match.human.transform.position);
                keys(Array.Empty<UnityEngine.InputSystem.Key>());match.Restart();Set(match,"Phase",MatchPhase.Rally);Set(match,"BallPosition",new Vector3(0,8,-3));Set(match,"BallVelocity",Vector3.zero);
                keys(Array.Empty<UnityEngine.InputSystem.Key>());keys(new[]{UnityEngine.InputSystem.Key.J});held=!match.human.Swinging && match.Selection.Contains("Drive");
                keys(new[]{UnityEngine.InputSystem.Key.J,UnityEngine.InputSystem.Key.K});keys(new[]{UnityEngine.InputSystem.Key.K});combo=match.human.CurrentClip.Contains("ThirdShotDrop");
                keys(Array.Empty<UnityEngine.InputSystem.Key>());match.Restart();Set(match,"Phase",MatchPhase.Rally);Set(match,"BallPosition",new Vector3(0,8,-3));Set(match,"BallVelocity",Vector3.zero);
                keys(Array.Empty<UnityEngine.InputSystem.Key>());keys(new[]{UnityEngine.InputSystem.Key.J});keys(new[]{UnityEngine.InputSystem.Key.J,UnityEngine.InputSystem.Key.Backspace});keys(Array.Empty<UnityEngine.InputSystem.Key>());cancel=!match.human.Swinging;
            } finally {UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);focus.SetValue(match,oldFocus);}
            match.hud.touchControls.SetActive(true);Canvas.ForceUpdateCanvases();
            var controls=match.hud.touchControls.GetComponentsInChildren<TouchControl>();var joystick=controls.Single(c=>c.joystick);var rect=(RectTransform)joystick.transform;var camera=match.hud.GetComponent<Canvas>().worldCamera;
            var pointer=new PointerEventData(EventSystem.current){pointerId=41,pointerPressRaycast=new RaycastResult{module=match.hud.GetComponent<UnityEngine.UI.GraphicRaycaster>()}};
            pointer.position=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));joystick.OnPointerDown(pointer);bool center=match.TouchMovement.magnitude<.01f;
            pointer.position=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center+Vector2.up*75));joystick.OnDrag(pointer);bool sprint=match.TouchRun&&match.TouchMovement.y>.9f;joystick.OnPointerUp(pointer);
            var drop=controls.Single(c=>c.shotMask==3);drop.OnPointerDown(pointer);bool touchCombo=match.TouchShotMask==3;drop.OnPointerUp(pointer);touchCombo&=match.TouchShotMask==0;
            var power=controls.Single(c=>c.shotMask==8);var drive=controls.Single(c=>c.shotMask==1);power.OnPointerDown(pointer);drive.OnPointerDown(pointer);bool touchPower=match.TouchShotMask==9;drive.OnPointerUp(pointer);touchPower&=match.TouchShotMask==8;power.OnPointerDown(pointer);touchPower&=match.TouchShotMask==0;
            string json=Newtonsoft.Json.JsonConvert.SerializeObject(new{walkDistance=walk,runDistance=run,shiftFaster=run>walk*1.2f,keyboardHoldSelect=held,keyboardJKDrop=combo,backspaceCancel=cancel,joystickCenter=center,joystickOuterSprint=sprint,touchDrop=touchCombo,touchPower=touchPower});Save("input-devices.json",json);
            match.hud.touchControls.SetActive(false);match.Restart();match.enabled=true;return json;
        }
        static void Save(string name,string text) { Directory.CreateDirectory("../UnityWork/BotMatch/QA");File.WriteAllText("../UnityWork/BotMatch/QA/"+name,text); }
    }
}
