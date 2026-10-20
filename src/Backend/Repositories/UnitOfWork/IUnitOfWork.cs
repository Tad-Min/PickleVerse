using System;
using System.Threading.Tasks;
using Repositories.Interfaces;

namespace Repositories.UnitOfWork
{
    public interface IUnitOfWork : IDisposable
    {
        IUserRepository Users { get; }
        IPlayerProfileRepository Profiles { get; }
        IEconomyRepository Economy { get; }
        ITrainingRepository Training { get; }
        IAiCoachingRepository AiCoaching { get; }
        IMatchRepository Matches { get; }
        IFriendRepository Friends { get; }
        IMissionRepository Missions { get; }
        IAchievementRepository Achievements { get; }

        Task<int> SaveChangesAsync();
        int SaveChanges();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}
