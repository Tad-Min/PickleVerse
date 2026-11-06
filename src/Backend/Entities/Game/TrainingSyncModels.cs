using System.ComponentModel.DataAnnotations;
using Entities.Enums;
using PickleVerse.Progression;

namespace Entities.Game;

public sealed class TrainingSyncRequest
{
    [Required, MinLength(1), MaxLength(50)] public List<TrainingAttempt> Attempts { get; set; } = [];
}
public sealed class TrainingSyncReceipt
{
    public string AttemptId { get; set; } = "";
    public SyncItemStatus Status { get; set; }
    public string Message { get; set; } = "";
    public bool Retryable { get; set; }
    public TrainingAttempt? Result { get; set; }
}
public sealed class TrainingSyncResult
{
    public GameSnapshot Snapshot { get; set; } = new();
    public List<TrainingSyncReceipt> Receipts { get; set; } = [];
}
public sealed class OnboardingRequest
{
    [Required, StringLength(64, MinimumLength = 8)] public string RequestId { get; set; } = "";
    [Required, RegularExpression("^(NEW|EXPERIENCED)$")] public string Choice { get; set; } = "";
}
