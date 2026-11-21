using Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Game;

public sealed partial class GameRepository
{
    public Task<List<UserSanction>> Sanctions(Guid? user = null) => db.UserSanctions.AsNoTracking()
        .Where(x => user == null || x.UserId == user).OrderByDescending(x => x.CreatedAt).ToListAsync();
    public Task<UserSanction?> Sanction(Guid id) => db.UserSanctions.SingleOrDefaultAsync(x => x.SanctionId == id);
    public void AddSanction(UserSanction value) => db.UserSanctions.Add(value);
    public Task<List<SanctionAppeal>> Appeals(Guid? user = null) => db.SanctionAppeals.AsNoTracking()
        .Where(x => user == null || x.UserId == user).OrderByDescending(x => x.CreatedAt).ToListAsync();
    public Task<SanctionAppeal?> Appeal(Guid id) => db.SanctionAppeals.SingleOrDefaultAsync(x => x.AppealId == id);
    public Task<SanctionAppeal?> AppealForSanction(Guid id) => db.SanctionAppeals.SingleOrDefaultAsync(x => x.SanctionId == id);
    public void AddAppeal(SanctionAppeal value) => db.SanctionAppeals.Add(value);
    public Task<List<Guid>> ActiveMatches(Guid user) => db.Gamematches.AsNoTracking()
        .Where(m => (m.Status == MatchStatus.Playing || m.Status == MatchStatus.Preparing) && m.Gamematchplayers.Any(p => p.Playerid == user))
        .OrderBy(m => m.Matchid).Select(m => m.Matchid).ToListAsync();
    public Task<Reportplayerinmatch?> LockReport(Guid reporter, Guid reported, Guid match) => db.Reportplayerinmatches
        .FromSqlInterpolated($"SELECT * FROM reportplayerinmatch WHERE reporterid={reporter} AND reportedplayerid={reported} AND matchid={match} FOR UPDATE")
        .SingleOrDefaultAsync();
    public async Task<DedicatedOperationRecord> RequestStop(Guid match, Guid actor, string reason, DateTime now)
    {
        var existing = await db.DedicatedOperations.SingleOrDefaultAsync(x => x.MatchId == match);
        if (existing != null) return existing;
        var op = new DedicatedOperationRecord { OperationId = Guid.NewGuid(), MatchId = match, ActorId = actor, Reason = reason, CreatedAt = now };
        db.DedicatedOperations.Add(op); return op;
    }
    // Caller holds the account row lock. Expiry never erases the original decision.
    public async Task<UserStatus> EffectiveRestriction(Guid user, DateTime now)
    {
        var active = await db.UserSanctions.Where(x => x.UserId == user && x.RevokedAt == null && (x.ExpiresAt == null || x.ExpiresAt > now))
            .Select(x => x.Restriction).ToListAsync();
        return active.Contains(UserStatus.Banned) ? UserStatus.Banned : active.Contains(UserStatus.Suspended) ? UserStatus.Suspended : UserStatus.Active;
    }
    public Task<bool> HasSanctionHistory(Guid user) => db.UserSanctions.AnyAsync(x => x.UserId == user);
    public Task<List<Guid>> ExpiredRestrictionUsers(DateTime now) => db.Users.Where(u => u.Status != UserStatus.Active
        && db.UserSanctions.Any(x => x.UserId == u.Userid) && !db.UserSanctions.Any(x => x.UserId == u.Userid && x.RevokedAt == null
            && (x.ExpiresAt == null || x.ExpiresAt > now))).Select(u => u.Userid).Take(100).ToListAsync();
    public Task BindOperation(Guid operation, string lease) => db.DedicatedOperations.Where(x => x.OperationId == operation && x.LeaseId == "")
        .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseId, lease));
    public Task<List<Guid>> PendingStops() => db.DedicatedOperations.Where(x => db.Gamematches.Any(m => m.Matchid == x.MatchId
        && (m.Status == MatchStatus.Playing || m.Status == MatchStatus.Preparing))).Select(x => x.MatchId).Take(10).ToListAsync();
}
