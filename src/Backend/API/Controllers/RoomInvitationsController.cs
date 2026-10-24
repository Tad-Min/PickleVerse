using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PickleVerse.Multiplayer;
using Services.Game;

namespace API.Controllers;

[ApiController,Authorize,Route("api/Game/lobby")]
public sealed class RoomInvitationsController(RoomInvitationService invitations):ControllerBase
{
    Guid Player=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpPost("presence")]public Task<LobbyPresenceReply> Presence(LobbyPresenceRequest request)=>invitations.Publish(Player,request);
    [HttpGet("friends")]public Task<InvitableFriendsPage> Friends(string? q=null,int offset=0)=>invitations.List(Player,q,offset);
    [HttpPost("invitations")]public Task<RoomInvitation> Send(SendRoomInvitationRequest request)=>invitations.Send(Player,request);
    [HttpPost("invitations/{id}/respond")]public Task<RoomInvitation> Respond(string id,RespondRoomInvitationRequest request)=>invitations.Respond(Player,id,request.Accept);
}
