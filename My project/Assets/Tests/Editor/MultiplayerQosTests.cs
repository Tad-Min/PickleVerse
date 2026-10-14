using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PickleVerse.Multiplayer;
using Unity.Services.Matchmaker.Models;

namespace PickleVerse.Tests
{
    public sealed class MultiplayerQosTests
    {
        static List<QosResult> Samples(params QosResult[] samples) => new List<QosResult>(samples);
        static QosResult Sample(double latency, double loss = 0, string region = "asia-southeast1") => new QosResult(region, loss, latency);
        static Task NoDelay(int milliseconds, CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.CompletedTask; }
        static GroupMatchmaker Matcher(Func<string, Task<List<QosResult>>> probe,
            Func<int, CancellationToken, Task> delay = null) => new GroupMatchmaker(probe, delay ?? NoDelay, _ => { });

        [Test] public async Task AcceptsHighPingAndKeepsLowestPingFirst()
        {
            int calls = 0;
            var matcher = Matcher(_ => { calls++; return Task.FromResult(Samples(Sample(150, .09, "boundary"), Sample(64), Sample(900, 0, "slow"))); });
            var result = await matcher.MeasureAsync("test");
            Assert.AreEqual(1, calls);
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("asia-southeast1", result[0].RegionId);
            Assert.AreEqual(900, result[2].Latency);
        }

        [Test] public async Task RetriesMissingResponseAndAcceptsNextValidMeasurement()
        {
            int calls = 0, waits = 0;
            var matcher = Matcher(_ => Task.FromResult(Samples(++calls == 1 ? Sample(int.MaxValue, 1) : Sample(207))),
                (ms, token) => { waits++; return NoDelay(ms, token); });
            var result = await matcher.MeasureAsync("test");
            Assert.AreEqual(2, calls); Assert.AreEqual(1, waits); Assert.AreEqual(207, result[0].Latency);
        }

        [Test] public async Task ProbeFailureDoesNotPreventMatchmaking()
        {
            int calls = 0;
            var matcher = Matcher(_ => { calls++; return Task.FromException<List<QosResult>>(new InvalidOperationException("QoS unavailable")); });
            Assert.IsEmpty(await matcher.MeasureAsync("test"));
            Assert.AreEqual(3, calls);
        }

        [Test] public async Task PacketLossDoesNotImposeAnotherQualityLimit()
        {
            var matcher = Matcher(_ => Task.FromResult(Samples(Sample(79, .2))));
            var result = await matcher.MeasureAsync("test");
            Assert.AreEqual(.2, result[0].PacketLoss);
        }

        [Test] public async Task MissingRepliesUseAutomaticRelayRegion()
        {
            var matcher = Matcher(_ => Task.FromResult(Samples(Sample(int.MaxValue, 1), Sample(double.NaN), Sample(20, double.NaN), null)));
            var result = await matcher.MeasureAsync("test");
            Assert.IsEmpty(result);
            Assert.IsNull(GroupMatchmaker.SelectRelayRegion(new List<Player> { new Player("local", qosResults: result) }));
        }

        [Test] public async Task FiltersInvalidDuplicatesBeforeAggregatingRegion()
        {
            var matcher = Matcher(_ => Task.FromResult(Samples(Sample(60), Sample(80), Sample(200), Sample(int.MaxValue, 1))));
            var result = await matcher.MeasureAsync("test");
            Assert.AreEqual(1, result.Count); Assert.AreEqual(340d/3, result[0].Latency); Assert.AreEqual(0, result[0].PacketLoss);
        }

        [Test] public void CancelledSearchDoesNotStartProbe()
        {
            int calls = 0; using var cancel = new CancellationTokenSource(); cancel.Cancel();
            var matcher = Matcher(_ => { calls++; return Task.FromResult(Samples(Sample(79))); });
            Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), async () => await matcher.MeasureAsync("test", cancel.Token));
            Assert.AreEqual(0, calls);
        }

        [Test] public async Task IgnoresSuccessfulProbeThatCompletesAfterCancellation()
        {
            using var cancel = new CancellationTokenSource(); var pending = new TaskCompletionSource<List<QosResult>>();
            var matcher = Matcher(_ => pending.Task); var result = matcher.MeasureAsync("test", cancel.Token);
            cancel.Cancel(); pending.SetResult(Samples(Sample(79)));
            try { await result; Assert.Fail("A cancelled probe returned a region."); } catch (OperationCanceledException) { }
            // The next search must still acquire the released SDK gate.
            Assert.AreEqual(1, (await Matcher(_ => Task.FromResult(Samples(Sample(79)))).MeasureAsync("test")).Count);
        }

        [Test] public void CancellationStopsRetryDelay()
        {
            using var cancel = new CancellationTokenSource(); int calls = 0;
            var matcher = Matcher(_ => { calls++; return Task.FromResult(Samples(Sample(int.MaxValue, 1))); },
                (ms, token) => { cancel.Cancel(); return Task.FromCanceled(token); });
            Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), async () => await matcher.MeasureAsync("test", cancel.Token));
            Assert.AreEqual(1, calls);
        }

        [Test] public async Task ConcurrentAdaptersDoNotOverlapSdkProbes()
        {
            var pending = new TaskCompletionSource<List<QosResult>>(); int secondCalls = 0;
            var first = Matcher(_ => pending.Task).MeasureAsync("one");
            var second = Matcher(_ => { secondCalls++; return Task.FromResult(Samples(Sample(79))); }).MeasureAsync("two");
            Assert.AreEqual(0, secondCalls);
            pending.SetResult(Samples(Sample(79))); await Task.WhenAll(first, second);
            Assert.AreEqual(1, secondCalls);
        }

        [Test] public async Task CancellationWhileWaitingForSdkDoesNotStartAnotherProbe()
        {
            var pending = new TaskCompletionSource<List<QosResult>>(); using var cancel = new CancellationTokenSource(); int calls = 0;
            var first = Matcher(_ => pending.Task).MeasureAsync("one");
            var second = Matcher(_ => { calls++; return Task.FromResult(Samples(Sample(79))); }).MeasureAsync("two", cancel.Token);
            cancel.Cancel();
            try { await second; Assert.Fail("A cancelled waiter ran a probe."); } catch (OperationCanceledException) { }
            pending.SetResult(Samples(Sample(79))); await first; Assert.AreEqual(0, calls);
        }

        [Test] public void PlayersWithDifferentRegionsRemainInOneTicket()
        {
            var members = new List<RoomMember> { new RoomMember { id="a", group="duo" }, new RoomMember { id="b", group="duo" } };
            var qos = new Dictionary<string,List<QosResult>> { ["a"]=Samples(Sample(800, region:"asia")), ["b"]=Samples(Sample(600, region:"europe")) };
            var players = GroupMatchmaker.BuildTicketPlayers(members, qos);
            Assert.AreEqual(2, players.Count);
            Assert.IsNull(players[0].QosResults);
            Assert.IsNull(players[1].QosResults);
            var first = players[0].CustomData.GetAs<GroupMatchmaker.TicketPlayerData>();
            var second = players[1].CustomData.GetAs<GroupMatchmaker.TicketPlayerData>();
            Assert.AreEqual("asia", first.relayRegions[0]);
            Assert.AreEqual("europe", second.relayRegions[0]);
            Assert.IsNull(GroupMatchmaker.SelectRelayRegion(players));
        }

        [Test] public void ChoosesRegionForAllMatchedPlayersInsteadOfFirstPlayersPreference()
        {
            var players = new List<Player> {
                new Player("a", qosResults:Samples(Sample(60, region:"asia"), Sample(200, region:"europe"))),
                new Player("b", qosResults:Samples(Sample(500, region:"asia"), Sample(180, region:"europe"))) };
            Assert.AreEqual("europe", GroupMatchmaker.SelectRelayRegion(players));
        }

        [Test] public void ManyRegionsAndSdkAnnotationsRespectTicketPayloadLimits()
        {
            var samples = Enumerable.Range(0, 30).Select(i => new QosResult("test-region-"+i, .2, 800+i,
                new Dictionary<string,List<string>> { ["MatchmakerPoolId"]=Enumerable.Range(0, 10).Select(_ => Guid.NewGuid().ToString()).ToList() })).ToList();
            Assert.Greater(Newtonsoft.Json.JsonConvert.SerializeObject(samples).Length, 200);
            var members = new List<RoomMember> { new RoomMember { id="a", name="Player", gender="Male", group="a" } };
            var player = GroupMatchmaker.BuildTicketPlayers(members, new Dictionary<string,List<QosResult>> { ["a"]=samples })[0];
            var raw = Newtonsoft.Json.Linq.JObject.FromObject(player.CustomData.GetAs<Dictionary<string,object>>());
            Assert.IsNull(raw["relayQosJson"]);
            Assert.IsNull(player.QosResults);
            foreach (var value in raw.Descendants())
            {
                if (value.Type == Newtonsoft.Json.Linq.JTokenType.String) Assert.LessOrEqual(((string)value).Length, 200);
                if (value is Newtonsoft.Json.Linq.JArray array) Assert.LessOrEqual(array.Count, 100);
            }
            var stored = player.CustomData.GetAs<GroupMatchmaker.TicketPlayerData>();
            Assert.AreEqual(30, stored.relayRegions.Count);
            Assert.AreEqual(30, stored.relayLatencies.Count);
            Assert.AreEqual("test-region-0", GroupMatchmaker.SelectRelayRegion(new List<Player> { player }));
        }

        [Test] public void RegionMetadataArraysStayBoundedAndIgnoreOversizedRegionIds()
        {
            var samples = Enumerable.Range(0, 101).Select(i => Sample(i, region:"region-"+i)).ToList();
            samples.Add(Sample(0, region:new string('x', 201)));
            var members = new List<RoomMember> { new RoomMember { id="a" } };
            var stored = GroupMatchmaker.BuildTicketPlayers(members, new Dictionary<string,List<QosResult>> { ["a"]=samples })[0]
                .CustomData.GetAs<GroupMatchmaker.TicketPlayerData>();
            Assert.AreEqual(100, stored.relayRegions.Count);
            Assert.AreEqual(100, stored.relayLatencies.Count);
            Assert.IsTrue(stored.relayRegions.All(region => region.Length <= 200));
            Assert.AreEqual(99, stored.relayLatencies[99]);
        }

        [Test] public void StoredMeasurementsSelectRegionAfterMatchingWithoutTicketQos()
        {
            var members = new List<RoomMember> { new RoomMember { id="a" }, new RoomMember { id="b" } };
            var qos = new Dictionary<string,List<QosResult>> {
                ["a"]=Samples(Sample(60, region:"asia"), Sample(200, region:"europe")),
                ["b"]=Samples(Sample(500, region:"asia"), Sample(180, region:"europe")) };
            var players = GroupMatchmaker.BuildTicketPlayers(members, qos);
            Assert.IsNull(players[0].QosResults);
            Assert.AreEqual("europe", GroupMatchmaker.SelectRelayRegion(players));
        }
        [TestCase(true)] [TestCase(false)]
        public void HostingAssignmentRegionCannotBeUsedAsRelayRegion(bool measured)
        {
            var members=new List<RoomMember>{new RoomMember{id="a"},new RoomMember{id="b"}};
            var qos=new Dictionary<string,List<QosResult>> {
                ["a"]=measured?Samples(Sample(60,region:"asia-southeast1")):Samples(),
                ["b"]=measured?Samples(Sample(75,region:"asia-southeast1")):Samples()};
            var players=GroupMatchmaker.BuildTicketPlayers(members,qos);
            var teams=new List<Team>{new Team("A",playerIds:new List<string>{"a"}),new Team("B",playerIds:new List<string>{"b"})};
            var roster=GroupMatchmaker.Roster("test","foreign-hosting-region",teams,players,PlayMode.Singles);
            Assert.AreEqual(measured?"asia-southeast1":null,roster.region);
        }
    }
}
