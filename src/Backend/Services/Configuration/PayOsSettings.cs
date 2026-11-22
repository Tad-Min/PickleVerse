namespace Services.Configuration;

public sealed class PayOsSettings
{
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ChecksumKey { get; set; } = "";
    public string ReturnUrl { get; set; } = "";
    public string CancelUrl { get; set; } = "";
    public int CheckoutLifetimeMinutes { get; set; } = 15;

    public void Validate()
    {
        if (!Enabled) return;
        if (string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(ApiKey) || string.IsNullOrWhiteSpace(ChecksumKey))
            throw new InvalidOperationException("PayOs requires ClientId, ApiKey and ChecksumKey when enabled.");
        if (!IsHttpsUrl(ReturnUrl) || !IsHttpsUrl(CancelUrl))
            throw new InvalidOperationException("PayOs requires absolute HTTPS ReturnUrl and CancelUrl.");
        _ = CheckoutExpiresAt(DateTimeOffset.UtcNow);
    }

    internal DateTime CheckoutExpiresAt(DateTimeOffset createdAt)
    {
        long expiration = createdAt.ToUnixTimeSeconds() + CheckoutLifetimeMinutes * 60L;
        if (CheckoutLifetimeMinutes <= 0 || expiration > int.MaxValue)
            throw new InvalidOperationException("PayOs:CheckoutLifetimeMinutes must be positive and keep the expiration within the PayOS timestamp range.");
        return DateTimeOffset.FromUnixTimeSeconds(expiration).UtcDateTime;
    }

    static bool IsHttpsUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0;
}
