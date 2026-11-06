using Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Game;

public sealed partial class GameRepository
{
    public Task SaveCatalog(string version, string data, DateTime now) => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO contentcatalogs(version,data,createdat) VALUES({version},{data}::jsonb,{now}) ON CONFLICT(version) DO NOTHING
        """);
    public Task<string?> CatalogSnapshot(string version) => db.ContentCatalogs.AsNoTracking().Where(c => c.Version == version)
        .Select(c => c.Data).SingleOrDefaultAsync();
    public Task<long> LearningEpoch(Guid player) => db.Playerprofiles.AsNoTracking().Where(p => p.Userid == player).Select(p => p.LearningEpoch).SingleAsync();
    public Task<bool> AttemptIdUsed(Guid attempt) => db.TrainingSessionRecords.AnyAsync(s => s.AttemptId == attempt);
    public void AddSyncedTraining(TrainingSessionRecord record) => db.TrainingSessionRecords.Add(record);
    public Task IncrementMastery(Guid player, string skill, int attempts, int errors, DateTime now) => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO playertechniquemastery(playerid,techniqueid,totalattempts,totalerrors,lastupdatedat)
        SELECT {player},techniqueid,{attempts},{errors},{now} FROM techniques WHERE techniquename={skill}
        ON CONFLICT(playerid,techniqueid) DO UPDATE SET totalattempts=playertechniquemastery.totalattempts+EXCLUDED.totalattempts,
            totalerrors=playertechniquemastery.totalerrors+EXCLUDED.totalerrors,lastupdatedat=EXCLUDED.lastupdatedat
        """);
    public Task<bool> HasReward(Guid player, string key) => db.ProgressRewardGrants.AnyAsync(r => r.PlayerId == player && r.RewardKey == key);
    public Task<int?> CosmeticId(string code) => db.Cosmetics.Where(c => c.Itemcode == code).Select(c => (int?)c.Cosmeticid).SingleOrDefaultAsync();
    public async Task<bool> GrantOnce(Guid player, string key, string details, DateTime now) => await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO progressrewardgrants(playerid,rewardkey,details,createdat) VALUES({player},{key},{details}::jsonb,{now})
        ON CONFLICT(playerid,rewardkey) DO NOTHING
        """) == 1;
    public Task<DedicatedOperationRecord?> DedicatedOperation(Guid match) => db.DedicatedOperations.AsNoTracking().SingleOrDefaultAsync(x => x.MatchId == match);
    public Task AcknowledgeOperation(Guid operation, string lease, DateTime now) => db.DedicatedOperations
        .Where(x => x.OperationId == operation && x.LeaseId == lease && x.AcknowledgedAt == null)
        .ExecuteUpdateAsync(s => s.SetProperty(x => x.AcknowledgedAt, now));
}
