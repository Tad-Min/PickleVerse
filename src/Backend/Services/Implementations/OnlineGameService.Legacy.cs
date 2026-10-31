using Entities.DTOs;
using PickleVerse.Progression;

namespace Services.Implementations;

public sealed partial class OnlineGameService
{
    public async Task<GameSnapshot> LegacyClaim(Guid id, int definition, bool achievement, string requestId)
    {
        var code = await _unitOfWork.Game.DefinitionCode(definition, achievement) ?? throw Bad("Không tìm thấy mục nhận thưởng.");
        return await Claim(id, new GameCommand { RequestId = requestId, Code = code });
    }
    public async Task<GameSnapshot> LegacyCosmetic(Guid id, int cosmetic, bool purchase, bool equip, string requestId)
    {
        var item = await _unitOfWork.Game.Cosmetic(cosmetic) ?? throw Bad("Không tìm thấy vật phẩm.");
        var command = new GameCommand { RequestId = requestId, Code = purchase || equip ? item.code : "paddle.default", Currency = item.currency == "ELITE TOKEN" ? "EliteToken" : "Tokenpad" };
        return purchase ? await Purchase(id, command) : await Equip(id, command);
    }
    public async Task<List<LessonDto>> LegacyLessons(Guid id)
    {
        var state = await Read(id);
        var ids = await _unitOfWork.Game.LessonLinks();
        return state.catalog.lessons.Select(l =>
        {
            var row = ids.Single(i => i.code == l.id);
            var record = state.player.training.lessons.SingleOrDefault(r => r.id == l.id);
            return new LessonDto
            {
                LessonId = row.lesson,
                LessonName = l.title,
                Description = l.explanation,
                Status = record?.bestStars > 0 ? "COMPLETED" : TrainingEngine.Unlocked(state.player.training, l) ? "UNLOCKED" : "LOCKED",
                Drills = [new DrillDto{DrillId = row.drill, TechniqueId = row.technique, TechniqueName = l.skill.ToString(), DrillName = l.title,
                    Description = l.explanation, Target = l.required, BestScore = record?.bestStars??0, IsClaimedReward = record?.bestStars>0}]
            };
        }).ToList();
    }
}
