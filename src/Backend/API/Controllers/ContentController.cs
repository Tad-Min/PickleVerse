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
    [HttpPut("{id:int}")] public Task<AdminLessonContent> Save(int id, SaveLessonContent request) => content.Save(id, request);
    [HttpPost("{id:int}/publish")] public Task<AdminLessonContent> Publish(int id, ContentVersionRequest request) => content.Publish(id, request, true);
    [HttpPost("{id:int}/unpublish")] public Task<AdminLessonContent> Unpublish(int id, ContentVersionRequest request) => content.Publish(id, request, false);
}

[ApiController, Authorize(Roles = "Admin"), Route("api/Admin/cms/techniques")]
public sealed class TechniqueReferencesController(IContentService content) : ControllerBase
{
    [HttpGet("references")] public Task<List<TechniqueReferenceDto>> References()=>content.References();
    [HttpPost("{id:int}/references")] public Task<TechniqueReferenceDto> Create(int id,CreateTechniqueReference request)=>content.CreateReference(id,request);
}
