using Entities.DTOs;
using Entities.Game;
using PickleVerse.Replays;

namespace Services.Interfaces;

public interface IReplayLaunchService
{
    Task<ReplayLaunchLink> Issue(Guid admin, int authVersion, Guid match);
    Task<ReplayLaunchView> Redeem(string code);
    Task<ReplayMetadata> Read(string token, Guid match);
    Task<ReplayChunkUpload> Chunk(string token, Guid match, int index);
}
