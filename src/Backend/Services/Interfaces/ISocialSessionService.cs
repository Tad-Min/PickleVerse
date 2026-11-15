using Entities.DTOs;

namespace Services.Interfaces;

public interface ISocialSessionService
{
    Task Touch(Guid player, string connectionId);
    Task Leave(Guid player, string connectionId);
    Task<string[]> Connections(Guid player);
    Task GrantVideo(string token, SocialVideoAccess access);
    Task<SocialVideoAccess?> ReadVideoGrant(string token);
}
