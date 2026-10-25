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
        [Obsolete("Use authenticated, chunked api/Game/replays endpoints.")]
        public IActionResult SaveReplay([FromBody] SaveReplayRequest request)
        {
            return StatusCode(410, new { message = "Hãy dùng api/Game/replays để lưu replay theo tài khoản và từng chunk." });
        }

        [HttpGet("{matchId}")]
        [Authorize]
        [Obsolete("Use authenticated api/Game/replays/{matchId}.")]
        public IActionResult GetReplay(Guid matchId)
        {
            return StatusCode(410, new { message = "Hãy dùng api/Game/replays/{matchId} để đọc replay thuộc tài khoản." });
        }
    }
}
