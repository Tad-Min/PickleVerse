using Entities.Game;

namespace Services.Interfaces;

public interface IDedicatedMatchResults
{
    Task CompleteDedicated(Guid match, List<DedicatedPlayerResult> input, int? replayChunkCount = null);
    Task InterruptDedicated(Guid match);
}
