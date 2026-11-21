namespace Entities.DTOs;

public sealed record SocialPage<T>(IReadOnlyList<T> Items, long? NextBefore);
public sealed record SocialPostInput(string? Body, string Audience, Guid? MediaId, Guid? ReplayMatchId, Guid[]? Exclude);
public sealed record SocialPostItem(long PostId, Guid AuthorId, string Author, string Body, string Audience,
    bool HasVideo, Guid? ReplayMatchId, DateTime CreatedAt, DateTime? EditedAt, int Likes, int Comments,
    bool Liked, Guid? MediaId, IReadOnlyList<Guid>? Exclude);
public sealed record SocialText(string? Body);
public sealed record SocialLikeInput(bool Liked);
public sealed record SocialCommentItem(long CommentId, Guid AuthorId, string Author, string Body, DateTime CreatedAt);
public sealed record SocialMediaReceipt(Guid MediaId, long Bytes);
public sealed record SocialPlayback(string Path);
public sealed record SocialVideo(Stream Stream, long Length, long Start, long End, bool Partial, long BytesToSkip);
public sealed record SocialVideoAccess(Guid PlayerId, int AuthVersion, long PostId);
public sealed record SocialMessageInput(Guid PlayerId, Guid ClientMessageId, string? Body);
public sealed record SocialMessageItem(long MessageId, Guid ConversationId, Guid SenderId, string Body, DateTime CreatedAt);
public sealed record SocialConversationItem(Guid ConversationId, Guid PlayerId, string PlayerName, string LastMessage,
    DateTime UpdatedAt, int Unread, bool CanSend);
public sealed record SocialReadInput(long Through);
public sealed record SocialConversationLookup(Guid? ConversationId);
public sealed record SocialWhisper(Guid SenderId, string Sender, string Body, DateTime SentAt);
