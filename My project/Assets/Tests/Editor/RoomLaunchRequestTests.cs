using NUnit.Framework;
using PickleVerse.Multiplayer;
using PickleVerse.Multiplayer.UI;
namespace PickleVerse.Tests
{
    public sealed class RoomLaunchRequestTests
    {
        [TearDown]public void Clean(){RoomLaunchRequest.Clear();}
        [TestCase(PlayMode.Singles,RoomLaunchAction.CreateSingles)]
        [TestCase(PlayMode.Doubles,RoomLaunchAction.CreateDoubles)]
        public void RoomTypeSurvivesNavigationExactlyOnce(PlayMode mode,RoomLaunchAction expected)
        {RoomLaunchRequest.Create(mode);Assert.AreEqual(expected,RoomLaunchRequest.Consume(out var code));Assert.IsNull(code);Assert.AreEqual(RoomLaunchAction.None,RoomLaunchRequest.Consume(out code));}
        [Test]public void JoinNormalizesSharedCode()
        {RoomLaunchRequest.Join("  abcD12 ");Assert.AreEqual(RoomLaunchAction.Join,RoomLaunchRequest.Consume(out var code));Assert.AreEqual("ABCD12",code);}
        [TestCase(null)][TestCase("")][TestCase("   ")]
        public void BlankCodeNeverSchedulesConnection(string code)
        {Assert.Throws<System.ArgumentException>(()=>RoomLaunchRequest.Join(code));Assert.AreEqual(RoomLaunchAction.None,RoomLaunchRequest.Action);}
        [Test]public void RankedEntryClearsPrivateIntent()
        {RoomLaunchRequest.Create(PlayMode.Doubles);RoomLaunchRequest.Clear();Assert.AreEqual(RoomLaunchAction.None,RoomLaunchRequest.Consume(out var code));Assert.IsNull(code);}
    }
}
