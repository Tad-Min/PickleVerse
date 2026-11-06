using Entities.Enums;
using Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Context;
public partial class PickleVerseContext
{
    public DbSet<PlayerLoginDay> PlayerLoginDays => Set<PlayerLoginDay>();
    public DbSet<PlayerActivityDay> PlayerActivityDays => Set<PlayerActivityDay>();
    public DbSet<MatchmakingSearchRecord> MatchmakingSearches => Set<MatchmakingSearchRecord>();
    static void ConfigureAnalytics(ModelBuilder model)
    {
        void Map<T>(string table, params string[] key) where T : class
        {
            var e = model.Entity<T>();e.ToTable(table).HasKey(key);
            foreach(var p in e.Metadata.GetProperties())p.SetColumnName(p.Name.ToLowerInvariant());
        }
        Map<PlayerLoginDay>("playerlogindays",nameof(PlayerLoginDay.PlayerId),nameof(PlayerLoginDay.Day));
        model.Entity<PlayerLoginDay>().HasOne<User>().WithMany().HasForeignKey(x=>x.PlayerId);
        Map<PlayerActivityDay>("playeractivitydays",nameof(PlayerActivityDay.PlayerId),nameof(PlayerActivityDay.Day));
        model.Entity<PlayerActivityDay>().HasOne<Playerprofile>().WithMany().HasForeignKey(x=>x.PlayerId);
        Map<MatchmakingSearchRecord>("matchmakingsearches",nameof(MatchmakingSearchRecord.SearchId));
        model.Entity<MatchmakingSearchRecord>().HasOne<Playerprofile>().WithMany().HasForeignKey(x=>x.PlayerId);
        model.Entity<MatchmakingSearchRecord>().Property(x=>x.State).HasConversion(v=>StatusWire.Format(v),v=>StatusWire.Parse<MatchmakingSearchStatus>(v));
    }
}
