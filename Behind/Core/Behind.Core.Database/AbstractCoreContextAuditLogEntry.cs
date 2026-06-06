using Behind.Common;
using Behind.Common.Database;
using static Behind.Common.FunctionalExtensions;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public partial class AbstractCoreContext : IRepository<AuditLogEntry, Guid>
{
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    
    public Guid GetKey(AuditLogEntry entity) => Entry(entity).Property<Guid>("Id").CurrentValue;

    async Task<Option<AuditLogEntry>> IRepository<AuditLogEntry, Guid>.TryFindAsync(
        Guid key,
        CancellationToken cancellationToken
    )
    {
        var query = from audit in AuditLogEntries
            where EF.Property<Guid>(audit, "Id") == key
            join u in Users
                on EF.Property<Guid?>(audit, "UserId") equals EF.Property<Guid?>(u, "Id") into userGroup
            from dbUser in userGroup.DefaultIfEmpty()
            join b in Users
                on EF.Property<Guid?>(audit, "BehalfOfId") equals EF.Property<Guid?>(b, "Id") into
                behalfOfGroup
            from dbBehalfOf in behalfOfGroup.DefaultIfEmpty()
            select new
            {
                Audit = audit,
                UserId = EF.Property<Guid?>(audit, "UserId"),
                dbUser.DisplayName,
                BehalfOfId = EF.Property<Guid?>(audit, "BehalfOfId"),
                BehalfOfDisplayName = dbBehalfOf.DisplayName,
                PrimaryEmails = Emails
                    .Where(e => EF.Property<Guid>(e, "UserId") == EF.Property<Guid?>(audit, "UserId"))
                    .Select(e => e.Value)
                    .ToList(),
                BehalfOfEmails = Emails
                    .Where(e => EF.Property<Guid>(e, "UserId") == EF.Property<Guid?>(audit, "BehalfOfId"))
                    .Select(e => e.Value)
                    .ToList()
            };

        var result = await query.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        if (result is null)
        {
            return None;
        }

        User user = result.UserId.HasValue
            ? result.UserId == DomainConstants.SystemUserId
                ? new SystemUser(None)
                : new HumanUser(
                    result.DisplayName,
                    result.PrimaryEmails.Select(val => new Email(val)),
                    None
                )
            : new GuestUser(None);

        Option<User> behalfOf = result.BehalfOfId.HasValue
            ? new HumanUser(
                result.BehalfOfDisplayName,
                result.BehalfOfEmails.Select(val => new Email(val)),
                None
            )
            : None;

        return Some(result.Audit with { User = user, BehalfOf = behalfOf });
    }
    async Task IRepository<AuditLogEntry, Guid>.
        AddAsync(AuditLogEntry aggregate, CancellationToken cancellationToken) =>
        await AuditLogEntries.AddAsync(aggregate, cancellationToken);

    void IRepository<AuditLogEntry, Guid>.Update(AuditLogEntry aggregate) =>
        AuditLogEntries.ToImmutableEntityRepository(this).UpdateImmutable([GetKey(aggregate)], aggregate);

    void IRepository<AuditLogEntry, Guid>.Update(Guid key, AuditLogEntry aggregate) =>
        AuditLogEntries.ToImmutableEntityRepository(this).UpdateImmutable([key], aggregate);
    
    void IRepository<AuditLogEntry, Guid>.Delete(AuditLogEntry aggregate) =>
        AuditLogEntries.ToImmutableEntityRepository(this).RemoveImmutable(aggregate);
}