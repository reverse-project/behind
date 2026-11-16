using Behind.Common;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public static class AuditLogFilterExtensions
{
    extension(Option<string> search)
    {
        public Filter<AuditLogEntry> ToAuditLogFilter() =>
            search.Match<Filter<AuditLogEntry>>(
                () => new EmptyFilter<AuditLogEntry>(),
                term =>
                {
                    var lowered = term.ToLowerInvariant();

                    return new Filter<AuditLogEntry>(entry =>
                        entry.EntityName.ToLower().Contains(lowered) || entry.EntityKey.ToLower().Contains(lowered));
                });
    }
}
