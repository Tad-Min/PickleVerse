using System.Text.Json;
using System.Text.Json.Serialization;

namespace Services.Game;

public static class HybridJson
{
    static readonly JsonSerializerOptions Options = new(GameJson.Options) {
        IgnoreReadOnlyProperties=false, DefaultIgnoreCondition=JsonIgnoreCondition.WhenWritingNull
    };
    public static string Write<T>(T value)=>JsonSerializer.Serialize(value,Options);
}
