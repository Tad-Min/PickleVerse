#nullable disable
using System;
using System.Security.Cryptography;
using System.Text;

namespace PickleVerse.Multiplayer
{
    public static class DedicatedAdmission
    {
        [Serializable] public sealed class Claims { public string matchId, playerId, leaseId, nonce; public long expires; }
        public static Claims Verify(byte[] token, string key, string match, string lease, long now)
        {
            if (token == null || token.Length != 105 || token[0] != 1 || key.Length < 32) return null;
            try
            {
                byte[] expected;
                using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key))) expected = hmac.ComputeHash(token, 0, 73);
                int diff = 0; for (int i = 0; i < expected.Length; i++) diff |= token[73 + i] ^ expected[i]; if (diff != 0) return null;
                string Id(int offset) { var bytes = new byte[16]; Buffer.BlockCopy(token, offset, bytes, 0, 16); return new Guid(bytes).ToString("N"); }
                ulong expires = 0; for (int i = 0; i < 8; i++) expires = (expires << 8) | token[65 + i];
                var claims = new Claims { matchId = Id(1), playerId = Id(17), leaseId = Id(33), nonce = Id(49), expires = (long)expires };
                if (claims == null || claims.matchId != match || claims.leaseId != lease || claims.expires <= now || claims.expires > now + 180 ||
                    claims.playerId == Guid.Empty.ToString("N") || claims.nonce == Guid.Empty.ToString("N")) return null;
                return claims;
            }
            catch (ArgumentException) { return null; }
        }
    }
}
