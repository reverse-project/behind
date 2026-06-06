using Behind.Core.Domain;

namespace Behind.Core.Database.Commands;

public sealed class RoleCommands(AbstractCoreContext context) : IRoleCommands
{
    public async Task CreateWithId(Guid id, Role role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);

        var repository = context.GetRepository<Role, Guid>();
        await repository.AddAsync(role, cancellationToken);
        context.Entry(role).Property<Guid>("Id").CurrentValue = id;
        await context.SaveChangesAsync(cancellationToken);
    }
}