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
        private readonly Services.Game.OnlineGameService _game;

        public AchievementsController(IAchievementService achievementService, Services.Game.OnlineGameService game)
        {
            _achievementService = achievementService;
            _game = game;
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
            await _game.Read(userId);
            var list = await _achievementService.GetMyAchievementsAsync(userId);
            return Ok(list);
        }

        [HttpPost("{achievementId}/claim")]
        public async Task<IActionResult> ClaimReward(int achievementId)
        {
            var userId = GetCurrentUserId();
            return Ok(await _game.LegacyClaim(userId, achievementId, true, Request.Headers["Idempotency-Key"].FirstOrDefault() ?? Guid.NewGuid().ToString("N")));
        }
    }
}
