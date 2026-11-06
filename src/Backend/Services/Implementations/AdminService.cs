using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Entities.Enums;
using Entities.Models;
using Microsoft.Extensions.Logging;
using Repositories.UnitOfWork;
using Entities.DTOs;
using Entities.Exceptions;
using Services.Interfaces;

namespace Services.Implementations
{
    public class AdminService : IAdminService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AdminService> _logger;
        private readonly IContentService _content;

        public AdminService(IUnitOfWork unitOfWork, ILogger<AdminService> logger, IContentService content)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _content = content;
        }

        public async Task<List<AdminUserDto>> GetUsersAsync(string? search, UserStatus? status, int page = 1, int pageSize = 20)
        {
            try
            {
                var users = await _unitOfWork.Users.GetPagedUsersAsync(search, status, page, pageSize);

                return users.Select(u => new AdminUserDto
                {
                    UserId = u.Userid,
                    EmailVerified = u.Verificationemails.OrderByDescending(v => v.Createdat).ThenByDescending(v => v.Verificationemailid).FirstOrDefault()?.Isverified == true,
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

        public async Task<bool> AssignRoleAsync(Guid actor, Guid userId, int roleId)
        {
            try
            {
                await using var tx = await _unitOfWork.Game.BeginTransactionAsync();
                if (!await _unitOfWork.Game.IsAdmin(actor)) throw new ForbiddenException("Bạn không có quyền gán vai trò.");
                await _unitOfWork.Users.LockUserAsync(userId);
                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                if (user == null)
                    throw new NotFoundException("Người dùng không tồn tại.");

                var role = await _unitOfWork.Users.GetRoleByIdAsync(roleId);
                if (role == null)
                    throw new NotFoundException("Vai trò không tồn tại.");

                user.Roleid = roleId;
                user.Authversion++;
                await _unitOfWork.Users.RevokeAllRefreshTokensAsync(userId);
                _unitOfWork.Game.AddAudit(new AdminAuditRecord { AuditId = Guid.NewGuid(), ActorId = actor, TargetId = userId,
                    Action = "ROLE_ASSIGN", Details = GameJson.Write(new { roleId }), CreatedAt = DateTime.UtcNow });
                await _unitOfWork.Users.UpdateAsync(user);
                await _unitOfWork.SaveChangesAsync();
                await tx.CommitAsync();
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

        public Task<bool> ResetPlayerEloAsync(Guid playerId, int defaultElo = 2000) =>
            throw new AppException("Hãy dùng chức năng đặt lại tiến trình có bước xem trước tác động.", System.Net.HttpStatusCode.Gone);

        public async Task<bool> ManualVerifyUserEmailAsync(Guid userId)
        {
            try
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                if (user == null)
                    throw new NotFoundException("Người dùng không tồn tại.");

                var verification = await _unitOfWork.Users.GetLatestVerificationEmailAsync(userId)
                    ?? throw new NotFoundException("Không tìm thấy yêu cầu xác thực email.");
                verification.Isverified = true;
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

        public Task<int> CreateLessonAsync(CreateLessonRequest request) => _content.Create(request);
        public Task<int> CreateDrillAsync(CreateDrillRequest request) => _content.UpdateDrill(request);

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
            if (request.Type is not ("DAILY" or "WEEKLY") || request.Target < 1 || request.Target > 100000
                || !Enum.IsDefined(request.Metric) || request.Reward.coins < 0 || request.Reward.gems < 0 || request.Reward.xp < 0)
                throw new AppException("Cấu hình nhiệm vụ không hợp lệ.", System.Net.HttpStatusCode.BadRequest);
            try
            {
                var scope = request.Type == "DAILY" ? PickleVerse.Progression.ProgressScope.Daily : PickleVerse.Progression.ProgressScope.Weekly;
                string code = "CMS." + Guid.NewGuid().ToString("N");
                var mission = new Mission
                {
                    Code = code,
                    Config = GameJson.Write(new PickleVerse.Progression.ProgressDefinition(code, request.MissionName, scope, request.Metric, request.Target, request.Reward)),
                    Missionname = request.MissionName,
                    Description = request.Description,
                    Type = request.Metric.ToString(),
                    Target = request.Target,
                    Reward = GameJson.Write(request.Reward)
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
                    var score = Repositories.Game.MatchScoreStore.GameWins(m.Matchscores);
                    result.Add(new MatchItemDto
                    {
                        MatchId = m.Matchid,
                        MatchType = m.Kind,
                        TeamAScore = score.teamA,
                        TeamBScore = score.teamB,
                        CreatedAt = m.Endedat!.Value,
                        ReplayUrl = replay == null ? null : $"/api/Replays/{m.Matchid}"
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

        public async Task<List<AdminReportDto>> GetReportsAsync(ReportStatus? status)
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
                        ReviewerId = r.ReviewerId,
                        DecisionReason = r.DecisionReason,
                        CreatedAt = r.Createdat,
                        ReplayUrl = replay == null ? null : $"/api/Replays/{r.Matchid}"
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

    }
}
