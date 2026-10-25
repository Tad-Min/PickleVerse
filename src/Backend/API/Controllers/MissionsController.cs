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
    public class MissionsController : ControllerBase
    {
        private readonly IMissionService _missionService;
        private readonly Services.Game.OnlineGameService _game;

        public MissionsController(IMissionService missionService, Services.Game.OnlineGameService game)
        {
            _missionService = missionService;
            _game = game;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyMissions()
        {
            var userId = GetCurrentUserId();
            await _game.Read(userId);
            var missions = await _missionService.GetMyMissionsAsync(userId);
            return Ok(missions);
        }

        [HttpPost("{missionId}/claim")]
        public async Task<IActionResult> ClaimReward(int missionId)
        {
            var userId = GetCurrentUserId();
            return Ok(await _game.LegacyClaim(userId, missionId, false, Request.Headers["Idempotency-Key"].FirstOrDefault() ?? Guid.NewGuid().ToString("N")));
        }
    }
}
