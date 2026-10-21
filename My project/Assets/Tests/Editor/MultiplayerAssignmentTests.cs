#if PICKLEVERSE_LEGACY_NGO
using System;
using NUnit.Framework;
using PickleVerse.Multiplayer;
using Unity.Services.Matchmaker.Models;

namespace PickleVerse.Tests
{
    public sealed class MultiplayerAssignmentTests
    {
        [Test] public void FailedAssignmentPreservesServerReasonAndIsNotATimeout()
        {
            var assignment = new MatchIdAssignment("MatchIdAssignment", "The ticket is not compatible with the match definition.", MatchIdAssignment.StatusOptions.Failed);
            var error = Assert.Throws<MatchmakingAssignmentException>(() => GroupMatchmaker.ValidateAssignment(assignment, "PickleVerse-1v1"));
            StringAssert.Contains("PickleVerse-1v1", error.Message);
            StringAssert.Contains(assignment.Message, error.Message);
        }

        [Test] public void ExpiredAssignmentRemainsARetryableTimeout()
        {
            var assignment = new MatchIdAssignment("MatchIdAssignment", status: MatchIdAssignment.StatusOptions.Timeout);
            Assert.Throws<TimeoutException>(() => GroupMatchmaker.ValidateAssignment(assignment, "PickleVerse-1v1"));
        }

        [TestCase(MatchIdAssignment.StatusOptions.InProgress)]
        [TestCase(MatchIdAssignment.StatusOptions.Found)]
        public void PendingAndSuccessfulAssignmentsAreNotRejected(MatchIdAssignment.StatusOptions status)
        {
            var assignment = new MatchIdAssignment("MatchIdAssignment", status: status, matchId: status == MatchIdAssignment.StatusOptions.Found ? "match" : null);
            Assert.DoesNotThrow(() => GroupMatchmaker.ValidateAssignment(assignment, "PickleVerse-1v1"));
        }
    }
}
#endif
