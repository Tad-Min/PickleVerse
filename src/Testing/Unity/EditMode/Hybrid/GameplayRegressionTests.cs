using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using PickleVerse.LocalMatch;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using UnityEngine;

public sealed class GameplayRegressionTests
{
    [Test] public void DedicatedClientNormalizesReceiverButServerPreservesSender()
    {
        var client = PlayerRef.FromEncoded(4);
        Assert.That(PhotonSession.DedicatedMessageSource(false, client), Is.EqualTo(PlayerRef.None));
        Assert.That(PhotonSession.DedicatedMessageSource(true, client), Is.EqualTo(client));
    }
    [TestCase("ServeTimeout", "10 giây")]
    [TestCase("DoubleBounce", "nảy thứ hai")]
    [TestCase("Out", "ra ngoài")]
    [TestCase("KitchenVolley", "chân")]
    public void BotFeedbackUsesConfirmedFaultWithoutTelemetry(string fault, string expected)
    {
        Assert.That(QuickCoachingRules.RallyTip(true, fault), Does.Contain(expected));
        Assert.That(QuickCoachingRules.RallyTip(false, fault), Does.Contain("Bạn thắng lượt bóng"));
    }
    [Test] public void BotQueuesFeedbackAsSoonAsBallStopsWithoutCoachingSession()
    {
        var root = new GameObject("Bot coaching regression");
        try
        {
            var match = root.AddComponent<BotMatch>();
            var human = new GameObject("Human"); human.transform.SetParent(root.transform); match.human = human.AddComponent<MatchActor>();
            var rules = new PickleVerse.Rules.SinglesRules(0);
            typeof(BotMatch).GetProperty("Rules").SetValue(match, rules);
            rules.Fail(0, PickleVerse.Rules.Fault.ServeTimeout);
            typeof(BotMatch).GetMethod("BeginFault", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
            string pending = (string)typeof(BotMatch).GetField("pendingQuickTip", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match);
            Assert.That(pending, Does.Contain("10 giây"));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    [Test] public void TrainingViewMirrorsEndsAndFramesDoubles()
    {
        var p = new Vector3(1.2f, 0, -6);
        var near = CourtFollowView.Position(p, -1);
        var far = CourtFollowView.Position(new Vector3(-p.x,0,-p.z), 1);
        Assert.That(near, Is.EqualTo(new Vector3(1.2f,5.3f,-10.25f)));
        Assert.That(far.x, Is.EqualTo(-near.x)); Assert.That(far.z, Is.EqualTo(-near.z));
        Assert.That(CourtFollowView.Position(p,-1,true).x, Is.Zero);
        Assert.That(CourtFollowView.Position(p,-1,true).y, Is.EqualTo(6.5f));
        Assert.That(CourtFollowView.Fov(false), Is.EqualTo(56)); Assert.That(CourtFollowView.Fov(true), Is.EqualTo(64));
    }
    [Test] public void FollowViewKeepsFeetAboveHudBandAndFarBaselineInView()
    {
        foreach (bool doubles in new[] { false, true })
        foreach (float line in new[] { CourtFollowView.DesktopFeetLine, CourtFollowView.TouchFeetLine })
        foreach (int end in new[] { -1, 1 })
        foreach (float depth in new[] { 2f, 4.5f, 6.7f, 8.5f })
        {
            var player = new Vector3(1.4f * -end, 0, end * depth);
            var camera = CourtFollowView.Position(player, end, doubles);
            CourtFollowView.Frame(camera, player, end, doubles, line, out var rotation, out var fov);
            Assert.That(CourtFollowView.ViewportY(rotation, camera, player, fov), Is.GreaterThanOrEqualTo(line - .001f), $"feet doubles={doubles} end={end} depth={depth}");
            Assert.That(CourtFollowView.ViewportY(rotation, camera, new Vector3(0, 0, -end * 6.71f), fov), Is.LessThan(1f), "far baseline");
            Assert.That(fov, Is.InRange(CourtFollowView.Fov(doubles), CourtFollowView.Fov(doubles) + 8));
        }
    }
    [Test] public void AnalysisShowsCurrentMeasuredSummaryAndSupportsOlderResults()
    {
        Assert.That(CoachingAnalysisText.Describe(new CoachingAnalysis {summary="Đã ghi nhận 4 lượt đánh."}), Does.Contain("4 lượt đánh"));
        var old = new CoachingAnalysis();
        old.metrics.Add(new TechniqueMetric {code="Net",opportunities=6,errors=2});
        Assert.That(CoachingAnalysisText.Describe(old), Does.Contain("Bóng chạm lưới: 2/6"));
        old.metrics[0].errors = 0;
        Assert.That(CoachingAnalysisText.Describe(old), Does.Contain("Chưa ghi nhận lỗi trong các tiêu chí đã đo"));
        Assert.That(CoachingAnalysisText.Describe(new CoachingAnalysis()), Does.Contain("chưa ghi nhận được số liệu"));
    }
}
