using System.Net;
using System.Text.RegularExpressions;
using Entities.DTOs;
using Entities.Exceptions;
using Entities.Models;
using PickleVerse.Progression;
using Repositories.UnitOfWork;
using Services.Interfaces;

namespace Services.Implementations;

public sealed class ContentService(IUnitOfWork unit) : IContentService
{
    public async Task<List<TechniqueReferenceDto>> References()
    {
        var versions=await unit.Game.ReferenceVersions();
        foreach(var version in versions)version.Parameters=GameJson.Read<TrainingReferenceData>((await unit.Game.Reference(version.ReferenceId))!.Data);
        return versions;
    }
    public async Task<TechniqueReferenceDto> CreateReference(int technique,CreateTechniqueReference request)
    {
        var data=request.Parameters;
        if(data==null || !float.IsFinite(data.contactWindowSeconds) || data.contactWindowSeconds is < .05f or > .3f
            || !float.IsFinite(data.earlyBufferSeconds) || data.earlyBufferSeconds is < 0 or > .5f
            || !float.IsFinite(data.recoveryRadius) || data.recoveryRadius is < .3f or > 1.5f
            || !float.IsFinite(data.recoveryTimeoutSeconds) || data.recoveryTimeoutSeconds is < 2 or > 10)
            throw Bad("Tham số kỹ thuật ngoài phạm vi runtime hỗ trợ.");
        await using var tx=await unit.Game.BeginTransactionAsync();await unit.Game.Lock("technique-reference:"+technique);
        var latest=await unit.Game.LatestReference(technique)??throw Bad("Kỹ thuật chưa được runtime hỗ trợ.");
        if(latest.Version!=request.ExpectedLatestVersion)throw new AppException("Phiên bản tham chiếu đã thay đổi. Hãy tải lại.",HttpStatusCode.Conflict);
        var row=new TechniqueReferenceRecord{ReferenceId=Guid.NewGuid(),TechniqueId=technique,Version=latest.Version+1,Data=GameJson.Write(data),CreatedAt=DateTime.UtcNow};
        unit.Game.AddReference(row);await unit.SaveChangesAsync();await tx.CommitAsync();
        return (await References()).Single(x=>x.ReferenceId==row.ReferenceId);
    }
    static AppException Bad(string text) => new(text, HttpStatusCode.BadRequest);
    static AppException Missing() => new("Không tìm thấy bài học.", HttpStatusCode.NotFound);
    static AdminLessonContent View(Lesson lesson) => new() { LessonId = lesson.Lessonid, Code = lesson.Code!, Status = lesson.PublishStatus,
        Version = lesson.ContentVersion, Order = lesson.SortOrder, Draft = GameJson.Read<TrainingDrill>(lesson.DraftConfig ?? lesson.Config),
        Published = lesson.PublishStatus == ContentPublishStatus.Draft ? null : GameJson.Read<TrainingDrill>(lesson.Config) };

    public async Task<List<AdminLessonContent>> Lessons() => (await unit.Game.ContentLessons()).Select(View).ToList();
    public async Task<AdminLessonContent> Get(int lessonId) => (await Lessons()).SingleOrDefault(l => l.LessonId == lessonId) ?? throw Missing();

    public async Task<int> Create(CreateLessonRequest request)
    {
        if (!Regex.IsMatch(request.Code ?? "", "^[a-z][a-z0-9-]{1,49}$")) throw Bad("Mã bài học chỉ gồm chữ thường, số và dấu gạch ngang.");
        await using var tx = await unit.Game.BeginTransactionAsync();
        await unit.Game.Lock("content-code:" + request.Code);
        if (await unit.Game.LessonCodeExists(request.Code!)) throw new AppException("Mã bài học đã tồn tại.", HttpStatusCode.Conflict);
        var config = request.Config ?? new TrainingDrill { skill = TrainingSkill.Serve, masks = [1], prerequisites = [], tier = 0, trials = 8, required = 6, reward = 150, xp = 5 };
        config.id = request.Code!; config.title = request.LessonName; config.explanation = request.Description;
        config.coaching ??= request.Description; config.requiredLevel = request.RequiredLevel;
        Basic(config);
        int technique = await unit.Game.TechniqueId(config.skill.ToString()) ?? throw Bad("Kỹ thuật chưa được runtime hỗ trợ.");
        var lesson = new Lesson { Code = config.id, Lessonname = config.title, Description = config.explanation, Type = config.skill.ToString(),
            Target = config.required, Config = GameJson.Write(config), DraftConfig = GameJson.Write(config), SortOrder = request.Order,
            PublishStatus = ContentPublishStatus.Draft, Reward = GameJson.Write(new RewardBundle(config.reward, 0, config.xp)) };
        unit.Game.AddContent(lesson, new Drill { Code = config.id, Drillname = config.title, Description = config.explanation, Techniqueid = technique });
        await unit.SaveChangesAsync(); await tx.CommitAsync(); return lesson.Lessonid;
    }

    public async Task<int> UpdateDrill(CreateDrillRequest request)
    {
        await using var tx = await unit.Game.BeginTransactionAsync();
        var lesson = await unit.Game.ContentLesson(request.LessonId) ?? throw Missing();
        var link = await unit.Game.ContentDrill(lesson.Lessonid) ?? throw Bad("Bài học chưa có cấu hình drill.");
        var draft = GameJson.Read<TrainingDrill>(lesson.DraftConfig ?? lesson.Config);
        draft.title = request.DrillName; draft.explanation = request.Description; draft.required = request.Target;
        Basic(draft);
        if (request.TechniqueId != link.Drill.Techniqueid) throw Bad("Hãy đổi mẫu kỹ thuật trong cấu hình bài học.");
        lesson.DraftConfig = GameJson.Write(draft); lesson.ContentVersion++;
        await unit.SaveChangesAsync(); await tx.CommitAsync(); return link.Drillid;
    }

    public async Task<AdminLessonContent> Save(int lessonId, SaveLessonContent request)
    {
        await using var tx = await unit.Game.BeginTransactionAsync();
        var lesson = await unit.Game.ContentLesson(lessonId) ?? throw Missing();
        Expected(lesson, request.ExpectedVersion);
        if (request.Config.id != lesson.Code) throw Bad("Không được thay mã bài học đã tạo.");
        Basic(request.Config);
        lesson.DraftConfig = GameJson.Write(request.Config); lesson.SortOrder = request.Order; lesson.ContentVersion++;
        await unit.SaveChangesAsync(); await tx.CommitAsync(); return View(lesson);
    }

    public async Task<AdminLessonContent> Publish(int lessonId, ContentVersionRequest request, bool publish)
    {
        await using var tx = await unit.Game.BeginTransactionAsync();
        await unit.Game.Lock("content-publish");
        var lesson = await unit.Game.ContentLesson(lessonId) ?? throw Missing();
        Expected(lesson, request.ExpectedVersion);
        if (publish)
        {
            var draft = GameJson.Read<TrainingDrill>(lesson.DraftConfig ?? lesson.Config);
            Basic(draft);
            var published = (await unit.Game.ContentLessons()).Where(l => l.PublishStatus == ContentPublishStatus.Published && l.Lessonid != lessonId)
                .Select(l => GameJson.Read<TrainingDrill>(l.Config)).Append(draft).ToArray();
            ValidateGraph(published);
            var link = await unit.Game.ContentDrill(lessonId) ?? throw Bad("Bài học chưa có drill liên kết.");
            int technique = await unit.Game.TechniqueId(draft.skill.ToString()) ?? throw Bad("Kỹ thuật chưa được hỗ trợ.");
            var reference=Guid.TryParse(draft.referenceId,out var referenceId)?await unit.Game.Reference(referenceId):
                string.IsNullOrEmpty(draft.referenceId)?await unit.Game.LatestReference(technique):null;
            if(reference==null || reference.TechniqueId!=technique)throw Bad("Phiên bản dữ liệu tham chiếu không khớp kỹ thuật.");
            draft.referenceId=reference.ReferenceId.ToString("D");draft.reference=GameJson.Read<TrainingReferenceData>(reference.Data);
            lesson.Config = GameJson.Write(draft); lesson.Lessonname = draft.title; lesson.Description = draft.explanation;
            lesson.Type = draft.skill.ToString(); lesson.Target = draft.required; lesson.Reward = GameJson.Write(new RewardBundle(draft.reward, 0, draft.xp));
            link.Target = draft.required; link.Drill.Techniqueid = technique; link.Drill.Drillname = draft.title; link.Drill.Description = draft.explanation;
            lesson.PublishStatus = ContentPublishStatus.Published;
        }
        else
        {
            var dependents = (await unit.Game.ContentLessons()).Where(l => l.PublishStatus == ContentPublishStatus.Published && l.Lessonid != lessonId)
                .Select(l => GameJson.Read<TrainingDrill>(l.Config));
            if (dependents.Any(d => d.prerequisites.Contains(lesson.Code))) throw Bad("Hãy gỡ các bài đang phụ thuộc trước.");
            if (!dependents.Any()) throw Bad("Phải giữ ít nhất một bài học xuất bản.");
            lesson.PublishStatus = ContentPublishStatus.Unpublished;
        }
        lesson.ContentVersion++;
        await unit.SaveChangesAsync(); await tx.CommitAsync(); return View(lesson);
    }

    static void Expected(Lesson lesson, long version)
    {
        if (lesson.ContentVersion != version) throw new AppException("Nội dung đã thay đổi. Hãy tải lại trước khi lưu.", HttpStatusCode.Conflict);
    }
    public static void Basic(TrainingDrill d)
    {
        if (string.IsNullOrWhiteSpace(d.id) || string.IsNullOrWhiteSpace(d.title) || d.title.Length > 254 || string.IsNullOrWhiteSpace(d.explanation)
            || d.explanation.Length > 254 || d.coaching == null || d.coaching.Length > 2000 || !Enum.IsDefined(d.skill)
            || d.tier is < 0 or > 3 || d.trials is < 1 or > 200 || d.required < 1 || d.required > d.trials || d.reward is < 0 or > 100000
            || d.xp is < 0 or > 10000 || d.requiredLevel is < 1 or > 1000 || !float.IsFinite(d.targetRadius) || d.targetRadius is < .3f or > 2f
            || !float.IsFinite(d.targetDepth) || d.targetDepth < 0 || d.targetDepth > 6.4f || d.masks == null || d.masks.Length is < 1 or > 24
            || d.prerequisites == null || d.prerequisites.Length > 30 || d.prerequisites.Distinct().Count() != d.prerequisites.Length)
            throw Bad("Cấu hình bài học không hợp lệ. Hãy kiểm tra mục tiêu, số lượt, phần thưởng và vùng đích.");
        int[] masks = d.skill switch { TrainingSkill.Serve or TrainingSkill.Drive => [1,9,17], TrainingSkill.Dink => [2,10,18],
            TrainingSkill.Volley => [4,12,20], TrainingSkill.Drop or TrainingSkill.Approach => [3], TrainingSkill.Reset => [6],
            TrainingSkill.Smash => [5], TrainingSkill.Footwork => [1,17], TrainingSkill.DoublesCover => [4,20],
            TrainingSkill.DoublesTransition => [3,2,4], _ => [1,2,4,9,17,10,18,12,20] };
        if (d.masks.Any(m => !masks.Contains(m))) throw Bad("Tổ hợp điều khiển không khớp mẫu kỹ thuật.");
        if (d.alternateTargets && d.skill != TrainingSkill.Serve) throw Bad("Luân phiên ô giao chỉ áp dụng cho bài giao bóng.");
    }
    public static void ValidateGraph(TrainingDrill[] lessons)
    {
        var all = lessons.ToDictionary(d => d.id); var visiting = new HashSet<string>(); var visited = new HashSet<string>();
        void Visit(string id)
        {
            if (visited.Contains(id)) return;
            if (!all.ContainsKey(id)) throw Bad("Bài điều kiện chưa được xuất bản.");
            if (!visiting.Add(id)) throw Bad("Điều kiện mở khóa tạo thành vòng lặp.");
            foreach (var parent in all[id].prerequisites) Visit(parent);
            visiting.Remove(id); visited.Add(id);
        }
        foreach (var id in all.Keys) Visit(id);
    }
}
