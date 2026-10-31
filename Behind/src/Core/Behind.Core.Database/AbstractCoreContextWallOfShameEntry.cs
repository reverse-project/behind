using Behind.Common;
using Behind.Common.Database;
using static Behind.Common.FunctionalExtensions;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public partial class AbstractCoreContext : IRepository<WallOfShameEntry, Guid>
{
    public DbSet<WallOfShameEntry> WallOfShameEntries => Set<WallOfShameEntry>();
    
    public Guid GetKey(WallOfShameEntry entity) => Entry(entity).Property<Guid>("Id").CurrentValue;

    async Task<Option<WallOfShameEntry>> IRepository<WallOfShameEntry, Guid>.TryFindAsync(
        Guid key,
        CancellationToken cancellationToken
    )
    {
        var query = from entry in WallOfShameEntries
            where EF.Property<Guid>(entry, "Id") == key
            join u in Users
                on EF.Property<Guid>(entry, "UserId") equals EF.Property<Guid>(u, "Id") into userGroup
            from dbUser in userGroup.DefaultIfEmpty()
            join g in Users
                on EF.Property<Guid>(entry, "GivenById") equals EF.Property<Guid>(g, "Id") into givenByGroup
            from dbGivenBy in givenByGroup.DefaultIfEmpty()
            select new
            {
                Entry = entry,
                UserId = EF.Property<Guid>(entry, "UserId"),
                dbUser.DisplayName,
                GivenById = EF.Property<Guid>(entry, "GivenById"),
                GivenByDisplayName = dbGivenBy.DisplayName,
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

        if (result is null)
        {
            return None;
        }

        User user = result.UserId == DomainConstants.SystemUserId
            ? new SystemUser(None)
            : new HumanUser(result.DisplayName, result.UserEmails.Select(v => new Email(v)), None);

        User givenBy = result.GivenById == DomainConstants.SystemUserId
            ? new SystemUser(None)
            : new HumanUser(result.GivenByDisplayName, result.GivenByEmails.Select(v => new Email(v)), None);

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
        WallOfShameEntries.ToImmutableEntityRepository(this).UpdateImmutable([GetKey(aggregate)], aggregate);

    void IRepository<WallOfShameEntry, Guid>.Update(Guid key, WallOfShameEntry aggregate) =>
        WallOfShameEntries.ToImmutableEntityRepository(this).UpdateImmutable([key], aggregate);
    
    void IRepository<WallOfShameEntry, Guid>.Delete(WallOfShameEntry aggregate) =>
        WallOfShameEntries.ToImmutableEntityRepository(this).RemoveImmutable(aggregate);

    Task IRepository<WallOfShameEntry, Guid>.DeleteAsync(Guid key, CancellationToken cancellationToken) =>
        this.DeleteByKeyAsync<WallOfShameEntry>([key], cancellationToken);
}