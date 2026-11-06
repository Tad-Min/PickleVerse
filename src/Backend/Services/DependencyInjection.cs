using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Repositories;
using Services.Configuration;
using Services.Implementations;
using Services.Interfaces;
using Services.Workers;

namespace Services;

public static class DependencyInjection
{
    public static IServiceCollection AddPickleVerseServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPickleVerseRepositories(configuration);
        services.AddSingleton(new ServerSettings(configuration));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<PasswordRecoveryMailQueue>();
        services.AddHostedService(sp => sp.GetRequiredService<PasswordRecoveryMailQueue>());
        services.AddScoped<IStorageService, StorageService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ITrainingService, TrainingService>();
        services.AddScoped<IAiCoachingService, AiCoachingService>();
        services.AddScoped<IMatchService, MatchService>();
        services.AddScoped<IFriendService, FriendService>();
        services.AddScoped<IEconomyService, EconomyService>();
        services.AddScoped<IMissionService, MissionService>();
        services.AddScoped<IAchievementService, AchievementService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IContentService, ContentService>();
        services.AddScoped<IProgressResetService, ProgressResetService>();
        services.AddScoped<IModerationService, ModerationService>();
        services.AddHostedService<ModerationExpiryWorker>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IExpertReviewService, ExpertReviewService>();
        services.AddHostedService<AnalyticsProjectionWorker>();
        services.AddScoped<IReadinessService, ReadinessService>();
        services.AddScoped<GameCatalogService>();
        services.AddScoped<IGameCatalogService>(sp => sp.GetRequiredService<GameCatalogService>());
        services.AddScoped<OnlineGameService>();
        services.AddScoped<IOnlineGameService>(sp => sp.GetRequiredService<OnlineGameService>());
        services.AddScoped<IGameTrainingService>(sp => sp.GetRequiredService<OnlineGameService>());
        services.AddScoped<IGameMatchService>(sp => sp.GetRequiredService<OnlineGameService>());
        services.AddScoped<IDedicatedMatchResults>(sp => sp.GetRequiredService<OnlineGameService>());
        services.AddScoped<IAppActivityService, AppActivityService>();
        services.AddScoped<OnlineSocialService>();
        services.AddScoped<IOnlineSocialService>(sp => sp.GetRequiredService<OnlineSocialService>());
        services.AddScoped<RoomInvitationService>();
        services.AddScoped<IRoomInvitationService>(sp => sp.GetRequiredService<RoomInvitationService>());
        services.AddScoped<MatchReplayService>();
        services.AddScoped<IMatchReplayService>(sp => sp.GetRequiredService<MatchReplayService>());
        services.AddScoped<HybridCoachingService>();
        services.AddScoped<IHybridCoachingService>(sp => sp.GetRequiredService<HybridCoachingService>());
        services.AddSingleton(sp => sp.GetRequiredService<ServerSettings>().HybridAi);
        services.AddHttpClient("hybrid-ai", client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddHostedService<HybridAiWorker>();
        services.AddScoped<IDedicatedServerService, DedicatedServerService>();
        services.AddHostedService<DedicatedLeaseMonitor>();
        return services;
    }
}
