using System.Net;
using System.Security.Cryptography;
using System.Text;
using Entities.Exceptions;
using Entities.Game;
using Microsoft.Extensions.Logging;
using PickleVerse.Progression;
using PickleVerse.Replays;
using Repositories.Caching;
using Repositories.UnitOfWork;
using Services.Configuration;
using Services.Interfaces;

namespace Services.Implementations;

public sealed class DedicatedServerService(IUnitOfWork unit, DedicatedLeaseRepository leases, ServerSettings settings,
    IDedicatedMatchResults game, IHybridCoachingService coaching, IMatchReplayService replays,
    ILogger<DedicatedServerService>? logger = null) : IDedicatedServerService
{
    public bool Authenticate(string key) => settings.Dedicated.Enabled && key.Length == settings.Dedicated.ServiceKey.Length &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(settings.Dedicated.ServiceKey));
    static AppException Error(string text, HttpStatusCode code = HttpStatusCode.Conflict) => new(text, code);
    public async Task<List<Entities.DTOs.LiveMatchDto>> LiveMatches()
    {
        var output = new List<Entities.DTOs.LiveMatchDto>();
        foreach (var match in await unit.Game.ActiveOnlineMatches())
            output.Add(new() { MatchId = match.Matchid, Status = match.Status, AuthorityMode = match.Authoritymode,
                CreatedAt = match.Createdat, StartedAt = match.Startedat, HeartbeatAt = match.Serverheartbeatat,
                HeartbeatStale = match.Authoritymode == MatchAuthorityMode.Dedicated &&
                    (match.Serverheartbeatat ?? match.Updatedat) < DateTime.UtcNow.AddSeconds(-settings.Dedicated.LeaseSeconds),
                Roster = Mapping.RelationalMatch.Roster(match), Live = await leases.Live(match.Matchid.ToString("N")),
                StopOperation = await unit.Game.DedicatedOperation(match.Matchid) });
        return output;
    }
    async Task<DedicatedAllocation> Guard(string lease, string worker)
    {
        var allocation = await leases.ReadLease(lease, worker);
        if (allocation == null) throw Error("Phiên máy chủ đã hết hiệu lực.", HttpStatusCode.Gone);
        return allocation;
    }
    public async Task<DedicatedTicket> Ticket(Guid player, DedicatedCommand request, int authVersion)
    {
        if (!await unit.Users.IsAuthVersionValidAsync(player, authVersion))
            throw Error("Phiên đăng nhập đã hết hiệu lực.", HttpStatusCode.Unauthorized);
        if (!settings.Dedicated.Enabled) throw Error("Chế độ máy chủ riêng chưa bật.", HttpStatusCode.ServiceUnavailable);
        if (!Guid.TryParse(request.MatchId, out var match)) throw Error("Mã trận không hợp lệ.", HttpStatusCode.BadRequest);
        var record = await unit.Game.Match(m => m.Matchid == match && m.Gamematchplayers.Any(p => p.Playerid == player));
        if (record == null) throw Error("Không tìm thấy trận của bạn.", HttpStatusCode.NotFound);
        if (record.Authoritymode != MatchAuthorityMode.Dedicated) throw Error("Không thể chuyển máy chủ khi trận đang diễn ra.");
        if (record.Status != MatchStatus.Playing) throw Error("Trận chưa sẵn sàng hoặc đã kết thúc.");
        if (!await unit.Game.CanAdmitDedicated(match, player)) throw Error("Bạn không còn quyền tham gia trận này.", HttpStatusCode.Forbidden);
        var allocation = await leases.ReadMatch(match.ToString("N"));
        if (allocation == null)
        {
            await game.InterruptDedicated(match);
            throw Error("Máy chủ mất phiên thi đấu; trận đã gián đoạn.", HttpStatusCode.Gone);
        }
        // A client can recover activation if the API stopped after committing PLAYING.
        if (allocation.State == DedicatedAllocationStatus.Reserved)
            allocation = await leases.Activate(allocation.MatchId, settings.Dedicated.LeaseSeconds)
                ?? throw Error("Máy chủ đã mất chỗ giữ cho trận.", HttpStatusCode.Gone);
        var ticket = new DedicatedTicket { State = allocation.State, Session = allocation.Session, Region = allocation.Region, Roster = allocation.Roster, AuthVersion = authVersion };
        if (allocation.State == DedicatedAllocationStatus.Ready)
        {
            ticket.ConnectionToken = Convert.ToBase64String(DedicatedToken.Create(settings.Dedicated.ServiceKey, allocation.MatchId,
                player.ToString("N"), allocation.LeaseId, Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeSeconds(), authVersion));
        }
        return ticket;
    }
    public Task<DedicatedAllocation?> Poll(string workerId, int protocol)
    {
        if (!Guid.TryParseExact(workerId, "N", out _)) throw Error("Mã worker không hợp lệ.", HttpStatusCode.BadRequest);
        if (protocol != settings.Dedicated.Protocol) throw Error("Phiên bản máy chủ thi đấu không khớp.");
        return leases.Poll(workerId, settings.Dedicated.LeaseSeconds);
    }
    public async Task<DedicatedHeartbeat> Renew(DedicatedCommand request, bool ready, string worker)
    {
        var allocation = await Guard(request.LeaseId, worker);
        if (request.Sessions == null || request.Sessions.Count > allocation.Roster.HumanCount ||
            request.Sessions.Any(s => s == null || s.AuthVersion < 0 ||
                !allocation.Roster.members.Any(m => !m.bot && Guid.TryParse(m.id, out var id) && id == s.PlayerId)) ||
            request.Sessions.Select(s => s.PlayerId).Distinct().Count() != request.Sessions.Count)
            throw Error("Danh sách phiên người chơi không hợp lệ.", HttpStatusCode.BadRequest);
        if (!await leases.Renew(allocation.LeaseId, worker, ready ? DedicatedAllocationStatus.Ready : null, settings.Dedicated.LeaseSeconds))
            throw Error("Phiên máy chủ đã hết hiệu lực.", HttpStatusCode.Gone);
        var match = Guid.Parse(allocation.MatchId);
        if (ready && allocation.State == DedicatedAllocationStatus.Starting)
            logger?.LogInformation("Dedicated ready {MatchId} on worker {WorkerId}", match, worker);
        await unit.Game.TouchDedicated(match);
        if (request.Live != null)
        {
            var live = request.Live;
            if (new[] { live.ScoreA, live.ScoreB, live.GamesA, live.GamesB }.Any(x => x < 0 || x > 100000)
                || live.Players == null || live.Players.Count > 4 || live.Players.Select(p => p.Id).Distinct().Count() != live.Players.Count
                || live.Players.Any(p => !allocation.Roster.members.Any(m => m.id == p.Id)))
                throw Error("Trạng thái live không hợp lệ.", HttpStatusCode.BadRequest);
            live.ObservedAt = DateTime.UtcNow;
            await leases.SaveLive(allocation.MatchId, live);
        }
        var reply = new DedicatedHeartbeat();
        foreach (var session in request.Sessions)
            if (!await unit.Users.IsAuthVersionValidAsync(session.PlayerId, session.AuthVersion))
                reply.ExpiredSessions.Add(session);
        var op = await unit.Game.DedicatedOperation(Guid.Parse(allocation.MatchId));
        if (op == null || op.AcknowledgedAt != null) return reply;
        await unit.Game.BindOperation(op.OperationId, request.LeaseId);
        await game.InterruptDedicated(op.MatchId);
        reply.StopOperationId = op.OperationId;
        reply.Reason = op.Reason;
        return reply;
    }
    public async Task Acknowledge(DedicatedCommand request, string worker)
    {
        var allocation = await Guard(request.LeaseId, worker);
        var op = await unit.Game.DedicatedOperation(Guid.Parse(allocation.MatchId));
        if (op == null || op.OperationId != request.OperationId || op.LeaseId != request.LeaseId)
            throw Error("Lệnh vận hành không thuộc phiên máy chủ.", HttpStatusCode.Forbidden);
        await game.InterruptDedicated(op.MatchId);
        await unit.Game.AcknowledgeOperation(op.OperationId, request.LeaseId, DateTime.UtcNow);
    }
    public async Task AuthorizeAdmission(DedicatedAdmissionCommand request, string worker)
    {
        var allocation = await Guard(request.LeaseId, worker);
        if (!allocation.Roster.members.Any(m => !m.bot && Guid.TryParse(m.id, out var id) && id == request.PlayerId)
            || !await unit.Game.CanAdmitDedicated(Guid.Parse(allocation.MatchId), request.PlayerId)
            || !await unit.Users.IsAuthVersionValidAsync(request.PlayerId, request.AuthVersion))
            throw Error("Quyền vào trận không còn hiệu lực.", HttpStatusCode.Forbidden);
    }
    public async Task Complete(DedicatedCompletion request, string worker)
    {
        var allocation = await Guard(request.LeaseId, worker);
        await game.CompleteDedicated(Guid.Parse(allocation.MatchId), request.Results, request.ReplayChunkCount);
        await leases.Renew(allocation.LeaseId, worker, DedicatedAllocationStatus.Finishing, settings.Dedicated.LeaseSeconds);
    }
    public async Task Release(DedicatedCommand request, string worker)
    {
        var allocation = await leases.Release(request.LeaseId, worker);
        if (allocation != null)
            await game.InterruptDedicated(Guid.Parse(allocation.MatchId));
    }
    public async Task InterruptExpired()
    {
        foreach (var match in await unit.Game.PendingStops()) await game.InterruptDedicated(match);
        var now = DateTime.UtcNow;
        foreach (var match in await unit.Game.ExpiredDedicated(now.AddSeconds(-settings.Dedicated.LeaseSeconds)))
            if (await leases.ReadMatch(match.ToString("N")) == null)
                await game.InterruptDedicated(match);
        foreach (var match in await unit.Game.ExpiredDedicatedPreparations(now.AddSeconds(-Math.Max(30, settings.Matchmaking.ReadyWindowSeconds + 15))))
            if (await leases.ReadMatch(match.ToString("N")) == null && !await leases.IsWaiting(match.ToString("N")))
                await game.InterruptDedicated(match);
    }
    public async Task<TelemetryReceipt> Telemetry(string lease, TelemetryBatch batch, string worker)
    {
        var allocation = await Guard(lease, worker);
        if (batch.sessionId != allocation.MatchId || batch.sessionType != "match" || !Guid.TryParse(batch.playerId, out var player) ||
            !allocation.Roster.members.Any(m => m.id == batch.playerId && !m.bot)) throw Error("Telemetry không thuộc trận được cấp.", HttpStatusCode.Forbidden);
        return await coaching.Ingest(player, batch, true);
    }
    async Task<(Guid writer, Guid match)> ReplayAccess(string lease, string worker)
    {
        var allocation = await Guard(lease, worker); var match = Guid.Parse(allocation.MatchId);
        var record = await unit.Game.Match(m => m.Matchid == match) ?? throw Error("Không tìm thấy trận.");
        return (record.Gamematchplayers.Single(p => p.Isreplaywriter).Playerid, match);
    }
    public async Task<ReplayMetadata> SaveReplay(string lease, ReplayMetadata metadata, string worker)
    {
        var access = await ReplayAccess(lease, worker);
        if (metadata.matchId != access.match.ToString("N")) throw Error("Replay không thuộc trận được cấp.", HttpStatusCode.Forbidden);
        metadata.playerId = access.writer.ToString("N");
        return await replays.Save(access.writer, access.match, metadata, true);
    }
    public async Task<ReplayChunkInfo> UploadReplay(string lease, int index, ReplayChunkUpload chunk, string worker)
    { var access = await ReplayAccess(lease, worker); return await replays.Upload(access.writer, access.match, index, chunk, true); }
    public async Task<ReplayBatchReceipt> UploadReplayBatch(string lease, ReplayChunkBatchUpload batch, string worker)
    {
        var allocation = await Guard(lease, worker);
        return await replays.UploadDedicatedBatch(Guid.Parse(allocation.ReplayWriter), Guid.Parse(allocation.MatchId), batch);
    }
}
