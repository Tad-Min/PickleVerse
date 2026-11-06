using System.Net;
using System.Security.Cryptography;
using System.Text;
using Entities.Exceptions;
using Entities.Game;
using PickleVerse.Progression;
using PickleVerse.Replays;
using Repositories.Caching;
using Repositories.UnitOfWork;
using Services.Configuration;
using Services.Interfaces;

namespace Services.Implementations;

public sealed class DedicatedServerService(IUnitOfWork unit, DedicatedLeaseRepository leases, ServerSettings settings,
    IDedicatedMatchResults game, IHybridCoachingService coaching, IMatchReplayService replays) : IDedicatedServerService
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
    async Task<DedicatedAllocation> Guard(string lease)
    {
        var allocation = await leases.Read();
        if (allocation == null || allocation.LeaseId != lease) throw Error("Phiên máy chủ đã hết hiệu lực.", HttpStatusCode.Gone);
        return allocation;
    }
    public async Task<DedicatedTicket> Ticket(Guid player, DedicatedCommand request)
    {
        if (!settings.Dedicated.Enabled) throw Error("Chế độ máy chủ riêng chưa bật.", HttpStatusCode.ServiceUnavailable);
        if (!Guid.TryParse(request.MatchId, out var match)) throw Error("Mã trận không hợp lệ.", HttpStatusCode.BadRequest);
        var record = await unit.Game.Match(m => m.Matchid == match && m.Gamematchplayers.Any(p => p.Playerid == player));
        if (record == null) throw Error("Không tìm thấy trận của bạn.", HttpStatusCode.NotFound);
        if (record.Authoritymode != MatchAuthorityMode.Dedicated) throw Error("Không thể chuyển máy chủ khi trận đang diễn ra.");
        if (record.Status != MatchStatus.Playing) throw Error("Trận chưa sẵn sàng hoặc đã kết thúc.");
        if (!await unit.Game.CanAdmitDedicated(match, player)) throw Error("Bạn không còn quyền tham gia trận này.", HttpStatusCode.Forbidden);
        var allocation = await leases.Read();
        if (allocation?.MatchId != match.ToString("N"))
        {
            if (record.Serverheartbeatat.HasValue)
            {
                await game.InterruptDedicated(match);
                throw Error("Máy chủ mất phiên thi đấu; trận đã gián đoạn.", HttpStatusCode.Gone);
            }
            if (!await leases.HasWorker()) throw Error("Máy chủ thi đấu chưa sẵn sàng.", HttpStatusCode.ServiceUnavailable);
            allocation = await leases.Allocate(new() { MatchId = match.ToString("N"), LeaseId = Guid.NewGuid().ToString("N"),
                Session = "ds-" + match.ToString("N"), Region = record.Region ?? "asia", Roster = Mapping.RelationalMatch.Roster(record),
                Protocol = settings.Dedicated.Protocol, TelemetryEnabled = settings.HybridAi.IngestEnabled,
                ReplayWriter = record.Gamematchplayers.Single(p => p.Isreplaywriter).Playerid.ToString("N") });
            if (allocation == null)
            {
                if (record.Kind == "Custom") throw Error("Máy chủ đang bận. Vui lòng thử lại.");
                return new() { State = DedicatedAllocationStatus.Busy };
            }
            await unit.Game.MarkDedicated(match);
        }
        var ticket = new DedicatedTicket { State = allocation.State, Session = allocation.Session, Region = allocation.Region, Roster = allocation.Roster };
        if (allocation.State == DedicatedAllocationStatus.Ready)
        {
            ticket.ConnectionToken = Convert.ToBase64String(DedicatedToken.Create(settings.Dedicated.ServiceKey, allocation.MatchId,
                player.ToString("N"), allocation.LeaseId, Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeSeconds()));
        }
        return ticket;
    }
    public async Task<DedicatedAllocation?> Poll(string workerId)
    {
        if (!Guid.TryParseExact(workerId, "N", out _)) throw Error("Mã worker không hợp lệ.", HttpStatusCode.BadRequest);
        await leases.WorkerAlive(); return await leases.Claim(workerId, settings.Dedicated.LeaseSeconds);
    }
    public async Task<DedicatedHeartbeat> Renew(DedicatedCommand request, bool ready)
    {
        var allocation = await Guard(request.LeaseId);
        if (ready) allocation.State = DedicatedAllocationStatus.Ready;
        if (!await leases.Renew(allocation, settings.Dedicated.LeaseSeconds)) throw Error("Phiên máy chủ đã hết hiệu lực.", HttpStatusCode.Gone);
        await leases.WorkerAlive();
        await unit.Game.TouchDedicated(Guid.Parse(allocation.MatchId));
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
        var op = await unit.Game.DedicatedOperation(Guid.Parse(allocation.MatchId));
        if (op == null || op.AcknowledgedAt != null) return new();
        await unit.Game.BindOperation(op.OperationId, request.LeaseId);
        await game.InterruptDedicated(op.MatchId);
        return new() { StopOperationId = op.OperationId, Reason = op.Reason };
    }
    public async Task Acknowledge(DedicatedCommand request)
    {
        var allocation = await Guard(request.LeaseId);
        var op = await unit.Game.DedicatedOperation(Guid.Parse(allocation.MatchId));
        if (op == null || op.OperationId != request.OperationId || op.LeaseId != request.LeaseId)
            throw Error("Lệnh vận hành không thuộc phiên máy chủ.", HttpStatusCode.Forbidden);
        await game.InterruptDedicated(op.MatchId);
        await unit.Game.AcknowledgeOperation(op.OperationId, request.LeaseId, DateTime.UtcNow);
    }
    public async Task AuthorizeAdmission(DedicatedAdmissionCommand request)
    {
        var allocation = await Guard(request.LeaseId);
        if (!allocation.Roster.members.Any(m => !m.bot && Guid.TryParse(m.id, out var id) && id == request.PlayerId)
            || !await unit.Game.CanAdmitDedicated(Guid.Parse(allocation.MatchId), request.PlayerId))
            throw Error("Quyền vào trận không còn hiệu lực.", HttpStatusCode.Forbidden);
    }
    public async Task Complete(DedicatedCompletion request)
    {
        var allocation = await Guard(request.LeaseId);
        await game.CompleteDedicated(Guid.Parse(allocation.MatchId), request.Results);
        allocation.State = DedicatedAllocationStatus.Finishing;
        await leases.Renew(allocation, settings.Dedicated.LeaseSeconds);
    }
    public Task Release(DedicatedCommand request) => leases.Release(request.LeaseId);
    public async Task InterruptExpired()
    {
        foreach (var match in await unit.Game.PendingStops()) await game.InterruptDedicated(match);
        foreach (var match in await unit.Game.ExpiredDedicated(DateTime.UtcNow.AddSeconds(-settings.Dedicated.LeaseSeconds)))
            await game.InterruptDedicated(match);
    }
    public async Task<TelemetryReceipt> Telemetry(string lease, TelemetryBatch batch)
    {
        var allocation = await Guard(lease);
        if (batch.sessionId != allocation.MatchId || batch.sessionType != "match" || !Guid.TryParse(batch.playerId, out var player) ||
            !allocation.Roster.members.Any(m => m.id == batch.playerId && !m.bot)) throw Error("Telemetry không thuộc trận được cấp.", HttpStatusCode.Forbidden);
        return await coaching.Ingest(player, batch, true);
    }
    async Task<(Guid writer, Guid match)> ReplayAccess(string lease)
    {
        var allocation = await Guard(lease); var match = Guid.Parse(allocation.MatchId);
        var record = await unit.Game.Match(m => m.Matchid == match) ?? throw Error("Không tìm thấy trận.");
        return (record.Gamematchplayers.Single(p => p.Isreplaywriter).Playerid, match);
    }
    public async Task<ReplayMetadata> SaveReplay(string lease, ReplayMetadata metadata)
    {
        var access = await ReplayAccess(lease);
        if (metadata.matchId != access.match.ToString("N")) throw Error("Replay không thuộc trận được cấp.", HttpStatusCode.Forbidden);
        metadata.playerId = access.writer.ToString("N");
        return await replays.Save(access.writer, access.match, metadata, true);
    }
    public async Task<ReplayChunkInfo> UploadReplay(string lease, int index, ReplayChunkUpload chunk)
    { var access = await ReplayAccess(lease); return await replays.Upload(access.writer, access.match, index, chunk, true); }
}
