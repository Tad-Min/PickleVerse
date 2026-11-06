using Entities.DTOs;
using Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Game;

public sealed partial class GameRepository
{
    public Task<bool> IsAdmin(Guid id) => db.Users.AnyAsync(u => u.Userid == id && u.Status == UserStatus.Active && u.Role.Rolename == "Admin");
    public async Task<ProgressResetPreview?> ResetPreview(Guid player)
    {
        var profile = await db.Playerprofiles.AsNoTracking().SingleOrDefaultAsync(p => p.Userid == player);
        if (profile == null) return null;
        var inventory = await Inventory(player);
        return new ProgressResetPreview { PlayerId = player, Revision = profile.Revision, Level = profile.Level, Elo = profile.Elo,
            Tokenpad = inventory.tokenpad, Elite = inventory.elite, OwnedCosmetics = inventory.owned,
            LessonRecords = await db.Playerlessondrills.CountAsync(p => p.Playerid == player),
            MasteryRecords = await db.Playertechniquemasteries.CountAsync(p => p.Playerid == player),
            MissionRecords = await db.Playermissions.CountAsync(p => p.Playerid == player),
            AchievementRecords = await db.Playerachievements.CountAsync(p => p.Playerid == player) };
    }
    public void AddAudit(AdminAuditRecord record) => db.AdminAudits.Add(record);
    public async Task ResetLearning(Guid player)
    {
        await db.Playerlessondrills.Where(p => p.Playerid == player).ExecuteDeleteAsync();
        await db.Playertechniquemasteries.Where(p => p.Playerid == player).ExecuteDeleteAsync();
        await db.TrainingSessionRecords.Where(s => s.PlayerId == player).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsCurrent, false));
        await db.AiCoachingProfiles.Where(p => p.PlayerId == player).ExecuteDeleteAsync();
        await db.AiAnalysisJobs.Where(j => db.AiTelemetrySessions.Any(s => s.BatchId == j.BatchId && s.PlayerId == player))
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.State, "failed").SetProperty(j => j.LastError, "Progress reset")
                .SetProperty(j => j.LeaseToken, (Guid?)null).SetProperty(j => j.LeaseUntil, (DateTime?)null));
    }
    public async Task ResetMissions(Guid player)
    {
        await db.Playermissions.Where(p => p.Playerid == player).ExecuteUpdateAsync(s => s.SetProperty(p => p.Progress, 0).SetProperty(p => p.CurrentStreak, 0));
        await db.Playerachievements.Where(p => p.Playerid == player).ExecuteUpdateAsync(s => s.SetProperty(p => p.Progress, 0));
    }
    public async Task ResetCosmetics(Guid player)
    {
        await db.Playercosmetics.Where(p => p.Playerid == player && p.Cosmetic.Itemcode != "paddle.default").ExecuteDeleteAsync();
        if (!await db.Playercosmetics.AnyAsync(p => p.Playerid == player && p.Cosmetic.Itemcode == "paddle.default"))
            db.Playercosmetics.Add(new Playercosmetic { Playerid = player, Cosmeticid = (await CosmeticId("paddle.default"))!.Value, Equipped = true });
        await db.Playercosmetics.Where(p => p.Playerid == player && p.Cosmetic.Itemcode == "paddle.default").ExecuteUpdateAsync(s => s.SetProperty(p => p.Equipped, true));
    }
}
