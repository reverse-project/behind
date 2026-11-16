using Behind.Common;
using Behind.Common.Database;
using static Behind.Common.FunctionalExtensions;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public partial class AbstractCoreContext : IRepository<Restriction, Guid>
{
    public DbSet<Restriction> Restrictions => Set<Restriction>();

    public Guid GetKey(Restriction entity) => Entry(entity).Property<Guid>("Id").CurrentValue;

    async Task<Option<Restriction>> IRepository<Restriction, Guid>.TryFindAsync(
        Guid key,
        CancellationToken cancellationToken
    )
    {
        var result = await Restrictions
            .Where(r => EF.Property<Guid>(r, "Id") == key)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return result is null ? None : Some(result);
    }

    async Task IRepository<Restriction, Guid>.AddAsync(Restriction aggregate, CancellationToken cancellationToken) =>
        await Restrictions.AddAsync(aggregate, cancellationToken);

    void IRepository<Restriction, Guid>.Update(Restriction aggregate) =>
        Restrictions.ToImmutableEntityRepository(this).UpdateImmutable([GetKey(aggregate)], aggregate);

    void IRepository<Restriction, Guid>.Update(Guid key, Restriction aggregate) =>
        Restrictions.ToImmutableEntityRepository(this).UpdateImmutable([key], aggregate);

    void IRepository<Restriction, Guid>.Delete(Restriction aggregate) =>
        Restrictions.ToImmutableEntityRepository(this).RemoveImmutable(aggregate);

    Task IRepository<Restriction, Guid>.DeleteAsync(Guid key, CancellationToken cancellationToken) =>
        this.DeleteByKeyAsync<Restriction>([key], cancellationToken);
}
