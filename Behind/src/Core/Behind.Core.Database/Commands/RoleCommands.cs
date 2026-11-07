using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Commands;

public sealed class RoleCommands(AbstractCoreContext context, AccessCatalog catalog) : IRoleCommands
{
    public async Task CreateWithId(Guid id, Role role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);

        await context.Set<Role>().AddAsync(role, cancellationToken);
        context.Entry(role).Property<Guid>("Id").CurrentValue = id;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Option<Guid>> Create(
        string name,
        bool isDeveloper = false,
        CancellationToken cancellationToken = default)
    {
        var exists = await context.Roles.AnyAsync(
            role => role.Name.ToLower() == name.ToLower(),
            cancellationToken);

        if (exists)
        {
            return None;
        }

        var role = new UserRole(name) { IsDeveloper = isDeveloper };

        await context.Roles.AddAsync(role, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return Some(context.GetKey(role));
    }

    public async Task<bool> SetDeveloper(Guid roleId, bool isDeveloper, CancellationToken cancellationToken = default)
    {
        var role = await context.Roles.FirstOrDefaultAsync(
            candidate => EF.Property<Guid>(candidate, "Id") == roleId,
            cancellationToken);

        if (role is null)
        {
            return false;
        }

        context.Entry(role).Property(candidate => candidate.IsDeveloper).CurrentValue = isDeveloper;
        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task Delete(Guid roleId, CancellationToken cancellationToken = default)
    {
        await context.GetRepository<UserRole, Guid>().DeleteAsync(roleId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> Restore(Guid roleId, CancellationToken cancellationToken = default)
    {
        var deleted = await IsDeleted(roleId, cancellationToken);

        if (!deleted)
        {
            return false;
        }

        await context.GetRestoreRepository<UserRole, Guid>().RestoreAsync(roleId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<HardDeleteResult> HardDelete(Guid roleId, CancellationToken cancellationToken = default)
    {
        var deleted = await IsDeleted(roleId, cancellationToken);

        if (!deleted)
        {
            return HardDeleteResult.NotFound;
        }

        await context.GetHardDeleteRepository<UserRole, Guid>().HardDeleteAsync(roleId, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return HardDeleteResult.Deleted;
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();
            return HardDeleteResult.Rejected;
        }
    }

    private async Task<bool> IsDeleted(Guid roleId, CancellationToken cancellationToken)
    {
        using var scope = context.SoftDelete.IncludeDeleted();
        var role = await context.Roles.FindAsync([roleId], cancellationToken);

        return role is not null && await context.Entry(role).IsDeletedAsync(cancellationToken);
    }

    public async Task AssignBuiltIn(Guid userId, BuiltInRole role, CancellationToken cancellationToken = default)
    {
        await context.EnsureBuiltInRolesAsync(catalog, cancellationToken);
        await context.AssignRoleToUserAsync(userId, role.Id, cancellationToken);
    }

    public Task RevokeBuiltIn(Guid userId, BuiltInRole role, CancellationToken cancellationToken = default) =>
        context.RemoveRoleFromUserAsync(userId, role.Id, cancellationToken);
}
