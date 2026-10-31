namespace Services.Interfaces;

public interface IReadinessService
{
    Task<bool> Check(CancellationToken cancellationToken);
}
