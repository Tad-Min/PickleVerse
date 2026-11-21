using System.ComponentModel.DataAnnotations;
using Entities.Enums;

namespace Entities.DTOs;

public sealed class SanctionRequest
{
    public Guid UserId { get; set; }
    public UserStatus Restriction { get; set; } = UserStatus.Banned;
    [Required, StringLength(2000, MinimumLength = 3)] public string Reason { get; set; } = "";
    public DateTime? ExpiresAt { get; set; }
}
public sealed class AppealRequest
{
    public Guid SanctionId { get; set; }
    [Required, StringLength(4000, MinimumLength = 10)] public string Statement { get; set; } = "";
}
public sealed class AppealReviewRequest
{
    public AppealStatus Status { get; set; }
    [Required, StringLength(2000, MinimumLength = 3)] public string Reason { get; set; } = "";
}
public sealed class RevokeSanctionRequest
{
    [Required, StringLength(2000, MinimumLength = 3)] public string Reason { get; set; } = "";
}
public sealed class StopMatchRequest
{
    [Required, StringLength(2000, MinimumLength = 3)] public string Reason { get; set; } = "";
}
public sealed class ReportTargetDto
{
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = "";
    public int Team { get; set; }
}
