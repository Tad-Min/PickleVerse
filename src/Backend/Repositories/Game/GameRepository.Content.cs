using Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Game;

public sealed partial class GameRepository
{
    /// <summary>Danh sách bài học kèm bài tập (chỉ đọc), sắp theo thứ tự bài học rồi thứ tự bài tập.</summary>
    public async Task<List<Lesson>> ContentLessons()
    {
        var lessons = await db.Lessons.AsNoTracking().Where(l => l.Code != null)
            .Include(l => l.Lessondrills).ThenInclude(d => d.Drill)
            .OrderBy(l => l.SortOrder).ThenBy(l => l.Lessonid).ToListAsync();
        foreach (var lesson in lessons)
            lesson.Lessondrills = lesson.Lessondrills.OrderBy(d => d.SortOrder).ThenBy(d => d.Drillid).ToList();
        return lessons;
    }
    /// <summary>Khóa hàng bài học trong giao dịch để các thao tác sửa nội dung không ghi đè lẫn nhau.</summary>
    public Task<Lesson?> ContentLesson(int id) => db.Lessons.FromSqlInterpolated($"SELECT * FROM lessons WHERE lessonid={id} FOR UPDATE")
        .SingleOrDefaultAsync();
    public Task<List<Lessondrill>> ContentDrills(int lessonId) => db.Lessondrills.Include(l => l.Drill)
        .Where(l => l.Lessonid == lessonId).OrderBy(l => l.SortOrder).ThenBy(l => l.Drillid).ToListAsync();
    public Task<bool> LessonCodeExists(string code) => db.Lessons.AnyAsync(l => l.Code == code);
    public Task<bool> DrillCodeExists(string code) => db.Drills.AnyAsync(d => d.Code == code);
    public Task<int?> TechniqueId(string code) => db.Techniques.Where(t => t.Techniquename == code).Select(t => (int?)t.Techniqueid).SingleOrDefaultAsync();
    public void AddLesson(Lesson lesson) => db.Lessons.Add(lesson);
    public void AddDrill(Lesson lesson, Drill drill, int target, int order) =>
        db.Lessondrills.Add(new Lessondrill { Lesson = lesson, Drill = drill, Target = target, SortOrder = order });
    /// <summary>Xóa bài tập khỏi bài học; chỉ được gọi khi chưa có tiến độ hay phiên luyện nào của người chơi.</summary>
    public void RemoveDrill(Lessondrill link) { db.Lessondrills.Remove(link); db.Drills.Remove(link.Drill); }
    public async Task<bool> DrillHasPlayerData(int drillId) =>
        await db.Playerlessondrills.AnyAsync(p => p.Drillid == drillId) || await db.TrainingSessionRecords.AnyAsync(s => s.DrillId == drillId);
    /// <summary>Cấu hình đã xuất bản của mọi bài tập thuộc bài học đang xuất bản, trừ bài học loại ra.</summary>
    public async Task<List<string>> PublishedDrillConfigs(int? exceptLesson = null) => await db.Lessondrills.AsNoTracking()
        .Where(ld => ld.Lesson.PublishStatus == Entities.Enums.ContentPublishStatus.Published && ld.Drill.Config != null
            && (exceptLesson == null || ld.Lessonid != exceptLesson))
        .Select(ld => ld.Drill.Config!).ToListAsync();
    public Task<List<Entities.DTOs.TechniqueReferenceDto>> ReferenceVersions() => (from r in db.TechniqueReferences.AsNoTracking()
        join t in db.Techniques on r.TechniqueId equals t.Techniqueid orderby t.Techniqueid,r.Version descending
        select new Entities.DTOs.TechniqueReferenceDto { ReferenceId=r.ReferenceId,TechniqueId=r.TechniqueId,Technique=t.Techniquename,
            Version=r.Version,CreatedAt=r.CreatedAt }).ToListAsync();
    public Task<TechniqueReferenceRecord?> Reference(Guid id) => db.TechniqueReferences.AsNoTracking().SingleOrDefaultAsync(x=>x.ReferenceId==id);
    public Task<TechniqueReferenceRecord?> LatestReference(int technique) => db.TechniqueReferences.AsNoTracking()
        .Where(x=>x.TechniqueId==technique).OrderByDescending(x=>x.Version).FirstOrDefaultAsync();
    public void AddReference(TechniqueReferenceRecord value) => db.TechniqueReferences.Add(value);
}
