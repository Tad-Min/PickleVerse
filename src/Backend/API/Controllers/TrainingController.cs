using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Entities.DTOs;
using Services.Interfaces;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrainingController : ControllerBase
    {
        private readonly ITrainingService _trainingService;
        private readonly Services.Interfaces.IOnlineGameService _game;

        public TrainingController(ITrainingService trainingService, Services.Interfaces.IOnlineGameService game)
        {
            _trainingService = trainingService;
            _game = game;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        [Authorize]
        [HttpGet("lessons")]
        public async Task<IActionResult> GetLessons()
        {
            var userId = GetCurrentUserId();
            var lessons = await _game.LegacyLessons(userId);
            return Ok(lessons);
        }

        [Authorize]
        [HttpPost("submit-drill")]
        [Obsolete("Use api/Game/training/start, checkpoint and complete.")]
        public IActionResult SubmitDrill([FromBody] SubmitDrillRequest request)
        {
            return StatusCode(410, new { message = "API gửi điểm tự do đã được thay bằng api/Game/training/start, checkpoint và complete." });
        }
    }
}
