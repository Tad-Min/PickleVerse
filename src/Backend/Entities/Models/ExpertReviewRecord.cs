namespace Entities.Models;

public sealed class ExpertReviewRecord
{
    public Guid ReviewId { get; set; }
    public Guid BatchId { get; set; }
    public Guid ReviewerId { get; set; }
    public long Version { get; set; }
    public string Data { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}
