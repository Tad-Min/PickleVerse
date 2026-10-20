using System.Security.Cryptography;
using System.Text;

namespace Services.Security;

public static class EmailOtpPolicy
{
    public const int LifetimeMinutes = 10;
    public const int ResendCooldownSeconds = 60;
    public const int MaxAttempts = 5;

    public static string Generate() => RandomNumberGenerator.GetInt32(1_000_000).ToString("D6");

    public static string Hash(Guid userId, string code, string signingKey) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey),
            Encoding.UTF8.GetBytes($"email-otp:{userId:D}:{code}")));

    public static bool Matches(Guid userId, string code, string storedHash, string signingKey)
    {
        if (code.Length != 6 || code.Any(c => c < '0' || c > '9') || storedHash.Length != 64) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(userId, code, signingKey)),
            Encoding.ASCII.GetBytes(storedHash));
    }

    public static bool CanVerify(DateTime expiresAt, bool verified, int failures, DateTime utcNow) =>
        !verified && failures < MaxAttempts && expiresAt > utcNow;
}
