using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Entities.DTOs;
using Entities.Exceptions;
using Services.Configuration;
using Services.Interfaces;

namespace Services.Implementations;

public sealed class PayOsGateway(IHttpClientFactory clients, PayOsSettings settings) : IPayOsGateway
{
    public const string ClientName = "payos-membership";
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    const string PaymentRequests = "v2/payment-requests";

    public async Task<PayOsCheckout> Create(long orderCode, int amount, DateTime expiresAt, CancellationToken cancellation)
    {
        const string description = "PKVTV";
        string data = FormattableString.Invariant(
            $"amount={amount}&cancelUrl={settings.CancelUrl}&description={description}&orderCode={orderCode}&returnUrl={settings.ReturnUrl}");
        var checkout = await Send(HttpMethod.Post, PaymentRequests, new
        {
            orderCode,
            amount,
            description,
            items = new[] { new { name = "Gói thành viên PickleVerse vĩnh viễn", quantity = 1, price = amount } },
            expiredAt = new DateTimeOffset(expiresAt).ToUnixTimeSeconds(),
            returnUrl = settings.ReturnUrl,
            cancelUrl = settings.CancelUrl,
            signature = Convert.ToHexStringLower(Sign(data))
        }, cancellation);
        string linkId = Text(checkout, "paymentLinkId");
        string url = Text(checkout, "checkoutUrl");
        if (Number(checkout, "orderCode") != orderCode || Number(checkout, "amount") != amount ||
            Text(checkout, "currency") != "VND" || Text(checkout, "status") != "PENDING" ||
            !ValidLinkId(linkId) || !ValidCheckoutUrl(url, linkId))
            throw InvalidResponse();
        return new(linkId, url);
    }

    public async Task<PayOsPayment> Read(long orderCode, CancellationToken cancellation)
    {
        var payment = await Send(HttpMethod.Get, PaymentRequests + "/" + orderCode.ToString(Invariant), null, cancellation);
        string id = Text(payment, "id");
        if (Number(payment, "orderCode") != orderCode || !ValidLinkId(id))
            throw InvalidResponse();
        long amount = Number(payment, "amount"), paid = Number(payment, "amountPaid"), remaining = Number(payment, "amountRemaining");
        if (amount is <= 0 or > int.MaxValue || paid is < 0 or > int.MaxValue || remaining is < 0 or > int.MaxValue)
            throw InvalidResponse();
        return new(orderCode, id, (int)amount, (int)paid, (int)remaining, Text(payment, "status"));
    }

    public PayOsNotification? VerifyWebhook(JsonElement body)
    {
        RequireEnabled();
        if (!TryProperty(body, "data", out var data) || !Verify(data, Text(body, "signature")))
            throw new ForbiddenException("Chữ ký PayOS không hợp lệ.");
        if (Text(body, "code") != "00" || Text(data, "code") != "00" || Text(data, "currency") != "VND" ||
            !TryProperty(body, "success", out var success) || success.ValueKind != JsonValueKind.True)
            return null;
        long code = Number(data, "orderCode");
        string linkId = Text(data, "paymentLinkId");
        return code > 0 && ValidLinkId(linkId) ? new(code, linkId) : null;
    }

    async Task<JsonElement> Send(HttpMethod method, string path, object? body, CancellationToken cancellation)
    {
        RequireEnabled();
        using var client = clients.CreateClient(ClientName);
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("x-client-id", settings.ClientId);
        request.Headers.Add("x-api-key", settings.ApiKey);
        if (body != null) request.Content = JsonContent.Create(body);
        try
        {
            using var response = await client.SendAsync(request, cancellation);
            if (!response.IsSuccessStatusCode) throw Unavailable();
            var envelope = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellation);
            if (Text(envelope, "code") != "00") throw Unavailable();
            if (!TryProperty(envelope, "data", out var data) || !Verify(data, Text(envelope, "signature")))
                throw InvalidResponse();
            return data;
        }
        catch (HttpRequestException) { throw Unavailable(); }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { throw Unavailable(); }
        catch (JsonException) { throw InvalidResponse(); }
    }

    void RequireEnabled()
    {
        if (!settings.Enabled) throw new AppException("Thanh toán PayOS hiện chưa khả dụng.", HttpStatusCode.ServiceUnavailable);
    }

    static AppException Unavailable() => new("Chưa kết nối được PayOS. Vui lòng thử lại.", HttpStatusCode.ServiceUnavailable);
    static AppException InvalidResponse() => new("Không xác minh được dữ liệu đơn thanh toán từ PayOS.", HttpStatusCode.BadGateway);
    static bool ValidLinkId(string value) => value.Length is > 0 and <= 64 && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_');
    static bool ValidCheckoutUrl(string value, string linkId) =>
        value.Length <= 512 && Uri.TryCreate(value, UriKind.Absolute, out var url) &&
        url.Scheme == Uri.UriSchemeHttps && url.IdnHost == "pay.payos.vn" && url.IsDefaultPort &&
        url.UserInfo.Length == 0 && url.Fragment.Length == 0 &&
        url.AbsolutePath.TrimEnd('/') == "/web/" + linkId;

    byte[] Sign(string query) => HMACSHA256.HashData(Encoding.UTF8.GetBytes(settings.ChecksumKey), Encoding.UTF8.GetBytes(query));
    bool Verify(JsonElement data, string hex)
    {
        if (data.ValueKind != JsonValueKind.Object || hex.Length != 64) return false;
        try
        {
            var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (!CollectFields(data, "", fields)) return false;
            var expected = Sign(string.Join('&', fields.Select(field => field.Key + "=" + field.Value)));
            return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(hex));
        }
        catch (FormatException) { return false; }
    }

    static bool CollectFields(JsonElement obj, string prefix, SortedDictionary<string, string> fields)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in obj.EnumerateObject())
        {
            if (!names.Add(property.Name)) return false;
            string path = prefix + property.Name;
            bool added = property.Value.ValueKind == JsonValueKind.Object
                ? CollectFields(property.Value, path + ".", fields)
                : fields.TryAdd(path, QueryValue(property.Value));
            if (!added) return false;
        }
        return true;
    }

    static string QueryValue(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) return "";
        if (value.ValueKind == JsonValueKind.String)
        {
            string text = value.GetString()!;
            return text is "null" or "undefined" ? "" : text;
        }
        if (value.ValueKind != JsonValueKind.Array) return value.GetRawText();
        // Payment signatures preserve array order and sort the keys of each object.
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            WriteSorted(writer, value);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    static void WriteSorted(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in value.EnumerateArray()) WriteSorted(writer, item);
            writer.WriteEndArray();
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteSorted(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else value.WriteTo(writer);
    }
    static bool TryProperty(JsonElement value, string name, out JsonElement property)
    {
        property = default;
        return value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out property);
    }
    static string Text(JsonElement value, string name) =>
        TryProperty(value, name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString()! : "";
    static long Number(JsonElement value, string name) =>
        TryProperty(value, name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var number) ? number : -1;
}
