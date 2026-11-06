using System.Collections.Generic;
using NUnit.Framework;
using PickleVerse.Multiplayer;

public sealed class MatchReadyPolicyTests
{
    static MatchRoster Roster(PlayMode mode)
    {
        var r=new MatchRoster{mode=mode};
        for(int i=0;i<(int)mode*2;i++)r.members.Add(new RoomMember{id="p"+i,team=i/(int)mode});
        return r;
    }
    static Dictionary<string,ReadyDecision> AcceptAll(MatchRoster r)
    {var d=new Dictionary<string,ReadyDecision>();foreach(var p in r.members)if(!p.bot)d[p.id]=ReadyDecision.Accepted;return d;}
    [TestCase(PlayMode.Singles)] [TestCase(PlayMode.Doubles)]
    public void EveryHumanMustAcceptAndConnect(PlayMode mode)
    {
        var r=Roster(mode);var d=AcceptAll(r);
        Assert.That(MatchReadyPolicy.Evaluate(r,d,false,true),Is.EqualTo(ReadyOutcome.Approved));
        Assert.That(MatchReadyPolicy.Evaluate(r,d,false,false),Is.EqualTo(ReadyOutcome.Waiting));
        d.Remove("p0");Assert.That(MatchReadyPolicy.Evaluate(r,d,false,true),Is.EqualTo(ReadyOutcome.Waiting));
    }
    [TestCase(PlayMode.Singles)] [TestCase(PlayMode.Doubles)]
    public void AnyDeclineCancelsWholeMatchButOnlyAcceptingPlayersRequeue(PlayMode mode)
    {
        var r=Roster(mode);var d=AcceptAll(r);d["p0"]=ReadyDecision.Declined;
        Assert.That(MatchReadyPolicy.Evaluate(r,d,false,true),Is.EqualTo(ReadyOutcome.Rejected));
        Assert.That(MatchReadyPolicy.ResumeSearch(d["p0"]),Is.False);
        Assert.That(MatchReadyPolicy.ResumeSearch(d["p1"]),Is.True);
        Assert.That(MatchReadyPolicy.ResumeSearch(ReadyDecision.Pending),Is.False);
    }
    [TestCase(PlayMode.Singles)] [TestCase(PlayMode.Doubles)]
    public void ExpiredDeadlineCannotApproveEvenWithLateAcceptances(PlayMode mode)
    {var r=Roster(mode);Assert.That(MatchReadyPolicy.Evaluate(r,AcceptAll(r),true,true),Is.EqualTo(ReadyOutcome.Rejected));}
    [Test] public void BotFallbackStillNeedsHumanConfirmation()
    {
        var r=Roster(PlayMode.Singles);r.members[1].bot=true;
        Assert.That(MatchReadyPolicy.Evaluate(r,new Dictionary<string,ReadyDecision>(),false,true),Is.EqualTo(ReadyOutcome.Waiting));
        Assert.That(MatchReadyPolicy.Evaluate(r,AcceptAll(r),false,true),Is.EqualTo(ReadyOutcome.Approved));
    }
    [Test] public void AcceptanceFromDifferentRosterCannotApprove()
    {Assert.That(MatchReadyPolicy.Evaluate(Roster(PlayMode.Singles),new Dictionary<string,ReadyDecision>{{"stale",ReadyDecision.Accepted}},false,true),Is.EqualTo(ReadyOutcome.Waiting));}
    [Test] public void InvalidRosterCannotStart()
    {var r=Roster(PlayMode.Doubles);r.members.RemoveAt(0);Assert.That(MatchReadyPolicy.Evaluate(r,AcceptAll(r),false,true),Is.EqualTo(ReadyOutcome.Rejected));}
    [TestCase(1)] [TestCase(2)]
    public void MatchmakerPartitionsBothQueuesByClientProtocolWithoutPinningARelease(int size)
    {
        var queue=Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("Assets/Settings/Multiplayer/Cloud/PickleVerse_"+size+"v"+size+".mmq"));
        var rules=(Newtonsoft.Json.Linq.JArray)queue["defaultPool"]["matchLogic"]["matchDefinition"]["matchRules"];
        var protocol=System.Linq.Enumerable.Single(rules,r=>(string)r["name"]=="SameProtocol");
        Assert.That((string)protocol["type"],Is.EqualTo("Equality"));
        Assert.That((string)protocol["source"],Is.EqualTo("Players.CustomData.protocol"));
        Assert.That(protocol["reference"],Is.Null);
    }
}
