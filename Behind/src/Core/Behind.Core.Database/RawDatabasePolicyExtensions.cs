using Behind.Common.Database.Raw;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public static class RawDatabasePolicyExtensions
{
    private static readonly IReadOnlySet<string> ReadOnlyEntities = new HashSet<string>
    {
        nameof(AuditLogEntry)
    };

    extension(RawTable table)
    {
        public bool IsReadOnly => ReadOnlyEntities.Contains(table.EntityName);
    }
}
