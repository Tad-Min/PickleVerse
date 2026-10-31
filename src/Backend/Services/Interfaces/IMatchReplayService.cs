using Entities.Game;
using PickleVerse.Progression;
using PickleVerse.Multiplayer;
using PickleVerse.Replays;
namespace Services.Interfaces;

public interface IMatchReplayService
{
    Task PublishAvailable(Guid match);
    Task<List<ReplayMetadata>> List(Guid player, bool admin = false);
    Task<ReplayMetadata> Read(Guid player, Guid match, bool admin = false);
    Task<ReplayMetadata> Save(Guid player, Guid match, ReplayMetadata input);
    Task<ReplayChunkInfo> Upload(Guid player, Guid match, int index, ReplayChunkUpload input);
    Task<ReplayChunkUpload> Chunk(Guid player, Guid match, int index, bool admin = false);
}
