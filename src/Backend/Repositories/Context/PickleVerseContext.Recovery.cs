using Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Context;

public partial class PickleVerseContext
{
    public DbSet<Passwordresetchallenge> Passwordresetchallenges { get; set; }
    static void ConfigureRecovery(ModelBuilder model)
    {
        model.Entity<User>().Property(u => u.Authversion).HasColumnName("authversion").HasDefaultValue(0);
        model.Entity<Gamematch>().Property(m => m.Authoritymode).HasColumnName("authoritymode").HasMaxLength(16).HasDefaultValue(MatchAuthorityMode.Shared);
        model.Entity<Gamematch>().Property(m => m.Serverheartbeatat).HasColumnName("serverheartbeatat").HasColumnType("timestamp with time zone");
        model.Entity<Passwordresetchallenge>(e =>
        {
            e.ToTable("passwordresetchallenges");
            e.HasKey(x => x.Userid);
            e.Property(x => x.Userid).HasColumnName("userid");
            e.Property(x => x.Challengeid).HasColumnName("challengeid");
            e.Property(x => x.Tokenhash).HasColumnName("tokenhash").HasMaxLength(64);
            e.Property(x => x.Createdat).HasColumnName("createdat");
            e.Property(x => x.Expiresat).HasColumnName("expiresat");
            e.Property(x => x.Failedattempts).HasColumnName("failedattempts");
            e.Property(x => x.Consumed).HasColumnName("consumed");
            e.HasOne<User>().WithMany().HasForeignKey(x => x.Userid).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
