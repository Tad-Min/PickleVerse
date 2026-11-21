using Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Game;

public sealed partial class GameRepository
{
    public Task<List<Lesson>> ContentLessons() => db.Lessons.AsNoTracking().Where(l => l.Code != null)
        .Include(l => l.Lessondrills).ThenInclude(d => d.Drill).OrderBy(l => l.SortOrder).ThenBy(l => l.Lessonid).ToListAsync();
    public Task<Lesson?> ContentLesson(int id) => db.Lessons.FromSqlInterpolated($"SELECT * FROM lessons WHERE lessonid={id} FOR UPDATE")
        .SingleOrDefaultAsync();
    public Task<Lessondrill?> ContentDrill(int id) => db.Lessondrills.Include(l => l.Drill).SingleOrDefaultAsync(l => l.Lessonid == id);
    public Task<bool> LessonCodeExists(string code) => db.Lessons.AnyAsync(l => l.Code == code);
    public Task<int?> TechniqueId(string code) => db.Techniques.Where(t => t.Techniquename == code).Select(t => (int?)t.Techniqueid).SingleOrDefaultAsync();
    public void AddContent(Lesson lesson, Drill drill) => db.Lessondrills.Add(new Lessondrill { Lesson = lesson, Drill = drill, Target = lesson.Target });
    public Task<List<Entities.DTOs.TechniqueReferenceDto>> ReferenceVersions() => (from r in db.TechniqueReferences.AsNoTracking()
        join t in db.Techniques on r.TechniqueId equals t.Techniqueid orderby t.Techniqueid,r.Version descending
        select new Entities.DTOs.TechniqueReferenceDto { ReferenceId=r.ReferenceId,TechniqueId=r.TechniqueId,Technique=t.Techniquename,
            Version=r.Version,CreatedAt=r.CreatedAt }).ToListAsync();
    public Task<TechniqueReferenceRecord?> Reference(Guid id) => db.TechniqueReferences.AsNoTracking().SingleOrDefaultAsync(x=>x.ReferenceId==id);
    public Task<TechniqueReferenceRecord?> LatestReference(int technique) => db.TechniqueReferences.AsNoTracking()
        .Where(x=>x.TechniqueId==technique).OrderByDescending(x=>x.Version).FirstOrDefaultAsync();
    public void AddReference(TechniqueReferenceRecord value) => db.TechniqueReferences.Add(value);
}
