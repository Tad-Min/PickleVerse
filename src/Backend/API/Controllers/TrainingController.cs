using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs;
using Services.Interfaces;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrainingController : ControllerBase
    {
        private readonly ITrainingService _trainingService;

        public TrainingController(ITrainingService trainingService)
        {
            _trainingService = trainingService;
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
            var lessons = await _trainingService.GetLessonsAsync(userId);
            return Ok(lessons);
        }

        [Authorize]
        [HttpPost("submit-drill")]
        public async Task<IActionResult> SubmitDrill([FromBody] SubmitDrillRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _trainingService.SubmitDrillResultAsync(userId, request);
            return Ok(result);
        }
    }
}
