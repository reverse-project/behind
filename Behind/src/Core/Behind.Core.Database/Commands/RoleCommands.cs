using Behind.Common;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

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

    public async Task<Option<Guid>> Create(string name, CancellationToken cancellationToken = default)
    {
        var exists = await context.Roles.AnyAsync(
            role => role.Name.ToLower() == name.ToLower(),
            cancellationToken);

        if (exists)
        {
            return None;
        }

        var role = new UserRole(name);

        await context.Roles.AddAsync(role, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return Some(context.GetKey(role));
    }

    public async Task Delete(Guid roleId, CancellationToken cancellationToken = default)
    {
        await context.GetRepository<UserRole, Guid>().DeleteAsync(roleId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignBuiltIn(Guid userId, BuiltInRole role, CancellationToken cancellationToken = default)
    {
        await context.EnsureBuiltInRolesAsync(cancellationToken);
        await context.AssignRoleToUserAsync(userId, role.Id, cancellationToken);
    }

    public Task RevokeBuiltIn(Guid userId, BuiltInRole role, CancellationToken cancellationToken = default) =>
        context.RemoveRoleFromUserAsync(userId, role.Id, cancellationToken);
}
