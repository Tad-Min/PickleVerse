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
    public class AiCoachingController : ControllerBase
    {
        private readonly IAiCoachingService _aiCoachingService;

        public AiCoachingController(IAiCoachingService aiCoachingService)
        {
            _aiCoachingService = aiCoachingService;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        [Authorize]
        [HttpGet("mastery")]
        public async Task<IActionResult> GetMastery()
        {
            var userId = GetCurrentUserId();
            var list = await _aiCoachingService.GetMasteryStatusAsync(userId);
            return Ok(list);
        }

        [Authorize]
        [HttpGet("recommendation")]
        public async Task<IActionResult> GetRecommendation()
        {
            var userId = GetCurrentUserId();
            var rec = await _aiCoachingService.GetRecommendedDrillsAsync(userId);
            return Ok(rec);
        }

        [Authorize]
        [HttpPost("log-error")]
        public async Task<IActionResult> LogTechniqueError([FromBody] LogTechniqueErrorRequest request)
        {
            var userId = GetCurrentUserId();
            var success = await _aiCoachingService.LogTechniqueErrorAsync(userId, request);
            if (!success)
                return BadRequest(new { message = "Ghi nhận lỗi thất bại." });
            return Ok(new { message = "Ghi nhận lỗi kỹ thuật thành công!" });
        }
    }
}
