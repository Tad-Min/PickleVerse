using System.Security.Claims;
using Entities.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController, Authorize(Roles = "Admin"), Route("api/Admin/coaching")]
public sealed class ExpertReviewController(IExpertReviewService reviews) : ControllerBase
{
    Guid Actor => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("sessions")]
    public Task<List<ReviewableTrainingSession>> Sessions([FromQuery] Guid? playerId, [FromQuery] int page = 1) => reviews.Sessions(playerId, page);

    [HttpGet("demo-sessions")]
    public Task<List<CoachingDemoSession>> DemoSessions() => reviews.DemoSessions();

    [HttpGet("demo-sessions/{sessionId}")]
    public Task<CoachingDemoEvidence> DemoEvidence(string sessionId) => reviews.DemoEvidence(sessionId);

    [HttpGet("sessions/{batch:guid}")]
    public Task<ExpertSessionEvidence> Evidence(Guid batch) => reviews.Evidence(batch);

    [HttpPost("sessions/{batch:guid}/review")]
    public Task<ExpertReviewReceipt> Review(Guid batch, ExpertReviewRequest request) => reviews.Review(Actor, batch, request);

    [HttpGet("dataset/tier"), API.OpenApi.FileResponse]
    public Task<IActionResult> TierDataset() => Dataset(true);

    [HttpGet("dataset/coaching"), API.OpenApi.FileResponse]
    public Task<IActionResult> CoachingDataset() => Dataset(false);

    async Task<IActionResult> Dataset(bool tierOnly)
    {
        var file = await reviews.Export(tierOnly);
        Response.Headers["X-Dataset-Sha256"] = file.Sha256;
        return File(file.Content, "application/json", file.Name);
    }
}
