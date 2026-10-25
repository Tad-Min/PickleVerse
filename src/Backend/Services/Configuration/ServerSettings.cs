using Microsoft.Extensions.Configuration;
using PickleVerse.Progression;
using Services.Game;

namespace Services.Configuration;

/// <summary>Validated startup configuration. Only public gameplay settings enter the Unity catalog.</summary>
public sealed class ServerSettings
{
    public sealed record SmtpSettings(string Host, int Port, string FromName, string FromEmail, string Username, string Password);
    public sealed record OtpSettings(int LifetimeMinutes, int ResendCooldownSeconds, int MaxAttempts);
    public sealed record AuthenticationSettings(int AccessTokenHours, int RefreshTokenDays);
    public sealed record MatchmakingSettings(int BotFallbackSeconds, int ReadyConfirmationSeconds);
    public sealed record OnlineMatchSettings(int DisconnectTimeoutSeconds, int HeartbeatSeconds);
    public sealed record RewardSettings(int Coins, int Gems, int Xp)
    {
        public RewardBundle Bundle() => new(Coins, Gems, Xp);
    }
    public sealed record ProgressionSettings(int WinXp, int LossXp, RewardSettings DailyBonus, RewardSettings WeeklyBonus,
        int BotWinReward, int BotLossReward, int OnlineWinReward, int OnlineLossReward);

    public SmtpSettings Smtp { get; }
    public OtpSettings Otp { get; }
    public AuthenticationSettings Authentication { get; }
    public MatchmakingSettings Matchmaking { get; }
    public OnlineMatchSettings OnlineMatch { get; }
    public ProgressionSettings Progression { get; }

    public ServerSettings(IConfiguration configuration)
    {
        int Number(string key, int minimum, int maximum)
        {
            if (!int.TryParse(configuration[key], out int value) || value < minimum || value > maximum)
                throw new InvalidOperationException($"Configuration '{key}' must be an integer between {minimum} and {maximum}.");
            return value;
        }
        string Text(string key)
        {
            var value = configuration[key];
            return string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"Configuration '{key}' is required.") : value;
        }
        RewardSettings Reward(string key) => new(Number(key + ":coins", 0, int.MaxValue), Number(key + ":gems", 0, int.MaxValue), Number(key + ":xp", 0, int.MaxValue));
        Smtp = new(Text("Smtp:Host"), Number("Smtp:Port", 1, 65535), Text("Smtp:FromName"),
            configuration["Smtp:FromEmail"] ?? "", configuration["Smtp:Username"] ?? "", configuration["Smtp:Password"] ?? "");
        Otp = new(Number("Otp:LifetimeMinutes", 1, 1440), Number("Otp:ResendCooldownSeconds", 1, 86400), Number("Otp:MaxAttempts", 1, 100));
        Authentication = new(Number("Authentication:AccessTokenHours", 1, 8760), Number("Authentication:RefreshTokenDays", 1, 3650));
        Matchmaking = new(Number("Matchmaking:BotFallbackSeconds", 1, 86400), Number("Matchmaking:ReadyConfirmationSeconds", 1, 3600));
        OnlineMatch = new(Number("OnlineMatch:DisconnectTimeoutSeconds", 2, 3600), Number("OnlineMatch:HeartbeatSeconds", 1, 3599));
        if (OnlineMatch.HeartbeatSeconds >= OnlineMatch.DisconnectTimeoutSeconds)
            throw new InvalidOperationException("OnlineMatch:HeartbeatSeconds must be less than OnlineMatch:DisconnectTimeoutSeconds.");
        Progression = new(Number("Progression:winXp", 0, int.MaxValue), Number("Progression:lossXp", 0, int.MaxValue),
            Reward("Progression:dailyBonus"), Reward("Progression:weeklyBonus"),
            Number("Progression:matchRewards:botWinReward", 0, int.MaxValue), Number("Progression:matchRewards:botLossReward", 0, int.MaxValue),
            Number("Progression:matchRewards:onlineWinReward", 0, int.MaxValue), Number("Progression:matchRewards:onlineLossReward", 0, int.MaxValue));
    }

    public GameCatalog CreateCatalog() => new()
    {
        winXp = Progression.WinXp,
        lossXp = Progression.LossXp,
        dailyBonus = Progression.DailyBonus.Bundle(),
        weeklyBonus = Progression.WeeklyBonus.Bundle(),
        matchRewards = new MatchRewards
        {
            botWinReward = Progression.BotWinReward,
            botLossReward = Progression.BotLossReward,
            onlineWinReward = Progression.OnlineWinReward,
            onlineLossReward = Progression.OnlineLossReward
        },
        matchmaking = new RuntimeMatchmakingSettings { botFallbackSeconds = Matchmaking.BotFallbackSeconds, readyConfirmationSeconds = Matchmaking.ReadyConfirmationSeconds },
        onlineMatch = new RuntimeOnlineMatchSettings { disconnectTimeoutSeconds = OnlineMatch.DisconnectTimeoutSeconds, heartbeatSeconds = OnlineMatch.HeartbeatSeconds }
    };
}
