using System;
using System.Linq;
using NUnit.Framework;
using PickleVerse.Progression;

public sealed class HybridAiTests
{
    [TestCase(400,400,true,16)] [TestCase(400,400,false,-16)]
    [TestCase(100,100,false,0)] [TestCase(400,360,false,-18)]
    public void StandardElo(int own,int opponent,bool won,int expected)
        => Assert.That(EloPolicy.StandardDelta(own,opponent,won),Is.EqualTo(expected));
    [Test] public void LegacyMatchRetainsFormulaAndNewPolicyIgnoresStreak()
    {
        Assert.That(EloPolicy.VersionedDelta("legacy-v1",400,360,false,0,0),Is.EqualTo(-160));
        Assert.That(EloPolicy.VersionedDelta("elo-v2",400,400,true,100,100),Is.EqualTo(16));
    }
    [Test] public void ProfileIsFrozenAndThresholdsPreserved()
    {
        var policy=new BotPolicy(); var profile=policy.Profile(500);
        policy.tiers[1].reactionSeconds=9;
        Assert.That(profile.reactionSeconds,Is.EqualTo(.15f));
        Assert.That(policy.Profile(2000).aggressiveness,Is.EqualTo(.75f));
    }
    [Test] public void ChunkingPreservesStableShotIdsAndPartMetadata()
    {
        var session=new CoachingSession{playerId=Guid.NewGuid().ToString("N"),sessionId=Guid.NewGuid().ToString("N"),sessionType="match",gameVersion="test"};
        for(int i=0;i<1001;i++) session.Attempt("Drive","Drive","test",i);
        var parts=CoachingRuntime.Split(session);
        Assert.That(parts.Count,Is.EqualTo(2));
        Assert.That(parts.All(p=>p.partCount==2),Is.True);
        Assert.That(parts.SelectMany(p=>p.shots).Select(s=>s.shotId).Distinct().Count(),Is.EqualTo(1001));
    }
}
