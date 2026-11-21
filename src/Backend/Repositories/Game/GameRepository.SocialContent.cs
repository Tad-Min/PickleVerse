using Entities.DTOs;
using Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Game;

public sealed partial class GameRepository
{
    const int SocialPageSize = 20;

    IQueryable<SocialPost> VisiblePosts(Guid viewer) => db.Set<SocialPost>().Where(post =>
        post.AuthorId == viewer ||
        !db.Blockedusers.Any(b => b.Userid == viewer && b.Blockeduserid == post.AuthorId || b.Blockeduserid == viewer && b.Userid == post.AuthorId)
        && (post.Audience == "community" || (post.Audience == "friends" || post.Audience == "friends_except")
            && db.Friendships.Any(f => f.Userid1 == viewer && f.Userid2 == post.AuthorId || f.Userid2 == viewer && f.Userid1 == post.AuthorId)
            && (post.Audience == "friends" || !db.Set<SocialPostExclusion>().Any(x => x.PostId == post.PostId && x.PlayerId == viewer))));

    public Task<SocialPost?> VisiblePost(Guid viewer, long post) => VisiblePosts(viewer).AsNoTracking().SingleOrDefaultAsync(x => x.PostId == post);
    public Task<SocialPost?> OwnedPost(Guid author, long post) => db.Set<SocialPost>().SingleOrDefaultAsync(x => x.PostId == post && x.AuthorId == author);
    public void AddSocialPost(SocialPost post) => db.Add(post);
    public void RemoveSocialPost(SocialPost post) => db.Remove(post);

    IQueryable<SocialPostItem> PostItems(Guid viewer, IQueryable<SocialPost> posts) =>
        from post in posts
        join profile in db.Playerprofiles on post.AuthorId equals profile.Userid
        select new SocialPostItem(post.PostId, post.AuthorId, profile.Playername, post.Body, post.Audience,
            post.MediaId != null, post.ReplayMatchId, post.CreatedAt, post.EditedAt,
            db.Set<SocialPostLike>().Count(x => x.PostId == post.PostId),
            db.Set<SocialPostComment>().Count(x => x.PostId == post.PostId &&
                !db.Blockedusers.Any(b => b.Userid == viewer && b.Blockeduserid == x.AuthorId || b.Blockeduserid == viewer && b.Userid == x.AuthorId)),
            db.Set<SocialPostLike>().Any(x => x.PostId == post.PostId && x.PlayerId == viewer),
            post.AuthorId == viewer ? post.MediaId : null,
            db.Set<SocialPostExclusion>().Where(x => x.PostId == post.PostId && post.AuthorId == viewer).Select(x => x.PlayerId).ToList());

    public async Task<SocialPage<SocialPostItem>> Feed(Guid viewer, long before)
    {
        var posts = VisiblePosts(viewer).Where(post => post.PostId < before)
            .OrderByDescending(post => post.PostId).Take(SocialPageSize + 1);
        var items = await PostItems(viewer, posts).ToListAsync();
        items.Sort((first, second) => second.PostId.CompareTo(first.PostId));
        return SocialPage(items, post => post.PostId);
    }

    public Task<SocialPostItem> PostItem(Guid viewer, long post) =>
        PostItems(viewer, VisiblePosts(viewer).Where(x => x.PostId == post)).SingleAsync();

    static SocialPage<T> SocialPage<T>(List<T> rows, Func<T, long> id)
    {
        bool more = rows.Count > SocialPageSize;
        if (more) rows.RemoveAt(SocialPageSize);
        return new SocialPage<T>(rows, more ? id(rows[^1]) : null);
    }

    public async Task SetPostExclusions(long post, Guid[] players)
    {
        await db.Set<SocialPostExclusion>().Where(x => x.PostId == post).ExecuteDeleteAsync();
        db.AddRange(players.Select(player => new SocialPostExclusion(post, player)));
    }

    public async Task<bool> PostExclusionTargetsExist(Guid[] players) =>
        await db.Playerprofiles.CountAsync(x => players.Contains(x.Userid)) == players.Length;

    public async Task SetPostLike(long post, Guid player, bool liked)
    {
        var existing = await db.Set<SocialPostLike>().FindAsync(post, player);
        if (liked && existing == null) db.Add(new SocialPostLike(post, player));
        else if (!liked && existing != null) db.Remove(existing);
    }

    public async Task<SocialPage<SocialCommentItem>> PostComments(Guid viewer, long post, long before)
    {
        var rows = await (from comment in db.Set<SocialPostComment>()
                          join profile in db.Playerprofiles on comment.AuthorId equals profile.Userid
                          where comment.PostId == post && comment.CommentId < before &&
                             !db.Blockedusers.Any(b => b.Userid == viewer && b.Blockeduserid == comment.AuthorId || b.Blockeduserid == viewer && b.Userid == comment.AuthorId)
                          orderby comment.CommentId descending
                          select new SocialCommentItem(comment.CommentId, comment.AuthorId, profile.Playername, comment.Body, comment.CreatedAt))
            .Take(SocialPageSize + 1).ToListAsync();
        return SocialPage(rows, comment => comment.CommentId);
    }
    public void AddPostComment(SocialPostComment comment) => db.Add(comment);
    public Task RemovePostComment(Guid player, SocialPost post, long comment) => db.Set<SocialPostComment>()
        .Where(x => x.PostId == post.PostId && x.CommentId == comment && (x.AuthorId == player || post.AuthorId == player))
        .ExecuteDeleteAsync();

    public void AddSocialMedia(SocialMedia media) => db.Add(media);
    public Task<SocialMedia?> SocialMediaById(Guid id) => db.Set<SocialMedia>().SingleOrDefaultAsync(x => x.MediaId == id);
    public Task<bool> MediaUsed(Guid id, long exceptPost) => db.Set<SocialPost>().AnyAsync(x => x.MediaId == id && x.PostId != exceptPost);
    public void RemoveSocialMedia(SocialMedia media) => db.Remove(media);
    public Task<List<Guid>> ExpiredSocialMedia(DateTime cutoff) => db.Set<SocialMedia>()
        .Where(x => x.Deleted || x.CreatedAt < cutoff && !db.Set<SocialPost>().Any(p => p.MediaId == x.MediaId))
        .OrderBy(x => x.CreatedAt).Select(x => x.MediaId).Take(100).ToListAsync();

    public Task<SocialMessage?> MessageByClientId(Guid player, Guid clientId) =>
        db.Set<SocialMessage>().SingleOrDefaultAsync(x => x.SenderId == player && x.ClientMessageId == clientId);
    public Task<SocialConversation?> SocialConversationById(Guid id) =>
        db.Set<SocialConversation>().SingleOrDefaultAsync(x => x.ConversationId == id);
    public Task<SocialConversation?> SocialConversationFor(Guid first, Guid second) =>
        db.Set<SocialConversation>().SingleOrDefaultAsync(x => x.FirstPlayerId == first && x.SecondPlayerId == second);
    public void AddSocialConversation(SocialConversation conversation)
    {
        db.Add(conversation);
        db.AddRange(new SocialConversationMember { ConversationId = conversation.ConversationId, PlayerId = conversation.FirstPlayerId },
            new SocialConversationMember { ConversationId = conversation.ConversationId, PlayerId = conversation.SecondPlayerId });
    }
    public void AddSocialMessage(SocialMessage message) => db.Add(message);
    public Task<bool> IsConversationMember(Guid player, Guid conversation) => db.Set<SocialConversationMember>()
        .AnyAsync(x => x.PlayerId == player && x.ConversationId == conversation);
    public Task<bool> IsConversationMessage(Guid conversation, long message) => db.Set<SocialMessage>()
        .AnyAsync(x => x.MessageId == message && x.ConversationId == conversation);
    public Task MarkSocialRead(Guid player, Guid conversation, long through) => db.Set<SocialConversationMember>()
        .Where(x => x.PlayerId == player && x.ConversationId == conversation && x.ReadThrough < through)
        .ExecuteUpdateAsync(update => update.SetProperty(x => x.ReadThrough, through));

    public Task<List<SocialConversationItem>> SocialInbox(Guid player, int offset) =>
        (from member in db.Set<SocialConversationMember>()
         join conversation in db.Set<SocialConversation>() on member.ConversationId equals conversation.ConversationId
         let other = conversation.FirstPlayerId == player ? conversation.SecondPlayerId : conversation.FirstPlayerId
         join profile in db.Playerprofiles on other equals profile.Userid
         where member.PlayerId == player
         orderby conversation.UpdatedAt descending, conversation.ConversationId
         select new SocialConversationItem(conversation.ConversationId, other, profile.Playername,
             db.Set<SocialMessage>().Where(x => x.ConversationId == conversation.ConversationId)
                 .OrderByDescending(x => x.MessageId).Select(x => x.Body).FirstOrDefault() ?? "",
             conversation.UpdatedAt,
             db.Set<SocialMessage>().Count(x => x.ConversationId == conversation.ConversationId && x.SenderId != player && x.MessageId > member.ReadThrough),
             profile.User.Status == UserStatus.Active &&
             db.Friendships.Any(x => x.Userid1 == conversation.FirstPlayerId && x.Userid2 == conversation.SecondPlayerId) &&
             !db.Blockedusers.Any(b => b.Userid == player && b.Blockeduserid == other || b.Blockeduserid == player && b.Userid == other)))
        .Skip(offset).Take(SocialPageSize + 1).ToListAsync();

    public async Task<SocialPage<SocialMessageItem>> SocialMessages(Guid conversation, long before)
    {
        var rows = await db.Set<SocialMessage>().Where(x => x.ConversationId == conversation && x.MessageId < before)
            .OrderByDescending(x => x.MessageId).Select(x => new SocialMessageItem(x.MessageId, x.ConversationId, x.SenderId, x.Body, x.CreatedAt))
            .Take(SocialPageSize + 1).ToListAsync();
        return SocialPage(rows, message => message.MessageId);
    }
}
