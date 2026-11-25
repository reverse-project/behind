using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Behind.Common.Database;

public static class UniqueConstraintViolation
{
    public static bool Matches(Exception exception, string constraintName, string sqliteColumns) => exception switch
    {
        DbUpdateException { InnerException: { } inner } => Matches(inner, constraintName, sqliteColumns),
        DbException { SqlState: "23505" } databaseException
            when databaseException.GetType().GetProperty("ConstraintName")?.GetValue(databaseException)
                 is string name && name == constraintName => true,
        DbException databaseException
            when databaseException.GetType().GetProperty("SqliteExtendedErrorCode")?.GetValue(databaseException)
                 is 1555 or 2067 && databaseException.Message.Contains($"UNIQUE constraint failed: {sqliteColumns}", StringComparison.Ordinal) => true,
        _ => false
    };
}
