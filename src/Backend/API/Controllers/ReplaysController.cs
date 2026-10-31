using System;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Entities.DTOs;
using Services.Interfaces;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReplaysController : ControllerBase
    {
        private readonly IMatchReplayService _replayService;

        public ReplaysController(IMatchReplayService replayService)
        {
            _replayService = replayService;
        }

        [Authorize]
        [HttpPost("save")]
        [Obsolete("Use authenticated, chunked api/Game/replays endpoints.")]
        public IActionResult SaveReplay([FromBody] SaveReplayRequest request)
        {
            return StatusCode(410, new { message = "Hãy dùng api/Game/replays để lưu replay chung của trận theo từng chunk." });
        }

        [HttpGet("{matchId}")]
        [Authorize]
        [API.OpenApi.FileResponse]
        public async Task<IActionResult> GetReplay(Guid matchId)
        {
            var player = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var replay = await _replayService.Read(player, matchId, User.IsInRole("Admin"));
            return File(Encoding.UTF8.GetBytes(GameJson.Write(replay)), "application/json", $"replay-{matchId:N}.json");
        }
    }
}
