using System.Security.Claims;
using Entities.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController, Authorize(Roles = "Admin"), Route("api/Admin/users/{player:guid}/progress-reset")]
public sealed class ProgressResetController(IProgressResetService resets) : ControllerBase
{
    [HttpPost("preview")] public Task<ProgressResetPreview> Preview(Guid player, ProgressResetSelection request) => resets.Preview(player, request);
    [HttpPost] public Task<ProgressResetReceipt> Reset(Guid player, ExecuteProgressReset request) => resets.Execute(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), player, request);
}
