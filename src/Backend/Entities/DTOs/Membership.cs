namespace Entities.DTOs;

public sealed record MembershipCheckout(long OrderCode, string CheckoutUrl, DateTime ExpiresAt);
public sealed record MembershipOffer(bool Active, bool CheckoutEnabled, int PriceVnd, int EliteTokenBonus,
    MembershipCheckout? PendingCheckout);

public sealed record PayOsCheckout(string PaymentLinkId, string CheckoutUrl);
public sealed record PayOsPayment(long OrderCode, string PaymentLinkId, int Amount, int AmountPaid, int AmountRemaining, string Status);
public sealed record PayOsNotification(long OrderCode, string PaymentLinkId);
