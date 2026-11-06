using System.Data;
using Repositories.Game;

namespace Repositories.Interfaces;

public interface IAppActivityRepository
{
    Task<IRepositoryTransaction> BeginTransactionAsync(IsolationLevel isolation = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);
    Task<long?> LockActivityTotal(Guid player);
    Task<Dictionary<DateOnly, long>> ActivityDays(Guid player, DateOnly since);
    Task SetActivityTotal(Guid player, long total);
    Task SaveActivityDay(Guid player, DateOnly day, long seconds);
}
