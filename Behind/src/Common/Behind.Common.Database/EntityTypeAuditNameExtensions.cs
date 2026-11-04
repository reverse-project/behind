using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Behind.Common.Database;

public static class EntityTypeAuditNameExtensions
{
    extension(IReadOnlyEntityType entityType)
    {
        public string AuditEntityName =>
            entityType.HasSharedClrType
                ? entityType.Name
                : entityType.GetRootType().ClrType.Name;
    }
}
