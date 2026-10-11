using System;
using System.Collections.Generic;
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

        [Test] public async Task Accepts150MsBoundaryAndKeepsLowestPingFirst()
        {
            int calls = 0;
            var matcher = Matcher(_ => { calls++; return Task.FromResult(Samples(Sample(150, .09, "boundary"), Sample(64), Sample(151, 0, "slow"))); });
            var result = await matcher.MeasureAsync("test", 150);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("asia-southeast1", result[0].RegionId);
            Assert.AreEqual(150, result[1].Latency);
        }

        [Test] public async Task RetriesTransientFailureWithoutRaisingLatencyLimit()
        {
            int calls = 0, waits = 0;
            var matcher = Matcher(_ => Task.FromResult(Samples(++calls == 1 ? Sample(int.MaxValue, 1) : calls == 2 ? Sample(207) : Sample(79))),
                (ms, token) => { waits++; return NoDelay(ms, token); });
            var result = await matcher.MeasureAsync("test", 150);
            Assert.AreEqual(3, calls); Assert.AreEqual(2, waits); Assert.AreEqual(79, result[0].Latency);
        }

        [Test] public void RejectsHighPingAfterBoundedRetries()
        {
            int calls = 0;
            var matcher = Matcher(_ => { calls++; return Task.FromResult(Samples(Sample(151))); });
            var error = Assert.ThrowsAsync<ConnectionQualityException>(async () => await matcher.MeasureAsync("test", 150));
            Assert.AreEqual(3, calls); StringAssert.Contains("151 ms", error.Message); StringAssert.Contains("150 ms", error.Message);
        }

        [Test] public void ReportsPacketLossSeparatelyFromPing()
        {
            var matcher = Matcher(_ => Task.FromResult(Samples(Sample(79, .2))));
            var error = Assert.ThrowsAsync<ConnectionQualityException>(async () => await matcher.MeasureAsync("test", 150));
            StringAssert.Contains("Mất gói", error.Message); StringAssert.Contains("20%", error.Message); StringAssert.Contains("79 ms", error.Message);
        }

        [Test] public void ReportsMissingRepliesWithoutDisplayingInvalidLatency()
        {
            var matcher = Matcher(_ => Task.FromResult(Samples(Sample(int.MaxValue, 1), Sample(double.NaN), Sample(20, double.NaN), null)));
            var error = Assert.ThrowsAsync<ConnectionQualityException>(async () => await matcher.MeasureAsync("test", 150));
            StringAssert.Contains("Không nhận được phản hồi", error.Message);
            StringAssert.DoesNotContain(int.MaxValue.ToString(), error.Message);
        }

        [Test] public async Task FiltersInvalidDuplicatesBeforeAggregatingRegion()
        {
            var matcher = Matcher(_ => Task.FromResult(Samples(Sample(60), Sample(80), Sample(20, .2), Sample(200))));
            var result = await matcher.MeasureAsync("test", 150);
            Assert.AreEqual(1, result.Count); Assert.AreEqual(70, result[0].Latency); Assert.AreEqual(0, result[0].PacketLoss);
        }

        [Test] public void CancelledSearchDoesNotStartProbe()
        {
            int calls = 0; using var cancel = new CancellationTokenSource(); cancel.Cancel();
            var matcher = Matcher(_ => { calls++; return Task.FromResult(Samples(Sample(79))); });
            Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), async () => await matcher.MeasureAsync("test", 150, cancel.Token));
            Assert.AreEqual(0, calls);
        }

        [Test] public async Task IgnoresSuccessfulProbeThatCompletesAfterCancellation()
        {
            using var cancel = new CancellationTokenSource(); var pending = new TaskCompletionSource<List<QosResult>>();
            var matcher = Matcher(_ => pending.Task); var result = matcher.MeasureAsync("test", 150, cancel.Token);
            cancel.Cancel(); pending.SetResult(Samples(Sample(79)));
            try { await result; Assert.Fail("A cancelled probe returned a region."); } catch (OperationCanceledException) { }
            // The next search must still acquire the released SDK gate.
            Assert.AreEqual(1, (await Matcher(_ => Task.FromResult(Samples(Sample(79)))).MeasureAsync("test", 150)).Count);
        }

        [Test] public void CancellationStopsRetryDelay()
        {
            using var cancel = new CancellationTokenSource(); int calls = 0;
            var matcher = Matcher(_ => { calls++; return Task.FromResult(Samples(Sample(200))); },
                (ms, token) => { cancel.Cancel(); return Task.FromCanceled(token); });
            Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), async () => await matcher.MeasureAsync("test", 150, cancel.Token));
            Assert.AreEqual(1, calls);
        }

        [Test] public async Task ConcurrentAdaptersDoNotOverlapSdkProbes()
        {
            var pending = new TaskCompletionSource<List<QosResult>>(); int secondCalls = 0;
            var first = Matcher(_ => pending.Task).MeasureAsync("one", 150);
            var second = Matcher(_ => { secondCalls++; return Task.FromResult(Samples(Sample(79))); }).MeasureAsync("two", 150);
            Assert.AreEqual(0, secondCalls);
            pending.SetResult(Samples(Sample(79))); await Task.WhenAll(first, second);
            Assert.AreEqual(1, secondCalls);
        }

        [Test] public async Task CancellationWhileWaitingForSdkDoesNotStartAnotherProbe()
        {
            var pending = new TaskCompletionSource<List<QosResult>>(); using var cancel = new CancellationTokenSource(); int calls = 0;
            var first = Matcher(_ => pending.Task).MeasureAsync("one", 150);
            var second = Matcher(_ => { calls++; return Task.FromResult(Samples(Sample(79))); }).MeasureAsync("two", 150, cancel.Token);
            cancel.Cancel();
            try { await second; Assert.Fail("A cancelled waiter ran a probe."); } catch (OperationCanceledException) { }
            pending.SetResult(Samples(Sample(79))); await first; Assert.AreEqual(0, calls);
        }
    }
}
