using Behind.Common;
using Behind.Common.Database;
using static Behind.Common.FunctionalExtensions;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public partial class AbstractCoreContext : IRepository<Grant, Guid>
{
    public Guid GetKey(Grant entity) => Entry(entity).Property<Guid>("Id").CurrentValue;

    async Task<Option<Grant>> IRepository<Grant, Guid>.TryFindAsync(
        Guid key,
        CancellationToken cancellationToken
    )
    {
        var result = await Grants
            .Where(g => EF.Property<Guid>(g, "Id") == key)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return result is null ? None : Some(result);
    }

    async Task IRepository<Grant, Guid>.AddAsync(Grant aggregate, CancellationToken cancellationToken) =>
        await Grants.AddAsync(aggregate, cancellationToken);

    void IRepository<Grant, Guid>.Update(Grant aggregate) =>
        Grants.ToImmutableEntityRepository(this).UpdateImmutable(aggregate);

    void IRepository<Grant, Guid>.Delete(Grant aggregate) =>
        Grants.ToImmutableEntityRepository(this).RemoveImmutable(aggregate);
}
