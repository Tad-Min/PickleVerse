using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PickleVerse.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Enforce Zero Trust Security Principle (JWT)
    public class LeaderboardController : ControllerBase
    {
        [HttpGet("top100")]
        public IActionResult GetTop100()
        {
            // Dummy data for now
            return Ok(new[] 
            {
                new { Username = "pro_player", DuprRating = 2850f },
                new { Username = "newbie", DuprRating = 2000f }
            });
        }
    }
}
