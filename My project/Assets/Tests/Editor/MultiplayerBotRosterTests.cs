using NUnit.Framework;
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
    [Test] public void BotsCannotBeInsertedIntoCustomOrDoublesMatches()
    {
        var roster=Singles(true); roster.custom=true; Assert.That(roster.IsValid(),Is.False); Assert.That(roster.RankedBot,Is.False);
        roster.custom=false; roster.mode=PlayMode.Doubles; Assert.That(roster.IsValid(),Is.False); Assert.That(roster.RankedBot,Is.False);
        roster.mode=PlayMode.Singles; roster.members[0].bot=true; Assert.That(roster.IsValid(),Is.False);
    }
}
