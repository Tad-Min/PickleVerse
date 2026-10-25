using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Repositories.Context;

namespace Services.Game;

public sealed class GameSql(PickleVerseContext context)
{
    public static NpgsqlParameter P(string name, object? value) => new(name, value ?? DBNull.Value);
    NpgsqlCommand Command(string sql, NpgsqlParameter[] parameters)
    {
        var command = new NpgsqlCommand(sql, (NpgsqlConnection)context.Database.GetDbConnection(),
            (NpgsqlTransaction?)context.Database.CurrentTransaction?.GetDbTransaction());
        command.Parameters.AddRange(parameters);
        return command;
    }
    public async Task<int> Run(string sql, params NpgsqlParameter[] parameters)
    {
        if (context.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
            await context.Database.OpenConnectionAsync();
        await using var command = Command(sql, parameters);
        return await command.ExecuteNonQueryAsync();
    }
    public async Task<List<T>> Query<T>(string sql, Func<DbDataReader, T> map, params NpgsqlParameter[] parameters)
    {
        if (context.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
            await context.Database.OpenConnectionAsync();
        await using var command = Command(sql, parameters);
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<T>();
        while (await reader.ReadAsync()) values.Add(map(reader));
        return values;
    }
    public async Task<T?> One<T>(string sql, Func<DbDataReader, T> map, params NpgsqlParameter[] parameters) =>
        (await Query(sql, map, parameters)).SingleOrDefault();
}
