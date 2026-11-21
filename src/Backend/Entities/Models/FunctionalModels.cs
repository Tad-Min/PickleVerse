using Entities.Enums;

namespace Entities.Models;

public sealed class ContentCatalogRecord
{
    public string Version { get; set; } = "";
    public string Data { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}

public sealed class ProgressRewardGrant
{
    public Guid PlayerId { get; set; }
    public string RewardKey { get; set; } = "";
    public string Details { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}

public sealed class DedicatedOperationRecord
{
    public Guid OperationId { get; set; }
    public Guid MatchId { get; set; }
    public string LeaseId { get; set; } = "";
    public Guid ActorId { get; set; }
    public string Reason { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
}

public partial class Lesson
{
    public ContentPublishStatus PublishStatus { get; set; } = ContentPublishStatus.Published;
    public string? DraftConfig { get; set; }
    public long ContentVersion { get; set; } = 1;
}

public partial class Playerprofile
{
    public DateTime? LearningResetAt { get; set; }
    public long LearningEpoch { get; set; }
    public long MissionEpoch { get; set; }
    public long EconomyEpoch { get; set; }
    public int TutorialCheckpoint { get; set; }
    public string? ExperienceChoice { get; set; }
}

public sealed class AdminAuditRecord
{
    public Guid AuditId { get; set; }
    public Guid ActorId { get; set; }
    public Guid? TargetId { get; set; }
    public string Action { get; set; } = "";
    public string Details { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}
