using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Caching;
using Repositories.Context;
using Repositories.Game;
using Repositories.Implementations;
using Repositories.Interfaces;
using Repositories.UnitOfWork;

namespace Repositories;

public static class DependencyInjection
{
    public static IServiceCollection AddPickleVerseRepositories(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection.");
        services.AddDbContext<PickleVerseContext>(options => options.UseNpgsql(connection));
        services.AddSingleton<RedisGameState>();
        services.AddSingleton<RedisAppActivity>();
        services.AddSingleton<LeaderboardCache>();
        services.AddSingleton<LobbyRepository>();
        services.AddSingleton<DedicatedLeaseRepository>();
        services.AddScoped<GameRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPlayerProfileRepository, PlayerProfileRepository>();
        services.AddScoped<IEconomyRepository, EconomyRepository>();
        services.AddScoped<ITrainingRepository, TrainingRepository>();
        services.AddScoped<IAiCoachingRepository, AiCoachingRepository>();
        services.AddScoped<IMatchRepository, MatchRepository>();
        services.AddScoped<IFriendRepository, FriendRepository>();
        services.AddScoped<IMissionRepository, MissionRepository>();
        services.AddScoped<IAchievementRepository, AchievementRepository>();
        services.AddScoped<RepositoryReadiness>();
        return services;
    }
}
