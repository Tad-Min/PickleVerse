using System.Reflection;
using NUnit.Framework;
using PickleVerse.LocalMatch;
using PickleVerse.Multiplayer;
using UnityEngine;
using LocalShotKind = PickleVerse.LocalMatch.ShotKind;

public sealed class NetworkShotLockTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    GameObject root;
    NetworkPlayer player;
    MatchActor actor;
    AnimationClip clip;
    ShotInput Input => (ShotInput)typeof(NetworkPlayer).GetField("shotInput", Private).GetValue(player);
    ShotRequestHistory Predictions => (ShotRequestHistory)typeof(NetworkPlayer).GetField("predictedShots", Private).GetValue(player);
    void Tick(int mask, bool serve) => typeof(NetworkPlayer).GetMethod("TickShot", Private).Invoke(player, new object[] { mask, false, serve });

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Network shot recovery");
        player = root.AddComponent<NetworkPlayer>();
        actor = new GameObject("Actor").AddComponent<MatchActor>();
        actor.transform.SetParent(root.transform, false);
        actor.visual = new GameObject("Rig").transform;
        actor.visual.SetParent(actor.transform, false);
        actor.paddleFace = new GameObject("PaddleFace").transform;
        actor.paddleFace.SetParent(actor.visual, false);
        clip = new AnimationClip { legacy = true };
        clip.SetCurve("PaddleFace", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1.5f, 1));
        actor.clips = new[]
        {
            new ClipEntry { name = "PKV_Idle_Ready", clip = clip },
            new ClipEntry { name = "PKV_Serve_FH_Underhand", clip = clip, contactPhase = .55f },
            new ClipEntry { name = "PKV_Groundstroke_FH", clip = clip, contactPhase = .55f },
            new ClipEntry { name = "PKV_Groundstroke_BH", clip = clip, contactPhase = .55f }
        };
        actor.Initialize();
        typeof(NetworkPlayer).GetProperty("Actor").SetValue(player, actor);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(clip);
    }

    [TestCase(1, false)] [TestCase(2, false)] [TestCase(4, false)]
    [TestCase(3, false)] [TestCase(5, false)] [TestCase(6, false)]
    [TestCase(9, false)] [TestCase(17, false)]
    [TestCase(1, true)] [TestCase(9, true)] [TestCase(17, true)]
    public void RepeatedInputCannotRestartAnimationOrRearmContact(int mask, bool serve)
    {
        var shot = new PickleVerse.LocalMatch.ShotIntent(serve ? LocalShotKind.Serve : LocalShotKind.Drive);
        Assert.That(actor.Swing(shot, Vector3.forward), Is.True);
        actor.RenderNetworkPose(Vector3.zero, false, actor.ContactDelay);
        Assert.That(actor.ContactActive, Is.True);
        actor.MissContact();
        float time = actor.SwingTime;
        string currentClip = actor.CurrentClip;
        for (int i = 0; i < 30; i++)
        {
            // No runner/match exists here: any extra prediction or RPC would throw.
            Assert.DoesNotThrow(() => Tick(mask, serve));
            Assert.DoesNotThrow(() => Tick(mask & 24, serve));
            Assert.DoesNotThrow(() => Tick(0, serve));
        }
        Assert.That(actor.Swinging, Is.True);
        Assert.That(actor.SwingTime, Is.EqualTo(time));
        Assert.That(actor.CurrentClip, Is.EqualTo(currentClip));
        Assert.That(actor.Intent.kind, Is.EqualTo(shot.kind));
        Assert.That(actor.ContactActive, Is.False, "Discarded input cannot reopen this strike's hit window.");
        Assert.That(Predictions.Current, Is.Null);
        Assert.That(Input.SelectionMask, Is.Zero);
    }

    [TestCase(false)] [TestCase(true)]
    public void PressDuringSwingIsDiscardedEvenWhenReleasedAfterRecovery(bool serve)
    {
        actor.Swing(new PickleVerse.LocalMatch.ShotIntent(serve ? LocalShotKind.Serve : LocalShotKind.Drive), Vector3.forward);
        Tick(1, serve);
        actor.RenderNetworkPose(Vector3.zero, false, actor.SwingDuration + .01f);
        Assert.That(actor.Swinging, Is.False);
        Assert.DoesNotThrow(() => Tick(1, serve));
        Assert.DoesNotThrow(() => Tick(0, serve));
        Assert.That(Predictions.Current, Is.Null);
        Assert.That(Input.Tick(1, false, serve, out _), Is.False);
        Assert.That(Input.Tick(0, false, serve, out _), Is.True, "A fresh press after recovery is accepted on release.");
    }
}
