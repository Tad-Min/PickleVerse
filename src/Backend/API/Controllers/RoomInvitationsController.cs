using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PickleVerse.Multiplayer;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("api/Game/lobby")]
public sealed class RoomInvitationsController(IRoomInvitationService invitations, IMembershipService membership) : ControllerBase
{
    Guid Player => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpPost("presence")]
    public Task<LobbyPresenceReply> Presence(LobbyPresenceRequest request) => invitations.Publish(Player, request);
    [HttpGet("friends")]
    public Task<InvitableFriendsPage> Friends(string? q = null, int offset = 0) => invitations.List(Player, q, offset);
    [HttpGet("rooms/{code}")]
    public async Task<RoomJoinInfo> JoinInfo(string code)
    {
        await membership.Require(Player);
        return await invitations.JoinInfo(code);
    }
    [HttpPost("invitations")]
    public async Task<RoomInvitation> Send(SendRoomInvitationRequest request)
    {
        if (Guid.TryParse(request.PlayerId, out var friend)) await membership.Require(Player, friend);
        else await membership.Require(Player);
        return await invitations.Send(Player, request);
    }
    [HttpPost("invitations/{id}/respond")]
    public async Task<RoomInvitation> Respond(string id, RespondRoomInvitationRequest request)
    {
        if (request.Accept) await membership.Require(Player);
        return await invitations.Respond(Player, id, request.Accept);
    }
}
