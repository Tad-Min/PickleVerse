using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Entities.Enums;
using Entities.Models;
using Microsoft.Extensions.Logging;
using Repositories.UnitOfWork;
using Services.DTOs;
using Services.Exceptions;
using Services.Interfaces;

namespace Services.Implementations
{
    public class AdminService : IAdminService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AdminService> _logger;

        public AdminService(IUnitOfWork unitOfWork, ILogger<AdminService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<List<AdminUserDto>> GetUsersAsync(string? search, string? status, int page = 1, int pageSize = 20)
        {
            try
            {
                var users = await _unitOfWork.Users.GetPagedUsersAsync(search, status, page, pageSize);

                return users.Select(u => new AdminUserDto
                {
                    UserId = u.Userid,
                    Email = u.Email,
                    PlayerName = u.Playerprofile?.Playername ?? string.Empty,
                    RoleId = u.Roleid,
                    RoleName = u.Role?.Rolename ?? "User",
                    Status = u.Status,
                    Elo = u.Playerprofile?.Elo ?? 0,
                    TimePlayedSeconds = u.Playerprofile?.Timeplayedseconds ?? 0,
                    CreatedAt = u.Createdat
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách người dùng cho Admin");
                throw new AppException("Không thể tải danh sách người dùng.", ex);
            }
        }

        public async Task<bool> AssignRoleAsync(Guid userId, int roleId)
        {
            try
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                if (user == null)
                    throw new NotFoundException("Người dùng không tồn tại.");

                var role = await _unitOfWork.Users.GetRoleByIdAsync(roleId);
                if (role == null)
                    throw new NotFoundException("Vai trò không tồn tại.");

                user.Roleid = roleId;
                await _unitOfWork.Users.UpdateAsync(user);
                await _unitOfWork.SaveChangesAsync();
                return true;
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gán vai trò {RoleId} cho UserId {UserId}", roleId, userId);
                throw new AppException("Gán vai trò thất bại.", ex);
            }
        }

        public async Task<(bool Success, string Message)> BanUserAsync(Guid userId, string reason, DateTime? bannedUntil)
        {
            try
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                if (user == null)
                    throw new NotFoundException("Người dùng không tồn tại.");

                user.Status = UserStatus.Banned.ToString();
                await _unitOfWork.Users.UpdateAsync(user);
                await _unitOfWork.SaveChangesAsync();
                return (true, $"Tài khoản {user.Email} đã bị khóa. Lý do: {reason}");
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi khóa tài khoản UserId {UserId}", userId);
                throw new AppException("Khóa tài khoản thất bại.", ex);
            }
        }

        public async Task<(bool Success, string Message)> UnbanUserAsync(Guid userId)
        {
            try
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                if (user == null)
                    throw new NotFoundException("Người dùng không tồn tại.");

                user.Status = UserStatus.Active.ToString();
                await _unitOfWork.Users.UpdateAsync(user);
                await _unitOfWork.SaveChangesAsync();
                return (true, $"Mở khóa tài khoản {user.Email} thành công.");
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi mở khóa tài khoản UserId {UserId}", userId);
                throw new AppException("Mở khóa tài khoản thất bại.", ex);
            }
        }

        public async Task<bool> ResetPlayerEloAsync(Guid playerId, int defaultElo = 2000)
        {
            try
            {
                var profile = await _unitOfWork.Profiles.GetByUserIdAsync(playerId);
                if (profile == null)
                    throw new NotFoundException("Không tìm thấy hồ sơ người chơi.");

                profile.Elo = defaultElo;
                await _unitOfWork.Profiles.UpdateProfileAsync(profile);
                await _unitOfWork.SaveChangesAsync();
                return true;
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi reset Elo cho PlayerId {PlayerId}", playerId);
                throw new AppException("Reset Elo thất bại.", ex);
            }
        }

        public async Task<bool> ManualVerifyUserEmailAsync(Guid userId)
        {
            try
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                if (user == null)
                    throw new NotFoundException("Người dùng không tồn tại.");

                user.Status = UserStatus.Active.ToString();
                await _unitOfWork.Users.UpdateAsync(user);
                await _unitOfWork.SaveChangesAsync();
                return true;
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kích hoạt email thủ công cho UserId {UserId}", userId);
                throw new AppException("Kích hoạt tài khoản thất bại.", ex);
            }
        }

        public async Task<int> CreateLessonAsync(CreateLessonRequest request)
        {
            try
            {
                var lesson = new Lesson
                {
                    Lessonname = request.LessonName,
                    Description = request.Description,
                    Type = "Basic",
                    Target = 10,
                    Reward = "50 EXP"
                };
                return await _unitOfWork.Training.CreateLessonAsync(lesson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo bài học mới");
                throw new AppException("Tạo bài học mới thất bại.", ex);
            }
        }

        public async Task<int> CreateDrillAsync(CreateDrillRequest request)
        {
            try
            {
                var drill = new Drill
                {
                    Techniqueid = request.TechniqueId,
                    Drillname = request.DrillName,
                    Description = request.Description
                };
                var drillId = await _unitOfWork.Training.CreateDrillAsync(drill);

                var lessonDrill = new Lessondrill
                {
                    Lessonid = request.LessonId,
                    Drillid = drillId,
                    Target = 10
                };
                await _unitOfWork.Training.LinkDrillToLessonAsync(lessonDrill);

                return drillId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo bài tập Drill mới");
                throw new AppException("Tạo bài drill mới thất bại.", ex);
            }
        }

        public async Task<int> CreateCosmeticAsync(CreateCosmeticRequest request)
        {
            try
            {
                var cosmetic = new Cosmetic
                {
                    Itemcode = request.ItemCode,
                    Cosmeticname = request.CosmeticName,
                    Description = request.Description,
                    Price = request.Price,
                    Type = request.Type,
                    Currencyid = request.CurrencyId
                };
                return await _unitOfWork.Economy.CreateCosmeticAsync(cosmetic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm vật phẩm thời trang mới vào Shop");
                throw new AppException("Thêm vật phẩm mới thất bại.", ex);
            }
        }

        public async Task<int> CreateMissionAsync(CreateMissionRequest request)
        {
            try
            {
                var mission = new Mission
                {
                    Missionname = request.MissionName,
                    Description = request.Description,
                    Type = request.Type,
                    Target = request.Target,
                    Reward = request.Reward
                };
                return await _unitOfWork.Missions.CreateMissionAsync(mission);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo nhiệm vụ mới");
                throw new AppException("Tạo nhiệm vụ thất bại.", ex);
            }
        }

        public async Task<List<MatchItemDto>> GetRecentMatchesAsync(int limit = 50)
        {
            try
            {
                var matches = await _unitOfWork.Matches.GetRecentMatchesAsync(limit);

                var result = new List<MatchItemDto>();
                foreach (var m in matches)
                {
                    var replay = await _unitOfWork.Matches.GetTelemetryReplayAsync(m.Matchid);
                    result.Add(new MatchItemDto
                    {
                        MatchId = m.Matchid,
                        MatchType = m.Matchtype,
                        TeamAScore = m.Teamascore,
                        TeamBScore = m.Teambscore,
                        CreatedAt = m.Createdat,
                        ReplayUrl = replay?.Replayurl
                    });
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách trận đấu gần đây");
                throw new AppException("Không thể tải danh sách trận đấu.", ex);
            }
        }

        public async Task<List<AdminReportDto>> GetReportsAsync(string? status)
        {
            try
            {
                var reports = await _unitOfWork.Matches.GetReportsAsync(status);

                var result = new List<AdminReportDto>();
                foreach (var r in reports)
                {
                    var replay = await _unitOfWork.Matches.GetTelemetryReplayAsync(r.Matchid);
                    result.Add(new AdminReportDto
                    {
                        ReporterId = r.Reporterid,
                        ReporterName = r.Reporter?.Playername ?? string.Empty,
                        ReportedPlayerId = r.Reportedplayerid,
                        ReportedPlayerName = r.Reportedplayer?.Playername ?? string.Empty,
                        MatchId = r.Matchid,
                        Reason = r.Reason,
                        Status = r.Status,
                        CreatedAt = r.Createdat,
                        ReplayUrl = replay?.Replayurl
                    });
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách tố cáo cho Admin");
                throw new AppException("Không thể tải danh sách tố cáo.", ex);
            }
        }

        public async Task<(bool Success, string Message)> ReviewReportAsync(ReviewReportRequest request)
        {
            try
            {
                var report = await _unitOfWork.Matches.GetReportAsync(request.ReporterId, request.ReportedPlayerId, request.MatchId);
                if (report == null)
                    throw new NotFoundException("Đơn tố cáo không tồn tại.");

                report.Status = request.Status;
                report.Updatedat = DateTime.UtcNow;

                if (request.BanReportedPlayer)
                {
                    var reportedUser = await _unitOfWork.Users.GetByIdAsync(request.ReportedPlayerId);
                    if (reportedUser != null)
                    {
                        reportedUser.Status = UserStatus.Banned.ToString();
                        await _unitOfWork.Users.UpdateAsync(reportedUser);
                    }
                }

                await _unitOfWork.Matches.UpdateReportAsync(report);
                await _unitOfWork.SaveChangesAsync();
                return (true, $"Đã cập nhật trạng thái đơn tố cáo thành: {request.Status}");
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi duyệt đơn tố cáo của MatchId {MatchId}", request.MatchId);
                throw new AppException("Duyệt đơn tố cáo thất bại.", ex);
            }
        }

        public async Task<AdminAnalyticsResponse> GetAnalyticsSummaryAsync()
        {
            try
            {
                var totalUsers = await _unitOfWork.Users.GetTotalUsersCountAsync(null, null);
                var recentMatches = await _unitOfWork.Matches.GetRecentMatchesAsync(1000);
                var leaderboard = await _unitOfWork.Profiles.GetLeaderboardAsync(1000);
                var reports = await _unitOfWork.Matches.GetReportsAsync(ReportStatus.Pending.ToString());

                return new AdminAnalyticsResponse
                {
                    TotalUsers = totalUsers,
                    ActiveUsersToday = totalUsers,
                    TotalMatchesPlayed = recentMatches.Count,
                    TotalPlayTimeSeconds = leaderboard.Sum(p => p.Timeplayedseconds),
                    TotalReportsPending = reports.Count
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tổng hợp báo cáo Analytics");
                throw new AppException("Không thể tải dữ liệu thống kê tổng hợp.", ex);
            }
        }

        public async Task<List<AiTechniqueErrorHeatmapDto>> GetAiTechniqueErrorHeatmapAsync()
        {
            try
            {
                var techniques = await _unitOfWork.AiCoaching.GetAllTechniquesAsync();
                var allErrors = await _unitOfWork.AiCoaching.GetAllErrorsAsync();

                var result = new List<AiTechniqueErrorHeatmapDto>();
                foreach (var t in techniques)
                {
                    var techErrors = allErrors.Where(e => e.Techniqueid == t.Techniqueid).ToList();
                    var breakdown = techErrors
                        .GroupBy(e => e.Errortype)
                        .Select(g => new ErrorTypeBreakdownDto
                        {
                            ErrorType = g.Key,
                            Count = g.Count()
                        })
                        .ToList();

                    result.Add(new AiTechniqueErrorHeatmapDto
                    {
                        TechniqueId = t.Techniqueid,
                        TechniqueName = t.Techniquename,
                        TotalErrorsCount = techErrors.Count,
                        ErrorBreakdown = breakdown
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tổng hợp Bản đồ nhiệt kỹ thuật AI");
                throw new AppException("Không thể tải bản đồ nhiệt kỹ thuật AI.", ex);
            }
        }
    }
}
