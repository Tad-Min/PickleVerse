using Entities.Enums;

namespace Entities.Models;

public sealed class UserSanction
{
    public Guid SanctionId { get; set; }
    public Guid UserId { get; set; }
    public Guid ActorId { get; set; }
    public UserStatus Restriction { get; set; }
    public string Reason { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? RevokedBy { get; set; }
    public string? RevocationReason { get; set; }
    public Guid? ReportMatchId { get; set; }
    public Guid? ReporterId { get; set; }
}

public sealed class SanctionAppeal
{
    public Guid AppealId { get; set; }
    public Guid SanctionId { get; set; }
    public Guid UserId { get; set; }
    public string Statement { get; set; } = "";
    public AppealStatus Status { get; set; } = AppealStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? ReviewerId { get; set; }
    public string? DecisionReason { get; set; }
}

public partial class Reportplayerinmatch
{
    public Guid? ReviewerId { get; set; }
    public string? DecisionReason { get; set; }
}
