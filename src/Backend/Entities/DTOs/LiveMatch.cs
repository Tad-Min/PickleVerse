using Entities.Enums;
using Entities.Game;
using Entities.Models;
using PickleVerse.Multiplayer;

namespace Entities.DTOs;

public sealed class LiveMatchDto
{
    public Guid MatchId { get; set; }
    public MatchStatus Status { get; set; }
    public MatchAuthorityMode AuthorityMode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? HeartbeatAt { get; set; }
    public bool HeartbeatStale { get; set; }
    public MatchRoster Roster { get; set; } = new();
    public DedicatedLiveState? Live { get; set; }
    public DedicatedOperationRecord? StopOperation { get; set; }
}
