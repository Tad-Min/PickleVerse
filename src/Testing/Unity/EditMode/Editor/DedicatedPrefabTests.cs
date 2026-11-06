using Fusion;
using NUnit.Framework;
using PickleVerse.Multiplayer;
using UnityEngine;

public sealed class DedicatedPrefabTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void SessionHeapFitsTheBakedMatchStateWithoutGrowingTheHeapBudget(bool dedicated)
    {
        var original = NetworkProjectConfig.Global;
        var oldShift = original.Heap.PageShift;
        int oldCount = original.Heap.PageCount;
        var config = PhotonSession.ConfigForSession(dedicated);
        var settings = Resources.Load<MultiplayerSettings>("MultiplayerSettings");
        int bytes = NetworkObject.GetWordCount(settings.MatchPrefab(dedicated).GetComponent<NetworkObject>()) * 4;
        Assert.That(bytes, Is.LessThan(1 << (int)config.Heap.PageShift));
        Assert.That((long)config.Heap.PageCount << (int)config.Heap.PageShift, Is.EqualTo((long)oldCount << (int)oldShift));
        Assert.That(original.Heap.PageShift, Is.EqualTo(oldShift));
        Assert.That(original.Heap.PageCount, Is.EqualTo(oldCount));
        if (dedicated) Assert.That(config.Simulation.InputDataWordCount, Is.GreaterThanOrEqualTo(8));
    }
    [Test]
    public void DedicatedAssetsDoNotCarrySharedOnlyAuthorityFlags()
    {
        var settings = Resources.Load<MultiplayerSettings>("MultiplayerSettings");
        Assert.That(settings, Is.Not.Null);
        foreach (var prefab in new[] { settings.dedicatedMatchPrefab, settings.dedicatedPlayerPrefab })
        {
            Assert.That(prefab, Is.Not.Null);
            var network = prefab.GetComponent<NetworkObject>();
            Assert.That(network, Is.Not.Null);
            Assert.That(network.Flags & (NetworkObjectFlags.MasterClientObject | NetworkObjectFlags.AllowStateAuthorityOverride), Is.EqualTo(NetworkObjectFlags.None));
        }
        Assert.That(settings.matchPrefab.GetComponent<NetworkObject>().Flags.HasFlag(NetworkObjectFlags.MasterClientObject), Is.True);
        Assert.That(settings.playerPrefab.GetComponent<NetworkObject>().Flags.HasFlag(NetworkObjectFlags.MasterClientObject), Is.True);
        Assert.That(settings.dedicatedMatchPrefab.GetComponent<NetworkMatch>(), Is.Not.Null);
        Assert.That(settings.dedicatedPlayerPrefab.GetComponent<NetworkPlayer>(), Is.Not.Null);
    }
}
