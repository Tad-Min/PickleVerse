using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Entities.Enums;
using PickleVerse.Progression;

namespace Entities.DTOs;

[JsonConverter(typeof(StatusJsonConverter<ExpertTier>))]
public enum ExpertTier
{
    [EnumMember(Value = "beginner")] Beginner,
    [EnumMember(Value = "intermediate")] Intermediate,
    [EnumMember(Value = "advanced")] Advanced
}

[JsonConverter(typeof(StatusJsonConverter<DatasetSplit>))]
public enum DatasetSplit
{
    [EnumMember(Value = "train")] Train,
    [EnumMember(Value = "calibration")] Calibration,
    [EnumMember(Value = "test")] Test
}

public sealed class ExpertShotLabel
{
    [Required, StringLength(100)] public string ShotId { get; set; } = "";
    [Required, StringLength(40)] public string Code { get; set; } = "";
    [Required] public bool? Present { get; set; }
    [Required, StringLength(1000, MinimumLength = 3)] public string Evidence { get; set; } = "";
}

public sealed class ExpertReviewRequest
{
    public long ExpectedVersion { get; set; }
    public ExpertTier? Tier { get; set; }
    [Required] public DatasetSplit? Split { get; set; }
    [Required, StringLength(120, MinimumLength = 3)] public string ExpertName { get; set; } = "";
    [Required, StringLength(2000, MinimumLength = 3)] public string Observation { get; set; } = "";
    public bool ConfirmedHumanSession { get; set; }
    [MaxLength(1000)] public List<ExpertShotLabel> ShotLabels { get; set; } = [];
}

public sealed class ExpertReviewReceipt
{
    public Guid ReviewId { get; set; }
    public Guid ReviewerId { get; set; }
    public long Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public ExpertReviewRequest Review { get; set; } = new();
}

public sealed class ReviewableTrainingSession
{
    public Guid BatchId { get; set; }
    public Guid SessionId { get; set; }
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = "";
    public string LessonTitle { get; set; } = "";
    public string CatalogVersion { get; set; } = "";
    public DateTime CompletedAt { get; set; }
    public long ReviewedVersion { get; set; }
}

public sealed class ExpertSessionEvidence
{
    public ReviewableTrainingSession Session { get; set; } = new();
    public List<ShotTelemetry> Shots { get; set; } = [];
    public ExpertReviewReceipt? LatestReview { get; set; }
    public string GameVersion { get; set; } = "";
}

public sealed record ExpertDatasetFile(byte[] Content, string Sha256, string Name);

public sealed record CoachingDemoSession(string SessionId, string PlayerCode, ExpertTier Tier, DatasetSplit Split);

public sealed record CoachingDemoEvidence(CoachingDemoSession Session, string GameVersion, List<ShotTelemetry> Shots);
