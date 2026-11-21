using System.Security.Claims;
using API.Filters;
using Entities.DTOs;
using Entities.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController, Authorize, AllowRestrictedSession, Route("api/Moderation")]
public sealed class ModerationController(IModerationService moderation) : ControllerBase
{
    Guid Player => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("decisions")] public Task<List<UserSanction>> Decisions() => moderation.Sanctions(Player);
    [HttpGet("appeals")] public Task<List<SanctionAppeal>> Appeals() => moderation.Appeals(Player);
    [HttpPost("appeals")] public Task<SanctionAppeal> Appeal(AppealRequest request) => moderation.SubmitAppeal(Player, request);
}

[ApiController, Authorize(Roles = "Admin"), Route("api/Admin")]
public sealed class AdminModerationController(IModerationService moderation) : ControllerBase
{
    Guid Actor => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("sanctions")] public Task<List<UserSanction>> Sanctions([FromQuery] Guid? userId) => moderation.Sanctions(userId);
    [HttpGet("appeals")] public Task<List<SanctionAppeal>> Appeals([FromQuery] Guid? userId) => moderation.Appeals(userId);
    [HttpPost("appeals/{id:guid}/review")] public Task<SanctionAppeal> Review(Guid id, AppealReviewRequest request) => moderation.ReviewAppeal(Actor, id, request);
    [HttpPost("matches/{match:guid}/stop")] public Task<DedicatedOperationRecord> Stop(Guid match, StopMatchRequest request) => moderation.StopMatch(Actor, match, request.Reason);
    [HttpGet("matches/{match:guid}/stop")] public Task<DedicatedOperationRecord?> StopOperation(Guid match) => moderation.StopOperation(match);
}

[ApiController, Authorize(Roles = "Admin"), Route("api/Admin/matches")]
public sealed class LiveMatchesController(IDedicatedServerService dedicated) : ControllerBase
{
    [HttpGet("live")] public Task<List<Entities.DTOs.LiveMatchDto>> Live() => dedicated.LiveMatches();
}
