namespace Entities.Models;

public sealed class TechniqueReferenceRecord
{
    public Guid ReferenceId { get; set; }
    public int TechniqueId { get; set; }
    public long Version { get; set; }
    public string Data { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}
