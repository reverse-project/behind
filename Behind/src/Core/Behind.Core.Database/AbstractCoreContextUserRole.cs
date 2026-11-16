using Behind.Common;
using Behind.Common.Database;
using static Behind.Common.FunctionalExtensions;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public partial class AbstractCoreContext : IRepository<UserRole, Guid>
{
    public DbSet<UserRole> Roles => Set<UserRole>();
    
    public Guid GetKey(UserRole entity) => Entry(entity).Property<Guid>("Id").CurrentValue;

    async Task<Option<UserRole>> IRepository<UserRole, Guid>.TryFindAsync(
        Guid key,
        CancellationToken cancellationToken
    )
    {
        var result = await Roles
            .Where(r => EF.Property<Guid>(r, "Id") == key)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (result is null)
        {
            return None;
        }

        var grantIds = await Set<Dictionary<string, object>>("RoleGrants")
            .Where(rg => EF.Property<Guid>(rg, "RoleId") == key)
            .Select(rg => EF.Property<Guid>(rg, "GrantId"))
            .ToListAsync(cancellationToken);

        Option<IEnumerable<Grant>> grants = None;

        if (grantIds.Count > 0)
        {
            var grantList = await Grants
                .Where(g => grantIds.Contains(EF.Property<Guid>(g, "Id")))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            grants = Some((IEnumerable<Grant>)grantList);
        }

        return Some(result with { Grants = grants });
    }

    async Task IRepository<UserRole, Guid>.AddAsync(UserRole aggregate, CancellationToken cancellationToken) =>
        await Roles.AddAsync(aggregate, cancellationToken);

    void IRepository<UserRole, Guid>.Update(UserRole aggregate) =>
        Roles.ToImmutableEntityRepository(this).UpdateImmutable([GetKey(aggregate)], aggregate);

    void IRepository<UserRole, Guid>.Update(Guid key, UserRole aggregate) =>
        Roles.ToImmutableEntityRepository(this).UpdateImmutable([key], aggregate);
    
    void IRepository<UserRole, Guid>.Delete(UserRole aggregate) =>
        Roles.ToImmutableEntityRepository(this).RemoveImmutable(aggregate);

    Task IRepository<UserRole, Guid>.DeleteAsync(Guid key, CancellationToken cancellationToken) =>
        this.DeleteByKeyAsync<UserRole>([key], cancellationToken);
}
