using Behind.Common;
using Behind.Core.Domain;
using static Behind.Common.FunctionalExtensions;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Queries;

public sealed class RoleQueries(AbstractCoreContext context) : IRoleQueries
{
    public async Task<Option<IRoleQueries.RoleResult>> GetRoleByName(string name,
        CancellationToken cancellationToken = default)
    {
        var found = await FindRoles(role => role.Name.ToLower() == name.ToLower(), cancellationToken);

        return found.Head();
    }

    public async Task<Option<IRoleQueries.RoleResult>> GetRoleById(Guid id,
        CancellationToken cancellationToken = default)
    {
        var found = await FindRoles(role => EF.Property<Guid>(role, "Id") == id, cancellationToken);

        return found.Head();
    }

    public Task<IReadOnlyList<IRoleQueries.RoleResult>> ListRoles(CancellationToken cancellationToken = default) =>
        FindRoles(_ => true, cancellationToken);

    public async Task<IReadOnlyList<Role>> GetRolesByUserId(Guid userId,
        CancellationToken cancellationToken = default) =>
        await context.WithAccessAsync(await context.FindRolesOfUserAsync(userId, cancellationToken), cancellationToken);

    private async Task<IReadOnlyList<IRoleQueries.RoleResult>> FindRoles(
        Expression<Func<UserRole, bool>> predicate,
        CancellationToken cancellationToken)
    {
        var roles = await context.Roles
            .Where(predicate)
            .OrderBy(role => role.Name)
            .Select(role => new
            {
                Id = EF.Property<Guid>(role, "Id"),
                Role = role
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var withAccess = await context.WithAccessAsync(
            roles.Select(role => (role.Id, (Role)role.Role)).ToList(),
            cancellationToken);

        return roles
            .Select((role, index) => new IRoleQueries.RoleResult(role.Id, withAccess[index]))
            .ToList();
    }
}
