using Behind.Common;
using Behind.Common.Database;
using static Behind.Common.FunctionalExtensions;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public partial class AbstractCoreContext : IRepository<WallOfShameEntry, Guid>
{
    public Guid GetKey(WallOfShameEntry entity) => Entry(entity).Property<Guid>("Id").CurrentValue;

    async Task<Option<WallOfShameEntry>> IRepository<WallOfShameEntry, Guid>.TryFindAsync(
        Guid key,
        CancellationToken cancellationToken
    )
    {
        var query = from entry in WallOfShameEntries
                    where EF.Property<Guid>(entry, "Id") == key
                    select new
                    {
                        Entry = entry,
                        UserId = EF.Property<Guid>(entry, "UserId"),
                        GivenById = EF.Property<Guid>(entry, "GivenById"),
                        ExpiresAt = EF.Property<DateTimeOffset?>(entry, "ExpiresAt"),
                        UserEmails = Emails
                            .Where(e => EF.Property<Guid>(e, "UserId") == EF.Property<Guid>(entry, "UserId"))
                            .Select(e => e.Value)
                            .ToList(),
                        GivenByEmails = Emails
                            .Where(e => EF.Property<Guid>(e, "UserId") == EF.Property<Guid>(entry, "GivenById"))
                            .Select(e => e.Value)
                            .ToList()
                    };

        var result = await query.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        if (result is null) return None;

        User user = result.UserId == DomainConstants.SystemUserId
            ? new SystemUser(None)
            : new HumanUser(result.UserEmails.Select(v => new Email(v)), None);

        User givenBy = result.GivenById == DomainConstants.SystemUserId
            ? new SystemUser(None)
            : new HumanUser(result.GivenByEmails.Select(v => new Email(v)), None);

        Option<DateTimeOffset> expires = result.ExpiresAt.HasValue
            ? Some(result.ExpiresAt.Value)
            : None;

        return Some(result.Entry
            .WithUser(user)
            .WithExpires(expires)
            .WithGivenBy(givenBy));
    }

    async Task IRepository<WallOfShameEntry, Guid>.AddAsync(
        WallOfShameEntry aggregate,
        CancellationToken cancellationToken
    ) =>
        await WallOfShameEntries.AddAsync(aggregate, cancellationToken);

    void IRepository<WallOfShameEntry, Guid>.Update(WallOfShameEntry aggregate) =>
        WallOfShameEntries.ToImmutableEntityRepository(this).UpdateImmutable(aggregate);

    void IRepository<WallOfShameEntry, Guid>.Delete(WallOfShameEntry aggregate) =>
        WallOfShameEntries.ToImmutableEntityRepository(this).RemoveImmutable(aggregate);
}
