using Microsoft.EntityFrameworkCore;
using Npgsql;
using StackExchange.Redis;

namespace Repositories.Data;

public static class PersistenceErrors
{
    public static bool IsCacheUnavailable(Exception error) => error is RedisException;
    public static bool IsUnavailable(Exception error) => error is RedisException or NpgsqlException { IsTransient: true }
        || error.InnerException != null && IsUnavailable(error.InnerException);
    public static bool IsUniqueViolation(Exception error, string constraint) => error is DbUpdateException
        { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres } && postgres.ConstraintName == constraint;
    public static string Diagnostic(Exception error) => error is PostgresException postgres
        ? $"PostgresException:{postgres.SqlState}:{postgres.ConstraintName}" : error.GetType().Name;
}
