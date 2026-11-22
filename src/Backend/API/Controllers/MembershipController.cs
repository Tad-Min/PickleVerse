using System.Security.Claims;
using System.Text.Json;
using Entities.DTOs;
using Entities.DTOs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/Game/membership")]
public sealed class MembershipController(IMembershipService membership) : ControllerBase
{
    Guid Player => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public Task<MembershipOffer> Read() => membership.Read(Player);

    [HttpPost("checkout"), EnableRateLimiting("auth")]
    public Task<MembershipOffer> Checkout(CancellationToken cancellation) => membership.Checkout(Player, cancellation);

    [HttpPost("refresh")]
    public Task<MembershipOffer> Refresh(CancellationToken cancellation) => membership.Refresh(Player, cancellation);

    [HttpPost("/api/payments/payos/webhook"), AllowAnonymous, RequestSizeLimit(32 * 1024)]
    public async Task<ApiResponse> Webhook(JsonElement body, CancellationToken cancellation)
    {
        await membership.ReceiveWebhook(body, cancellation);
        return ApiResponse.SuccessResponse();
    }
}
