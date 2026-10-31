using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("api/Game/social")]
public sealed class OnlineSocialController(IOnlineSocialService social) : ControllerBase
{
    Guid Player => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet]
    public Task<OnlineSocialPage> Read(string view = "friends", string? q = null, int offset = 0) => social.Read(Player, view, q, offset);
    [HttpPost]
    public async Task<IActionResult> Act(SocialActionRequest request)
    {
        await social.Act(Player, request);
        return Ok(new { message = "Danh sách đã được cập nhật." });
    }
}
