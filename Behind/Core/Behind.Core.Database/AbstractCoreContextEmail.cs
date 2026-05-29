using Behind.Common;
using Behind.Common.Database;
using static Behind.Common.FunctionalExtensions;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public partial class AbstractCoreContext : IRepository<Email, Guid>
{
    public Guid GetKey(Email entity) => Entry(entity).Property<Guid>("Id").CurrentValue;

    async Task<Option<Email>> IRepository<Email, Guid>.TryFindAsync(
        Guid key,
        CancellationToken cancellationToken
    )
    {
        var result = await Emails
            .Where(e => EF.Property<Guid>(e, "Id") == key)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return result is null ? None : Some(result);
    }

    async Task IRepository<Email, Guid>.AddAsync(Email aggregate, CancellationToken cancellationToken) =>
        await Emails.AddAsync(aggregate, cancellationToken);

    void IRepository<Email, Guid>.Update(Email aggregate) =>
        Emails.ToImmutableEntityRepository(this).UpdateImmutable(aggregate);

    void IRepository<Email, Guid>.Delete(Email aggregate) =>
        Emails.ToImmutableEntityRepository(this).RemoveImmutable(aggregate);
}
