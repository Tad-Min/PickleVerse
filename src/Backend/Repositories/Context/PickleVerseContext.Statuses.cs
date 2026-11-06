using Entities.Enums;
using Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Context;

public partial class PickleVerseContext
{
    static void ConfigureStatusMappings(ModelBuilder model)
    {
        model.Entity<User>().Property(e => e.Status)
            .HasConversion(v => StatusWire.Format(v), v => StatusWire.Parse<UserStatus>(v));
        model.Entity<Friendrequest>().Property(e => e.Status)
            .HasConversion(v => StatusWire.Format(v), v => StatusWire.Parse<FriendRequestStatus>(v));
        model.Entity<Reportplayerinmatch>().Property(e => e.Status)
            .HasConversion(v => StatusWire.Format(v), v => StatusWire.Parse<ReportStatus>(v));
        model.Entity<Gamematch>().Property(e => e.Status)
            .HasConversion(v => StatusWire.Format(v), v => StatusWire.Parse<MatchStatus>(v)).HasDefaultValue(MatchStatus.Preparing);
        model.Entity<Gamematch>().Property(e => e.Authoritymode)
            .HasConversion(v => StatusWire.Format(v), v => StatusWire.Parse<MatchAuthorityMode>(v)).HasDefaultValue(MatchAuthorityMode.Shared);
    }
}
