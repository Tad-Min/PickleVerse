using System.Security.Cryptography;
using System.Text;
using PickleVerse.Progression;
using static Services.Game.GameSql;

namespace Services.Game;

public sealed class GameCatalogService(GameSql sql)
{
    public async Task<GameCatalog> Load()
    {
        var settings = await sql.One("SELECT value::text FROM gamesettings WHERE key='progression'",r=>r.GetString(0))
            ?? throw new InvalidOperationException("Game content seed is missing.");
        var catalog = GameJson.Read<GameCatalog>(settings);
        catalog.definitions.AddRange(await sql.Query("SELECT achievementcode,achievementname,type,target,reward::text FROM achievements ORDER BY achievementid",r=>
            new ProgressDefinition(r.GetString(0),r.GetString(1),ProgressScope.Achievement,Enum.Parse<ProgressMetric>(r.GetString(2)),r.GetInt32(3),GameJson.Read<RewardBundle>(r.GetString(4)))));
        catalog.definitions.AddRange(await sql.Query("SELECT config::text FROM missions WHERE code IS NOT NULL ORDER BY missionid",r=>GameJson.Read<ProgressDefinition>(r.GetString(0))));
        catalog.lessons=(await sql.Query("SELECT config::text FROM lessons WHERE code IS NOT NULL ORDER BY sortorder,lessonid",r=>GameJson.Read<TrainingDrill>(r.GetString(0)))).ToArray();
        catalog.cosmetics=(await sql.Query("SELECT config::text FROM cosmetics WHERE itemcode LIKE 'paddle.%' ORDER BY cosmeticid",r=>GameJson.Read<CosmeticOffer>(r.GetString(0)))).ToArray();
        TrainingCurriculum.Configure(catalog.lessons);
        catalog.version=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(GameJson.Write(catalog))))[..16].ToLowerInvariant();
        return catalog;
    }
}
