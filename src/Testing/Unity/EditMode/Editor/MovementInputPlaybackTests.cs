using NUnit.Framework;
using PickleVerse.Multiplayer;

public sealed class MovementInputPlaybackTests
{
    [Test] public void AckWaitsForBothSimulationSubsteps()
    {
        var playback = new MovementInputPlayback();
        MovementInputFrame? Read(uint seq) => new MovementInputFrame { sequence = seq };
        Assert.That(playback.Take(1f/120, 1f/60, Read, out _, out _), Is.True);
        Assert.That(playback.Acknowledged, Is.Zero);
        playback.Take(1f/120, 1f/60, Read, out _, out _);
        Assert.That(playback.Acknowledged, Is.EqualTo(1));
    }
    [Test] public void BatchedSnapshotsDoNotSkipUnsimulatedInputs()
    {
        var playback = new MovementInputPlayback();
        MovementInputFrame? Read(uint seq) => seq <= 3 ? new MovementInputFrame { sequence = seq } : (MovementInputFrame?)null;
        for (int i = 0; i < 6; i++) playback.Take(1f/120, 1f/60, Read, out _, out _);
        Assert.That(playback.Acknowledged, Is.EqualTo(3));
        Assert.That(playback.Take(1f/120, 1f/60, Read, out _, out _), Is.False);
        Assert.That(playback.Acknowledged, Is.EqualTo(3));
    }
    [Test] public void MissingInputNeverAdvancesAck()
    {
        var playback = new MovementInputPlayback(); playback.Reset(12);
        playback.Take(1f/60, 1f/60, _ => null, out _, out _);
        Assert.That(playback.Acknowledged, Is.EqualTo(12));
    }
    [Test] public void RebindingDiscardsPartiallySimulatedInputFromPreviousPeer()
    {
        var playback = new MovementInputPlayback();
        MovementInputFrame? Read(uint seq) => new MovementInputFrame { sequence = seq };
        playback.Take(1f/120, 1f/60, Read, out _, out _);
        playback.Reset(20);
        playback.Take(1f/120, 1f/60, Read, out _, out _);
        Assert.That(playback.Acknowledged, Is.EqualTo(20));
        playback.Take(1f/120, 1f/60, Read, out _, out _);
        Assert.That(playback.Acknowledged, Is.EqualTo(21));
    }
}
