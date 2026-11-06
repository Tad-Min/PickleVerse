using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using Entities.Enums;

namespace Entities.Game;

public sealed class DedicatedAllocation
{
    public string MatchId { get; set; } = "";
    public string LeaseId { get; set; } = "";
    public string WorkerId { get; set; } = "";
    public string Session { get; set; } = "";
    public string Region { get; set; } = "asia";
    public DedicatedAllocationStatus State { get; set; } = DedicatedAllocationStatus.Queued;
    public MatchRoster Roster { get; set; } = new();
    public bool TelemetryEnabled { get; set; }
    public string ReplayWriter { get; set; } = "";
    public int Protocol { get; set; }
}
public sealed class DedicatedTicket
{
    public DedicatedAllocationStatus State { get; set; } = DedicatedAllocationStatus.Pending;
    public string Session { get; set; } = "";
    public string Region { get; set; } = "";
    public string ConnectionToken { get; set; } = "";
    public MatchRoster Roster { get; set; } = new();
}
public sealed class DedicatedCommand
{
    public string MatchId { get; set; } = "";
    public string LeaseId { get; set; } = "";
    public Guid? OperationId { get; set; }
    public DedicatedLiveState? Live { get; set; }
}
public sealed class DedicatedHeartbeat
{
    public Guid? StopOperationId { get; set; }
    public string? Reason { get; set; }
}
public sealed class DedicatedAdmissionCommand
{
    public string LeaseId { get; set; } = "";
    public Guid PlayerId { get; set; }
}
public sealed class DedicatedLiveState
{
    public int ScoreA { get; set; }
    public int ScoreB { get; set; }
    public int GamesA { get; set; }
    public int GamesB { get; set; }
    public bool Paused { get; set; }
    public bool Started { get; set; }
    public bool Finished { get; set; }
    public DateTime ObservedAt { get; set; }
    public List<DedicatedLivePlayer> Players { get; set; } = [];
}
public sealed class DedicatedLivePlayer
{
    public string Id { get; set; } = "";
    public bool Connected { get; set; }
    public DateTime? ReconnectUntil { get; set; }
}
public sealed class DedicatedCompletion
{
    public string LeaseId { get; set; } = "";
    public List<DedicatedPlayerResult> Results { get; set; } = [];
}
public sealed class DedicatedPlayerResult
{
    public string onlineId = "";
    public MatchResult result = new();
}
