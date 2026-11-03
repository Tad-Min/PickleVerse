using System;
using System.Text;

namespace PickleVerse.Multiplayer
{
    // Bounded, order-independent chunks fit the existing Shared command channel too.
    public sealed class MatchResultTransfer
    {
        const int PartSize = 1000, MaxBytes = 65536, MaxParts = 88;
        string transfer;
        string[] parts;
        int received;
        public static string[] Encode(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            if (bytes.Length > MaxBytes) throw new ArgumentException("Kết quả trận vượt giới hạn dữ liệu.");
            string encoded = Convert.ToBase64String(bytes);
            var output = new string[Math.Max(1, (encoded.Length + PartSize - 1) / PartSize)];
            for (int i = 0; i < output.Length; i++) output[i] = encoded.Substring(i * PartSize, Math.Min(PartSize, encoded.Length - i * PartSize));
            return output;
        }
        public bool Add(string id, int index, int count, string value, out string result)
        {
            result = null;
            if (!Guid.TryParseExact(id, "N", out _) || count < 1 || count > MaxParts || index < 0 || index >= count || value == null || value.Length > PartSize) return false;
            if (id != transfer) { transfer = id; parts = new string[count]; received = 0; }
            if (parts.Length != count || parts[index] != null && parts[index] != value) return false;
            if (parts[index] == null) { parts[index] = value; received++; }
            if (received != count) return false;
            try
            {
                byte[] bytes = Convert.FromBase64String(string.Concat(parts));
                if (bytes.Length > MaxBytes) return false;
                result = new UTF8Encoding(false, true).GetString(bytes);
                return true;
            }
            catch (ArgumentException) { return false; }
            catch (FormatException) { return false; }
        }
    }
}
