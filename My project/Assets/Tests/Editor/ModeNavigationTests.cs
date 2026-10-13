using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PickleVerse.Multiplayer.Services;
namespace PickleVerse.Tests
{
    public sealed class ModeNavigationTests
    {
        [Test]public async Task InvalidateBeforeAwaitAndLateCallbacksStayInvalidAfterReopen()
        {
            var guard=new ModeNavigationGuard();int page=guard.Epoch;var leave=new TaskCompletionSource<bool>();bool invalid=false,navigated=false;
            var returning=guard.ReturnAsync(()=>invalid=true,()=>Task.CompletedTask,()=>Task.CompletedTask,()=>leave.Task,()=>navigated=true);
            Assert.That(invalid,Is.True);Assert.That(guard.IsCurrent(page),Is.False);Assert.That(navigated,Is.False);leave.SetResult(true);await returning;guard.Open();Assert.That(guard.IsCurrent(page),Is.False);Assert.That(navigated,Is.True);
        }
        [Test]public async Task DuplicateBackDoesNotLeaveTwice(){var guard=new ModeNavigationGuard();var leave=new TaskCompletionSource<bool>();int calls=0;var first=guard.ReturnAsync(()=>{},()=>Task.CompletedTask,()=>Task.CompletedTask,()=>{calls++;return leave.Task;},()=>{});await guard.ReturnAsync(()=>{},()=>Task.CompletedTask,()=>Task.CompletedTask,()=>{calls++;return Task.CompletedTask;},()=>{});Assert.That(calls,Is.EqualTo(1));leave.SetResult(true);await first;}
        [TestCase(false)][TestCase(true)]public async Task LeaveFailureKeepsPageAndAllowsRetry(bool matchSession)
        {
            var guard=new ModeNavigationGuard();bool navigate=false;int oldPage=guard.Epoch;
            Func<Task> failed=()=>Task.FromException(new InvalidOperationException("Leave failed"));
            Assert.ThrowsAsync<InvalidOperationException>(async()=>await guard.ReturnAsync(()=>{},()=>Task.CompletedTask,matchSession?failed:()=>Task.CompletedTask,matchSession?()=>Task.CompletedTask:failed,()=>navigate=true));
            Assert.That(guard.Leaving,Is.False);Assert.That(guard.IsCurrent(oldPage),Is.False);Assert.That(navigate,Is.False);
            await guard.ReturnAsync(()=>{},()=>Task.CompletedTask,()=>Task.CompletedTask,()=>Task.CompletedTask,()=>navigate=true);Assert.That(navigate,Is.True);
        }
        [Test]public void CancellationFailureDoesNotNavigate(){var guard=new ModeNavigationGuard();int leaves=0;Assert.ThrowsAsync<InvalidOperationException>(async()=>await guard.ReturnAsync(()=>{},()=>Task.FromException(new InvalidOperationException("Cancel failed")),()=>{leaves++;return Task.CompletedTask;},()=>Task.CompletedTask,()=>Assert.Fail("Should remain in room")));Assert.That(leaves,Is.Zero);Assert.That(guard.Leaving,Is.False);}
    }
}
