using Entities.DTOs;

namespace Services.Interfaces;
public interface IAnalyticsService
{
    Task<AdminAnalyticsResponse> Summary(DateOnly? from=null,DateOnly? to=null);
    Task<List<AiTechniqueErrorHeatmapDto>> Errors(DateOnly? from=null,DateOnly? to=null);
    Task<AnalyticsReport> Report(DateOnly? from=null,DateOnly? to=null);
    Task ProjectMatchmaking();
}
