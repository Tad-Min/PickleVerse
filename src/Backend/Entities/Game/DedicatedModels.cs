using PickleVerse.Multiplayer;
using PickleVerse.Progression;

namespace Entities.Game;

public sealed class DedicatedAllocation
{
    public string MatchId { get; set; } = "";
    public string LeaseId { get; set; } = "";
    public string WorkerId { get; set; } = "";
    public string Session { get; set; } = "";
    public string Region { get; set; } = "asia";
    public string State { get; set; } = "queued";
    public MatchRoster Roster { get; set; } = new();
    public bool TelemetryEnabled { get; set; }
    public string ReplayWriter { get; set; } = "";
    public int Protocol { get; set; }
}
public sealed class DedicatedTicket
{
    public string State { get; set; } = "pending";
    public string Session { get; set; } = "";
    public string Region { get; set; } = "";
    public string ConnectionToken { get; set; } = "";
    public MatchRoster Roster { get; set; } = new();
}
public sealed class DedicatedCommand
{
    public string MatchId { get; set; } = "";
    public string LeaseId { get; set; } = "";
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
