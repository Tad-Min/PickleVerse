using Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Context;

public partial class PickleVerseContext
{
    public DbSet<MembershipOrder> MembershipOrders => Set<MembershipOrder>();

    static void ConfigureMembership(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MembershipOrder>(order =>
        {
            order.ToTable("membershiporders");
            order.HasKey(o => o.OrderCode);
            order.HasOne<Playerprofile>().WithMany().HasForeignKey(o => o.PlayerId).OnDelete(DeleteBehavior.Cascade);
            order.HasIndex(o => o.PaymentLinkId).IsUnique().HasDatabaseName("membershiporders_paymentlinkid_key");
            order.HasIndex(o => o.PlayerId).IsUnique().HasFilter("status = 'PENDING'")
                .HasDatabaseName("membershiporders_one_pending_idx");
            order.HasIndex(o => new { o.PlayerId, o.PaidAt }).HasFilter("paidat IS NOT NULL")
                .HasDatabaseName("membershiporders_member_idx");
            order.Property(o => o.OrderCode).HasColumnName("ordercode").ValueGeneratedNever();
            order.Property(o => o.PlayerId).HasColumnName("playerid");
            order.Property(o => o.PaymentLinkId).HasColumnName("paymentlinkid").HasMaxLength(64);
            order.Property(o => o.CheckoutUrl).HasColumnName("checkouturl").HasMaxLength(512);
            order.Property(o => o.AmountVnd).HasColumnName("amountvnd");
            order.Property(o => o.EliteTokenBonus).HasColumnName("elitetokenbonus");
            order.Property(o => o.Status).HasColumnName("status").HasMaxLength(16);
            order.Property(o => o.CreatedAt).HasColumnName("createdat");
            order.Property(o => o.ExpiresAt).HasColumnName("expiresat");
            order.Property(o => o.PaidAt).HasColumnName("paidat");
            order.Property(o => o.LastCheckedAt).HasColumnName("lastcheckedat");
        });
    }
}
