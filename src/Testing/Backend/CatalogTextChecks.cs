using Microsoft.EntityFrameworkCore;
using Repositories.Context;
using Services.Implementations;

static class CatalogTextChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, string repairPath, Action<bool, string> check)
    {
        await using var context = new PickleVerseContext(options);
        var sql = new PostgresCommands(context);var sqlData = new UnitOfWork(context);
        // Keep administrator edits to valid text, thresholds and rewards while repairing damaged text.
        await sql.Run("""
            UPDATE lessons SET config=jsonb_set(jsonb_set(config,'{reward}','987'),'{explanation}','"Bài tập riêng đã chỉnh"') WHERE code='drive';
            UPDATE missions SET target=321,config=jsonb_set(config,'{target}','321') WHERE code='D.Serve10';
            """);
        async Task<string> Snapshot() => (await sql.Query("""
            SELECT jsonb_build_object(
                'lessons',(SELECT jsonb_agg(to_jsonb(t) ORDER BY lessonid) FROM lessons t),
                'drills',(SELECT jsonb_agg(to_jsonb(t) ORDER BY drillid) FROM drills t),
                'techniques',(SELECT jsonb_agg(to_jsonb(t) ORDER BY techniqueid) FROM techniques t),
                'missions',(SELECT jsonb_agg(to_jsonb(t) ORDER BY missionid) FROM missions t),
                'achievements',(SELECT jsonb_agg(to_jsonb(t) ORDER BY achievementid) FROM achievements t),
                'cosmetics',(SELECT jsonb_agg(to_jsonb(t) ORDER BY cosmeticid) FROM cosmetics t),
                'players',(SELECT jsonb_agg(to_jsonb(t) ORDER BY userid) FROM playerprofiles t),
                'users',(SELECT jsonb_agg(to_jsonb(t) ORDER BY userid) FROM users t))::text
            """, r => r.GetString(0))).Single();
        string before = await Snapshot();
        await sql.Run("""
            UPDATE lessons SET lessonname='??',description='??',config=config||'{"title":"??","coaching":"??"}'::jsonb;
            UPDATE lessons SET config=config||'{"explanation":"??"}'::jsonb WHERE code<>'drive';
            UPDATE drills SET drillname='??',description='??';
            UPDATE techniques SET description='??';
            UPDATE missions SET missionname='??',description='??',config=config||'{"title":"??"}'::jsonb;
            UPDATE achievements SET achievementname='??',description='??';
            UPDATE cosmetics SET cosmeticname='??',description='??',config=config||'{"title":"??","description":"??","collection":"??"}'::jsonb;
            """);
        check(before != await Snapshot(), "fixture reproduces damaged database catalog text");
        string repair = string.Join('\n', (await File.ReadAllLinesAsync(repairPath)).Where(line => !line.StartsWith('\\')));
        await sql.Run(repair);
        check(before == await Snapshot(), "repair restores all six catalogs while preserving valid custom text, gameplay config and player data");
        await sql.Run(repair);
        check(before == await Snapshot(), "catalog text repair can be run again without changing healthy data");
        var catalog = await new GameCatalogService(sqlData, TestSettings.Load()).Load();
        check(catalog.lessons.Single(l => l.id == "serve").title == "Giao bóng đúng ô",
            "API catalog reads repaired Vietnamese text from PostgreSQL");
    }
}
