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

        public MissionsController(IMissionService missionService)
        {
            _missionService = missionService;
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
            var missions = await _missionService.GetMyMissionsAsync(userId);
            return Ok(missions);
        }

        [HttpPost("{missionId}/claim")]
        public async Task<IActionResult> ClaimReward(int missionId)
        {
            var userId = GetCurrentUserId();
            var result = await _missionService.ClaimRewardAsync(userId, missionId);
            if (!result.Success) return BadRequest(new { message = result.Message });
            return Ok(new { message = result.Message, expGained = result.ExpGained });
        }
    }
}
