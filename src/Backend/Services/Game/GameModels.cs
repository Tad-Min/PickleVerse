using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using PickleVerse.Progression;

namespace Services.Game;

public static class GameJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        IncludeFields = true, PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, IgnoreReadOnlyProperties = true,
        Converters = { new JsonStringEnumConverter() }
    };
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Options)
        ?? throw new InvalidOperationException("Invalid stored game data.");
}
public sealed class CosmeticOffer
{
    public string id = "", title = "", description = "", collection = "";
    public int tokenpadPrice, elitePrice, releaseOrder;
    public bool gemstone;
}
public sealed class GameCatalog
{
    public string version = "";
    public int winXp, lossXp;
    public RewardBundle dailyBonus, weeklyBonus;
    public MatchRewards matchRewards = new();
    public List<ProgressDefinition> definitions = [];
    public TrainingDrill[] lessons = [];
    public CosmeticOffer[] cosmetics = [];
    public ProgressionConfig Rules() => new() { winXp=winXp, lossXp=lossXp, dailyBonus=dailyBonus, weeklyBonus=weeklyBonus, definitions=definitions };
}
public sealed class MatchRewards
{
    public int botWinReward, botLossReward, onlineWinReward, onlineLossReward;
}
public sealed class OnlineInventory
{
    public long tokenpad, elite;
    public List<string> owned = [];
    public string equippedPaddle = "paddle.default";
}
public sealed class GameSnapshot
{
    public long revision;
    public bool isMale;
    public PlayerProfile player = new();
    public OnlineInventory inventory = new();
    public GameCatalog catalog = new();
    public TutorialState tutorial = new();
}
public sealed class GameCommand
{
    [Required, StringLength(64, MinimumLength=8)] public string RequestId { get; set; } = "";
    [Required, StringLength(50)] public string Code { get; set; } = "";
    public string Currency { get; set; } = "Tokenpad";
}
public sealed class StartTrainingRequest
{
    [Required, StringLength(64, MinimumLength=8)] public string RequestId { get; set; } = "";
    [Required, StringLength(50)] public string LessonCode { get; set; } = "";
    public bool Retry { get; set; }
}
public sealed class TrainingUpdateRequest
{
    [Required, StringLength(64, MinimumLength=8)] public string RequestId { get; set; } = "";
    [Required] public TrainingAttempt Attempt { get; set; } = new();
}
public sealed class MatchStartRequest
{
    [Required, StringLength(64, MinimumLength=8)] public string RequestId { get; set; } = "";
    public MatchKind Kind { get; set; } = MatchKind.BotPractice;
}
public sealed class MatchFinishRequest
{
    [Required, StringLength(64, MinimumLength=8)] public string RequestId { get; set; } = "";
    [Required] public MatchResult Result { get; set; } = new();
}
public sealed class TutorialUpdateRequest
{
    [Required, StringLength(64, MinimumLength=8)] public string RequestId { get; set; } = "";
    [Required] public string Key { get; set; } = "";
}
public sealed class TutorialState { public bool completed, skipped; }
