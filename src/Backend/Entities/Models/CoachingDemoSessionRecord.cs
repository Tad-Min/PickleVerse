using Entities.DTOs;

namespace Entities.Models;

public sealed class CoachingDemoSessionRecord
{
    public string SessionId { get; set; } = "";
    public string PlayerCode { get; set; } = "";
    public ExpertTier Tier { get; set; }
    public DatasetSplit Split { get; set; }
    public string GameVersion { get; set; } = "";
    public string Shots { get; set; } = "[]";
}
