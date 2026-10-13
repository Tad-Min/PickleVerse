using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using PickleVerse.Multiplayer;

namespace PickleVerse.Tests
{
    public class MatchEnvironmentPayloadTests
    {
        static MatchRoster Roster()=>new MatchRoster{matchId="lighting-qa",region="asia",mode=PlayMode.Singles,custom=true,members=new List<RoomMember>{new RoomMember{id="host",team=0},new RoomMember{id="guest",team=1}}};

        [TestCase(false)]
        [TestCase(true)]
        public void HostChoiceSurvivesRosterSnapshotWithoutChangingRoom(bool night)
        {
            var room=Roster();room.night=!night;
            var client=JsonConvert.DeserializeObject<MatchRoster>(JsonConvert.SerializeObject(room.WithEnvironment(night)));
            Assert.That(client.night,Is.EqualTo(night));Assert.That(room.night,Is.EqualTo(!night));
            Assert.That(client.IsValid(),Is.True);Assert.That(client.matchId,Is.EqualTo(room.matchId));Assert.That(client.members.Count,Is.EqualTo(2));
        }
        [Test]
        public void RosterWithoutEnvironmentRemainsCompatibleAndDefaultsToDay()
        {
            var client=JsonConvert.DeserializeObject<MatchRoster>("{\"matchId\":\"old-match\",\"mode\":1,\"custom\":true,\"members\":[{\"id\":\"a\",\"team\":0},{\"id\":\"b\",\"team\":1}]}");
            Assert.That(client.night,Is.False);Assert.That(client.IsValid(),Is.True);
        }
    }
}
