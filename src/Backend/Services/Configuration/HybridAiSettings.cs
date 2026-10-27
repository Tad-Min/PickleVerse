using Microsoft.Extensions.Configuration;
using PickleVerse.Progression;

namespace Services.Configuration;

public sealed class HybridAiSettings
{
    public bool IngestEnabled { get; }
    public bool DispatcherEnabled { get; }
    public bool CoachingEnabled { get; }
    public bool ModelEnabled { get; }
    public string ServiceUrl { get; }
    public string ServiceKey { get; }
    public string RatingPolicyVersion { get; }
    public BotPolicy BotPolicy { get; }
    public HybridAiSettings(IConfiguration configuration)
    {
        bool Flag(string name) => bool.TryParse(configuration["HybridAi:" + name], out var value) && value;
        IngestEnabled = Flag("IngestEnabled");
        DispatcherEnabled = Flag("DispatcherEnabled");
        CoachingEnabled = Flag("CoachingEnabled");
        ModelEnabled = Flag("ModelEnabled");
        ServiceUrl = configuration["HybridAi:ServiceUrl"] ?? "http://ai-service:8000";
        ServiceKey = configuration["HybridAi:ServiceKey"] ?? "";
        RatingPolicyVersion = configuration["HybridAi:RatingPolicyVersion"] ?? EloPolicy.CurrentVersion;
        if (RatingPolicyVersion is not (EloPolicy.CurrentVersion or EloPolicy.LegacyVersion))
            throw new InvalidOperationException("Unknown HybridAi:RatingPolicyVersion.");
        if (DispatcherEnabled && (!Uri.TryCreate(ServiceUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || ServiceKey.Length < 32))
            throw new InvalidOperationException("Hybrid AI dispatcher requires an HTTP service URL and a service key of at least 32 characters.");
        BotPolicy = new() { version = configuration["HybridAi:BotPolicy:version"] ?? "bot-v1" };
        for (int i = 0; i < BotPolicy.tiers.Length; i++)
        {
            float Number(string name, float fallback)
            {
                var text = configuration[$"HybridAi:BotPolicy:tiers:{i}:" + name];
                if (text == null) return fallback;
                if (!float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
                    throw new InvalidOperationException("Invalid bot profile number.");
                return value;
            }
            var tier = BotPolicy.tiers[i];
            tier.reactionSeconds = Number("reactionSeconds", tier.reactionSeconds);
            tier.aimError = Number("aimError", tier.aimError);
            tier.aggressiveness = Number("aggressiveness", tier.aggressiveness);
        }
        if (BotPolicy.tiers?.Length != 3 || string.IsNullOrWhiteSpace(BotPolicy.version)
            || !BotPolicy.tiers.Select(t => t.minimumElo).SequenceEqual(new[] { 100, 500, 2000 })
            || BotPolicy.tiers.Any(t => !float.IsFinite(t.reactionSeconds) || t.reactionSeconds < 0 || !float.IsFinite(t.aimError) || t.aimError < 0 || !float.IsFinite(t.aggressiveness) || t.aggressiveness is < 0 or > 1))
            throw new InvalidOperationException("Invalid HybridAi:BotPolicy.");
    }
}
