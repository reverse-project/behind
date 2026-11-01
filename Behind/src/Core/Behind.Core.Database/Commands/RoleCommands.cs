using Behind.Core.Domain;

namespace Behind.Core.Database.Commands;

public sealed class RoleCommands(AbstractCoreContext context) : IRoleCommands
{
    public async Task CreateWithId(Guid id, Role role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);

        await context.Set<Role>().AddAsync(role, cancellationToken);
        context.Entry(role).Property<Guid>("Id").CurrentValue = id;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignBuiltIn(Guid userId, BuiltInRole role, CancellationToken cancellationToken = default)
    {
        await context.EnsureBuiltInRolesAsync(cancellationToken);
        await context.AssignRoleToUserAsync(userId, role.Id, cancellationToken);
    }
}
