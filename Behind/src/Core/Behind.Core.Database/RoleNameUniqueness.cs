using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public static class RoleNameUniqueness
{
    public const string IndexName = "IX_Roles_Name";

    public static bool IsConflict(Exception exception) => exception switch
    {
        DbUpdateException { InnerException: { } inner } => IsConflict(inner),
        DbException { SqlState: "23505" } databaseException
            when databaseException.GetType().GetProperty("ConstraintName")?.GetValue(databaseException)
                 is string name && name == IndexName => true,
        DbException databaseException
            when databaseException.GetType().GetProperty("SqliteExtendedErrorCode")?.GetValue(databaseException)
                 is 2067 && databaseException.Message.Contains("UNIQUE constraint failed: Roles.Name", StringComparison.Ordinal) => true,
        _ => false
    };
}
