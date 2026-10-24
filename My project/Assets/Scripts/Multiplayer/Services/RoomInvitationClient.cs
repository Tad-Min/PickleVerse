using System;
using System.Threading;
using System.Threading.Tasks;
using PickleVerse.Services;

namespace PickleVerse.Multiplayer.Services
{
    public sealed class RoomInvitationClient
    {
        readonly AccountService account;
        public RoomInvitationClient(AccountService account){this.account=account;}
        public Task<LobbyPresenceReply> PublishAsync(LobbyPresenceRequest request,CancellationToken cancellation=default)=>
            account.SendAuthorizedAsync<LobbyPresenceReply>("POST","api/Game/lobby/presence",request,cancellation);
        public Task<InvitableFriendsPage> FriendsAsync(string query,int offset=0,CancellationToken cancellation=default)=>
            account.SendAuthorizedAsync<InvitableFriendsPage>("GET","api/Game/lobby/friends?q="+Uri.EscapeDataString(query??"")+"&offset="+offset,cancellation:cancellation);
        public Task<RoomInvitation> SendAsync(string player,CancellationToken cancellation=default)=>
            account.SendAuthorizedAsync<RoomInvitation>("POST","api/Game/lobby/invitations",new SendRoomInvitationRequest{PlayerId=player},cancellation);
        public Task<RoomInvitation> RespondAsync(string id,bool accept,CancellationToken cancellation=default)=>
            account.SendAuthorizedAsync<RoomInvitation>("POST","api/Game/lobby/invitations/"+Guid.Parse(id).ToString("N")+"/respond",new RespondRoomInvitationRequest{Accept=accept},cancellation);
    }
}
