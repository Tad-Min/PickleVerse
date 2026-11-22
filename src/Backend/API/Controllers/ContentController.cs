using Entities.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController, Authorize(Roles = "Admin"), Route("api/Admin/cms/lessons")]
public sealed class ContentController(IContentService content) : ControllerBase
{
    [HttpGet] public Task<List<AdminLessonContent>> List() => content.Lessons();
    [HttpGet("{id:int}")] public Task<AdminLessonContent> Get(int id) => content.Get(id);
    [HttpPost] public async Task<AdminLessonContent> Create(CreateLessonRequest request) => await content.Get(await content.CreateLesson(request));
    [HttpPut("{id:int}")] public Task<AdminLessonContent> Save(int id, SaveLessonRequest request) => content.SaveLesson(id, request);
    [HttpPost("{id:int}/drills")] public Task<AdminLessonContent> AddDrill(int id, SaveDrillRequest request) => content.AddDrill(id, request);
    [HttpPut("{id:int}/drills/{drillId:int}")] public Task<AdminLessonContent> SaveDrill(int id, int drillId, SaveDrillRequest request) => content.SaveDrill(id, drillId, request);
    [HttpDelete("{id:int}/drills/{drillId:int}")] public Task<AdminLessonContent> DeleteDrill(int id, int drillId, [FromQuery] long expectedVersion) => content.DeleteDrill(id, drillId, expectedVersion);
    [HttpPost("{id:int}/publish")] public Task<AdminLessonContent> Publish(int id, ContentVersionRequest request) => content.Publish(id, request, true);
    [HttpPost("{id:int}/unpublish")] public Task<AdminLessonContent> Unpublish(int id, ContentVersionRequest request) => content.Publish(id, request, false);
}

[ApiController, Authorize(Roles = "Admin"), Route("api/Admin/cms/techniques")]
public sealed class TechniqueReferencesController(IContentService content) : ControllerBase
{
    [HttpGet("references")] public Task<List<TechniqueReferenceDto>> References()=>content.References();
    [HttpPost("{id:int}/references")] public Task<TechniqueReferenceDto> Create(int id,CreateTechniqueReference request)=>content.CreateReference(id,request);
}
