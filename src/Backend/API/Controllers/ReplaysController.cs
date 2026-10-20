using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs;
using Services.Interfaces;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReplaysController : ControllerBase
    {
        private readonly IReplayService _replayService;

        public ReplaysController(IReplayService replayService)
        {
            _replayService = replayService;
        }

        [Authorize]
        [HttpPost("save")]
        public async Task<IActionResult> SaveReplay([FromBody] SaveReplayRequest request)
        {
            var result = await _replayService.SaveReplayAsync(request.MatchId, request.ReplayData);
            if (!result.Success) return BadRequest(new { message = result.Message });
            return Ok(new { message = result.Message, replayUrl = result.ReplayUrl });
        }

        [HttpGet("{matchId}")]
        public async Task<IActionResult> GetReplay(Guid matchId)
        {
            var result = await _replayService.GetReplayAsync(matchId);
            if (result == null) return NotFound(new { message = "Không tìm thấy dữ liệu Replay cho trận đấu này." });
            return Ok(result);
        }
    }
}
