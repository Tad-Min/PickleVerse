using System.ComponentModel.DataAnnotations;
using PickleVerse.Replays;

namespace Entities.DTOs;

public sealed record ReplayLaunchLink(Guid MatchId, string Url, DateTime ExpiresAt);
public sealed record ReplayLaunchAccess(Guid AdminId, int AuthVersion, Guid MatchId, DateTime ExpiresAt);
public sealed record ReplayLaunchView(Guid MatchId, string Grant, DateTime ExpiresAt, ReplayMetadata Metadata);
public sealed class ReplayLaunchRedeem
{
    [Required, RegularExpression("^[a-f0-9]{64}$")]
    public string Code { get; set; } = "";
}
