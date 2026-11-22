namespace Entities.Models;

public sealed class MembershipOrder
{
    public long OrderCode { get; set; }
    public Guid PlayerId { get; set; }
    public required string PaymentLinkId { get; set; }
    public required string CheckoutUrl { get; set; }
    public int AmountVnd { get; set; }
    public int EliteTokenBonus { get; set; }
    public string Status { get; set; } = Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? LastCheckedAt { get; set; }

    public const string Pending = "PENDING";
    public const string Paid = "PAID";
    public const string Cancelled = "CANCELLED";
    public const string Expired = "EXPIRED";
}
