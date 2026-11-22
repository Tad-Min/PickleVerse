using System.Net;
using System.Text.RegularExpressions;
using Entities.DTOs;
using Entities.Exceptions;
using Entities.Models;
using PickleVerse.Progression;
using Repositories.UnitOfWork;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>
/// Quản trị nội dung luyện tập. Bài học là nhóm; mỗi bài học chứa nhiều bài tập (Drill).
/// Bài tập là đơn vị người chơi luyện và mang toàn bộ cấu hình (TrainingDrill).
/// Xuất bản theo từng bài học: mọi bài tập đang soạn của bài học được áp dụng cùng lúc.
/// </summary>
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
    static AppException MissingDrill() => new("Không tìm thấy bài tập trong bài học.", HttpStatusCode.NotFound);

    static AdminDrillContent DrillView(Lessondrill link)
    {
        var drill = link.Drill;
        var draft = GameJson.Read<TrainingDrill>(drill.DraftConfig ?? drill.Config!);
        var published = drill.Config == null ? null : GameJson.Read<TrainingDrill>(drill.Config);
        return new AdminDrillContent { DrillId = drill.Drillid, Code = drill.Code!, Order = link.SortOrder, Draft = draft, Published = published,
            HasChanges = published == null || GameJson.Write(draft) != GameJson.Write(published) };
    }
    static AdminLessonContent View(Lesson lesson, IEnumerable<Lessondrill> links) => new() { LessonId = lesson.Lessonid, Code = lesson.Code!,
        Name = lesson.Lessonname, Description = lesson.Description, Status = lesson.PublishStatus, Version = lesson.ContentVersion, Order = lesson.SortOrder,
        Drills = links.OrderBy(l => l.SortOrder).ThenBy(l => l.Drillid).Select(DrillView).ToList() };

    public async Task<List<AdminLessonContent>> Lessons() => (await unit.Game.ContentLessons()).Select(l => View(l, l.Lessondrills)).ToList();
    public async Task<AdminLessonContent> Get(int lessonId) => (await Lessons()).SingleOrDefault(l => l.LessonId == lessonId) ?? throw Missing();

    public async Task<int> CreateLesson(CreateLessonRequest request)
    {
        if (!Regex.IsMatch(request.Code ?? "", "^[a-z][a-z0-9-]{1,49}$")) throw Bad("Mã bài học chỉ gồm chữ thường, số và dấu gạch ngang.");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 254 || (request.Description ?? "").Length > 254) throw Bad("Tên hoặc mô tả bài học không hợp lệ.");
        await using var tx = await unit.Game.BeginTransactionAsync();
        await unit.Game.Lock("content-code:" + request.Code);
        if (await unit.Game.LessonCodeExists(request.Code!)) throw new AppException("Mã bài học đã tồn tại.", HttpStatusCode.Conflict);
        var lesson = new Lesson { Code = request.Code, Lessonname = request.Name.Trim(), Description = request.Description?.Trim() ?? "",
            SortOrder = request.Order, PublishStatus = ContentPublishStatus.Draft, ContentVersion = 1 };
        unit.Game.AddLesson(lesson);
        await unit.SaveChangesAsync(); await tx.CommitAsync(); return lesson.Lessonid;
    }

    public async Task<AdminLessonContent> SaveLesson(int lessonId, SaveLessonRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 254 || (request.Description ?? "").Length > 254) throw Bad("Tên hoặc mô tả bài học không hợp lệ.");
        await using var tx = await unit.Game.BeginTransactionAsync();
        var lesson = await unit.Game.ContentLesson(lessonId) ?? throw Missing();
        Expected(lesson, request.ExpectedVersion);
        lesson.Lessonname = request.Name.Trim(); lesson.Description = request.Description?.Trim() ?? ""; lesson.SortOrder = request.Order; lesson.ContentVersion++;
        await unit.SaveChangesAsync(); await tx.CommitAsync();
        return View(lesson, await unit.Game.ContentDrills(lessonId));
    }

    public async Task<AdminLessonContent> AddDrill(int lessonId, SaveDrillRequest request)
    {
        var config = request.Config;
        if (!Regex.IsMatch(config?.id ?? "", "^[a-z][a-z0-9-]{1,49}$")) throw Bad("Mã bài tập chỉ gồm chữ thường, số và dấu gạch ngang.");
        await using var tx = await unit.Game.BeginTransactionAsync();
        await unit.Game.Lock("content-code:" + config!.id);
        var lesson = await unit.Game.ContentLesson(lessonId) ?? throw Missing();
        Expected(lesson, request.ExpectedVersion);
        if (await unit.Game.DrillCodeExists(config.id)) throw new AppException("Mã bài tập đã tồn tại.", HttpStatusCode.Conflict);
        config.coaching ??= config.explanation;
        Basic(config);
        int technique = await unit.Game.TechniqueId(config.skill.ToString()) ?? throw Bad("Kỹ thuật chưa được runtime hỗ trợ.");
        var drill = new Drill { Code = config.id, Techniqueid = technique, Drillname = config.title, Description = config.explanation,
            DraftConfig = GameJson.Write(config), Config = null };
        unit.Game.AddDrill(lesson, drill, config.required, request.Order);
        lesson.ContentVersion++;
        await unit.SaveChangesAsync(); await tx.CommitAsync();
        return View(lesson, await unit.Game.ContentDrills(lessonId));
    }

    public async Task<AdminLessonContent> SaveDrill(int lessonId, int drillId, SaveDrillRequest request)
    {
        await using var tx = await unit.Game.BeginTransactionAsync();
        var lesson = await unit.Game.ContentLesson(lessonId) ?? throw Missing();
        Expected(lesson, request.ExpectedVersion);
        var links = await unit.Game.ContentDrills(lessonId);
        var link = links.SingleOrDefault(l => l.Drillid == drillId) ?? throw MissingDrill();
        if (request.Config.id != link.Drill.Code) throw Bad("Không được thay mã bài tập đã tạo.");
        Basic(request.Config);
        link.Drill.DraftConfig = GameJson.Write(request.Config); link.SortOrder = request.Order; lesson.ContentVersion++;
        await unit.SaveChangesAsync(); await tx.CommitAsync();
        return View(lesson, links);
    }

    public async Task<AdminLessonContent> DeleteDrill(int lessonId, int drillId, long expectedVersion)
    {
        await using var tx = await unit.Game.BeginTransactionAsync();
        var lesson = await unit.Game.ContentLesson(lessonId) ?? throw Missing();
        Expected(lesson, expectedVersion);
        var links = await unit.Game.ContentDrills(lessonId);
        var link = links.SingleOrDefault(l => l.Drillid == drillId) ?? throw MissingDrill();
        if (lesson.PublishStatus == ContentPublishStatus.Published && link.Drill.Config != null)
            throw Bad("Hãy gỡ xuất bản bài học trước khi xóa bài tập đã xuất bản.");
        if (await unit.Game.DrillHasPlayerData(drillId)) throw Bad("Bài tập đã có dữ liệu luyện tập của người chơi nên không thể xóa.");
        var code = link.Drill.Code!;
        var others = (await unit.Game.ContentLessons()).SelectMany(l => l.Lessondrills).Where(l => l.Drillid != drillId);
        if (others.Any(l => GameJson.Read<TrainingDrill>(l.Drill.DraftConfig ?? l.Drill.Config!).prerequisites.Contains(code)
            || l.Drill.Config != null && GameJson.Read<TrainingDrill>(l.Drill.Config).prerequisites.Contains(code)))
            throw Bad("Có bài tập khác đang dùng bài này làm điều kiện mở khóa.");
        unit.Game.RemoveDrill(link);
        lesson.ContentVersion++;
        await unit.SaveChangesAsync(); await tx.CommitAsync();
        return View(lesson, links.Where(l => l.Drillid != drillId));
    }

    public async Task<AdminLessonContent> Publish(int lessonId, ContentVersionRequest request, bool publish)
    {
        await using var tx = await unit.Game.BeginTransactionAsync();
        await unit.Game.Lock("content-publish");
        var lesson = await unit.Game.ContentLesson(lessonId) ?? throw Missing();
        Expected(lesson, request.ExpectedVersion);
        var links = await unit.Game.ContentDrills(lessonId);
        if (publish)
        {
            if (links.Count == 0) throw Bad("Bài học cần có ít nhất một bài tập trước khi xuất bản.");
            var drafts = new List<(Lessondrill link, TrainingDrill draft, int technique)>();
            foreach (var link in links)
            {
                var draft = GameJson.Read<TrainingDrill>(link.Drill.DraftConfig ?? link.Drill.Config!);
                Basic(draft);
                int technique = await unit.Game.TechniqueId(draft.skill.ToString()) ?? throw Bad("Kỹ thuật chưa được hỗ trợ.");
                var reference = Guid.TryParse(draft.referenceId, out var referenceId) ? await unit.Game.Reference(referenceId) :
                    string.IsNullOrEmpty(draft.referenceId) ? await unit.Game.LatestReference(technique) : null;
                if (reference == null || reference.TechniqueId != technique) throw Bad("Phiên bản dữ liệu tham chiếu không khớp kỹ thuật.");
                draft.referenceId = reference.ReferenceId.ToString("D"); draft.reference = GameJson.Read<TrainingReferenceData>(reference.Data);
                drafts.Add((link, draft, technique));
            }
            var others = (await unit.Game.PublishedDrillConfigs(lessonId)).Select(c => GameJson.Read<TrainingDrill>(c));
            ValidateGraph(others.Concat(drafts.Select(d => d.draft)).ToArray());
            foreach (var (link, draft, technique) in drafts)
            {
                link.Drill.Config = GameJson.Write(draft); link.Drill.DraftConfig = link.Drill.Config;
                link.Drill.Techniqueid = technique; link.Drill.Drillname = draft.title; link.Drill.Description = draft.explanation;
                link.Target = draft.required;
            }
            lesson.PublishStatus = ContentPublishStatus.Published;
        }
        else
        {
            var dependents = (await unit.Game.PublishedDrillConfigs(lessonId)).Select(c => GameJson.Read<TrainingDrill>(c)).ToList();
            var codes = links.Select(l => l.Drill.Code!).ToHashSet();
            if (dependents.Any(d => d.prerequisites.Any(codes.Contains))) throw Bad("Hãy gỡ các bài đang phụ thuộc trước.");
            if (dependents.Count == 0) throw Bad("Phải giữ ít nhất một bài học xuất bản.");
            lesson.PublishStatus = ContentPublishStatus.Unpublished;
        }
        lesson.ContentVersion++;
        await unit.SaveChangesAsync(); await tx.CommitAsync();
        return View(lesson, links);
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
            throw Bad("Cấu hình bài tập không hợp lệ. Hãy kiểm tra mục tiêu, số lượt, phần thưởng và vùng đích.");
        int[] masks = d.skill switch { TrainingSkill.Serve or TrainingSkill.Drive => [1,9,17], TrainingSkill.Dink => [2,10,18],
            TrainingSkill.Volley => [4,12,20], TrainingSkill.Drop or TrainingSkill.Approach => [3], TrainingSkill.Reset => [6],
            TrainingSkill.Smash => [5], TrainingSkill.Footwork => [1,17], TrainingSkill.DoublesCover => [4,20],
            TrainingSkill.DoublesTransition => [3,2,4], _ => [1,2,4,9,17,10,18,12,20] };
        if (d.masks.Any(m => !masks.Contains(m))) throw Bad("Tổ hợp điều khiển không khớp mẫu kỹ thuật.");
        if (d.alternateTargets && d.skill != TrainingSkill.Serve) throw Bad("Luân phiên ô giao chỉ áp dụng cho bài giao bóng.");
    }
    public static void ValidateGraph(TrainingDrill[] drills)
    {
        var all = drills.ToDictionary(d => d.id); var visiting = new HashSet<string>(); var visited = new HashSet<string>();
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
