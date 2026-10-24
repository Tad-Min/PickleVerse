using System;

namespace PickleVerse.Multiplayer
{
    [Serializable] public sealed class LobbyPresenceRequest
    {
        public string SessionId="", State="busy", RoomCode="", Region="";
        public bool IsHost, Custom;
        public int Mode=1;
        public string[] Members=Array.Empty<string>();
    }
    [Serializable] public sealed class RoomInvitation
    {
        public string Id="", SenderId="", SenderName="", RoomCode="", Region="", State="pending";
        public int Mode;
        public bool Custom;
        public long ExpiresAt;
    }
    [Serializable] public sealed class LobbyPresenceReply
    {
        public RoomInvitation[] Invitations=Array.Empty<RoomInvitation>();
        public int PollSeconds=3;
    }
    [Serializable] public sealed class InvitableFriend
    {
        public string Id="", Name="", Status="offline";
        public int Elo;
        public bool CanInvite, Pending;
    }
    [Serializable] public sealed class InvitableFriendsPage
    {
        public InvitableFriend[] Players=Array.Empty<InvitableFriend>();
        public bool HasMore;
        public int Offset;
    }
    [Serializable] public sealed class SendRoomInvitationRequest { public string PlayerId=""; }
    [Serializable] public sealed class RespondRoomInvitationRequest { public bool Accept; }
}
