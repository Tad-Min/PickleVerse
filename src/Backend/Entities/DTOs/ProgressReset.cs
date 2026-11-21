using System.ComponentModel.DataAnnotations;
using Entities.Enums;

namespace Entities.DTOs;

public class ProgressResetSelection
{
    [Required, MinLength(1), MaxLength(4)] public List<ProgressResetGroup> Groups { get; set; } = [];
}
public sealed class ExecuteProgressReset : ProgressResetSelection
{
    [Required, StringLength(64, MinimumLength = 8)] public string RequestId { get; set; } = "";
    [Range(0, long.MaxValue)] public long ExpectedRevision { get; set; }
}
public sealed class ProgressResetPreview
{
    public Guid PlayerId { get; set; }
    public long Revision { get; set; }
    public bool Busy { get; set; }
    public List<ProgressResetGroup> Groups { get; set; } = [];
    public List<string> Effects { get; set; } = [];
    public int LessonRecords { get; set; }
    public int MasteryRecords { get; set; }
    public int MissionRecords { get; set; }
    public int AchievementRecords { get; set; }
    public int Level { get; set; }
    public int Elo { get; set; }
    public long Tokenpad { get; set; }
    public long Elite { get; set; }
    public List<string> OwnedCosmetics { get; set; } = [];
}
public sealed class ProgressResetReceipt
{
    public Guid AuditId { get; set; }
    public Guid PlayerId { get; set; }
    public long Revision { get; set; }
    public List<ProgressResetGroup> Groups { get; set; } = [];
    public string Message { get; set; } = "Đã đặt lại các nhóm tiến trình đã chọn. Nhật ký thưởng được giữ nguyên.";
}
