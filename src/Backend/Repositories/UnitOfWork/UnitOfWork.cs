using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;
using Repositories.Context;
using Repositories.Implementations;
using Repositories.Interfaces;
using Repositories.Game;

namespace Repositories.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly PickleVerseContext _context;
        private IDbContextTransaction? _transaction;

        private IUserRepository? _users;
        private IPlayerProfileRepository? _profiles;
        private IEconomyRepository? _economy;
        private ITrainingRepository? _training;
        private IAiCoachingRepository? _aiCoaching;
        private IMatchRepository? _matches;
        private IFriendRepository? _friends;
        private IMissionRepository? _missions;
        private IAchievementRepository? _achievements;
        private GameRepository? _game;

        public UnitOfWork(PickleVerseContext context)
        {
            _context = context;
        }

        public IUserRepository Users => _users ??= new UserRepository(_context);
        public IPlayerProfileRepository Profiles => _profiles ??= new PlayerProfileRepository(_context);
        public IEconomyRepository Economy => _economy ??= new EconomyRepository(_context);
        public ITrainingRepository Training => _training ??= new TrainingRepository(_context);
        public IAiCoachingRepository AiCoaching => _aiCoaching ??= new AiCoachingRepository(_context);
        public IMatchRepository Matches => _matches ??= new MatchRepository(_context);
        public IFriendRepository Friends => _friends ??= new FriendRepository(_context);
        public IMissionRepository Missions => _missions ??= new MissionRepository(_context);
        public IAchievementRepository Achievements => _achievements ??= new AchievementRepository(_context);
        public IAppActivityRepository Activity => Game;
        public GameRepository Game => _game ??= new GameRepository(_context);

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public int SaveChanges()
        {
            return _context.SaveChanges();
        }

        public async Task BeginTransactionAsync()
        {
            _transaction = await _context.Database.BeginTransactionAsync();
        }

        public async Task CommitTransactionAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
                if (_transaction != null)
                {
                    await _transaction.CommitAsync();
                }
            }
            finally
            {
                if (_transaction != null)
                {
                    await _transaction.DisposeAsync();
                    _transaction = null;
                }
            }
        }

        public async Task RollbackTransactionAsync()
        {
            try
            {
                if (_transaction != null)
                {
                    await _transaction.RollbackAsync();
                }
            }
            finally
            {
                if (_transaction != null)
                {
                    await _transaction.DisposeAsync();
                    _transaction = null;
                }
            }
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            _context.Dispose();
        }
    }
}
