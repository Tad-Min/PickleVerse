using Entities.DTOs;
using Repositories.Caching;
using Services.Interfaces;

namespace Services.Implementations;

public sealed class SocialSessionService(SocialPresenceRepository presence) : ISocialSessionService
{
    public Task Touch(Guid player, string connectionId) => presence.Touch(player, connectionId);
    public Task Leave(Guid player, string connectionId) => presence.Clear(player, connectionId);
    public Task<string[]> Connections(Guid player) => presence.Connections(player);
    public Task GrantVideo(string token, SocialVideoAccess access) =>
        presence.GrantVideo(token, new(access.PlayerId, access.AuthVersion, access.PostId));
    public async Task<SocialVideoAccess?> ReadVideoGrant(string token)
    {
        var grant = await presence.ReadVideoGrant(token);
        return grant == null ? null : new(grant.PlayerId, grant.AuthVersion, grant.PostId);
    }
}
