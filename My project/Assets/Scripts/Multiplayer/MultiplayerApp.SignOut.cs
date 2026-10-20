#if PICKLEVERSE_LEGACY_NGO
using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;

namespace PickleVerse.Multiplayer
{
    public sealed partial class MultiplayerApp
    {
        public async Task SignOutFromLobbyAsync()
        {
            if(State==FlowState.InMatch||State==FlowState.Reconnecting)throw new InvalidOperationException("Rời trận trước khi đăng xuất.");
            CancelLocal();await LeaveMatchAsync();await LeaveRoomAsync();
            if(UnityServices.State==ServicesInitializationState.Initialized&&AuthenticationService.Instance.IsSignedIn)AuthenticationService.Instance.SignOut();
            identity.DisplayName="";SetState(FlowState.Menu,"Đã đăng xuất.");
        }
    }
    public static class PlayerSignOut
    {
        public static async Task FromLobbyAsync()
        {
            if(MultiplayerApp.Instance!=null)await MultiplayerApp.Instance.SignOutFromLobbyAsync();
            else if(UnityServices.State==ServicesInitializationState.Initialized&&AuthenticationService.Instance.IsSignedIn)AuthenticationService.Instance.SignOut();
        }
    }
}

#endif
