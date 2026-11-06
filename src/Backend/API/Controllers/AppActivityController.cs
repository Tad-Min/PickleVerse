using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("api/Game/activity")]
public sealed class AppActivityController(IAppActivityService activity) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("open")]
    public Task<AppActivityReceipt> OpenActivity(AppActivityRequest request) => activity.OpenActivity(request);

    [HttpPost("checkpoint")]
    public Task<AppActivityReceipt> Activity(AppActivityRequest request) =>
        activity.Activity(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), request);
}
