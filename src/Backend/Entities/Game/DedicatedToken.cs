using System.Security.Cryptography;
using System.Text;

namespace Entities.Game;

// Fusion 2.1.3 NetCommandConnect carries at most 128 bytes. Transport v2:
// version(1), four Guid.ToByteArray UUIDs(64), expiry(8), auth version(4), HMAC-SHA256(32).
// Integer fields are big-endian. The authentication epoch fences previous devices.
public static class DedicatedToken
{
    public const int Size = 109;
    public static byte[] Create(string key, string match, string player, string lease, string nonce, long expires, int authVersion)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(authVersion);
        var token = new byte[Size];
        token[0] = 2;
        string[] ids = [match, player, lease, nonce];
        for (int index = 0; index < ids.Length; index++) Guid.ParseExact(ids[index], "N").ToByteArray().CopyTo(token, 1 + index * 16);
        for (int index = 0; index < 8; index++) token[65 + index] = (byte)((ulong)expires >> (56 - index * 8));
        for (int index = 0; index < 4; index++) token[73 + index] = (byte)((uint)authVersion >> (24 - index * 8));
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), token.AsSpan(0, 77)).CopyTo(token, 77);
        return token;
    }
}
