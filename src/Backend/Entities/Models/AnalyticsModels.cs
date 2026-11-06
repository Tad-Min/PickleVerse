using Entities.Enums;

namespace Entities.Models;

public sealed class PlayerLoginDay
{
    public Guid PlayerId { get; set; }
    public DateOnly Day { get; set; }
    public DateTime FirstAt { get; set; }
    public DateTime LastAt { get; set; }
}
public sealed class MatchmakingSearchRecord
{
    public Guid SearchId { get; set; }
    public Guid PlayerId { get; set; }
    public int Mode { get; set; }
    public string Region { get; set; } = "";
    public int Protocol { get; set; }
    public MatchmakingSearchStatus State { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public long? WaitMilliseconds { get; set; }
    public Guid? MatchId { get; set; }
    public bool CancelledAfterAssignment { get; set; }
}
public sealed class PlayerActivityDay
{
    public Guid PlayerId { get; set; }
    public DateOnly Day { get; set; }
    public long Seconds { get; set; }
}
