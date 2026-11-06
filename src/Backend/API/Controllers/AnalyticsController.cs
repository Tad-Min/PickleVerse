using Entities.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;
[ApiController,Authorize(Roles="Admin"),Route("api/Admin/analytics")]
public sealed class AnalyticsController(IAnalyticsService analytics):ControllerBase
{
    [HttpGet("summary")]public Task<AdminAnalyticsResponse> Summary([FromQuery]DateOnly? from,[FromQuery]DateOnly? to)=>analytics.Summary(from,to);
    [HttpGet("error-heatmap")]public Task<List<AiTechniqueErrorHeatmapDto>> Errors([FromQuery]DateOnly? from,[FromQuery]DateOnly? to)=>analytics.Errors(from,to);
    [HttpGet("report")]public Task<AnalyticsReport> Report([FromQuery]DateOnly? from,[FromQuery]DateOnly? to)=>analytics.Report(from,to);
}
