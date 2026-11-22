#nullable disable
using System;
using System.Linq;

namespace PickleVerse.Replays
{
    public sealed class ReplayWebLink
    {
        public string MatchId { get; }
        public string Code { get; }
        ReplayWebLink(string match, string code) { MatchId = match; Code = code; }
        public static bool IsToken(string value) => value != null && value.Length == 64 &&
            value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
        public static bool TryParse(string value, out ReplayWebLink link)
        {
            link = null;
            if (string.IsNullOrEmpty(value) || value.Length > 512 || value != value.Trim() || value.Any(char.IsControl) ||
                !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "pickleverse" || uri.Host != "replay" ||
                !string.IsNullOrEmpty(uri.UserInfo) || uri.Port != -1 || !string.IsNullOrEmpty(uri.Fragment) ||
                uri.AbsolutePath.Length != 33 || !Guid.TryParseExact(uri.AbsolutePath.Substring(1), "N", out var match) ||
                !uri.Query.StartsWith("?code=", StringComparison.Ordinal) || !IsToken(uri.Query.Substring(6)))
                return false;
            string id = match.ToString("N"), code = uri.Query.Substring(6);
            if (value != "pickleverse://replay/" + id + "?code=" + code) return false;
            link = new ReplayWebLink(id, code);
            return true;
        }
    }
}
