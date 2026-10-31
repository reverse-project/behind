using Behind.Common;
using Behind.Core.Domain;
using static Behind.Common.FunctionalExtensions;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Queries;

public sealed class RoleQueries(AbstractCoreContext context) : IRoleQueries
{
    public async Task<Option<IRoleQueries.RoleResult>> GetRoleByName(string name,
        CancellationToken cancellationToken = default)
    {
        var searchQuery = from _role in context.Roles
            where _role.Name.ToLower() == name.ToLower()
            select new
            {
                Id = EF.Property<Guid>(_role, "Id"),
                Role = _role
            };

        var role = await searchQuery.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        if (role is null)
        {
            return None;
        }

        var withGrants = await WithGrants([(role.Id, role.Role)], cancellationToken);

        return new IRoleQueries.RoleResult(role.Id, withGrants[0]);
    }

    public async Task<IReadOnlyList<Role>> GetRolesByUserId(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var rolesQuery = from userRole in context.Set<Dictionary<string, object>>("UserRoles")
            where EF.Property<Guid>(userRole, "UserId") == userId
            join role in context.Roles
                on EF.Property<Guid>(userRole, "RoleId") equals EF.Property<Guid>(role, "Id")
            select new
            {
                Id = EF.Property<Guid>(role, "Id"),
                Role = role
            };

        var roles = await rolesQuery.AsNoTracking().ToListAsync(cancellationToken);

        return await WithGrants(roles.Select(r => (r.Id, (Role)r.Role)).ToList(), cancellationToken);
    }

    private async Task<IReadOnlyList<Role>> WithGrants(
        IReadOnlyList<(Guid Id, Role Role)> roles,
        CancellationToken cancellationToken)
    {
        var roleIds = roles.Select(r => r.Id).ToList();

        var grantsQuery = from roleGrant in context.Set<Dictionary<string, object>>("RoleGrants")
            where roleIds.Contains(EF.Property<Guid>(roleGrant, "RoleId"))
            join grant in context.Grants
                on EF.Property<Guid>(roleGrant, "GrantId") equals EF.Property<Guid>(grant, "Id")
            select new
            {
                RoleId = EF.Property<Guid>(roleGrant, "RoleId"),
                Grant = grant
            };

        var grantsByRole = (await grantsQuery.AsNoTracking().ToListAsync(cancellationToken))
            .ToLookup(g => g.RoleId, g => g.Grant);

        return roles
            .Select(r => r.Role with { Grants = Some<IEnumerable<Grant>>(grantsByRole[r.Id].ToList()) })
            .ToList();
    }
}
