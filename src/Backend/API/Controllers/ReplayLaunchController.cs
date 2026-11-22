using System.Security.Claims;
using Entities.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/replay-launch")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReplayLaunchController(IReplayLaunchService launch) : ControllerBase
{
    [HttpPost("{match:guid}"), Authorize(Roles = "Admin")]
    public Task<ReplayLaunchLink> Issue(Guid match) => launch.Issue(
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), int.Parse(User.FindFirstValue("auth_version")!), match);

    [HttpPost("redeem"), AllowAnonymous, EnableRateLimiting("auth")]
    public Task<ReplayLaunchView> Redeem(ReplayLaunchRedeem input) => launch.Redeem(input.Code);

    // An opaque view capability is deliberately not a login JWT. Every read rechecks
    // its match scope, expiry, issuer's current role, account status and login epoch.
    string Grant => Request.Headers.Authorization.ToString() is var value && value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        ? value[7..] : "";
    [HttpGet("view/{match:guid}"), AllowAnonymous]
    public Task<PickleVerse.Replays.ReplayMetadata> Read(Guid match) => launch.Read(Grant, match);
    [HttpGet("view/{match:guid}/chunks/{index:int}"), AllowAnonymous]
    public Task<Entities.Game.ReplayChunkUpload> Chunk(Guid match, int index) => launch.Chunk(Grant, match, index);
}
