using Services.DTOs;
using PickleVerse.Progression;
using static Services.Game.GameSql;

namespace Services.Game;

public sealed partial class OnlineGameService
{
    public async Task<GameSnapshot> LegacyClaim(Guid id,int definition,bool achievement,string requestId)
    {
        string query=achievement?"SELECT achievementcode FROM achievements WHERE achievementid=@item":"SELECT code FROM missions WHERE missionid=@item";
        var code=await sql.One(query,r=>r.GetString(0),P("item",definition))??throw Bad("Không tìm thấy mục nhận thưởng.");
        return await Claim(id,new GameCommand{RequestId=requestId,Code=code});
    }
    public async Task<GameSnapshot> LegacyCosmetic(Guid id,int cosmetic,bool purchase,bool equip,string requestId)
    {
        var item=await sql.One("SELECT c.itemcode,u.currencyname FROM cosmetics c JOIN currencies u USING(currencyid) WHERE c.cosmeticid=@item",r=>new{code=r.GetString(0),currency=r.GetString(1)},P("item",cosmetic))??throw Bad("Không tìm thấy vật phẩm.");
        var command=new GameCommand{RequestId=requestId,Code=purchase||equip?item.code:"paddle.default",Currency=item.currency=="ELITE TOKEN"?"EliteToken":"Tokenpad"};
        return purchase?await Purchase(id,command):await Equip(id,command);
    }
    public async Task<List<LessonDto>> LegacyLessons(Guid id)
    {
        var state=await Read(id);
        var ids=await sql.Query("SELECT l.lessonid,l.code,d.drillid,d.techniqueid FROM lessons l JOIN drills d ON d.code=l.code WHERE l.code IS NOT NULL",r=>new{lesson=r.GetInt32(0),code=r.GetString(1),drill=r.GetInt32(2),technique=r.GetInt32(3)});
        return state.catalog.lessons.Select(l=>
        {
            var row=ids.Single(i=>i.code==l.id);
            var record=state.player.training.lessons.SingleOrDefault(r=>r.id==l.id);
            return new LessonDto{LessonId=row.lesson,LessonName=l.title,Description=l.explanation,
                Status=record?.bestStars>0?"COMPLETED":TrainingEngine.Unlocked(state.player.training,l)?"UNLOCKED":"LOCKED",
                Drills=[new DrillDto{DrillId=row.drill,TechniqueId=row.technique,TechniqueName=l.skill.ToString(),DrillName=l.title,
                    Description=l.explanation,Target=l.required,BestScore=record?.bestStars??0,IsClaimedReward=record?.bestStars>0}]};
        }).ToList();
    }
}
