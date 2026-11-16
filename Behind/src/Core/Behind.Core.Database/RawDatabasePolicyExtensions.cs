using Behind.Common.Database.Raw;
using Behind.Common.Modules;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public static class RawDatabasePolicyExtensions
{
    private static readonly IReadOnlySet<string> ReadOnlyEntities = new HashSet<string>
    {
        nameof(AuditLogEntry)
    };

    extension(AbstractCoreContext context)
    {
        public bool IsReadOnly(RawTable table) =>
            ReadOnlyEntities.Contains(table.EntityName) || context.Modules.ReadOnlyEntities.Contains(table.EntityName);
    }
}
