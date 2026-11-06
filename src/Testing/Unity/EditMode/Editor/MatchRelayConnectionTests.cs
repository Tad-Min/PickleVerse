#if PICKLEVERSE_LEGACY_NGO
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using PickleVerse.Multiplayer;
using Unity.Services.Multiplayer;
using Unity.Services.Relay;

public sealed class MatchRelayConnectionTests
{
    [Test] public async Task ValidRegionIsPreserved()
    {
        var options=new List<RelayNetworkOptions>();
        await MatchRelayConnection.StartAsync("asia-southeast1",o=>{options.Add(o);return Task.CompletedTask;},()=>true);
        Assert.That(options.Count,Is.EqualTo(1));Assert.That(options[0].Region,Is.EqualTo("asia-southeast1"));
    }
    [Test] public async Task MissingRegionStartsAutomaticSelectionOnce()
    {
        var options=new List<RelayNetworkOptions>();
        await MatchRelayConnection.StartAsync(null,o=>{options.Add(o);return Task.CompletedTask;},()=>true);
        Assert.That(options.Count,Is.EqualTo(1));Assert.That(options[0].Region,Is.Null);
    }
    [Test] public async Task RegionNotFoundInsideSdkWrapperRetriesAutomatically()
    {
        var options=new List<RelayNetworkOptions>();
        await MatchRelayConnection.StartAsync("retired",o=>
        {options.Add(o);return options.Count==1?Task.FromException(new SessionException("allocation failed",SessionError.Unknown,new RelayServiceException(RelayExceptionReason.RegionNotFound,"region missing"))):Task.CompletedTask;},()=>true);
        Assert.That(options.Count,Is.EqualTo(2));Assert.That(options[1].Region,Is.Null);
    }
    [Test] public void PermissionFailureIsNotHiddenByRegionFallback()
    {
        int calls=0;
        Assert.ThrowsAsync<RelayServiceException>(async()=>await MatchRelayConnection.StartAsync("asia",o=>
        {calls++;return Task.FromException(new RelayServiceException(RelayExceptionReason.Forbidden,"no permission"));},()=>true));
        Assert.That(calls,Is.EqualTo(1));
    }
    [Test] public void CancelledMatchNeverStartsTransport()
    {
        int calls=0;
        Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(),async()=>await MatchRelayConnection.StartAsync("asia",o=>{calls++;return Task.CompletedTask;},()=>false));
        Assert.That(calls,Is.Zero);
    }
}
#endif
