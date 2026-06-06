using Behind.Common;
using Behind.Core.Domain;
using static Behind.Common.FunctionalExtensions;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Queries;

public sealed class AuditLogEntryQueries(AbstractCoreContext context) : IAuditLogEntryQueries
{
    public async Task<Option<PagedResult<AuditLogEntry>>> GetAuditLog(IAuditLogEntryQueries.RequestAuditLog request,
        CancellationToken cancellationToken = default)
    {
        DateTime fromDate = DateTime.SpecifyKind(request.FromDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        DateTime toDate =
            DateTime.SpecifyKind(request.ToDate.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        var baseQuery = request.Filter is EmptyFilter<AuditLogEntry>
            ? context.AuditLogEntries.Where(x => x.Timestamp >= fromDate && x.Timestamp < toDate)
            : request.Filter.Apply(
                context.AuditLogEntries.Where(x => x.Timestamp >= fromDate && x.Timestamp < toDate)
            );

        var total = await baseQuery.CountAsync(cancellationToken);

        var projectionQuery = (from audit in baseQuery
            join user in context.Users
                on EF.Property<Guid?>(audit, "UserId")
                equals EF.Property<Guid?>(user, "Id")
                into userGroups
            from dbUser in userGroups.DefaultIfEmpty()
            join behalfOfUser in context.Users
                on EF.Property<Guid?>(audit, "BehalfOfId")
                equals EF.Property<Guid?>(behalfOfUser, "Id")
                into behalfOfGroups
            from dbBehalfOf in behalfOfGroups.DefaultIfEmpty()
            select new
            {
                Audit = audit,
                UserId = EF.Property<Guid?>(audit, "UserId"),
                dbUser.DisplayName,
                BehalfOfId = EF.Property<Guid?>(audit, "BehalfOfId"),
                BehalfOfDisplayName = dbBehalfOf.DisplayName,
                PrimaryEmails = context.Emails
                    .Where(e => EF.Property<Guid?>(e, "UserId") == EF.Property<Guid?>(audit, "UserId"))
                    .Select(e => e.Value).ToList(),
                BehalfOfEmails = context.Emails
                    .Where(e => EF.Property<Guid?>(e, "UserId") == EF.Property<Guid?>(audit, "BehalfOfId"))
                    .Select(e => e.Value).ToList()
            }).AsNoTracking();


        if (!request.OrderByCollection.Collection.Any())
        {
            request = request with { OrderByCollection = new OrderByCollection([new OrderBy("audit.Timestamp")]) };
        }

        var items = await request.Flipper.Apply(
            request.OrderByCollection.Apply(projectionQuery)
        ).ToListAsync(cancellationToken);

        var populatedItems = items.Select(item =>
        {
            var user = item.UserId.HasValue
                ? item.UserId == DomainConstants.SystemUserId
                    ? (User)new SystemUser(None)
                    : new HumanUser(item.DisplayName, item.PrimaryEmails.Select(v => new Email(v)).ToList(), None)
                : new GuestUser(None);

            var behalfOf = item.BehalfOfId.HasValue
                ? Some((User)new HumanUser(item.BehalfOfDisplayName,
                    item.BehalfOfEmails.Select(v => new Email(v)).ToList(), None))
                : None;

            return item.Audit with { User = user, BehalfOf = behalfOf };
        });

        return total > 0
            ? Some(
                new PagedResult<AuditLogEntry>(
                    total,
                    request.Flipper.Index,
                    request.Flipper.PageCount(total),
                    request.Flipper.Size,
                    Some(populatedItems)))
            : None;
    }
}