using Entities.Models;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;
namespace Repositories.Game;
public static class MatchQueries
{
    public static IQueryable<Gamematch> Query(PickleVerseContext db) => db.Gamematches.AsNoTracking()
        .Include(m => m.Gamematchplayers).ThenInclude(p => p.Player)
            .ThenInclude(p => p.Playercosmetics.Where(c => c.Equipped)).ThenInclude(c => c.Cosmetic)
        .Include(m => m.Matchscores).Include(m => m.Ratingledgers).AsSplitQuery();

}
