using Behind.Common;
using static Behind.Common.FunctionalExtensions;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public partial class AbstractCoreContext : IRepository<Password, Guid>
{
    public DbSet<Password> Passwords => Set<Password>();
    
    public Guid GetKey(Password entity) => Entry(entity).Property<Guid>("Id").CurrentValue;

    async Task<Option<Password>> IRepository<Password, Guid>.TryFindAsync(
        Guid key,
        CancellationToken cancellationToken
    )
    {
        var result = await Passwords
            .Where(g => EF.Property<Guid>(g, "Id") == key)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return result is null ? None : Some(result);
    }

    async Task IRepository<Password, Guid>.AddAsync(Password aggregate, CancellationToken cancellationToken) =>
        await Passwords.AddAsync(aggregate, cancellationToken);

    void IRepository<Password, Guid>.Update(Password aggregate) =>
        Passwords.ToImmutableEntityRepository(this).UpdateImmutable([GetKey(aggregate)], aggregate);
    
    void IRepository<Password, Guid>.Update(Guid key, Password aggregate) =>
        Passwords.ToImmutableEntityRepository(this).UpdateImmutable([key], aggregate);

    void IRepository<Password, Guid>.Delete(Password aggregate) =>
        Passwords.ToImmutableEntityRepository(this).RemoveImmutable(aggregate);

    Task IRepository<Password, Guid>.DeleteAsync(Guid key, CancellationToken cancellationToken) =>
        this.DeleteByKeyAsync<Password>([key], cancellationToken);
}