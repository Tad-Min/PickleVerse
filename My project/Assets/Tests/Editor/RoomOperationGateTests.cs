using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PickleVerse.Multiplayer.Services;

namespace PickleVerse.Tests
{
    public sealed class RoomOperationGateTests
    {
        [Test]
        public async Task NavigationWaitsForInitialSceneAndCancelledRoomCleanup()
        {
            var gate=new RoomOperationGate();
            var connected=new TaskCompletionSource<bool>();
            var cleaned=new TaskCompletionSource<bool>();
            bool navigated=false;
            async Task Create(){await connected.Task;await cleaned.Task;}
            var create=gate.Run(Create);
            async Task Leave(){await gate.DrainAsync();navigated=true;}
            var leave=Leave();
            Assert.That(navigated,Is.False);
            connected.SetResult(true);
            Assert.That(navigated,Is.False);
            cleaned.SetResult(true);await create;await leave;
            Assert.That(navigated,Is.True);
        }

        [Test]
        public async Task FailedConnectionStillAllowsLeavingAndRetry()
        {
            var gate=new RoomOperationGate();var connection=new TaskCompletionSource<bool>();
            var create=gate.Run(()=>connection.Task);var leave=gate.DrainAsync();
            connection.SetException(new InvalidOperationException("Connection failed"));
            Assert.ThrowsAsync<InvalidOperationException>(async()=>await create);
            await leave;await gate.Run(()=>Task.CompletedTask);
        }

        [Test]
        public async Task SecondCreateCannotReplaceTheOperationNavigationIsWaitingFor()
        {
            var gate=new RoomOperationGate();var connection=new TaskCompletionSource<bool>();
            var create=gate.Run(()=>connection.Task);
            Assert.Throws<InvalidOperationException>(()=>gate.Run(()=>Task.CompletedTask));
            var leave=gate.DrainAsync();Assert.That(leave.IsCompleted,Is.False);
            connection.SetResult(true);await create;await leave;
        }
    }
}
