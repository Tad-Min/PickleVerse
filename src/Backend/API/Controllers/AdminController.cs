using Entities.Enums;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Entities.DTOs;
using Services.Interfaces;

namespace API.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly IModerationService _moderation;
        Guid Actor => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

        public AdminController(IAdminService adminService, IModerationService moderation)
        {
            _adminService = adminService;
            _moderation = moderation;
        }

        // W01 - User Management & RBAC
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers([FromQuery] string? search, [FromQuery] UserStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var list = await _adminService.GetUsersAsync(search, status, page, pageSize);
            return Ok(list);
        }

        [HttpPost("users/assign-role")]
        public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request)
        {
            var success = await _adminService.AssignRoleAsync(Actor, request.UserId, request.RoleId);
            if (!success)
                return BadRequest(new { message = "Gán vai trò thất bại." });
            return Ok(new { message = "Gán vai trò thành công." });
        }

        [HttpPost("users/ban")]
        public async Task<IActionResult> BanUser([FromBody] BanUserRequest request)
        {
            return Ok(await _moderation.Apply(Actor, new SanctionRequest { UserId = request.UserId, Reason = request.Reason,
                Restriction = request.Restriction, ExpiresAt = request.BannedUntil }));
        }

        [HttpPost("users/{userId}/unban")]
        public async Task<IActionResult> UnbanUser(Guid userId, [FromBody] RevokeSanctionRequest request)
        {
            await _moderation.Revoke(Actor, userId, request.Reason);
            return Ok(new { message = "Đã thu hồi các quyết định hạn chế đang hiệu lực." });
        }

        [HttpPost("users/{playerId}/reset-elo")]
        public async Task<IActionResult> ResetPlayerElo(Guid playerId, [FromQuery] int defaultElo = 2000)
        {
            var success = await _adminService.ResetPlayerEloAsync(playerId, defaultElo);
            if (!success)
                return BadRequest(new { message = "Reset Elo thất bại." });
            return Ok(new { message = $"Đã reset điểm Elo của người chơi về {defaultElo}." });
        }

        [HttpPost("users/{userId}/verify-email")]
        public async Task<IActionResult> ManualVerifyUserEmail(Guid userId)
        {
            var success = await _adminService.ManualVerifyUserEmailAsync(userId);
            if (!success)
                return BadRequest(new { message = "Kích hoạt tài khoản thất bại." });
            return Ok(new { message = "Kích hoạt tài khoản người dùng thành công." });
        }

        // W02 - Content CMS
        [HttpPost("cms/lessons")]
        public async Task<IActionResult> CreateLesson([FromBody] CreateLessonRequest request)
        {
            var id = await _adminService.CreateLessonAsync(request);
            return Ok(new { lessonId = id, message = "Tạo bài học mới thành công!" });
        }

        [HttpPost("cms/drills")]
        public async Task<IActionResult> CreateDrill([FromBody] CreateDrillRequest request)
        {
            var id = await _adminService.CreateDrillAsync(request);
            return Ok(new { drillId = id, message = "Tạo bài tập Drill mới thành công!" });
        }

        [HttpPost("cms/cosmetics")]
        public async Task<IActionResult> CreateCosmetic([FromBody] CreateCosmeticRequest request)
        {
            var id = await _adminService.CreateCosmeticAsync(request);
            return Ok(new { cosmeticId = id, message = "Đăng bán vật phẩm thời trang mới vào Shop thành công!" });
        }

        [HttpPost("cms/missions")]
        public async Task<IActionResult> CreateMission([FromBody] CreateMissionRequest request)
        {
            var id = await _adminService.CreateMissionAsync(request);
            return Ok(new { missionId = id, message = "Tạo nhiệm vụ sự kiện mới thành công!" });
        }

        // W03 - Match Monitoring
        [HttpGet("matches/recent")]
        public async Task<IActionResult> GetRecentMatches([FromQuery] int limit = 50)
        {
            var list = await _adminService.GetRecentMatchesAsync(limit);
            return Ok(list);
        }

        // W04 - Moderation & Tribunal
        [HttpGet("reports")]
        public async Task<IActionResult> GetReports([FromQuery] ReportStatus? status)
        {
            var reports = await _adminService.GetReportsAsync(status);
            return Ok(reports);
        }

        [HttpPost("reports/review")]
        public async Task<IActionResult> ReviewReport([FromBody] ReviewReportRequest request)
        {
            await _moderation.ReviewReport(Actor, request);
            return Ok(new { message = "Đã ghi kết luận xử lý tố cáo." });
        }

    }
}
