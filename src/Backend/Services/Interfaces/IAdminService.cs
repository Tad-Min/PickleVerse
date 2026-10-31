using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Entities.DTOs;

namespace Services.Interfaces
{
    public interface IAdminService
    {
        // W01 - User Management & RBAC
        Task<List<AdminUserDto>> GetUsersAsync(string? search, string? status, int page = 1, int pageSize = 20);
        Task<bool> AssignRoleAsync(Guid userId, int roleId);
        Task<(bool Success, string Message)> BanUserAsync(Guid userId, string reason, DateTime? bannedUntil);
        Task<(bool Success, string Message)> UnbanUserAsync(Guid userId);
        Task<bool> ResetPlayerEloAsync(Guid playerId, int defaultElo = 2000);
        Task<bool> ManualVerifyUserEmailAsync(Guid userId);

        // W02 - Content CMS
        Task<int> CreateLessonAsync(CreateLessonRequest request);
        Task<int> CreateDrillAsync(CreateDrillRequest request);
        Task<int> CreateCosmeticAsync(CreateCosmeticRequest request);
        Task<int> CreateMissionAsync(CreateMissionRequest request);

        // W03 - Match Monitoring
        Task<List<MatchItemDto>> GetRecentMatchesAsync(int limit = 50);

        // W04 - Moderation & Tribunal
        Task<List<AdminReportDto>> GetReportsAsync(string? status);
        Task<(bool Success, string Message)> ReviewReportAsync(ReviewReportRequest request);

        // W05 - Analytics & Telemetry
        Task<AdminAnalyticsResponse> GetAnalyticsSummaryAsync();
        Task<List<AiTechniqueErrorHeatmapDto>> GetAiTechniqueErrorHeatmapAsync();
    }
}
