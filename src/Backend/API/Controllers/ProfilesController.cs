using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs;
using Services.Interfaces;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProfilesController : ControllerBase
    {
        private readonly IProfileService _profileService;

        public ProfilesController(IProfileService profileService)
        {
            _profileService = profileService;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMyProfile()
        {
            var userId = GetCurrentUserId();
            var profile = await _profileService.GetMyProfileAsync(userId);
            if (profile == null) return NotFound(new { message = "Không tìm thấy hồ sơ người chơi." });
            return Ok(profile);
        }

        [HttpGet("{playerId}")]
        public async Task<IActionResult> GetProfileById(Guid playerId)
        {
            var profile = await _profileService.GetProfileByIdAsync(playerId);
            if (profile == null) return NotFound(new { message = "Không tìm thấy hồ sơ người chơi." });
            return Ok(profile);
        }

        [Authorize]
        [HttpPut("me")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var userId = GetCurrentUserId();
            var success = await _profileService.UpdateProfileAsync(userId, request);
            if (!success) return BadRequest(new { message = "Cập nhật hồ sơ thất bại." });
            return Ok(new { message = "Cập nhật hồ sơ thành công." });
        }

        [Authorize]
        [HttpPost("sync-time")]
        public async Task<IActionResult> SyncTime([FromBody] SyncTimeRequest request)
        {
            var userId = GetCurrentUserId();
            var success = await _profileService.SyncTimePlayedAsync(userId, request.PlayedSeconds);
            if (!success) return BadRequest(new { message = "Đồng bộ giờ chơi thất bại." });
            return Ok(new { message = "Đồng bộ giờ chơi thành công." });
        }

        [HttpGet("leaderboard")]
        public async Task<IActionResult> GetLeaderboard([FromQuery] int top = 50)
        {
            var list = await _profileService.GetLeaderboardAsync(top);
            return Ok(list);
        }
    }
}
