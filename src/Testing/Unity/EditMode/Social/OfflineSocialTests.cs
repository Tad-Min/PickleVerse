using System.Linq;
using NUnit.Framework;
using PickleVerse.Progression;
using PickleVerse.Social;

namespace PickleVerse.Tests
{
    public sealed class OfflineSocialTests
    {
        OfflineSocialDirectory data;
        PlayerProfile player;
        [SetUp] public void Setup()
        { data=new OfflineSocialDirectory(); player=new PlayerProfile{id="SELF",playerName="Tester",elo=100}; }

        [Test] public void FixturesAreThirtyUniquePlayersAndDoNotChangeProfile()
        {
            var list=data.Search("");
            Assert.That(list.Count,Is.EqualTo(30)); Assert.That(list.Select(p=>p.Id).Distinct().Count(),Is.EqualTo(30));
            Assert.That(list.All(p=>p.Elo>=100),Is.True);
            string before=UnityEngine.JsonUtility.ToJson(player);
            data.AddFriend(player.id,"DEMO-001"); data.RemoveFriend(player.id,"DEMO-004"); data.Leaderboard(player);
            Assert.That(UnityEngine.JsonUtility.ToJson(player),Is.EqualTo(before));
        }
        [Test] public void LivePlayerIsIncludedExactlyOnceAndEloChangesReorder()
        {
            var first=data.Leaderboard(player);
            Assert.That(first.Count,Is.EqualTo(31)); Assert.That(first.Count(e=>e.Player.Id==player.id),Is.EqualTo(1));
            Assert.That(first.Last().Player.Id,Is.EqualTo(player.id));
            player.elo=8000; player.playerName="New name";
            var current=data.Leaderboard(player).First();
            Assert.That(current.Position,Is.EqualTo(1)); Assert.That(current.Player.Id,Is.EqualTo(player.id));
            Assert.That(current.Player.Name,Is.EqualTo("New name")); Assert.That(current.Player.Elo,Is.EqualTo(8000));
            Assert.That(data.Search("").First().Elo,Is.EqualTo(7200));
        }
        [Test] public void TiesHaveStableIdOrderAndFilteredPositionsStayGlobal()
        {
            player.elo=220;
            var all=data.Leaderboard(player);
            Assert.That(all.Select(e=>e.Position),Is.EqualTo(Enumerable.Range(1,31)));
            Assert.That(all.First(e=>e.Player.Id=="DEMO-028").Position,Is.LessThan(all.First(e=>e.Player.Id==player.id).Position));
            var filtered=data.Leaderboard(player,"self").Single();
            Assert.That(filtered.Position,Is.EqualTo(all.Single(e=>e.Player.Id==player.id).Position));
        }
        [TestCase(" dan thanh ","DEMO-005")]
        [TestCase("ĐAN THANH","DEMO-005")]
        [TestCase("quynh nhu","DEMO-019")]
        [TestCase("demo-030","DEMO-030")]
        [TestCase("Quốc Bảo","DEMO-018")]
        public void SearchMatchesVietnameseWithoutAccentsOrCase(string query,string expected)
        { Assert.That(data.Search(query).Select(p=>p.Id),Does.Contain(expected)); }
        [Test] public void EmptyAndMissingQueriesAreHandled()
        { Assert.That(data.Search(null).Count,Is.EqualTo(30)); Assert.That(data.Search("no such player"),Is.Empty); }
        [Test] public void AddRejectsSelfUnknownAndDuplicatesAndRemoveIsIdempotent()
        {
            Assert.That(data.FriendCount(player.id),Is.EqualTo(6));
            Assert.That(data.AddFriend(player.id,"DEMO-001"),Is.True);
            Assert.That(data.AddFriend(player.id,"DEMO-001"),Is.False);
            Assert.That(data.AddFriend(player.id,player.id),Is.False); Assert.That(data.AddFriend(player.id,"UNKNOWN"),Is.False);
            Assert.That(data.FriendCount(player.id),Is.EqualTo(7));
            Assert.That(data.GetFriends(player.id,"an binh").Single().Id,Is.EqualTo("DEMO-001"));
            Assert.That(data.RemoveFriend(player.id,"DEMO-001"),Is.True); Assert.That(data.RemoveFriend(player.id,"DEMO-001"),Is.False);
            Assert.That(data.FriendCount(player.id),Is.EqualTo(6));
        }
        [Test] public void PlayersHaveSeparateSessionListsAndANewSessionRestoresSeeds()
        {
            data.AddFriend("A","DEMO-001"); data.RemoveFriend("A","DEMO-004");
            Assert.That(data.IsFriend("B","DEMO-001"),Is.False); Assert.That(data.IsFriend("B","DEMO-004"),Is.True);
            Assert.That(data.IsFriend("A","DEMO-001"),Is.True); Assert.That(data.IsFriend("A","DEMO-004"),Is.False);
            var next=new OfflineSocialDirectory();
            Assert.That(next.FriendCount("A"),Is.EqualTo(6)); Assert.That(next.IsFriend("A","DEMO-001"),Is.False); Assert.That(next.IsFriend("A","DEMO-004"),Is.True);
        }
    }
}
