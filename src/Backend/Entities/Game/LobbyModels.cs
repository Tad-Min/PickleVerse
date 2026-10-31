using PickleVerse.Multiplayer;
namespace Entities.Game;
    public sealed class LobbyPresence
    {
        public string PlayerId = "";
        public LobbyPresenceRequest Value = new();
        public long ExpiresAt;
    }
    public sealed class StoredRoomInvitation
    {
        public RoomInvitation Value = new();
        public string SenderSession = "", OwnerSession = "", OwnerId = "", ReceiverId = "";
    }
