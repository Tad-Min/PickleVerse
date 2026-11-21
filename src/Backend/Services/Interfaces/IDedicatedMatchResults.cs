using Entities.Game;

namespace Services.Interfaces;

public interface IDedicatedMatchResults
{
    Task CompleteDedicated(Guid match, List<DedicatedPlayerResult> input);
    Task InterruptDedicated(Guid match);
}
