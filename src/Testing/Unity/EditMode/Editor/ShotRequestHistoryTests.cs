using System;
using NUnit.Framework;
using PickleVerse.Multiplayer;

public sealed class ShotRequestHistoryTests
{
    [Test]
    public void OldRejectionCannotCancelTheNextPredictedStrike()
    {
        var history = new ShotRequestHistory();
        string a = Guid.NewGuid().ToString("N"), b = Guid.NewGuid().ToString("N");
        Assert.That(history.Add(a) && history.Add(b), Is.True);
        Assert.That(history.RejectCurrent(a), Is.False);
        Assert.That(history.Current, Is.EqualTo(b));
        Assert.That(history.Contains(a), Is.True, "A late server echo must still be recognized.");
        Assert.That(history.RejectCurrent(b), Is.True);
        Assert.That(history.Contains(b), Is.True, "Rejected predictions must not be replayed by an echo.");
    }
    [Test]
    public void DuplicateAndMalformedRequestsDoNotStartAnotherStrike()
    {
        var history = new ShotRequestHistory(); string id = Guid.NewGuid().ToString("N");
        Assert.That(history.Add(id), Is.True);
        Assert.That(history.Add(id) || history.Add("bad-id") || history.Add(null), Is.False);
        history.Clear();
        Assert.That(history.Contains(id) || history.RejectCurrent(null), Is.False);
    }
}
