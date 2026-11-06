using Microsoft.EntityFrameworkCore;

namespace Repositories.Game;

public sealed partial class GameRepository
{
    public Task<List<Entities.Models.Gamematch>> ActiveOnlineMatches() => MatchQueries.Query(db)
        .Where(m => m.Kind != "BotPractice" && (m.Status == MatchStatus.Playing || m.Status == MatchStatus.Preparing))
        .OrderBy(m => m.Createdat).ToListAsync();
    public Task MarkDedicated(Guid match) => db.Gamematches.Where(m => m.Matchid == match && m.Authoritymode == MatchAuthorityMode.Dedicated && m.Status == MatchStatus.Playing)
        .ExecuteUpdateAsync(s => s.SetProperty(m => m.Serverheartbeatat, DateTime.UtcNow));
    public Task<bool> IsDedicated(Guid match) => db.Gamematches.AsNoTracking().AnyAsync(m => m.Matchid == match && m.Authoritymode == MatchAuthorityMode.Dedicated);
    public Task<bool> CanAdmitDedicated(Guid match, Guid player) => db.Gamematches.AsNoTracking().AnyAsync(m =>
        m.Matchid == match && m.Authoritymode == MatchAuthorityMode.Dedicated && m.Status == MatchStatus.Playing
        && m.Gamematchplayers.Any(p => p.Playerid == player) && db.Users.Any(u => u.Userid == player && u.Status == UserStatus.Active)
        && !db.DedicatedOperations.Any(o => o.MatchId == match));
    public Task TouchDedicated(Guid match) => db.Gamematches.Where(m => m.Matchid == match && m.Authoritymode == MatchAuthorityMode.Dedicated && m.Status == MatchStatus.Playing)
        .ExecuteUpdateAsync(s => s.SetProperty(m => m.Serverheartbeatat, DateTime.UtcNow));
    public Task<List<Guid>> ExpiredDedicated(DateTime cutoff) => db.Gamematches.Where(m => m.Authoritymode == MatchAuthorityMode.Dedicated && m.Status == MatchStatus.Playing && (m.Serverheartbeatat ?? m.Updatedat) < cutoff)
        .Select(m => m.Matchid).Take(10).ToListAsync();
}
