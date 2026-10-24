using System;
using System.Threading.Tasks;

namespace PickleVerse.Multiplayer.Services
{
    /// <summary>Keep the room scene alive until an in-flight create/join and its cleanup finish.</summary>
    public sealed class RoomOperationGate
    {
        Task pending=Task.CompletedTask;

        public Task Run(Func<Task> operation)
        {
            if(!pending.IsCompleted)throw new InvalidOperationException("Đang kết nối phòng. Vui lòng chờ.");
            return pending=operation();
        }

        public async Task DrainAsync()
        {
            // The operation's owner still receives its failure. Leaving must also be able
            // to finish after that operation has cleaned up a failed Photon connection.
            try{await pending;}
            catch{ }
        }
    }
}
