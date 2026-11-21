using System.Net;
using Entities.DTOs;
using Entities.Exceptions;
using Entities.Models;
using PickleVerse.Multiplayer;
using Repositories.UnitOfWork;
using Services.Interfaces;

namespace Services.Implementations;

public sealed class ModerationService(IUnitOfWork unit, TimeProvider clock) : IModerationService
{
    DateTime Now => clock.GetUtcNow().UtcDateTime;
    static AppException Conflict(string message) => new(message, HttpStatusCode.Conflict);
    static string Reason(string? reason, int max = 2000)
    {
        string value = reason?.Trim() ?? "";
        if (value.Length < 3 || value.Length > max) throw new BadRequestException($"Lý do phải có từ 3 đến {max} ký tự.");
        return value;
    }
    async Task Admin(Guid actor)
    {
        if (!await unit.Game.IsAdmin(actor)) throw new ForbiddenException("Bạn không có quyền xử lý vi phạm.");
    }
    public Task<List<UserSanction>> Sanctions(Guid? user) => unit.Game.Sanctions(user);
    public Task<List<SanctionAppeal>> Appeals(Guid? user) => unit.Game.Appeals(user);
    public Task<DedicatedOperationRecord?> StopOperation(Guid match) => unit.Game.DedicatedOperation(match);
    public async Task Expire()
    {
        foreach (var id in await unit.Game.ExpiredRestrictionUsers(Now))
        {
            await using var tx = await unit.Game.BeginTransactionAsync();
            await unit.Users.LockUserAsync(id);
            var user = await unit.Users.GetByIdAsync(id);
            if (user != null && user.Status != UserStatus.Active && await unit.Game.EffectiveRestriction(id, Now) == UserStatus.Active)
            {
                user.Status = UserStatus.Active; await RevokeSessions(user);
                Audit(user.Userid, id, "SANCTION_EXPIRED", new { at = Now });
                await unit.SaveChangesAsync();
            }
            await tx.CommitAsync();
        }
    }
    async Task<User> LockAccount(Guid id)
    {
        if (RankedBotAccounts.Contains(id.ToString("N"))) throw new BadRequestException("Không thể xử lý tài khoản hệ thống.");
        // Same ordering as online match finalization. Recheck after the user lock for a concurrently created match.
        var locked = await unit.Game.ActiveMatches(id);
        foreach (var match in locked) await unit.Game.Lock("online-match:" + match.ToString("N"));
        await unit.Users.LockUserAsync(id);
        var user = await unit.Users.GetByIdAsync(id) ?? throw new NotFoundException("Không tìm thấy tài khoản.");
        foreach (var match in (await unit.Game.ActiveMatches(id)).Except(locked)) await unit.Game.Lock("online-match:" + match.ToString("N"));
        return user;
    }
    async Task RevokeSessions(User user)
    {
        user.Authversion++;
        await unit.Users.RevokeAllRefreshTokensAsync(user.Userid);
    }
    void Audit(Guid actor, Guid user, string action, object details) => unit.Game.AddAudit(new AdminAuditRecord
    { AuditId = Guid.NewGuid(), ActorId = actor, TargetId = user, Action = action, Details = GameJson.Write(details), CreatedAt = Now });
    async Task<UserSanction> ApplyLocked(Guid actor, User user, SanctionRequest request, Guid? match = null, Guid? reporter = null)
    {
        string reason = Reason(request.Reason);
        if (request.Restriction is not (UserStatus.Banned or UserStatus.Suspended) || request.ExpiresAt <= Now
            || request.Restriction == UserStatus.Suspended && request.ExpiresAt == null)
            throw new BadRequestException("Tạm ngưng cần thời hạn trong tương lai; khóa có thể không có thời hạn.");
        if (request.ExpiresAt.HasValue && request.ExpiresAt.Value.Kind != DateTimeKind.Utc)
            throw new BadRequestException("Thời hạn phải có múi giờ UTC.");
        if (actor == user.Userid) throw new BadRequestException("Không thể tự khóa tài khoản quản trị.");
        var decision = new UserSanction { SanctionId = Guid.NewGuid(), UserId = user.Userid, ActorId = actor,
            Restriction = request.Restriction, Reason = reason, CreatedAt = Now, ExpiresAt = request.ExpiresAt,
            ReportMatchId = match, ReporterId = reporter };
        unit.Game.AddSanction(decision);
        await unit.SaveChangesAsync();
        user.Status = await unit.Game.EffectiveRestriction(user.Userid, Now);
        await RevokeSessions(user);
        foreach (var active in await unit.Game.ActiveMatches(user.Userid))
            await unit.Game.RequestStop(active, actor, "Tài khoản bị hạn chế: " + reason, Now);
        Audit(actor, user.Userid, "SANCTION_APPLY", decision);
        return decision;
    }
    public async Task<UserSanction> Apply(Guid actor, SanctionRequest request)
    {
        await Admin(actor);
        await using var tx = await unit.Game.BeginTransactionAsync();
        var user = await LockAccount(request.UserId);
        var decision = await ApplyLocked(actor, user, request);
        await unit.SaveChangesAsync(); await tx.CommitAsync(); return decision;
    }
    public async Task Revoke(Guid actor, Guid id, string reason)
    {
        await Admin(actor); reason = Reason(reason);
        await using var tx = await unit.Game.BeginTransactionAsync();
        var user = await LockAccount(id);
        foreach (var entry in await unit.Game.Sanctions(id))
            if (entry.RevokedAt == null && (entry.ExpiresAt == null || entry.ExpiresAt > Now))
            {
                var sanction = (await unit.Game.Sanction(entry.SanctionId))!;
                sanction.RevokedAt = Now; sanction.RevokedBy = actor; sanction.RevocationReason = reason;
            }
        await unit.SaveChangesAsync();
        user.Status = await unit.Game.EffectiveRestriction(id, Now);
        await RevokeSessions(user); Audit(actor, id, "SANCTION_REVOKE_ALL", new { reason });
        await unit.SaveChangesAsync(); await tx.CommitAsync();
    }
    public async Task ReviewReport(Guid actor, ReviewReportRequest request)
    {
        await Admin(actor);
        if (request.Status is not (ReportStatus.Checking or ReportStatus.Accepted or ReportStatus.Declined)
            || request.BanReportedPlayer && request.Status != ReportStatus.Accepted) throw new BadRequestException("Trạng thái xử lý không hợp lệ.");
        string reason = Reason(request.DecisionReason);
        await using var tx = await unit.Game.BeginTransactionAsync();
        var report = await unit.Game.LockReport(request.ReporterId, request.ReportedPlayerId, request.MatchId)
            ?? throw new NotFoundException("Không tìm thấy tố cáo.");
        if (report.Status is ReportStatus.Accepted or ReportStatus.Declined) throw Conflict("Tố cáo đã có kết luận; lịch sử không thể bị ghi đè.");
        if (request.BanReportedPlayer)
        {
            var user = await LockAccount(request.ReportedPlayerId);
            await ApplyLocked(actor, user, new SanctionRequest { UserId = user.Userid, Restriction = request.Restriction,
                Reason = request.BanReason ?? reason, ExpiresAt = request.BannedUntil }, request.MatchId, request.ReporterId);
        }
        report.Status = request.Status; report.Updatedat = Now; report.ReviewerId = actor; report.DecisionReason = reason;
        Audit(actor, request.ReportedPlayerId, "REPORT_REVIEW", request);
        await unit.SaveChangesAsync(); await tx.CommitAsync();
    }
    public async Task<SanctionAppeal> SubmitAppeal(Guid user, AppealRequest request)
    {
        string text = Reason(request.Statement, 4000);
        if (text.Length < 10) throw new BadRequestException("Nội dung khiếu nại cần ít nhất 10 ký tự.");
        await using var tx = await unit.Game.BeginTransactionAsync();
        await unit.Game.Lock("appeal-sanction:" + request.SanctionId);
        var sanction = await unit.Game.Sanction(request.SanctionId);
        if (sanction == null || sanction.UserId != user) throw new NotFoundException("Không tìm thấy quyết định của bạn.");
        var existing = await unit.Game.AppealForSanction(request.SanctionId);
        if (existing != null)
        {
            if (existing.Statement != text) throw Conflict("Quyết định này đã có khiếu nại.");
            return existing;
        }
        var appeal = new SanctionAppeal { AppealId = Guid.NewGuid(), UserId = user, SanctionId = sanction.SanctionId,
            Statement = text, CreatedAt = Now, UpdatedAt = Now };
        unit.Game.AddAppeal(appeal); await unit.SaveChangesAsync(); await tx.CommitAsync(); return appeal;
    }
    public async Task<SanctionAppeal> ReviewAppeal(Guid actor, Guid id, AppealReviewRequest request)
    {
        await Admin(actor); string reason = Reason(request.Reason);
        if (request.Status is not (AppealStatus.Checking or AppealStatus.Accepted or AppealStatus.Declined)) throw new BadRequestException("Trạng thái khiếu nại không hợp lệ.");
        await using var tx = await unit.Game.BeginTransactionAsync();
        await unit.Game.Lock("appeal-review:" + id);
        var appeal = await unit.Game.Appeal(id) ?? throw new NotFoundException("Không tìm thấy khiếu nại.");
        if (appeal.Status is AppealStatus.Accepted or AppealStatus.Declined) throw Conflict("Khiếu nại đã có quyết định.");
        var user = await LockAccount(appeal.UserId);
        if (request.Status == AppealStatus.Accepted)
        {
            var sanction = (await unit.Game.Sanction(appeal.SanctionId))!;
            sanction.RevokedAt ??= Now; sanction.RevokedBy ??= actor; sanction.RevocationReason ??= reason;
            await unit.SaveChangesAsync();
            user.Status = await unit.Game.EffectiveRestriction(user.Userid, Now);
            await RevokeSessions(user);
        }
        appeal.Status = request.Status; appeal.UpdatedAt = Now; appeal.ReviewerId = actor; appeal.DecisionReason = reason;
        Audit(actor, user.Userid, "APPEAL_REVIEW", new { id, request });
        await unit.SaveChangesAsync(); await tx.CommitAsync(); return appeal;
    }
    public async Task<DedicatedOperationRecord> StopMatch(Guid actor, Guid match, string reason)
    {
        await Admin(actor); reason = Reason(reason);
        await using var tx = await unit.Game.BeginTransactionAsync();
        await unit.Game.Lock("online-match:" + match.ToString("N"));
        var record = await unit.Game.Match(m => m.Matchid == match) ?? throw new NotFoundException("Không tìm thấy trận.");
        var existing = await unit.Game.DedicatedOperation(match);
        if (existing != null) return existing;
        if (record.Authoritymode != MatchAuthorityMode.Dedicated || record.Status is not (MatchStatus.Playing or MatchStatus.Preparing))
            throw Conflict("Trận không còn hoạt động trên Dedicated.");
        var op = await unit.Game.RequestStop(match, actor, reason, Now);
        Audit(actor, actor, "MATCH_STOP", new { match, op.OperationId, reason });
        await unit.SaveChangesAsync(); await tx.CommitAsync(); return op;
    }
}
