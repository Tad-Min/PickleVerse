using Entities.DTOs;
using Entities.Exceptions;
using Microsoft.Extensions.Logging;
using Repositories.Caching;
using Repositories.UnitOfWork;
using Services.Interfaces;

namespace Services.Implementations;
public sealed class AnalyticsService(IUnitOfWork unit,RedisGameState queue,TimeProvider clock,ILogger<AnalyticsService> logger) : IAnalyticsService
{
    DateOnly Today=>DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(7));
    static DateTime Utc(DateOnly day)=>day.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc).AddHours(-7);
    void Validate(DateOnly? from,DateOnly? to)
    { if(from>to || from>Today || to>Today || from<DateOnly.FromDateTime(new DateTime(2000,1,1)))throw new BadRequestException("Khoảng ngày thống kê không hợp lệ."); }
    public Task<AdminAnalyticsResponse> Summary(DateOnly? from=null,DateOnly? to=null)
    { Validate(from,to);return unit.Game.AnalyticsSummary(from.HasValue?Utc(from.Value):null,to.HasValue?Utc(to.Value.AddDays(1)):null,Today); }
    public Task<List<AiTechniqueErrorHeatmapDto>> Errors(DateOnly? from=null,DateOnly? to=null)
    { Validate(from,to);return unit.Game.ErrorHeatmap(from.HasValue?Utc(from.Value):null,to.HasValue?Utc(to.Value.AddDays(1)):null); }
    public async Task<AnalyticsReport> Report(DateOnly? from=null,DateOnly? to=null)
    {
        to??=Today;from??=to.Value.AddDays(-29);Validate(from,to);
        if(to.Value.DayNumber-from.Value.DayNumber>365)throw new BadRequestException("Biểu đồ hỗ trợ tối đa 366 ngày mỗi lần xem.");
        var start=Utc(from.Value);var end=Utc(to.Value.AddDays(1));
        var daily=await unit.Game.DailyActive(from.Value,to.Value);var dailyByDay=daily.ToDictionary(x=>x.Day);
        return new AnalyticsReport{From=from.Value,To=to.Value,Summary=await Summary(from,to),
            Daily=Enumerable.Range(0,to.Value.DayNumber-from.Value.DayNumber+1).Select(i=>dailyByDay.GetValueOrDefault(from.Value.AddDays(i))??new DailyActivePlayers{Day=from.Value.AddDays(i)}).ToList(),
            Retention=await unit.Game.Retention(from.Value,to.Value,Today),Lessons=await unit.Game.PopularLessons(start,end),
            Matchmaking=await unit.Game.QueueMetrics(start,end),Errors=await Errors(from,to)};
    }
    public async Task ProjectMatchmaking()
    {
        foreach(var item in await queue.ReadJournal())
        {
            try
            {
                var entry=GameJson.Read<QueueEntry>(item.data);
                await unit.Game.RecordSearch(entry,queue.QueueLeaseSeconds);
                await queue.AcknowledgeJournal(item.id);
            }
            catch(Exception error) { logger.LogWarning("Matchmaking journal item {EntryId} remains pending ({ErrorType}).",item.id,error.GetType().Name); }
        }
        var cutoff=clock.GetUtcNow().UtcDateTime.AddSeconds(-queue.QueueLeaseSeconds);
        foreach(var search in await unit.Game.StaleSearches(cutoff))
        {
            var current=await queue.SearchSnapshot(search.SearchId);
            if(current==null){await unit.Game.TimeoutMissingSearch(search.SearchId,cutoff,queue.QueueLeaseSeconds);continue;}
            if(current.state==MatchmakingSearchStatus.Searching && current.lastSeen<new DateTimeOffset(cutoff).ToUnixTimeMilliseconds())
            {
                string before=GameJson.Write(current);current.state=MatchmakingSearchStatus.TimedOut;
                if(!await queue.Compare([before],[current]))continue;
            }
            await unit.Game.RecordSearch(current,queue.QueueLeaseSeconds);
        }
    }
}
