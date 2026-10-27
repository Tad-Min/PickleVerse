using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using PickleVerse.LocalMatch;
using PickleVerse.Progression;
using UnityEngine;

public sealed class AdaptiveBotGraphTests
{
    sealed class Context : IAdaptiveBotContext, IDisposable
    {
        public readonly GameObject root = new GameObject("HybridBotTest") { hideFlags=HideFlags.HideAndDontSave };
        public MatchActor Actor {get;}
        public readonly List<string> requests=new List<string>();
        public BhTreeBot brain;
        readonly BotMatch predictionMatch;
        public Vector3 BallPosition => predictionMatch ? new Vector3(0,2.5f,-4) : new Vector3(0,1,4);
        public Vector3 BallVelocity => predictionMatch ? new Vector3(0,4,10) : Vector3.back*7;
        public Vector3 OpponentPosition => new Vector3(1,0,-4);
        public bool Active => true;
        public bool Incoming => !Serving;
        public bool Serving {get;set;}
        public bool IsServer => true;
        public int Stroke {get;set;}=3;
        public int Bounces => Stroke<=2?0:1;
        public int Server => 1;
        public int ExpectedHitter => 1;
        public string Phase => Serving?"PreServe":"Rally";
        public float ServeElapsed => 2;
        public Context(int seed, bool productionPrediction=false)
        {
            Actor=root.AddComponent<MatchActor>();Actor.playerId=1;Actor.end=1;
            Actor.clips=Array.Empty<ClipEntry>();Actor.transform.position=new Vector3(0,0,4);
            if(productionPrediction)
            {
                predictionMatch=root.AddComponent<BotMatch>();
                var rules=new PickleVerse.Rules.SinglesRules();
                typeof(PickleVerse.Rules.SinglesRules).GetProperty("Bounces").SetValue(rules,1);
                typeof(BotMatch).GetProperty("Rules").SetValue(predictionMatch,rules);
                typeof(BotMatch).GetProperty("BallPosition").SetValue(predictionMatch,BallPosition);
                typeof(BotMatch).GetProperty("BallVelocity").SetValue(predictionMatch,BallVelocity);
            }
            brain=root.AddComponent<BhTreeBot>();brain.Initialize(this,2000,new BotPolicy(),seed);
        }
        public bool PredictContact(bool bounce,out Vector3 point,out float delay,float height,bool beforeBounce)
        {
            if(predictionMatch)return predictionMatch.PredictContact(Actor.end,bounce,out point,out delay,height,beforeBounce);
            point=new Vector3(0,height,Actor.transform.position.z);delay=.05f;return true;
        }
        public bool RequestShot(ShotIntent shot)
        {requests.Add(shot.kind+":"+shot.power+":"+shot.cross);return true;}
        public void Dispose()=>UnityEngine.Object.DestroyImmediate(root);
    }
    [Test] public void SeededGraphIsEquivalentAcrossContextsAndCommandsAreLegal()
    {
        using(var a=new Context(731)) using(var b=new Context(731))
        {
            for(int i=0;i<240;i++)
            {
                a.Stroke=b.Stroke=3+i/60;
                a.brain.Step(1f/120,out var ma,out var ra);
                b.brain.Step(1f/120,out var mb,out var rb);
                Assert.That(ma,Is.EqualTo(mb));Assert.That(ra,Is.EqualTo(rb));
            }
            Assert.That(a.requests,Is.Not.Empty);Assert.That(a.requests,Is.EqualTo(b.requests));
            Assert.That(a.requests.Any(s=>s.EndsWith(":True:True")),Is.False,"power and cross modifiers are exclusive");
        }
    }
    [Test] public void MandatoryBounceAndKitchenExcludeVolleyBranches()
    {
        using(var context=new Context(731))
        {
            context.Stroke=1;
            for(int i=0;i<120;i++) context.brain.Step(1f/120,out _,out _);
            Assert.That(context.requests.All(s=>s.StartsWith("Drive:")),Is.True);
            context.requests.Clear();context.Stroke=4;context.Actor.transform.position=new Vector3(0,0,1);
            for(int i=0;i<120;i++) context.brain.Step(1f/120,out _,out _);
            Assert.That(context.requests.Any(s=>s.StartsWith("Volley:")||s.StartsWith("Smash:")||s.StartsWith("Reset:")),Is.False);
        }
    }
    [Test] public void DesktopGraphProfileIsMeasured()
    {
        using(var context=new Context(731,true))
        {
            var samples=new double[1000];
            for(int i=0;i<20;i++)context.brain.Step(1f/120,out _,out _);
            for(int i=0;i<samples.Length;i++)
            {
                long began=Stopwatch.GetTimestamp();context.brain.Step(1f/120,out _,out _);
                samples[i]=(Stopwatch.GetTimestamp()-began)*1000d/Stopwatch.Frequency;
            }
            Array.Sort(samples);
            string path=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../../artifacts/verification/hybrid-bot-desktop.json"));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path,"{\"samples\":1000,\"p99Ms\":"+samples[989].ToString("R",System.Globalization.CultureInfo.InvariantCulture)+",\"productionPrediction\":true,\"platform\":\"Windows Editor\"}");
            Assert.That(samples[989],Is.LessThan(1),"Desktop budget only; Android still requires device profiling");
        }
    }
}
