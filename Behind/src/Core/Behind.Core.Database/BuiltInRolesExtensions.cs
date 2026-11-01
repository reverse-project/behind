using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public static class BuiltInRolesExtensions
{
    extension(AbstractCoreContext context)
    {
        public async Task EnsureBuiltInRolesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var grant in BuiltInAccess.Grants)
            {
                await context.EnsureBuiltInGrantAsync(grant, cancellationToken);
            }

            foreach (var role in BuiltInAccess.Roles)
            {
                await context.EnsureBuiltInRoleAsync(role, cancellationToken);

                await context.RemoveStaleRoleGrantsAsync(role, cancellationToken);

                foreach (var grant in role.Grants)
                {
                    await context.EnsureRoleGrantAsync(role.Id, grant.Id, cancellationToken);
                }
            }
        }

        public async Task AssignRoleToUserAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
        {
            var userRoles = context.Set<Dictionary<string, object>>("UserRoles");

            var assigned = await userRoles.AnyAsync(
                userRole => EF.Property<Guid>(userRole, "UserId") == userId
                            && EF.Property<Guid>(userRole, "RoleId") == roleId,
                cancellationToken);

            if (assigned)
            {
                return;
            }

            await userRoles.AddAsync(
                new Dictionary<string, object> { ["UserId"] = userId, ["RoleId"] = roleId },
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task EnsureUsersHaveUsersRoleAsync(CancellationToken cancellationToken = default)
        {
            var assigned = context.Set<Dictionary<string, object>>("UserRoles")
                .Where(userRole => EF.Property<Guid>(userRole, "RoleId") == BuiltInAccess.Users.Id)
                .Select(userRole => EF.Property<Guid>(userRole, "UserId"));

            var unassigned = await context.Users
                .Select(user => EF.Property<Guid>(user, "Id"))
                .Where(id => id != DomainConstants.SystemUserId
                             && id != DomainConstants.GuestUserId
                             && !assigned.Contains(id))
                .ToListAsync(cancellationToken);

            foreach (var userId in unassigned)
            {
                await context.AssignRoleToUserAsync(userId, BuiltInAccess.Users.Id, cancellationToken);
            }
        }

        private async Task EnsureBuiltInGrantAsync(BuiltInGrant builtIn, CancellationToken cancellationToken)
        {
            var exists = await context.Grants
                .IgnoreQueryFilters()
                .AnyAsync(grant => EF.Property<Guid>(grant, "Id") == builtIn.Id, cancellationToken);

            if (exists)
            {
                return;
            }

            var grant = new Grant(builtIn.Grant.Name);

            await context.Grants.AddAsync(grant, cancellationToken);
            context.Entry(grant).Property<Guid>("Id").CurrentValue = builtIn.Id;

            await context.SaveChangesAsync(cancellationToken);
        }

        private async Task EnsureBuiltInRoleAsync(BuiltInRole builtIn, CancellationToken cancellationToken)
        {
            var exists = await context.Roles
                .IgnoreQueryFilters()
                .AnyAsync(role => EF.Property<Guid>(role, "Id") == builtIn.Id, cancellationToken);

            if (exists)
            {
                return;
            }

            var role = new UserRole(builtIn.Role.Name);

            await context.Roles.AddAsync(role, cancellationToken);
            context.Entry(role).Property<Guid>("Id").CurrentValue = builtIn.Id;

            await context.SaveChangesAsync(cancellationToken);
        }

        private async Task RemoveStaleRoleGrantsAsync(BuiltInRole role, CancellationToken cancellationToken)
        {
            var roleGrants = context.Set<Dictionary<string, object>>("RoleGrants");
            var expected = role.Grants.Select(grant => grant.Id).ToList();

            var stale = await roleGrants
                .Where(roleGrant => EF.Property<Guid>(roleGrant, "RoleId") == role.Id
                                    && !expected.Contains(EF.Property<Guid>(roleGrant, "GrantId")))
                .ToListAsync(cancellationToken);

            if (stale.Count == 0)
            {
                return;
            }

            roleGrants.RemoveRange(stale);
            await context.SaveChangesAsync(cancellationToken);
        }

        private async Task EnsureRoleGrantAsync(Guid roleId, Guid grantId, CancellationToken cancellationToken)
        {
            var roleGrants = context.Set<Dictionary<string, object>>("RoleGrants");

            var exists = await roleGrants.AnyAsync(
                roleGrant => EF.Property<Guid>(roleGrant, "RoleId") == roleId
                             && EF.Property<Guid>(roleGrant, "GrantId") == grantId,
                cancellationToken);

            if (exists)
            {
                return;
            }

            await roleGrants.AddAsync(
                new Dictionary<string, object> { ["RoleId"] = roleId, ["GrantId"] = grantId },
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
