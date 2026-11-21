using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using PickleVerse.Multiplayer;

public sealed class MultiplayerBotRosterTests
{
    static MatchRoster Singles(bool bot) => new MatchRoster {mode=PlayMode.Singles,members=new System.Collections.Generic.List<RoomMember> {
        new RoomMember {id="human",team=0,name="MINH"},new RoomMember {id="opponent",team=1,name="ANH",bot=bot}
    }};
    [Test] public void NamedBotOccupiesOpponentSeatWithoutRequiringASecondConnection()
    {
        var roster=Singles(true);
        Assert.That(roster.IsValid(),Is.True); Assert.That(roster.Count,Is.EqualTo(2));
        Assert.That(roster.HumanCount,Is.EqualTo(1)); Assert.That(roster.RankedBot,Is.True);
        Assert.That(roster.Find("opponent").name,Is.EqualTo("ANH"));
    }
    [Test] public void HumanSinglesStillRequireBothConnectedPlayers()
    { var roster=Singles(false); Assert.That(roster.IsValid(),Is.True); Assert.That(roster.HumanCount,Is.EqualTo(2)); Assert.That(roster.RankedBot,Is.False); }
    [Test] public void BotsCannotBeInsertedIntoCustomOrEmptyHumanMatches()
    {
        var roster=Singles(true); roster.custom=true; Assert.That(roster.IsValid(),Is.False); Assert.That(roster.RankedBot,Is.False);
        roster.custom=false; roster.mode=PlayMode.Doubles; Assert.That(roster.IsValid(),Is.False); Assert.That(roster.RankedBot,Is.False);
        roster.mode=PlayMode.Singles; roster.members[0].bot=true; Assert.That(roster.IsValid(),Is.False);
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void DoublesWithBotsWaitsForEveryHuman(int humanCount)
    {
        var roster = new MatchRoster { mode = PlayMode.Doubles };
        for (int i = 0; i < 4; i++)
            roster.members.Add(new RoomMember { id = "player-" + i, team = i / 2, bot = i >= humanCount });
        Assert.That(roster.IsValid(), Is.True);
        Assert.That(roster.HumanCount, Is.EqualTo(humanCount));
        var decisions = new Dictionary<string, ReadyDecision>();
        Assert.That(MatchReadyPolicy.Evaluate(roster, decisions, false, true), Is.EqualTo(ReadyOutcome.Waiting));
        foreach (var human in roster.members.Where(member => !member.bot))
            decisions.Add(human.id, ReadyDecision.Accepted);
        Assert.That(MatchReadyPolicy.Evaluate(roster, decisions, false, true), Is.EqualTo(ReadyOutcome.Approved));
        roster.custom = true;
        Assert.That(roster.IsValid(), Is.False);
    }
}
