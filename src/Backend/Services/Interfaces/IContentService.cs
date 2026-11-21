using Entities.DTOs;

namespace Services.Interfaces;

public interface IContentService
{
    Task<List<TechniqueReferenceDto>> References();
    Task<TechniqueReferenceDto> CreateReference(int technique, CreateTechniqueReference request);
    Task<List<AdminLessonContent>> Lessons();
    Task<AdminLessonContent> Get(int lessonId);
    Task<int> Create(CreateLessonRequest request);
    Task<int> UpdateDrill(CreateDrillRequest request);
    Task<AdminLessonContent> Save(int lessonId, SaveLessonContent request);
    Task<AdminLessonContent> Publish(int lessonId, ContentVersionRequest request, bool publish);
}
