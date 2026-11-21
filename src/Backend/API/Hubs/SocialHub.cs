using System.Security.Claims;
using Entities.DTOs;
using Entities.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Services.Interfaces;

namespace API.Hubs;

[Authorize]
public sealed class SocialHub(ISocialSessionService sessions, IAuthService auth, ISocialDirectService direct) : Hub
{
    Guid Player => Guid.Parse(Context.UserIdentifier!);

    public async Task Presence()
    {
        await Validate();
        await sessions.Touch(Player, Context.ConnectionId);
    }

    public async Task Whisper(Guid recipient, string body)
    {
        await Validate();
        if (!(await sessions.Connections(Player)).Contains(Context.ConnectionId, StringComparer.Ordinal))
            throw new HubException("Bạn cần ở sảnh để thì thầm.");
        try { await direct.Whisper(Player, recipient, body); }
        catch (AppException error) { throw new HubException(error.Message); }
    }

    async Task Validate()
    {
        if (Context.User?.FindFirstValue("access_scope") == "game" &&
            int.TryParse(Context.User.FindFirstValue("auth_version"), out var version) &&
            await auth.ValidateSessionAsync(Player, version))
            return;
        Context.Abort();
        throw new HubException("Phiên đăng nhập không còn hiệu lực.");
    }

    public override Task OnDisconnectedAsync(Exception? exception) =>
        Guid.TryParse(Context.UserIdentifier, out var player) ? sessions.Leave(player, Context.ConnectionId) : Task.CompletedTask;
}

public sealed class SocialEvents(IHubContext<SocialHub> hub, ISocialSessionService sessions) : ISocialEvents
{
    public Task Message(Guid recipient, SocialMessageItem message) =>
        hub.Clients.Users(recipient.ToString(), message.SenderId.ToString()).SendAsync("MessageChanged", message.ConversationId);

    public async Task Whisper(Guid sender, Guid recipient, SocialWhisper message)
    {
        var targets = await sessions.Connections(recipient);
        if (targets.Length == 0 || (await sessions.Connections(sender)).Length == 0)
            throw new NotFoundException("Người chơi không còn ở sảnh.");
        await hub.Clients.Clients(targets).SendAsync("WhisperReceived", message);
    }
}
