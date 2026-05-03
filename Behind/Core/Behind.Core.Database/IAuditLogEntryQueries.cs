using Behind.Common;
using Behind.Core.Domain;
using LaYumba.Functional;

namespace Behind.Core.Database;

public interface IAuditLogEntryQueries
{
    public sealed record RequestAuditLog(
        DateOnly FromDate,
        DateOnly ToDate,
        Filter<AuditLogEntry> Filter,
        Flipper Flipper,
        OrderByCollection OrderByCollection
    )
    {
        public static RequestAuditLog Create(DateOnly fromDate, DateOnly toDate,
            Filter<AuditLogEntry> filter, Flipper flipper, string orderBy) =>
            new(fromDate, toDate, filter, flipper, orderBy.Create());
    }
    
    Task<Option<PagedResult<AuditLogEntry>>> GetAuditLog(RequestAuditLog request, CancellationToken cancellationToken = default);
}