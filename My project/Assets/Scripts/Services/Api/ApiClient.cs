#nullable disable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace PickleVerse.Services.Api
{
    public sealed class ApiException : Exception
    {
        public long StatusCode { get; }
        public string Code { get; }
        public ApiException(long status, string message, string code = "") : base(message)
        {
            StatusCode = status;
            Code = code ?? "";
        }
    }
    public sealed class ApiHttpResponse
    {
        public long StatusCode { get; }
        public string Body { get; }
        public ApiHttpResponse(long statusCode, string body)
        {
            StatusCode = statusCode;
            Body = body ?? "";
        }
    }
    public interface IApiTransport
    {
        Task<ApiHttpResponse> SendAsync(string method, string path, string json, string bearerToken, CancellationToken cancellation);
    }
    public sealed class ApiClient
    {
        readonly IApiTransport transport;
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = 64
        };
        public ApiClient(IApiTransport transport)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }
        public async Task<T> SendAsync<T>(string method, string path, object body = null, string token = null, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            var response = await transport.SendAsync(method, path, body == null ? null : JsonConvert.SerializeObject(body, Settings), token, cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (response.StatusCode < 200 || response.StatusCode >= 300)
                throw ParseError(response);
            try
            {
                var payload = JToken.Parse(response.Body);
                if (payload is JObject envelope && envelope["success"]?.Type == JTokenType.Boolean)
                {
                    if (!(bool)envelope["success"])
                        throw ParseError(response);
                    if (envelope.Property("data") != null)
                        payload = envelope["data"];
                }
                var result = payload.ToObject<T>(JsonSerializer.Create(Settings));
                if (result == null)
                    throw new JsonSerializationException();
                return result;
            }
            catch (JsonException)
            {
                throw new ApiException(response.StatusCode, "Dữ liệu máy chủ trả về không hợp lệ.", "invalid_response");
            }
        }
        public static ApiException ParseError(ApiHttpResponse response)
        {
            string message = response.StatusCode == 401 ? "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại."
                : response.StatusCode == 429 ? "Quá nhiều yêu cầu. Vui lòng thử lại sau 60 giây."
                : response.StatusCode >= 500 ? "Máy chủ tạm thời không sẵn sàng. Vui lòng thử lại."
                : "Yêu cầu không thành công.";
            string code = "";
            try
            {
                var body = JObject.Parse(response.Body);
                message = (string)body["message"] ?? (string)body["title"] ?? message;
                code = (string)body["code"] ?? "";
                var errors = body["errors"] as JObject;
                if (errors != null)
                    message = string.Join("\n", errors.Properties().SelectMany(p => p.Value.Values<string>()).Take(4));
            }
            catch (JsonException) { }
            return new ApiException(response.StatusCode, message, code);
        }
    }
}
