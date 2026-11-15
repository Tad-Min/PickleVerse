using System.Net;
using Entities.DTOs;
using Entities.Exceptions;
using Entities.Models;
using Microsoft.Extensions.Logging;
using Repositories.UnitOfWork;
using Services.Interfaces;

namespace Services.Implementations;

public sealed class SocialDirectService(IUnitOfWork unit, ISocialEvents events, TimeProvider clock,
    ILogger<SocialDirectService> logger) : ISocialDirectService
{
    internal static string MessageText(string? body)
    {
        string value = (body ?? "").Trim();
        if (value.Length is < 1 or > 1000) throw new BadRequestException("Nhập nội dung từ 1 đến 1.000 ký tự.");
        return value;
    }
    static void ValidateTarget(Guid player, Guid recipient)
    {
        if (recipient == Guid.Empty || recipient == player) throw new BadRequestException("Chọn một người chơi khác.");
    }
    public async Task<IReadOnlyList<SocialConversationItem>> Inbox(Guid player, int offset)
    {
        if (offset is < 0 or > 100000) throw new BadRequestException("Trang hộp thư không hợp lệ.");
        return await unit.Game.SocialInbox(player, offset);
    }
    public async Task<Guid?> Conversation(Guid player, Guid recipient)
    {
        ValidateTarget(player, recipient);
        var first = player.CompareTo(recipient) < 0 ? player : recipient;
        return (await unit.Game.SocialConversationFor(first, first == player ? recipient : player))?.ConversationId;
    }
    async Task RequireMember(Guid player, Guid conversation)
    {
        if (!await unit.Game.IsConversationMember(player, conversation)) throw new NotFoundException("Không tìm thấy hội thoại.");
    }
    public async Task<SocialPage<SocialMessageItem>> History(Guid player, Guid conversation, long? before)
    {
        await RequireMember(player, conversation);
        return await unit.Game.SocialMessages(conversation, before ?? long.MaxValue);
    }
    public async Task MarkRead(Guid player, Guid conversation, long through)
    {
        await RequireMember(player, conversation);
        if (through < 1 || !await unit.Game.IsConversationMessage(conversation, through))
            throw new BadRequestException("Tin nhắn không thuộc hội thoại.");
        await unit.Game.MarkSocialRead(player, conversation, through);
    }
    public async Task<SocialMessageItem> Send(Guid player, SocialMessageInput input)
    {
        Guid recipient = input.PlayerId;
        ValidateTarget(player, recipient);
        if (input.ClientMessageId == Guid.Empty) throw new BadRequestException("Thiếu mã gửi tin.");
        string text = MessageText(input.Body);
        await using var transaction = await unit.Game.BeginTransactionAsync();
        if (await unit.Game.LockPlayers(player, recipient) != 2) throw new NotFoundException("Không tìm thấy người chơi.");
        await unit.Game.Lock($"social-message:{player:N}:{input.ClientMessageId:N}");
        var existing = await unit.Game.MessageByClientId(player, input.ClientMessageId);
        var first = player.CompareTo(recipient) < 0 ? player : recipient;
        var second = first == player ? recipient : player;
        if (existing != null)
        {
            var previous = await unit.Game.SocialConversationById(existing.ConversationId);
            if (previous?.FirstPlayerId != first || previous.SecondPlayerId != second || existing.Body != text)
                throw new AppException("Mã gửi đã được dùng cho tin khác.", HttpStatusCode.Conflict);
            return Item(existing);
        }
        if (!await unit.Game.Friends(player, recipient) || await unit.Game.Blocked(player, recipient)
            || await unit.Game.AccountStatus(recipient) != UserStatus.Active)
            throw new ForbiddenException("Bạn cần kết bạn và có quyền nhắn tin với người chơi này.");
        var conversation = await unit.Game.SocialConversationFor(first, second);
        var now = clock.GetUtcNow().UtcDateTime;
        if (conversation == null)
        {
            conversation = new() { ConversationId = Guid.NewGuid(), FirstPlayerId = first, SecondPlayerId = second, UpdatedAt = now };
            unit.Game.AddSocialConversation(conversation);
        }
        conversation.UpdatedAt = now;
        var message = new SocialMessage
        {
            ConversationId = conversation.ConversationId, SenderId = player, ClientMessageId = input.ClientMessageId,
            Body = text, CreatedAt = now
        };
        unit.Game.AddSocialMessage(message);
        await unit.SaveChangesAsync();
        await transaction.CommitAsync();
        var dto = Item(message);
        try { await events.Message(recipient, dto); }
        catch (Exception error) { logger.LogWarning("Social message notification deferred ({ErrorType})", error.GetType().Name); }
        return dto;
    }
    static SocialMessageItem Item(SocialMessage row) => new(row.MessageId, row.ConversationId, row.SenderId, row.Body, row.CreatedAt);

    public async Task Whisper(Guid player, Guid recipient, string? body)
    {
        ValidateTarget(player, recipient);
        var text = MessageText(body);
        await using var transaction = await unit.Game.BeginTransactionAsync();
        if (await unit.Game.LockPlayers(player, recipient) != 2 ||
            await unit.Game.Blocked(player, recipient) || !await unit.Game.ActiveVerified(recipient))
            throw new ForbiddenException("Bạn không thể thì thầm với người chơi này.");
        string name = await unit.Game.PlayerName(player) ?? "";
        await events.Whisper(player, recipient, new(player, name, text, clock.GetUtcNow().UtcDateTime));
        await transaction.CommitAsync();
    }
}
