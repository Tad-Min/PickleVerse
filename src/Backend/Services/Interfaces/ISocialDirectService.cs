using Entities.DTOs;

namespace Services.Interfaces;

public interface ISocialDirectService
{
    Task<IReadOnlyList<SocialConversationItem>> Inbox(Guid player, int offset);
    Task<Guid?> Conversation(Guid player, Guid recipient);
    Task<SocialPage<SocialMessageItem>> History(Guid player, Guid conversation, long? before);
    Task MarkRead(Guid player, Guid conversation, long through);
    Task<SocialMessageItem> Send(Guid player, SocialMessageInput input);
    Task Whisper(Guid player, Guid recipient, string? body);
}

public interface ISocialEvents
{
    Task Message(Guid recipient, SocialMessageItem message);
    Task Whisper(Guid sender, Guid recipient, SocialWhisper message);
}
