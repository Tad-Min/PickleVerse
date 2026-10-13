using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PickleVerse.LocalMatch;
using PickleVerse.Rules;
using Unity.Behavior;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace PickleVerse.Tests
{
    public sealed class BhTreeTests
    {
        GameObject root;BotMatch match;BhTreeBot tree;
        [SetUp]public void Setup()
        {
            var previous=SceneManager.GetActiveScene();var source=SceneManager.GetSceneByPath("Assets/Scenes/BotMatch.unity");bool opened=!source.isLoaded;
            if(opened)source=EditorSceneManager.OpenScene("Assets/Scenes/BotMatch.unity",OpenSceneMode.Additive);
            var original=source.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BotMatch>()).Single();
            SceneManager.SetActiveScene(previous);root=new GameObject("BhTree fixture");match=root.AddComponent<BotMatch>();
            match.human=UnityEngine.Object.Instantiate(original.human,root.transform);match.bot=UnityEngine.Object.Instantiate(original.bot,root.transform);
            match.human.Initialize();match.bot.Initialize();match.human.ResetAt(new Vector3(1,0,-5),-1);match.bot.ResetAt(new Vector3(-1,0,5),1);
            Set(match,"Rules",new SinglesRules());Set(match,"Phase",MatchPhase.PreServe);tree=root.AddComponent<BhTreeBot>();tree.Initialize(match,2000);
            if(opened)EditorSceneManager.CloseScene(source,true);
        }
        [TearDown]public void Cleanup(){UnityEngine.Object.DestroyImmediate(root);}
        static void Set(object obj,string field,object value)=>obj.GetType().GetProperty(field,BindingFlags.Public|BindingFlags.Instance).SetValue(obj,value);
        void Incoming(int stroke=4,int bounces=0,Vector3? position=null,Vector3? velocity=null)
        {
            Set(match,"Phase",MatchPhase.Rally);Set(match.Rules,"Live",true);Set(match.Rules,"LastHitter",0);Set(match.Rules,"Stroke",stroke);Set(match.Rules,"Bounces",bounces);
            Set(match,"BallPosition",position??new Vector3(-1,1.2f,3));Set(match,"BallVelocity",velocity??new Vector3(0,-2,1));
        }
        [TestCase(100,.28f,.30f,0)][TestCase(499,.28f,.30f,0)][TestCase(500,.15f,.16f,1)][TestCase(1999,.15f,.16f,1)][TestCase(2000,.08f,.08f,2)]
        public void EloBoundaries(int elo,float reaction,float error,int tier){tree.Initialize(match,elo);Assert.That(tree.ReactionSeconds,Is.EqualTo(reaction));Assert.That(tree.AimError,Is.EqualTo(error));Assert.That(tree.Tier,Is.EqualTo(tier));}
        [Test]public void RealGraphWaitsWhenPaused(){Set(match,"Paused",true);tree.Step(.01f,out var move,out _);Assert.That(tree.Branch,Is.EqualTo("Chờ"));Assert.That(move,Is.EqualTo(Vector2.zero));Assert.That(root.GetComponent<BehaviorGraphAgent>().enabled,Is.False);}
        [Test]public void RealGraphSelectsServeBeforeRecovery(){tree.Step(.01f,out _,out _);Assert.That(tree.Branch,Is.EqualTo("Giao / chờ giao"));}
        [Test]public void RealGraphRecoversWhenOtherPlayerHits(){Incoming();Set(match.Rules,"LastHitter",1);tree.Step(.1f,out _,out _);Assert.That(tree.Branch,Is.EqualTo("Hồi vị"));}
        [Test]public void BasicTierUsesDriveAfterMandatoryBounce(){tree.Initialize(match,499);Incoming(1,1);tree.Step(.3f,out _,out _);Assert.That(tree.Branch,Is.EqualTo("Drive"));}
        [Test]public void ThirdShotUsesDropAtMiddleTier(){tree.Initialize(match,500);Incoming(2,1);tree.Step(.2f,out _,out _);Assert.That(tree.Branch,Is.EqualTo("Drop"));}
        [Test]public void DinkNearNet(){tree.Initialize(match,500);Incoming(4,1,new Vector3(-1,1.2f,2.5f),new Vector3(0,-2,0));tree.Step(.2f,out _,out _);Assert.That(tree.Branch,Is.EqualTo("Dink"));}
        [Test]public void HighIncomingBallSelectsSmash(){Incoming(4,0,new Vector3(-1,2.15f,3),new Vector3(0,-1,0));tree.Step(.1f,out _,out _);Assert.That(tree.Branch,Is.EqualTo("Smash"));}
        [Test]public void FastIncomingBallSelectsReset(){tree.Initialize(match,500);Incoming(4,0,new Vector3(-1,1.2f,3),new Vector3(0,-2,9));tree.Step(.2f,out _,out _);Assert.That(tree.Branch,Is.EqualTo("Reset"));}
        [Test]public void VolleyOutsideKitchen(){tree.Initialize(match,500);match.bot.ResetAt(new Vector3(-1,0,3),1);Incoming(4,0);tree.Step(.2f,out _,out _);Assert.That(tree.Branch,Is.EqualTo("Volley"));}
        [Test]public void MandatoryTwoBounceAndKitchenRejectAirShots(){Incoming(2,0);Assert.That(tree.Shot(ShotKind.Volley),Is.False);Assert.That(tree.Shot(ShotKind.Smash),Is.False);Incoming();match.bot.ResetAt(new Vector3(-1,0,1),1);Assert.That(match.bot.InKitchen,Is.True);Assert.That(tree.Shot(ShotKind.Reset),Is.False);Assert.That(tree.Shot(ShotKind.Volley),Is.False);}
        [Test]public void EloRemainsFixedAndSimulationInputIsExposed(){tree.Step(.01f,out _,out _);tree.Step(.02f,out _,out _);var agent=root.GetComponent<BehaviorGraphAgent>();Assert.That(agent.GetVariableID("SimulationTime",out _),Is.True);Assert.That(tree.Elo,Is.EqualTo(2000));tree.ResetPoint();tree.Step(.01f,out var move,out _);Assert.That(tree.Branch,Is.EqualTo("Giao / chờ giao"));Assert.That(move,Is.EqualTo(Vector2.zero));}
    }
}
