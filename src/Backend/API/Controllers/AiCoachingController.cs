using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Entities.DTOs;
using Services.Interfaces;
using Services.Configuration;
using PickleVerse.Progression;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AiCoachingController : ControllerBase
    {
        private readonly IAiCoachingService _aiCoachingService;
        private readonly IHybridCoachingService _hybrid;
        private readonly HybridAiSettings _settings;

        public AiCoachingController(IAiCoachingService aiCoachingService, IHybridCoachingService hybrid, HybridAiSettings settings)
        {
            _aiCoachingService = aiCoachingService;
            _hybrid = hybrid;
            _settings = settings;
        }
        [Authorize]
        [HttpPost("telemetry")]
        [RequestSizeLimit(1048576)]
        public async Task<IActionResult> Telemetry([FromBody] TelemetryBatch batch)
            => Accepted(await _hybrid.Ingest(GetCurrentUserId(), batch));

        [Authorize]
        [HttpGet("summary")]
        public async Task<IActionResult> Summary() => Ok(await _hybrid.Summary(GetCurrentUserId()));

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
            if (_settings.CoachingEnabled) return Ok(await _hybrid.LegacyRecommendation(userId));
            var rec = await _aiCoachingService.GetRecommendedDrillsAsync(userId);
            return Ok(rec);
        }

        [Authorize]
        [HttpPost("log-error")]
        public async Task<IActionResult> LogTechniqueError([FromBody] LogTechniqueErrorRequest request)
        {
            if (_settings.IngestEnabled || _settings.CoachingEnabled || _settings.DispatcherEnabled)
                return StatusCode(410, new { message = "Lỗi kỹ thuật được phân tích từ telemetry của phiên đã xác nhận.", code = "hybrid_telemetry_required" });
            var userId = GetCurrentUserId();
            var success = await _aiCoachingService.LogTechniqueErrorAsync(userId, request);
            if (!success)
                return BadRequest(new { message = "Ghi nhận lỗi thất bại." });
            return Ok(new { message = "Ghi nhận lỗi kỹ thuật thành công!" });
        }
    }
}
