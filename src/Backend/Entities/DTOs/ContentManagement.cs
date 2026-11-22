using System.ComponentModel.DataAnnotations;
using Entities.Enums;
using PickleVerse.Progression;

namespace Entities.DTOs;

/// <summary>Bài tập trong một bài học: bản đang soạn và bản đã xuất bản.</summary>
public sealed class AdminDrillContent
{
    public int DrillId { get; set; }
    public string Code { get; set; } = "";
    public int Order { get; set; }
    public TrainingDrill Draft { get; set; } = new();
    public TrainingDrill? Published { get; set; }
    /// <summary>Bản đang soạn khác bản đã xuất bản (hoặc chưa từng xuất bản).</summary>
    public bool HasChanges { get; set; }
}

/// <summary>Bài học: nhóm nhiều bài tập.</summary>
public sealed class AdminLessonContent
{
    public int LessonId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ContentPublishStatus Status { get; set; }
    public long Version { get; set; }
    public int Order { get; set; }
    public List<AdminDrillContent> Drills { get; set; } = [];
}

public sealed class CreateLessonRequest
{
    [Required] public string Code { get; set; } = "";
    [Required, StringLength(254)] public string Name { get; set; } = "";
    [StringLength(254)] public string Description { get; set; } = "";
    public int Order { get; set; }
}

public sealed class SaveLessonRequest
{
    [Range(1, long.MaxValue)] public long ExpectedVersion { get; set; }
    [Required, StringLength(254)] public string Name { get; set; } = "";
    [StringLength(254)] public string Description { get; set; } = "";
    public int Order { get; set; }
}

/// <summary>Tạo hoặc cập nhật một bài tập của bài học; mã bài tập là Config.id.</summary>
public sealed class SaveDrillRequest
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
