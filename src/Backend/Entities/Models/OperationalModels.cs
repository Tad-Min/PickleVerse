namespace Entities.Models;

public sealed class CosmeticPrice
{
    public int CosmeticId { get; set; }
    public int CurrencyId { get; set; }
    public int Price { get; set; }
}

public sealed class GameCommandRecord
{
    public Guid PlayerId { get; set; }
    public string CommandId { get; set; } = null!;
    public string Kind { get; set; } = null!;
    public string Fingerprint { get; set; } = null!;
    public string Result { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public sealed class CurrencyTransaction
{
    public long TransactionId { get; set; }
    public Guid PlayerId { get; set; }
    public int CurrencyId { get; set; }
    public string CommandId { get; set; } = null!;
    public long Delta { get; set; }
    public long BalanceAfter { get; set; }
    public string Reason { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public sealed class TrainingSessionRecord
{
    public Guid AttemptId { get; set; }
    public long Sequence { get; set; }
    public Guid PlayerId { get; set; }
    public int LessonId { get; set; }
    public int DrillId { get; set; }
    public string Data { get; set; } = null!;
    public bool Completed { get; set; }
    public bool IsCurrent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class AiTelemetrySession
{
    public Guid BatchId { get; set; }
    public Guid PlayerId { get; set; }
    public Guid SessionId { get; set; }
    public string SessionType { get; set; } = null!;
    public int PartCount { get; set; }
    public string Metadata { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public sealed class AiTelemetryPart
{
    public Guid BatchId { get; set; }
    public int PartIndex { get; set; }
    public string Fingerprint { get; set; } = null!;
    public string Shots { get; set; } = null!;
}

public sealed class AiAnalysisJob
{
    public Guid BatchId { get; set; }
    public string State { get; set; } = null!;
    public int Attempts { get; set; }
    public DateTime AvailableAt { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public string? Request { get; set; }
    public string RuleVersion { get; set; } = null!;
    public string? LastError { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class AiCoachingSession
{
    public Guid BatchId { get; set; }
    public Guid PlayerId { get; set; }
    public Guid SessionId { get; set; }
    public string SessionType { get; set; } = null!;
    public string Result { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public sealed class AiCoachingProfile
{
    public Guid PlayerId { get; set; }
    public Guid BatchId { get; set; }
    public string Result { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }
}

public partial class Cosmetic
{
    public string Config { get; set; } = "{}";
}

public partial class Mission
{
    public string? Code { get; set; }
    public string Config { get; set; } = "{}";
}

public partial class Lesson
{
    public string? Code { get; set; }
    public int SortOrder { get; set; }
    public string Config { get; set; } = "{}";
}

public partial class Drill
{
    public string? Code { get; set; }
}

public partial class Playerlessondrill
{
    public int Attempts { get; set; }
    public int Retries { get; set; }
    public int Completions { get; set; }
    public int Trials { get; set; }
    public int Successes { get; set; }
    public double Seconds { get; set; }
    public double BestAccuracy { get; set; }
    public string Errors { get; set; } = "[]";
}

