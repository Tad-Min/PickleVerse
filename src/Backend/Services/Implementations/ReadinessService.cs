using Repositories;
using Services.Interfaces;

namespace Services.Implementations;

public sealed class ReadinessService(RepositoryReadiness readiness) : IReadinessService
{
    public Task<bool> Check(CancellationToken cancellationToken) => readiness.Check(cancellationToken);
}
