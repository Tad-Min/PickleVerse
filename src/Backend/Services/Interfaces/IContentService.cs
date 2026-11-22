using Entities.DTOs;

namespace Services.Interfaces;

public interface IContentService
{
    Task<List<TechniqueReferenceDto>> References();
    Task<TechniqueReferenceDto> CreateReference(int technique, CreateTechniqueReference request);
    Task<List<AdminLessonContent>> Lessons();
    Task<AdminLessonContent> Get(int lessonId);
    Task<int> CreateLesson(CreateLessonRequest request);
    Task<AdminLessonContent> SaveLesson(int lessonId, SaveLessonRequest request);
    Task<AdminLessonContent> AddDrill(int lessonId, SaveDrillRequest request);
    Task<AdminLessonContent> SaveDrill(int lessonId, int drillId, SaveDrillRequest request);
    Task<AdminLessonContent> DeleteDrill(int lessonId, int drillId, long expectedVersion);
    Task<AdminLessonContent> Publish(int lessonId, ContentVersionRequest request, bool publish);
}
