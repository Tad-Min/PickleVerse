using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Entities.Enums;

/// <summary>Explicit, stable SQL/JSON codes. Enum member names are not persistence values.</summary>
public static class StatusWire
{
    static class Codes<T> where T : struct, Enum
    {
        public static readonly IReadOnlyDictionary<T, string> Values = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static)
            .ToDictionary(f => (T)f.GetValue(null)!, f => f.GetCustomAttribute<EnumMemberAttribute>()?.Value
                ?? throw new InvalidOperationException($"Missing wire code: {typeof(T).Name}.{f.Name}"));
        public static readonly IReadOnlyDictionary<string, T> Reverse = Values.ToDictionary(p => p.Value, p => p.Key, StringComparer.Ordinal);
    }

    public static string Format<T>(T value) where T : struct, Enum => Codes<T>.Values.TryGetValue(value, out var code)
        ? code : throw new ArgumentOutOfRangeException(nameof(value), "Trạng thái không hợp lệ.");
    public static T Parse<T>(string value) where T : struct, Enum => TryParse<T>(value, out var result)
        ? result : throw new ArgumentException("Trạng thái không hợp lệ.", nameof(value));
    public static bool TryParse<T>(string? value, out T result) where T : struct, Enum
    {
        result = default;
        return value != null && Codes<T>.Reverse.TryGetValue(value, out result);
    }
}

public sealed class StatusJsonConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String && StatusWire.TryParse<T>(reader.GetString(), out var value)
            ? value : throw new JsonException("Mã trạng thái không hợp lệ.");
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => writer.WriteStringValue(StatusWire.Format(value));
}

/// <summary>Named gameplay enums keep their protocol; canonical statuses use their explicit converter attributes.</summary>
public sealed class GameplayEnumConverter : JsonConverterFactory
{
    readonly JsonStringEnumConverter fallback = new();
    public override bool CanConvert(Type type) => type.IsEnum && type.GetCustomAttribute<JsonConverterAttribute>() == null;
    public override JsonConverter? CreateConverter(Type type, JsonSerializerOptions options) => fallback.CreateConverter(type, options);
}
