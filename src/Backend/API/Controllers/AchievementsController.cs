using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs;
using Services.Interfaces;

namespace API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AchievementsController : ControllerBase
    {
        private readonly IAchievementService _achievementService;

        public AchievementsController(IAchievementService achievementService)
        {
            _achievementService = achievementService;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyAchievements()
        {
            var userId = GetCurrentUserId();
            var list = await _achievementService.GetMyAchievementsAsync(userId);
            return Ok(list);
        }

        [HttpPost("{achievementId}/claim")]
        public async Task<IActionResult> ClaimReward(int achievementId)
        {
            var userId = GetCurrentUserId();
            var result = await _achievementService.ClaimRewardAsync(userId, achievementId);
            if (!result.Success) return BadRequest(new { message = result.Message });
            return Ok(new { message = result.Message, expGained = result.ExpGained });
        }
    }
}
