using System.Net;
using System.Security.Cryptography;
using System.Text;
using Entities.DTOs;
using Entities.Exceptions;
using Entities.Models;
using PickleVerse.Multiplayer;
using Repositories.UnitOfWork;
using Services.Configuration;
using Services.Interfaces;

namespace Services.Implementations;

public sealed class ProgressResetService(IUnitOfWork unit, RedisGameState matchmaking, LeaderboardCache leaderboard,
    ServerSettings settings, TimeProvider clock) : IProgressResetService
{
    DateTime Now => clock.GetUtcNow().UtcDateTime;
    static void Validate(ProgressResetSelection selection)
    {
        if (selection.Groups == null || selection.Groups.Count is < 1 or > 4 || selection.Groups.Any(g => !Enum.IsDefined(g))
            || selection.Groups.Distinct().Count() != selection.Groups.Count) throw new AppException("Hãy chọn các nhóm tiến trình hợp lệ.", HttpStatusCode.BadRequest);
    }
    Task<bool> Busy(Guid player) => BusyCore(player);
    async Task<bool> BusyCore(Guid player) => await unit.Game.InMatch(player, Now.AddSeconds(-settings.Matchmaking.ReadyConfirmationSeconds))
        || await matchmaking.HasActiveSearch(player, new DateTimeOffset(Now).ToUnixTimeMilliseconds());
    public async Task<ProgressResetPreview> Preview(Guid player, ProgressResetSelection selection)
    {
        Validate(selection);
        if (RankedBotAccounts.Contains(player.ToString("N"))) throw new AppException("Không đặt lại tài khoản bot hệ thống.", HttpStatusCode.BadRequest);
        var preview = await unit.Game.ResetPreview(player) ?? throw new NotFoundException("Không tìm thấy hồ sơ người chơi.");
        preview.Groups = selection.Groups.ToList(); preview.Busy = await Busy(player);
        foreach (var group in selection.Groups)
            preview.Effects.Add(group switch {
                ProgressResetGroup.Learning => "XP/cấp độ, kết quả bài học, mastery và hồ sơ coaching về ban đầu. Lượt offline cũ không được phục hồi. Tiền/vật phẩm đã thưởng không được nhận lại.",
                ProgressResetGroup.Ranking => "Elo về 2000, chuỗi xếp hạng về 0; giữ lịch sử trận và rating ledger.",
                ProgressResetGroup.Missions => "Đặt lại bộ đếm nhiệm vụ/thành tựu, thắng/thua và chuỗi tiến trình; giữ các mốc thưởng đã nhận.",
                _ => "Số dư về 0, chỉ giữ và trang bị vợt mặc định. Nhật ký giao dịch và thưởng được giữ nguyên." });
        return preview;
    }
    public async Task<ProgressResetReceipt> Execute(Guid actor, Guid player, ExecuteProgressReset request)
    {
        Validate(request);
        if (request.RequestId.Length is < 8 or > 64) throw new AppException("Mã yêu cầu không hợp lệ.", HttpStatusCode.BadRequest);
        if (!await unit.Game.IsAdmin(actor)) throw new ForbiddenException("Không có quyền đặt lại tiến trình.");
        if (RankedBotAccounts.Contains(player.ToString("N"))) throw new AppException("Không đặt lại tài khoản bot hệ thống.", HttpStatusCode.BadRequest);
        string key = "admin-reset:" + request.RequestId;
        string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(GameJson.Payload(new { actor, player, request.ExpectedRevision, groups = request.Groups.Order().ToArray() }))));
        await using var tx = await unit.Game.BeginTransactionAsync();
        // Same ordering as AI completion: profile advisory lock, then rows. No stale analysis can repopulate a reset profile.
        await unit.Game.Lock("ai-profile:" + player);
        var profile = await unit.Game.LockProfile(player) ?? throw new NotFoundException("Không tìm thấy hồ sơ người chơi.");
        var previous = await unit.Game.Command(player, key);
        if (previous != null)
        {
            if (previous.Kind != "admin-reset" || previous.Fingerprint != fingerprint) throw new AppException("Mã yêu cầu đã dùng cho thao tác khác.", HttpStatusCode.Conflict);
            await tx.CommitAsync(); return GameJson.Read<ProgressResetReceipt>(previous.Result);
        }
        if (profile.Revision != request.ExpectedRevision) throw new AppException("Hồ sơ đã thay đổi. Hãy xem trước lại tác động.", HttpStatusCode.Conflict);
        if (await Busy(player)) throw new AppException("Người chơi đang tìm trận hoặc có trận chưa hoàn tất.", HttpStatusCode.Conflict);
        if (request.Groups.Contains(ProgressResetGroup.Learning))
        { profile.Level = 1; profile.Exp = 0; profile.LearningEpoch++; profile.LearningResetAt = Now; await unit.Game.ResetLearning(player); }
        if (request.Groups.Contains(ProgressResetGroup.Ranking)) { profile.Elo = 2000; profile.RankWinStreak = profile.RankLossStreak = 0; }
        if (request.Groups.Contains(ProgressResetGroup.Missions))
        {
            profile.MissionEpoch++; profile.Wincount = profile.Losecount = profile.Winstreak = profile.BestWinStreak = profile.Daystreak = 0;
            profile.LifetimeMatches = profile.LifetimePoints = profile.LifetimeReturns = profile.LifetimeServes = profile.LifetimeBotMatches = 0;
            profile.LifetimeHumanMatches = profile.LifetimeHumanWins = profile.LifetimeRankedWins = profile.LifetimeDoublesWins = profile.LifetimeHigherRatedWins = 0;
            profile.DailyStreakEnabledAt = Now; await unit.Game.ResetMissions(player);
        }
        if (request.Groups.Contains(ProgressResetGroup.Economy))
        {
            var inventory = await unit.Game.Inventory(player); profile.EconomyEpoch++;
            await unit.Game.SaveBalance(player, "TOKENPAD", 0, inventory.tokenpad, key, "admin-reset");
            await unit.Game.SaveBalance(player, "ELITE TOKEN", 0, inventory.elite, key, "admin-reset");
            await unit.Game.ResetCosmetics(player);
        }
        var receipt = new ProgressResetReceipt { AuditId = Guid.NewGuid(), PlayerId = player, Revision = ++profile.Revision, Groups = request.Groups.ToList() };
        unit.Game.AddAudit(new AdminAuditRecord { AuditId = receipt.AuditId, ActorId = actor, TargetId = player, Action = "progress-reset", Details = GameJson.Write(receipt), CreatedAt = Now });
        unit.Game.AddCommand(player, key, "admin-reset", fingerprint, GameJson.Write(receipt));
        await unit.SaveChangesAsync(); await tx.CommitAsync();
        if (request.Groups.Contains(ProgressResetGroup.Ranking))
            try { await leaderboard.Invalidate(); } catch (Exception e) when (PersistenceErrors.IsCacheUnavailable(e)) { System.Diagnostics.Trace.TraceWarning("Leaderboard invalidation pending after progress reset."); }
        return receipt;
    }
}
