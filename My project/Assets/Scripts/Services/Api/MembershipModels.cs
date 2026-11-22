using System;

namespace PickleVerse.Services.Api
{
    [Serializable]
    public sealed class MembershipOffer
    {
        public bool active, checkoutEnabled;
        public int priceVnd, eliteTokenBonus;
        public MembershipCheckout pendingCheckout;
    }

    [Serializable]
    public sealed class MembershipCheckout
    {
        public long orderCode;
        public string checkoutUrl;
        public DateTime expiresAt;

        public bool IsOpen => expiresAt > DateTime.UtcNow;
        public bool HasSafeUrl => Uri.TryCreate(checkoutUrl, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps && uri.IdnHost == "pay.payos.vn" && uri.IsDefaultPort &&
            uri.UserInfo.Length == 0 && uri.Fragment.Length == 0 && uri.AbsolutePath.StartsWith("/web/", StringComparison.Ordinal);
    }
}
