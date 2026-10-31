using Entities.Game;
using PickleVerse.Progression;
using PickleVerse.Multiplayer;
using PickleVerse.Replays;
namespace Services.Interfaces;

public interface IRoomInvitationService
{
    Task<LobbyPresenceReply> Publish(Guid self, LobbyPresenceRequest input);
    Task<InvitableFriendsPage> List(Guid self, string? query, int offset);
    Task<RoomInvitation> Send(Guid self, SendRoomInvitationRequest input);
    Task<RoomInvitation> Respond(Guid self, string id, bool accept);
}
