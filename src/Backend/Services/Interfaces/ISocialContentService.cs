using Entities.DTOs;
using Entities.Game;
using PickleVerse.Replays;

namespace Services.Interfaces;

public interface ISocialContentService
{
    Task<SocialPage<SocialPostItem>> Feed(Guid viewer, long? before);
    Task<SocialPostItem> Save(Guid actor, long? postId, SocialPostInput input);
    Task Delete(Guid actor, long postId);
    Task<SocialPostItem> Like(Guid actor, long postId, bool liked);
    Task<SocialPage<SocialCommentItem>> Comments(Guid viewer, long postId, long? before);
    Task<SocialCommentItem> Comment(Guid actor, long postId, string? body);
    Task DeleteComment(Guid actor, long postId, long commentId);
    Task<ReplayMetadata> Replay(Guid viewer, long postId);
    Task<ReplayChunkUpload> ReplayChunk(Guid viewer, long postId, int index);
    Task<Guid> VideoMedia(Guid viewer, long postId);
}
