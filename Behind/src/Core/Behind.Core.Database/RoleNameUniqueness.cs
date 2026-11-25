using Behind.Common.Database;

namespace Behind.Core.Database;

public static class RoleNameUniqueness
{
    public const string IndexName = "IX_Roles_Name";

    public static bool IsConflict(Exception exception) =>
        UniqueConstraintViolation.Matches(exception, IndexName, "Roles.Name");
}
