using System.Text.Json;
using System.Text.Json.Serialization;

namespace Entities.Game;

// Previously deployed workers send compact roster IDs. Accept that representation
// at this boundary and keep writing canonical JSON GUIDs.
public sealed class DedicatedPlayerIdJsonConverter : JsonConverter<Guid>
{
    public override Guid Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string? value = reader.GetString();
            if (Guid.TryParseExact(value, "D", out var id) || Guid.TryParseExact(value, "N", out id))
                return id;
        }
        throw new JsonException("Mã người chơi phải là UUID dạng D hoặc N.");
    }

    public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}
