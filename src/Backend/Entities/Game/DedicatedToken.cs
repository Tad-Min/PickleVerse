using System.Security.Cryptography;
using System.Text;

namespace Entities.Game;

// Fusion 2.1.3 NetCommandConnect carries at most 128 bytes. Transport v1:
// version(1), four Guid.ToByteArray UUIDs(64), expiry big-endian(8), HMAC-SHA256(32).
public static class DedicatedToken
{
    public const int Size = 105;
    public static byte[] Create(string key, string match, string player, string lease, string nonce, long expires)
    {
        var token = new byte[Size];
        token[0] = 1;
        string[] ids = [match, player, lease, nonce];
        for (int index = 0; index < ids.Length; index++) Guid.ParseExact(ids[index], "N").ToByteArray().CopyTo(token, 1 + index * 16);
        for (int index = 0; index < 8; index++) token[65 + index] = (byte)((ulong)expires >> (56 - index * 8));
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), token.AsSpan(0, 73)).CopyTo(token, 73);
        return token;
    }
}
