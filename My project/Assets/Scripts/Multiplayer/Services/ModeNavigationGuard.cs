using System;
using System.Threading.Tasks;
namespace PickleVerse.Multiplayer.Services
{
    /// <summary>Owns the lifetime of callbacks from one room-page visit.</summary>
    public sealed class ModeNavigationGuard
    {
        public int Epoch {get;private set;}
        public bool Leaving {get;private set;}
        public void Open(){Leaving=false;}
        public bool IsCurrent(int epoch)=>!Leaving&&epoch==Epoch;
        public async Task ReturnAsync(Action invalidate,Func<Task> cancel,Func<Task> leaveMatch,Func<Task> leaveRoom,Action navigate)
        {
            if(Leaving)return;
            Leaving=true;Epoch++;
            try{invalidate();await cancel();await leaveMatch();await leaveRoom();navigate();}
            catch{Leaving=false;throw;}
        }
    }
}
