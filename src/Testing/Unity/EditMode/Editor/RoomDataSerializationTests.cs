using Newtonsoft.Json;
using NUnit.Framework;
using PickleVerse.Multiplayer;

public sealed class RoomDataSerializationTests
{
    [Test] public void TeamAssignmentsVersionsAndAcknowledgementsSurviveJson()
    {
        var room=new RoomData {generation=3,revision=7,readyEpoch=2};
        room.teams["player-a"]=1; room.teamVersions["player-a"]=6;
        room.handledTeamRequests["player-a"]="request-123";
        var loaded=JsonConvert.DeserializeObject<RoomData>(JsonConvert.SerializeObject(room));
        Assert.That(loaded.teams["player-a"],Is.EqualTo(1));
        Assert.That(loaded.teamVersions["player-a"],Is.EqualTo(6));
        Assert.That(loaded.handledTeamRequests["player-a"],Is.EqualTo("request-123"));
        Assert.That(loaded.generation,Is.EqualTo(3)); Assert.That(loaded.revision,Is.EqualTo(7)); Assert.That(loaded.readyEpoch,Is.EqualTo(2));
    }
    [Test] public void OlderRoomWithoutMapsKeepsUsableEmptyDictionaries()
    {
        var room=JsonConvert.DeserializeObject<RoomData>("{\"generation\":1,\"mode\":1}");
        Assert.That(room.teams,Is.Empty); Assert.That(room.teamVersions,Is.Empty);
        Assert.That(room.handledTeamRequests,Is.Empty); Assert.That(room.mode,Is.EqualTo(PlayMode.Singles));
    }
}
