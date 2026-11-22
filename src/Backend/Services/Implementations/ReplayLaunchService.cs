using System.Net;
using System.Security.Cryptography;
using Entities.DTOs;
using Entities.Enums;
using Entities.Exceptions;
using Entities.Game;
using PickleVerse.Replays;
using Repositories.Caching;
using Repositories.UnitOfWork;
using Services.Interfaces;

namespace Services.Implementations;

// These capabilities only read one replay; they never create a login or alter a game session.
public sealed class ReplayLaunchService(IUnitOfWork unit, IMatchReplayService replays,
    ReplayLaunchRepository grants, TimeProvider clock) : IReplayLaunchService
{
    static readonly TimeSpan LaunchLifetime = TimeSpan.FromMinutes(2);
    static readonly TimeSpan ViewLifetime = TimeSpan.FromHours(2);
    DateTime Now => clock.GetUtcNow().UtcDateTime;
    static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    static bool Valid(string token) => token is { Length: 64 } && token.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    static AppException Expired() => new("Vé xem replay đã hết hạn hoặc đã được sử dụng. Hãy bấm Xem replay lại trên trang Admin.", HttpStatusCode.Unauthorized);
    async Task Guard(ReplayLaunchAccess access)
    {
        if (access.ExpiresAt <= Now) throw Expired();
        var user = await unit.Users.GetByIdWithRoleAndProfileAsync(access.AdminId);
        if (user == null || user.Authversion != access.AuthVersion || user.Status != UserStatus.Active || user.Role?.Rolename != "Admin")
            throw new AppException("Quyền xem replay của phiên Admin này đã bị thu hồi.", HttpStatusCode.Forbidden);
    }
    public async Task<ReplayLaunchLink> Issue(Guid admin, int authVersion, Guid match)
    {
        var access = new ReplayLaunchAccess(admin, authVersion, match, Now + LaunchLifetime);
        await Guard(access);
        await replays.Read(admin, match, admin: true);
        string code = Token();
        if (!await grants.Issue(code, access, LaunchLifetime)) throw new AppException("Chưa tạo được vé xem replay. Hãy thử lại.", HttpStatusCode.ServiceUnavailable);
        return new(match, $"pickleverse://replay/{match:N}?code={code}", access.ExpiresAt);
    }
    public async Task<ReplayLaunchView> Redeem(string code)
    {
        if (!Valid(code)) throw Expired();
        var access = await grants.ReadCode(code) ?? throw Expired();
        await Guard(access);
        var metadata = await replays.Read(access.AdminId, access.MatchId, admin: true);
        string token = Token();
        var view = access with { ExpiresAt = Now + ViewLifetime };
        // Consume only after metadata is available. Concurrent redeems have exactly one winner.
        if (!await grants.Exchange(code, access, token, view, ViewLifetime)) throw Expired();
        return new(access.MatchId, token, view.ExpiresAt, metadata);
    }
    async Task<ReplayLaunchAccess> Access(string token, Guid match)
    {
        if (!Valid(token)) throw Expired();
        var access = await grants.ReadView(token) ?? throw Expired();
        await Guard(access);
        if (access.MatchId != match) throw new AppException("Vé xem không thuộc replay này.", HttpStatusCode.Forbidden);
        return access;
    }
    public async Task<ReplayMetadata> Read(string token, Guid match)
    {
        var access = await Access(token, match);
        return await replays.Read(access.AdminId, match, admin: true);
    }
    public async Task<ReplayChunkUpload> Chunk(string token, Guid match, int index)
    {
        var access = await Access(token, match);
        return await replays.Chunk(access.AdminId, match, index, admin: true);
    }
}
