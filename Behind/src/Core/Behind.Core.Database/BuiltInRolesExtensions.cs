using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public static class BuiltInRolesExtensions
{
    extension(AbstractCoreContext context)
    {
        public async Task EnsureBuiltInRolesAsync(AccessCatalog catalog, CancellationToken cancellationToken = default)
        {
            var addedGrants = new HashSet<Guid>();

            foreach (var grant in catalog.Grants)
            {
                if (await context.EnsureBuiltInGrantAsync(grant, cancellationToken))
                {
                    addedGrants.Add(grant.Id);
                }
            }

            foreach (var restriction in catalog.Restrictions)
            {
                await context.EnsureBuiltInRestrictionAsync(restriction, cancellationToken);
            }

            foreach (var role in catalog.Roles)
            {
                var created = await context.EnsureBuiltInRoleAsync(role, cancellationToken);

                foreach (var grant in role.Grants.Where(grant => created || addedGrants.Contains(grant.Id)))
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

        public async Task RemoveRoleFromUserAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
        {
            var userRoles = context.Set<Dictionary<string, object>>("UserRoles");

            var assigned = await userRoles
                .Where(userRole => EF.Property<Guid>(userRole, "UserId") == userId
                                   && EF.Property<Guid>(userRole, "RoleId") == roleId)
                .ToListAsync(cancellationToken);

            if (assigned.Count == 0)
            {
                return;
            }

            userRoles.RemoveRange(assigned);

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

        private async Task<bool> EnsureBuiltInGrantAsync(BuiltInGrant builtIn, CancellationToken cancellationToken)
        {
            var exists = await context.Grants
                .IgnoreQueryFilters()
                .AnyAsync(grant => EF.Property<Guid>(grant, "Id") == builtIn.Id, cancellationToken);

            if (exists)
            {
                return false;
            }

            var grant = new Grant(builtIn.Grant.Name);

            await context.Grants.AddAsync(grant, cancellationToken);
            context.Entry(grant).Property<Guid>("Id").CurrentValue = builtIn.Id;

            await context.SaveChangesAsync(cancellationToken);

            return true;
        }

        private async Task EnsureBuiltInRestrictionAsync(BuiltInRestriction builtIn, CancellationToken cancellationToken)
        {
            var exists = await context.Restrictions
                .IgnoreQueryFilters()
                .AnyAsync(restriction => EF.Property<Guid>(restriction, "Id") == builtIn.Id, cancellationToken);

            if (exists)
            {
                return;
            }

            var restriction = new Restriction(builtIn.Restriction.Name);

            await context.Restrictions.AddAsync(restriction, cancellationToken);
            context.Entry(restriction).Property<Guid>("Id").CurrentValue = builtIn.Id;

            await context.SaveChangesAsync(cancellationToken);
        }

        private async Task<bool> EnsureBuiltInRoleAsync(BuiltInRole builtIn, CancellationToken cancellationToken)
        {
            var existing = await context.Roles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(role => EF.Property<Guid>(role, "Id") == builtIn.Id, cancellationToken);

            if (existing is not null)
            {
                await context.AlignDeveloperFlagAsync(existing, builtIn, cancellationToken);

                return false;
            }

            var role = new UserRole(builtIn.Role.Name) { IsDeveloper = builtIn.Role.IsDeveloperRole };

            await context.Roles.AddAsync(role, cancellationToken);
            context.Entry(role).Property<Guid>("Id").CurrentValue = builtIn.Id;

            await context.SaveChangesAsync(cancellationToken);

            return true;
        }

        private async Task AlignDeveloperFlagAsync(
            UserRole existing,
            BuiltInRole builtIn,
            CancellationToken cancellationToken)
        {
            if (existing.IsDeveloper == builtIn.Role.IsDeveloperRole)
            {
                return;
            }

            context.Entry(existing).Property(role => role.IsDeveloper).CurrentValue = builtIn.Role.IsDeveloperRole;

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
