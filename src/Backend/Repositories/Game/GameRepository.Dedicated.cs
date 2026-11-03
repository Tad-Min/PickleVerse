using Microsoft.EntityFrameworkCore;

namespace Repositories.Game;

public sealed partial class GameRepository
{
    public Task MarkDedicated(Guid match) => db.Gamematches.Where(m => m.Matchid == match && m.Authoritymode == "dedicated" && m.Status == "PLAYING")
        .ExecuteUpdateAsync(s => s.SetProperty(m => m.Serverheartbeatat, DateTime.UtcNow));
    public Task<bool> IsDedicated(Guid match) => db.Gamematches.AsNoTracking().AnyAsync(m => m.Matchid == match && m.Authoritymode == "dedicated");
    public Task TouchDedicated(Guid match) => db.Gamematches.Where(m => m.Matchid == match && m.Authoritymode == "dedicated" && m.Status == "PLAYING")
        .ExecuteUpdateAsync(s => s.SetProperty(m => m.Serverheartbeatat, DateTime.UtcNow));
    public Task<List<Guid>> ExpiredDedicated(DateTime cutoff) => db.Gamematches.Where(m => m.Authoritymode == "dedicated" && m.Status == "PLAYING" && (m.Serverheartbeatat ?? m.Updatedat) < cutoff)
        .Select(m => m.Matchid).Take(10).ToListAsync();
}
