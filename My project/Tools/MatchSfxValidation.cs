using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using PickleVerse.Audio;
using PickleVerse.LocalMatch;
using PickleVerse.Rules;
using PickleVerse.Settings;
using UnityEngine;

public static class MatchSfxValidation
{
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool passed){checks.Add(new{name,passed});if(!passed)throw new Exception(name);}
    static void Invoke(BotMatch match,string method,params object[] args)=>typeof(BotMatch).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(match,args);
    static void SetRules(BotMatch match,SinglesRules rules)=>typeof(BotMatch).GetProperty("Rules").SetValue(match,rules);
    public static async Task<object> Run()
    {
        checks.Clear();var match=UnityEngine.Object.FindAnyObjectByType<BotMatch>();
        if(!Application.isPlaying||!match)throw new InvalidOperationException("Play BotMatch first.");
        if(match.opening&&match.opening.IsPlaying)match.opening.Skip();match.Restart();match.SetPaused(true);
        var saved=GameSettings.Current.Copy();
        try
        {
            Check("One listener and bounded shared SFX sources",UnityEngine.Object.FindObjectsByType<AudioListener>().Count(l=>l.enabled)==1&&UnityEngine.Object.FindAnyObjectByType<SfxPlayer>().GetComponentsInChildren<AudioSource>().Length==16);
            var library=Resources.Load<SfxLibrary>("Audio/Match SFX");
            Check("All eight effects ready before first hit",SfxPlayer.Ready&&library.clips.Length==8&&library.clips.All(c=>c&&c.loadState==AudioDataLoadState.Loaded&&c.channels==1&&c.loadType==AudioClipLoadType.DecompressOnLoad));
            AudioSettings.GetDSPBufferSize(out var size,out var buffers);Check("DSP buffer is 256 samples",size==256);
            var audition=saved.Copy();audition.master=1;audition.effects=1;SettingsRuntime.Apply(audition);
            var peaks=new List<object>();var output=new float[512];
            foreach(GameSfx sound in Enum.GetValues(typeof(GameSfx)))
            {
                SfxPlayer.StopAll();SfxPlayer.Play(sound,match.human.transform.position);
                var source=UnityEngine.Object.FindObjectsByType<AudioSource>().First(s=>s.isPlaying&&s.clip==library.Clip(sound));
                source.GetOutputData(output,0);float peak=0;
                for(int poll=0;poll<4;poll++){await Task.Delay(35);source.GetOutputData(output,0);peak=Mathf.Max(peak,output.Max(x=>Mathf.Abs(x)));}
                peaks.Add(new{sound=sound.ToString(),peak});Check(sound+" produces nonzero audio samples",peak>.00001f);
            }
            SfxPlayer.StopAll();var quiet=audition.Copy();quiet.effects=0;SettingsRuntime.Apply(quiet);
            foreach(var intent in new[]{new ShotIntent{kind=ShotKind.Drive},new ShotIntent{kind=ShotKind.Dink,cross=true},new ShotIntent{kind=ShotKind.Volley},new ShotIntent{kind=ShotKind.Drive,power=true},new ShotIntent{kind=ShotKind.Serve}})
            {
                var actor=match.human;actor.ResetAt(actor.transform.position,actor.end);var expected=intent.kind==ShotKind.Serve?GameSfx.Serve:intent.power?GameSfx.HitStrong:GameSfx.HitNormal;
                int before=SfxPlayer.PlayCounts[(int)expected];actor.Swing(intent,actor.transform.position+Vector3.up);Check("Swing alone has no impact sound: "+intent,SfxPlayer.PlayCounts[(int)expected]==before);
                actor.ConsumeContact(false);actor.ConsumeContact(false);Check("One impact on contact: "+intent,SfxPlayer.PlayCounts[(int)expected]==before+1);
            }
            int steps=SfxPlayer.PlayCounts[(int)GameSfx.Footstep],skids=SfxPlayer.PlayCounts[(int)GameSfx.ShoeSkid];var foley=new MovementFoley();foley.Step(Vector3.zero,false,.02f);
            for(int i=0;i<20;i++)foley.Step(Vector3.zero,false,.02f);
            Check("Idle produces no footsteps",SfxPlayer.PlayCounts[(int)GameSfx.Footstep]==steps);
            for(int i=1;i<=40;i++)foley.Step(Vector3.forward*(i*.065f),false,.02f);
            Check("Walking produces footsteps",SfxPlayer.PlayCounts[(int)GameSfx.Footstep]>steps);
            for(int i=1;i<=40;i++)foley.Step(Vector3.forward*(2.6f+i*.115f),true,.02f);
            Check("Running produces shoe friction",SfxPlayer.PlayCounts[(int)GameSfx.ShoeSkid]>skids);
            steps=SfxPlayer.PlayCounts[(int)GameSfx.Footstep];skids=SfxPlayer.PlayCounts[(int)GameSfx.ShoeSkid];foley.Step(Vector3.one*30,false,.02f);foley.Step(Vector3.one*30+Vector3.forward*.1f,true,.02f,false);
            Check("Teleport or disabled movement produces no step",steps==SfxPlayer.PlayCounts[(int)GameSfx.Footstep]&&skids==SfxPlayer.PlayCounts[(int)GameSfx.ShoeSkid]);
            var cue=new ServeCountdownCue();int ticks=SfxPlayer.PlayCounts[(int)GameSfx.Countdown];for(int i=0;i<=1200;i++)cue.Step(i/120f);
            Check("10-second cue emits exactly ten ticks",SfxPlayer.PlayCounts[(int)GameSfx.Countdown]==ticks+10);
            var rules=new SinglesRules(0);rules.PrepareServe(-1);SetRules(match,rules);Invoke(match,"PreparePoint");rules.Fail(1,Fault.Out);
            int faults=SfxPlayer.PlayCounts[(int)GameSfx.FaultWhistle];Invoke(match,"BeginFault");Invoke(match,"BeginFault");Check("One referee whistle per fault",SfxPlayer.PlayCounts[(int)GameSfx.FaultWhistle]==faults+1);
            rules.Points[0]=10;int large=SfxPlayer.PlayCounts[(int)GameSfx.GameWhistle];Invoke(match,"FinishRally");Check("Game win plays large whistle",match.Phase==MatchPhase.BetweenGames&&SfxPlayer.PlayCounts[(int)GameSfx.GameWhistle]==large+1);
            match.Restart();match.SetPaused(true);rules=new SinglesRules(0);rules.PrepareServe(-1);rules.Games[0]=1;rules.Points[0]=10;rules.Fail(1,Fault.Out);SetRules(match,rules);large=SfxPlayer.PlayCounts[(int)GameSfx.GameWhistle];Invoke(match,"FinishRally");Check("Match win plays large whistle",match.Phase==MatchPhase.MatchOver&&SfxPlayer.PlayCounts[(int)GameSfx.GameWhistle]==large+1);
            SettingsRuntime.Apply(audition);SfxPlayer.StopAll();SfxPlayer.Play(GameSfx.HitNormal,match.human.transform.position);var active=UnityEngine.Object.FindObjectsByType<AudioSource>().First(s=>s.isPlaying);
            var reduced=audition.Copy();reduced.effects=.25f;SettingsRuntime.Apply(reduced);Check("Effects volume previews immediately",Mathf.Abs(active.volume-.225f)<.001f);
            reduced.master=.4f;SettingsRuntime.Apply(reduced);Check("Master volume applies separately",Mathf.Abs(AudioListener.volume-.4f)<.001f);
            match.SetPaused(false);match.SetPaused(true);Check("Pause stops playing match audio",UnityEngine.Object.FindObjectsByType<AudioSource>().All(s=>!s.isPlaying));
            var result=new{passed=true,count=checks.Count,dsp=new{size,buffers,rate=AudioSettings.outputSampleRate},peaks,checks};
            Directory.CreateDirectory("../UnityWork/SFX");File.WriteAllText("../UnityWork/SFX/validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally{SettingsRuntime.Apply(saved);SfxPlayer.StopAll();match.Restart();match.SetPaused(true);}
    }
}
