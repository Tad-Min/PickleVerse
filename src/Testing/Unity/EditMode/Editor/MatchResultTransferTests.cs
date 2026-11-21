using System;
using NUnit.Framework;
using PickleVerse.Multiplayer;

public sealed class MatchResultTransferTests
{
    [Test]
    public void ReorderedDuplicatedChunksPreserveVietnameseAndSurrogatePairs()
    {
        string text = "{\"tên\":\"Người chơi 🎾\",\"dữ liệu\":\"" + new string('ế', 9000) + "\"}";
        var parts = MatchResultTransfer.Encode(text);
        var receiver = new MatchResultTransfer();
        string id = Guid.NewGuid().ToString("N"), result = null;
        for (int i = parts.Length - 1; i >= 0; i--)
        {
            Assert.That(parts[i].Length, Is.LessThanOrEqualTo(1000));
            bool completed = receiver.Add(id, i, parts.Length, parts[i], out var decoded);
            if (completed) result = decoded;
            receiver.Add(id, i, parts.Length, parts[i], out _);
        }
        Assert.That(result, Is.EqualTo(text));
    }
    [Test]
    public void IncompleteConflictingAndOversizedTransfersDoNotProduceAResult()
    {
        var receiver = new MatchResultTransfer(); string id = Guid.NewGuid().ToString("N");
        Assert.That(receiver.Add(id, 0, 2, "YQ==", out _), Is.False);
        Assert.That(receiver.Add(id, 0, 2, "Yg==", out _), Is.False);
        Assert.That(receiver.Add(id, 1, 999, "", out _), Is.False);
        Assert.That(receiver.Add(id, -1, 2, "", out _), Is.False);
        Assert.Throws<ArgumentException>(() => MatchResultTransfer.Encode(new string('x', 65537)));
    }
}
