using Entities.Enums;
using Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Context;

public partial class PickleVerseContext
{
    public DbSet<UserSanction> UserSanctions => Set<UserSanction>();
    public DbSet<SanctionAppeal> SanctionAppeals => Set<SanctionAppeal>();
    static void ConfigureModeration(ModelBuilder model)
    {
        var sanctions = model.Entity<UserSanction>();
        sanctions.ToTable("usersanctions").HasKey(x => x.SanctionId);
        foreach (var p in sanctions.Metadata.GetProperties()) p.SetColumnName(p.Name.ToLowerInvariant());
        sanctions.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        sanctions.HasOne<User>().WithMany().HasForeignKey(x => x.ActorId);
        sanctions.Property(x => x.Restriction).HasConversion(v => StatusWire.Format(v), v => StatusWire.Parse<UserStatus>(v));
        var appeals = model.Entity<SanctionAppeal>();
        appeals.ToTable("sanctionappeals").HasKey(x => x.AppealId);
        foreach (var p in appeals.Metadata.GetProperties()) p.SetColumnName(p.Name.ToLowerInvariant());
        appeals.HasIndex(x => x.SanctionId).IsUnique();
        appeals.HasOne<UserSanction>().WithMany().HasForeignKey(x => x.SanctionId);
        appeals.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        appeals.Property(x => x.Status).HasConversion(v => StatusWire.Format(v), v => StatusWire.Parse<AppealStatus>(v));
        model.Entity<Reportplayerinmatch>().Property(x => x.ReviewerId).HasColumnName("reviewerid");
        model.Entity<Reportplayerinmatch>().Property(x => x.DecisionReason).HasColumnName("decisionreason");
    }
}
