using System;

namespace PickleVerse.Services.Utilities
{
    public static class ApiUrl
    {
        public static string Build(string baseUrl, string path, bool development)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var root) ||
                root.Scheme != Uri.UriSchemeHttps && root.Scheme != Uri.UriSchemeHttp ||
                root.UserInfo.Length > 0 || root.Query.Length > 0 || root.Fragment.Length > 0)
                throw new ArgumentException("Địa chỉ API không hợp lệ.");
            if (root.Scheme == Uri.UriSchemeHttp && (!development || !root.IsLoopback))
                throw new ArgumentException("API cần HTTPS. HTTP chỉ được dùng với localhost trong bản development.");
            if (string.IsNullOrEmpty(path) || !path.StartsWith("api/", StringComparison.Ordinal) || path.Contains("..") || path.Contains(":") || path.Contains("\\"))
                throw new ArgumentException("Đường dẫn API không hợp lệ.");
            return root.AbsoluteUri.TrimEnd('/') + "/" + path;
        }
    }
}
