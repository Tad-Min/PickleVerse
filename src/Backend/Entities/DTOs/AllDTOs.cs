using System.ComponentModel.DataAnnotations;

namespace Entities.DTOs
{
    // Auth
    public class RegisterRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu.")]
        [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận chưa khớp.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public bool IsMale { get; set; } = true;
    }

    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class VerifyEmailRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^\d{6}$")]
        public string Token { get; set; } = string.Empty;
    }

    public class ResendOtpRequest
    {
        [Required]
        [EmailAddress]
        [MaxLength(254)]
        public string Email { get; set; } = string.Empty;
    }

    public class ResendOtpResponse
    {
        public int OtpLifetimeMinutes { get; set; }
        public int OtpResendCooldownSeconds { get; set; }
        public string Code { get; set; } = "verification_requested";
        public string Message { get; set; } = "Nếu email thuộc tài khoản chưa xác thực, mã mới sẽ được gửi. Hãy kiểm tra cả mục thư rác.";
    }

    public class RefreshTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public int OtpLifetimeMinutes { get; set; }
        public int OtpResendCooldownSeconds { get; set; }
        public string Code { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public PlayerProfileResponse? Profile { get; set; }
    }

    // Profile
    public class PlayerProfileResponse
    {
        public Guid UserId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public int Elo { get; set; }
        public bool IsMale { get; set; }
        public int Level { get; set; }
        public int Exp { get; set; }
        public int WinStreak { get; set; }
        public int DayStreak { get; set; }
        public int WinCount { get; set; }
        public int LoseCount { get; set; }
        public double WinRate { get; set; }
        public long TimePlayedSeconds { get; set; }
        public string TimePlayedFormatted { get; set; } = string.Empty;
    }

    public class UpdateProfileRequest
    {
        [MinLength(3)]
        [MaxLength(18)]
        public string? PlayerName { get; set; }
        public string? Avatar { get; set; }
        public bool? IsMale { get; set; }
    }

    public class SyncTimeRequest
    {
        public long PlayedSeconds { get; set; }
    }

    // Training
    public class LessonDto
    {
        public int LessonId { get; set; }
        public string LessonName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "LOCKED"; // UNLOCKED, COMPLETED, LOCKED
        public List<DrillDto> Drills { get; set; } = new List<DrillDto>();
    }

    public class DrillDto
    {
        public int DrillId { get; set; }
        public int TechniqueId { get; set; }
        public string TechniqueName { get; set; } = string.Empty;
        public string DrillName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Target { get; set; }
        public int BestScore { get; set; }
        public bool IsClaimedReward { get; set; }
    }

    public class SubmitDrillRequest
    {
        public int LessonId { get; set; }
        public int DrillId { get; set; }
        public int Score { get; set; } // 1 - 3
        public int TotalAttempts { get; set; } = 1;
        public int TotalErrors { get; set; } = 0;
    }

    public class SubmitDrillResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int ExpGained { get; set; }
        public bool LevelUp { get; set; }
        public int NewLevel { get; set; }
        public int NewExp { get; set; }
    }

    // AI Coaching
    public class MasteryDto
    {
        public int TechniqueId { get; set; }
        public string TechniqueName { get; set; } = string.Empty;
        public int MasteryScore { get; set; } // % 0 - 100
        public int TotalAttempts { get; set; }
        public int TotalErrors { get; set; }
    }

    public class RecommendationResponse
    {
        public string WeakestTechniqueName { get; set; } = string.Empty;
        public int CurrentMasteryScore { get; set; }
        public string CoachMessage { get; set; } = string.Empty;
        public List<DrillDto> RecommendedDrills { get; set; } = new List<DrillDto>();
    }

    public class LogTechniqueErrorRequest
    {
        public int TechniqueId { get; set; }
        public Guid? MatchId { get; set; }
        public int? LessonId { get; set; }
        public string ErrorType { get; set; } = string.Empty;
        public string CoachingTip { get; set; } = string.Empty;
    }

    // Match
    public class FinishMatchRequest
    {
        public string MatchType { get; set; } = "1Vs1";
        public int TeamAScore { get; set; }
        public int TeamBScore { get; set; }
        public int DurationSeconds { get; set; }
        public string ReplayUrl { get; set; } = string.Empty;
        public List<PlayerMatchItem> Players { get; set; } = new List<PlayerMatchItem>();
    }

    public class PlayerMatchItem
    {
        public Guid PlayerId { get; set; }
        public bool IsTeamA { get; set; }
    }

    public class MatchItemDto
    {
        public Guid MatchId { get; set; }
        public string MatchType { get; set; } = string.Empty;
        public int TeamAScore { get; set; }
        public int TeamBScore { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsWinner { get; set; }
        public int ReceivedElo { get; set; }
        public string? ReplayUrl { get; set; }
    }

    // Social
    public class SendFriendRequestDto
    {
        public Guid ReceiverId { get; set; }
    }

    public class RespondFriendRequestDto
    {
        public int RequestId { get; set; }
        public bool Accept { get; set; }
    }

    public class FriendDto
    {
        public Guid FriendId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public int Level { get; set; }
        public int Elo { get; set; }
    }

    // Moderation
    public class ReportPlayerRequest
    {
        public Guid ReportedPlayerId { get; set; }
        public Guid MatchId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class BanUserRequest
    {
        public Guid UserId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime? BannedUntil { get; set; }
    }

    // Economy & Cosmetics
    public class CurrencyDto
    {
        public int CurrencyId { get; set; }
        public string CurrencyName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Amount { get; set; }
    }

    public class CosmeticShopItemDto
    {
        public int CosmeticId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string CosmeticName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Price { get; set; }
        public string Type { get; set; } = string.Empty; // PADDLE, OUTFIT, COURT, BALL, TITLE, EMOTE
        public int CurrencyId { get; set; }
        public string CurrencyName { get; set; } = string.Empty;
        public bool IsOwned { get; set; }
        public bool IsEquipped { get; set; }
    }

    public class PlayerCosmeticDto
    {
        public int CosmeticId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string CosmeticName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool Equipped { get; set; }
    }

    public class BuyCosmeticRequest
    {
        public int CosmeticId { get; set; }
    }

    public class EquipCosmeticRequest
    {
        public int CosmeticId { get; set; }
        public bool Equip { get; set; } = true;
    }

    // Missions & Quests
    public class MissionDto
    {
        public int MissionId { get; set; }
        public string MissionName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // DAILY, WEEKLY, EVENT
        public int Target { get; set; }
        public string Reward { get; set; } = string.Empty;
        public int CurrentProgress { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsClaimed { get; set; }
    }

    // Achievements & Badges
    public class AchievementDto
    {
        public int AchievementId { get; set; }
        public string AchievementCode { get; set; } = string.Empty;
        public string AchievementName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Target { get; set; }
        public string Reward { get; set; } = string.Empty;
        public int CurrentProgress { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsClaimed { get; set; }
    }

    // Replay System
    public class SaveReplayRequest
    {
        public Guid MatchId { get; set; }
        public string ReplayData { get; set; } = string.Empty; // JSON or telemetry stream
    }

    public class MatchReplayResponse
    {
        public Guid MatchId { get; set; }
        public string ReplayUrl { get; set; } = string.Empty;
        public string? ReplayContent { get; set; }
    }

    // Admin & CMS & Telemetry
    public class AdminUserDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Elo { get; set; }
        public long TimePlayedSeconds { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AssignRoleRequest
    {
        public Guid UserId { get; set; }
        public int RoleId { get; set; }
    }

    public class AdminReportDto
    {
        public Guid ReporterId { get; set; }
        public string ReporterName { get; set; } = string.Empty;
        public Guid ReportedPlayerId { get; set; }
        public string ReportedPlayerName { get; set; } = string.Empty;
        public Guid MatchId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? ReplayUrl { get; set; }
    }

    public class ReviewReportRequest
    {
        public Guid ReporterId { get; set; }
        public Guid ReportedPlayerId { get; set; }
        public Guid MatchId { get; set; }
        public string Status { get; set; } = "RESOLVED"; // RESOLVED, DISMISSED
        public bool BanReportedPlayer { get; set; } = false;
        public string? BanReason { get; set; }
    }

    public class AdminAnalyticsResponse
    {
        public int TotalUsers { get; set; }
        public int ActiveUsersToday { get; set; }
        public int TotalMatchesPlayed { get; set; }
        public long TotalPlayTimeSeconds { get; set; }
        public int TotalReportsPending { get; set; }
    }

    public class AiTechniqueErrorHeatmapDto
    {
        public int TechniqueId { get; set; }
        public string TechniqueName { get; set; } = string.Empty;
        public int TotalErrorsCount { get; set; }
        public List<ErrorTypeBreakdownDto> ErrorBreakdown { get; set; } = new List<ErrorTypeBreakdownDto>();
    }

    public class ErrorTypeBreakdownDto
    {
        public string ErrorType { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class CreateLessonRequest
    {
        [Required]
        public string LessonName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int RequiredLevel { get; set; } = 1;
        public int Order { get; set; } = 1;
    }

    public class CreateDrillRequest
    {
        public int LessonId { get; set; }
        public int TechniqueId { get; set; }
        [Required]
        public string DrillName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Target { get; set; } = 10;
        public int Order { get; set; } = 1;
    }

    public class CreateCosmeticRequest
    {
        [Required]
        public string ItemCode { get; set; } = string.Empty;
        [Required]
        public string CosmeticName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Price { get; set; } = 100;
        public string Type { get; set; } = "PADDLE";
        public int CurrencyId { get; set; } = 1;
    }

    public class CreateMissionRequest
    {
        [Required]
        public string MissionName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = "DAILY"; // DAILY, WEEKLY
        public int Target { get; set; } = 5;
        public string Reward { get; set; } = "50 Gold";
    }
}
