#if UNITY_EDITOR_WIN
using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PickleVerse.FrontEnd;

namespace PickleVerse.Tests
{
    public sealed class WindowsReplayBridgeTests
    {
        [Test] public void WindowsCommandQuotesExecutableAndOnlyOneUriArgument()
        {
            Assert.That(WindowsReplayBridge.Command(@"D:\Game Folder\PickleVerse.exe"), Is.EqualTo("\"D:\\Game Folder\\PickleVerse.exe\" \"%1\""));
            Assert.Throws<ArgumentException>(() => WindowsReplayBridge.Command("game.exe"));
            Assert.Throws<ArgumentException>(() => WindowsReplayBridge.Command("D:\\evil\".exe"));
        }
        [Test] public void SameUserBrokerForwardsAndSurvivesPrimaryExit()
        {
            var first = new TaskCompletionSource<string>(); var next = new TaskCompletionSource<string>();
            string backend = "https://replay-" + Guid.NewGuid().ToString("N") + ".example.invalid";
            string link = "pickleverse://replay/879f2b5bd5eb4c7e979020a3ee98ee79?code=" + new string('a', 64);
            var a = new WindowsReplayBridge(backend, uri => first.TrySetResult(uri));
            try
            {
                using (var b = new WindowsReplayBridge(backend, uri => next.TrySetResult(uri)))
                {
                    Assert.That(a.IsPrimary, Is.True); Assert.That(b.IsPrimary, Is.False);
                    Assert.That(b.Forward(link), Is.True, a.Failure);
                    Assert.That(first.Task.Wait(3000), Is.True);
                    Assert.That(first.Task.Result, Is.EqualTo(link)); Assert.That(b.Forward("invalid"), Is.False);
                    a.Dispose();
                    Assert.That(SpinWait.SpinUntil(() => b.IsPrimary, 1000), Is.True, b.Failure);
                    Assert.That(b.IsPrimary, Is.True, b.Failure);
                    using (var c = new WindowsReplayBridge(backend, _ => { }))
                        Assert.That(c.Forward(link), Is.True, b.Failure);
                    Assert.That(next.Task.Wait(3000), Is.True);
                    Assert.That(next.Task.Result, Is.EqualTo(link));
                }
            }
            finally { a.Dispose(); }
        }
    }
}
#endif
