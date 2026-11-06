#if PICKLEVERSE_LEGACY_NGO
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PickleVerse.Multiplayer;
using Unity.Services.Matchmaker;
using Unity.Services.Matchmaker.Models;

namespace PickleVerse.Tests
{
    public sealed class MultiplayerTicketCleanupTests
    {
        long now;
        readonly List<long> requests = new List<long>();
        readonly List<string> warnings = new List<string>();
        GroupMatchmaker Matcher(Func<string, Task> remove)
        {
            return new GroupMatchmaker(_ => Task.FromResult(new List<QosResult>()),
                (ms, token) => { Assert.IsFalse(token.CanBeCanceled); now += ms; return Task.CompletedTask; },
                _ => { }, ticket => { requests.Add(now); return remove(ticket); }, () => now, warnings.Add);
        }

        [SetUp] public void Setup() { now = 0; requests.Clear(); warnings.Clear(); }

        [Test] public async Task AdjacentCleanupRequestsRespectDeletionRateLimit()
        {
            var matcher = Matcher(_ => Task.CompletedTask);
            await matcher.DeleteTicketAsync("first");
            await matcher.DeleteTicketAsync("second");
            CollectionAssert.AreEqual(new long[] { 0, 1100 }, requests);
            Assert.IsEmpty(warnings);
        }

        [Test] public async Task RateLimitedCleanupRetriesSameTicketWithBackoff()
        {
            int calls = 0;
            var matcher = Matcher(ticket => {
                Assert.AreEqual("cancelled-ticket", ticket);
                return ++calls < 3 ? Task.FromException(new MatchmakerServiceException(MatchmakerExceptionReason.RateLimited, "429")) : Task.CompletedTask;
            });
            await matcher.DeleteTicketAsync("cancelled-ticket");
            CollectionAssert.AreEqual(new long[] { 0, 1100, 3300 }, requests);
            Assert.IsEmpty(warnings);
        }

        [TestCase(MatchmakerExceptionReason.EntityNotFound)]
        [TestCase(MatchmakerExceptionReason.Gone)]
        public async Task ExpiredTicketNeedsNoRetryOrWarning(MatchmakerExceptionReason reason)
        {
            await Matcher(_ => Task.FromException(new MatchmakerServiceException(reason, "already gone"))).DeleteTicketAsync("expired");
            Assert.AreEqual(1, requests.Count);
            Assert.IsEmpty(warnings);
        }

        [Test] public async Task PersistentRateLimitIsBoundedAndRemainsVisible()
        {
            await Matcher(_ => Task.FromException(new MatchmakerServiceException(MatchmakerExceptionReason.RateLimited, "429"))).DeleteTicketAsync("blocked");
            Assert.AreEqual(4, requests.Count);
            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains("429", warnings[0]);
        }

        [Test] public async Task CleanupSerializesOverlappingCancelledSearches()
        {
            var first = new TaskCompletionSource<bool>();
            int calls = 0;
            var matcher = Matcher(_ => ++calls == 1 ? first.Task : Task.CompletedTask);
            var a = matcher.DeleteTicketAsync("a");
            var b = matcher.DeleteTicketAsync("b");
            Assert.AreEqual(1, calls);
            first.SetResult(true);
            await Task.WhenAll(a, b);
            CollectionAssert.AreEqual(new long[] { 0, 1100 }, requests);
        }

        [Test] public async Task PermanentFailureIsReportedWithoutRepeatedRequests()
        {
            await Matcher(_ => Task.FromException(new MatchmakerServiceException(MatchmakerExceptionReason.Unauthorized, "401"))).DeleteTicketAsync("unauthorized");
            Assert.AreEqual(1, requests.Count);
            Assert.AreEqual(1, warnings.Count);
        }
    }
}
#endif
