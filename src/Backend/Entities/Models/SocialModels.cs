namespace Entities.Models;

public sealed class SocialMedia
{
    public Guid MediaId { get; set; }
    public Guid OwnerId { get; set; }
    public long Bytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool Ready { get; set; }
    public bool Deleted { get; set; }
}

public sealed class SocialPost
{
    public long PostId { get; set; }
    public Guid AuthorId { get; set; }
    public string Body { get; set; } = "";
    public string Audience { get; set; } = "";
    public Guid? MediaId { get; set; }
    public Guid? ReplayMatchId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? EditedAt { get; set; }
}

public sealed record SocialPostExclusion(long PostId, Guid PlayerId);
public sealed record SocialPostLike(long PostId, Guid PlayerId);

public sealed class SocialPostComment
{
    public long CommentId { get; set; }
    public long PostId { get; set; }
    public Guid AuthorId { get; set; }
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class SocialConversation
{
    public Guid ConversationId { get; set; }
    public Guid FirstPlayerId { get; set; }
    public Guid SecondPlayerId { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class SocialConversationMember
{
    public Guid ConversationId { get; set; }
    public Guid PlayerId { get; set; }
    public long ReadThrough { get; set; }
}

public sealed class SocialMessage
{
    public long MessageId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public Guid ClientMessageId { get; set; }
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
