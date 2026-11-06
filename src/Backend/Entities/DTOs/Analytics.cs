namespace Entities.DTOs;

public sealed class AnalyticsReport
{
    public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public string DauDefinition { get; set; } = "Tài khoản Player đăng nhập thành công theo ngày Việt Nam; không tính refresh token hoặc bot.";
    public string RetentionDefinition { get; set; } = "Quay lại đăng nhập đúng ngày D1/D7/D30 tính từ ngày đăng nhập đầu tiên; cohort chưa đủ tuổi trả null.";
    public string TimeDefinition { get; set; } = "Tổng giây hoạt động ứng dụng được xác nhận, hợp nhất khoảng thời gian trùng giữa thiết bị, theo ngày Việt Nam.";
    public AdminAnalyticsResponse Summary { get; set; } = new();
    public List<DailyActivePlayers> Daily { get; set; } = [];
    public List<RetentionCohort> Retention { get; set; } = [];
    public List<PopularLesson> Lessons { get; set; } = [];
    public QueueHealth Matchmaking { get; set; } = new();
    public List<AiTechniqueErrorHeatmapDto> Errors { get; set; } = [];
}
public sealed class DailyActivePlayers { public DateOnly Day { get; set; } public int Players { get; set; } }
public sealed class RetentionCohort
{
    public DateOnly Day { get; set; }
    public int Players { get; set; }
    public double? D1 { get; set; }
    public double? D7 { get; set; }
    public double? D30 { get; set; }
}
public sealed class PopularLesson
{
    public int LessonId { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public int Starts { get; set; }
    public int Finished { get; set; }
    public int Passed { get; set; }
    public int Players { get; set; }
    public double CompletionRate { get; set; }
}
public sealed class QueueHealth
{
    public int Searches { get; set; }
    public int Searching { get; set; }
    public int Matched { get; set; }
    public int Cancelled { get; set; }
    public int TimedOut { get; set; }
    public int BotFallback { get; set; }
    public int CancelledAfterAssignment { get; set; }
    public double? AverageWaitSeconds { get; set; }
    public double? P95WaitSeconds { get; set; }
}
