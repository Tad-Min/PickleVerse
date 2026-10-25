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
    public class MatchesController : ControllerBase
    {
        private readonly IMatchService _matchService;

        public MatchesController(IMatchService matchService)
        {
            _matchService = matchService;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        [Authorize]
        [HttpPost("finish")]
        [Obsolete("Use a server-created match and its validated completion endpoint.")]
        public IActionResult FinishMatch([FromBody] FinishMatchRequest request)
        {
            return StatusCode(410, new { message = "API cho client tự ghi Elo đã ngừng sử dụng. Hãy hoàn tất trận đã được cấp mã qua api/Game." });
        }

        [Authorize]
        [HttpGet("my-history")]
        public async Task<IActionResult> GetMyMatchHistory([FromQuery] int pageSize = 20)
        {
            var userId = GetCurrentUserId();
            var history = await _matchService.GetMyMatchHistoryAsync(userId, pageSize);
            return Ok(history);
        }

        [Authorize]
        [HttpPost("report")]
        public async Task<IActionResult> ReportPlayer([FromBody] ReportPlayerRequest request)
        {
            var reporterId = GetCurrentUserId();
            var success = await _matchService.ReportPlayerAsync(reporterId, request);
            if (!success)
                return BadRequest(new { message = "Gửi báo cáo tố cáo thất bại." });
            return Ok(new { message = "Đã gửi đơn tố cáo. Ban quản trị sẽ sớm xem xét bằng chứng!" });
        }
    }
}
