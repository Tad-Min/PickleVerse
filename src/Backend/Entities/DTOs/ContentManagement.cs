using System.ComponentModel.DataAnnotations;
using Entities.Enums;
using PickleVerse.Progression;

namespace Entities.DTOs;

public sealed class AdminLessonContent
{
    public int LessonId { get; set; }
    public string Code { get; set; } = "";
    public ContentPublishStatus Status { get; set; }
    public long Version { get; set; }
    public int Order { get; set; }
    public TrainingDrill Draft { get; set; } = new();
    public TrainingDrill? Published { get; set; }
}

public sealed class SaveLessonContent
{
    [Range(1, long.MaxValue)] public long ExpectedVersion { get; set; }
    public int Order { get; set; }
    [Required] public TrainingDrill Config { get; set; } = new();
}

public sealed class ContentVersionRequest
{
    [Range(1, long.MaxValue)] public long ExpectedVersion { get; set; }
}
public sealed class TechniqueReferenceDto
{
    public Guid ReferenceId { get; set; }
    public int TechniqueId { get; set; }
    public string Technique { get; set; } = "";
    public long Version { get; set; }
    public TrainingReferenceData Parameters { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}
public sealed class CreateTechniqueReference
{
    public long ExpectedLatestVersion { get; set; }
    public TrainingReferenceData Parameters { get; set; } = new();
}
