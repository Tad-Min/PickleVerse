using System.Threading.Tasks;

namespace PickleVerse.Multiplayer.Services
{
    public sealed class MatchmakingService : IMatchmakingService
    {
        readonly IMultiplayerBackend backend;
        public IMultiplayerState State => backend;
        public MatchmakingService(IMultiplayerBackend backend) { this.backend = backend; }
        public Task FindAsync() => backend.ExecuteAsync(backend.FindAsync);
        // Cancellation must remain available while another asynchronous action is running.
        public Task CancelAsync() => backend.CancelSearchAsync();
    }
}
