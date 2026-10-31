using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Repositories.Context;

static class SchemaMappingChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, Action<bool, string> check)
    {
        await using var db = new PickleVerseContext(options);
        var columns = await db.Database.SqlQueryRaw<SchemaColumn>("""
            SELECT table_name AS "Table", column_name AS "Column"
            FROM information_schema.columns WHERE table_schema='public'
            """).ToListAsync();
        var tables = db.Model.GetEntityTypes().Select(e => e.GetTableName()).Where(t => t != null).ToHashSet();
        check(tables.SetEquals(columns.Select(c => c.Table)), "EF Core maps exactly the tables created by Database.sql");
        foreach (var entity in db.Model.GetEntityTypes())
        {
            string table = entity.GetTableName()!;
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            var model = entity.GetProperties().Select(p => p.GetColumnName(store)).ToHashSet();
            var actual = columns.Where(c => c.Table == table).Select(c => c.Column).ToHashSet();
            check(model.SetEquals(actual), "EF columns match Database.sql: " + table +
                (model.SetEquals(actual) ? "" : "; missing=" + string.Join(',', actual.Except(model)) + "; extra=" + string.Join(',', model.Except(actual))));
        }
    }
    sealed class SchemaColumn
    {
        public string Table { get; set; } = "";
        public string Column { get; set; } = "";
    }
}
