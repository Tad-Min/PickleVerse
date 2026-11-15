using Entities.DTOs;
using Entities.Exceptions;
using Entities.Game;
using Entities.Models;
using PickleVerse.Replays;
using Repositories.UnitOfWork;
using Services.Interfaces;

namespace Services.Implementations;

public sealed class SocialContentService(IUnitOfWork unit, IMatchReplayService replays, TimeProvider clock) : ISocialContentService
{
    static NotFoundException Missing() => new("Không tìm thấy bài đăng.");
    async Task<SocialPost> View(Guid viewer, long postId)
        => await unit.Game.VisiblePost(viewer, postId) ?? throw Missing();
    public Task<SocialPage<SocialPostItem>> Feed(Guid viewer, long? before) => unit.Game.Feed(viewer, before ?? long.MaxValue);

    public async Task<SocialPostItem> Save(Guid actor, long? postId, SocialPostInput input)
    {
        string body = (input.Body ?? "").Trim();
        var excluded = (input.Exclude ?? []).Distinct().ToArray();
        if (input.Audience is not ("community" or "private" or "friends" or "friends_except")
            || body.Length > 2000 || body.Length == 0 && input.MediaId == null && input.ReplayMatchId == null
            || excluded.Length > 200 || excluded.Contains(actor) || input.Audience != "friends_except" && excluded.Length != 0)
            throw new BadRequestException("Kiểm tra nội dung hoặc quyền xem bài đăng.");
        await using var transaction = await unit.Game.BeginTransactionAsync();
        if (postId.HasValue) await LockPost(postId.Value);
        var post = postId.HasValue ? await unit.Game.OwnedPost(actor, postId.Value) ?? throw Missing()
            : new SocialPost { AuthorId = actor, CreatedAt = clock.GetUtcNow().UtcDateTime };
        if (excluded.Length > 0 && !await unit.Game.PostExclusionTargetsExist(excluded))
            throw new BadRequestException("Danh sách ngoại trừ chứa người chơi không hợp lệ.");
        if (input.ReplayMatchId.HasValue && input.ReplayMatchId != post.ReplayMatchId)
        {
            var replay = await replays.Read(actor, input.ReplayMatchId.Value);
            if (!replay.complete) throw new BadRequestException("Replay đang được hoàn thiện.");
        }
        foreach (var mediaId in new[] { post.MediaId, input.MediaId }.OfType<Guid>().Distinct().Order())
            await unit.Game.Lock("social-media:" + mediaId);
        if (input.MediaId.HasValue && input.MediaId != post.MediaId)
        {
            var media = await unit.Game.SocialMediaById(input.MediaId.Value);
            if (media == null || media.OwnerId != actor || media.Deleted || !media.Ready ||
                await unit.Game.MediaUsed(media.MediaId, post.PostId))
                throw new BadRequestException("Video chưa sẵn sàng hoặc không thuộc bài đăng.");
        }
        if (post.MediaId.HasValue && post.MediaId != input.MediaId)
            await RetireMedia(post.MediaId.Value);
        post.Body = body;
        post.Audience = input.Audience;
        post.MediaId = input.MediaId;
        post.ReplayMatchId = input.ReplayMatchId;
        if (postId.HasValue) post.EditedAt = clock.GetUtcNow().UtcDateTime;
        else unit.Game.AddSocialPost(post);
        await unit.SaveChangesAsync();
        await unit.Game.SetPostExclusions(post.PostId, excluded);
        await unit.SaveChangesAsync();
        var result = await unit.Game.PostItem(actor, post.PostId);
        await transaction.CommitAsync();
        return result;
    }

    Task LockPost(long postId) => unit.Game.Lock("social-post:" + postId);
    async Task<SocialMedia?> LockMedia(Guid id)
    {
        await unit.Game.Lock("social-media:" + id);
        return await unit.Game.SocialMediaById(id);
    }
    async Task RetireMedia(Guid id)
    {
        var media = await LockMedia(id);
        if (media != null) media.Deleted = true;
    }

    public async Task Delete(Guid actor, long postId)
    {
        await using var transaction = await unit.Game.BeginTransactionAsync();
        await LockPost(postId);
        var post = await unit.Game.OwnedPost(actor, postId) ?? throw Missing();
        if (post.MediaId.HasValue) await RetireMedia(post.MediaId.Value);
        unit.Game.RemoveSocialPost(post);
        await unit.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    async Task<SocialPost> LockInteraction(Guid actor, long postId)
    {
        await LockPost(postId);
        var post = await View(actor, postId);
        if (post.AuthorId == actor) return post;
        await unit.Game.LockPlayers(actor, post.AuthorId);
        return await View(actor, postId);
    }

    public async Task<SocialPostItem> Like(Guid actor, long postId, bool liked)
    {
        await using var transaction = await unit.Game.BeginTransactionAsync();
        await LockInteraction(actor, postId);
        await unit.Game.SetPostLike(postId, actor, liked);
        await unit.SaveChangesAsync();
        var result = await unit.Game.PostItem(actor, postId);
        await transaction.CommitAsync();
        return result;
    }

    public async Task<SocialPage<SocialCommentItem>> Comments(Guid viewer, long postId, long? before)
    {
        await View(viewer, postId);
        return await unit.Game.PostComments(viewer, postId, before ?? long.MaxValue);
    }

    public async Task<SocialCommentItem> Comment(Guid actor, long postId, string? body)
    {
        var text = SocialDirectService.MessageText(body);
        await using var transaction = await unit.Game.BeginTransactionAsync();
        await LockInteraction(actor, postId);
        var row = new SocialPostComment { AuthorId = actor, PostId = postId, Body = text, CreatedAt = clock.GetUtcNow().UtcDateTime };
        unit.Game.AddPostComment(row);
        await unit.SaveChangesAsync();
        var name = await unit.Game.PlayerName(actor);
        await transaction.CommitAsync();
        return new(row.CommentId, actor, name ?? "", text, row.CreatedAt);
    }

    public async Task DeleteComment(Guid actor, long postId, long commentId)
    {
        await using var transaction = await unit.Game.BeginTransactionAsync();
        var post = await LockInteraction(actor, postId);
        await unit.Game.RemovePostComment(actor, post, commentId);
        await transaction.CommitAsync();
    }

    public async Task<ReplayMetadata> Replay(Guid viewer, long postId)
    {
        var post = await View(viewer, postId);
        return await replays.Read(post.AuthorId, post.ReplayMatchId ?? throw Missing());
    }
    public async Task<ReplayChunkUpload> ReplayChunk(Guid viewer, long postId, int index)
    {
        var post = await View(viewer, postId);
        return await replays.Chunk(post.AuthorId, post.ReplayMatchId ?? throw Missing(), index);
    }
    public async Task<Guid> VideoMedia(Guid viewer, long postId) => (await View(viewer, postId)).MediaId ?? throw Missing();
}
