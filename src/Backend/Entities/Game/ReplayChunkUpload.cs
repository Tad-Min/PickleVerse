namespace Entities.Game;
public sealed class ReplayChunkUpload { public string Data { get; set; } = ""; }

public sealed class ReplayChunkBatchUpload
{
    public const int MaxChunks = 4;
    public int StartIndex { get; set; }
    public List<ReplayChunkUpload> Chunks { get; set; } = [];
    public bool Complete { get; set; }
}

public sealed record ReplayBatchReceipt(int ChunkCount, bool Complete);
