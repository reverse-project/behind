using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database;

public static class AccessExtensions
{
    extension(AbstractCoreContext context)
    {
        public async Task<IReadOnlyList<(Guid Id, Role Role)>> FindRolesOfUserAsync(
            Guid userId,
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

            return roles.Select(role => (role.Id, (Role)role.Role)).ToList();
        }

        public async Task<IReadOnlyList<Role>> WithAccessAsync(
            IReadOnlyList<(Guid Id, Role Role)> roles,
            CancellationToken cancellationToken = default)
        {
            var roleIds = roles.Select(role => role.Id).ToList();

            var grantsQuery = from roleGrant in context.Set<Dictionary<string, object>>("RoleGrants")
                where roleIds.Contains(EF.Property<Guid>(roleGrant, "RoleId"))
                join grant in context.Grants
                    on EF.Property<Guid>(roleGrant, "GrantId") equals EF.Property<Guid>(grant, "Id")
                select new
                {
                    RoleId = EF.Property<Guid>(roleGrant, "RoleId"),
                    Grant = grant
                };

            var restrictionsQuery = from roleRestriction in context.Set<Dictionary<string, object>>("RoleRestrictions")
                where roleIds.Contains(EF.Property<Guid>(roleRestriction, "RoleId"))
                join restriction in context.Restrictions
                    on EF.Property<Guid>(roleRestriction, "RestrictionId") equals EF.Property<Guid>(restriction, "Id")
                select new
                {
                    RoleId = EF.Property<Guid>(roleRestriction, "RoleId"),
                    Restriction = restriction
                };

            var grantsByRole = (await grantsQuery.AsNoTracking().ToListAsync(cancellationToken))
                .ToLookup(entry => entry.RoleId, entry => entry.Grant);

            var restrictionsByRole = (await restrictionsQuery.AsNoTracking().ToListAsync(cancellationToken))
                .ToLookup(entry => entry.RoleId, entry => entry.Restriction);

            return roles
                .Select(role => role.Role with
                {
                    Grants = Some<IEnumerable<Grant>>(grantsByRole[role.Id].ToList()),
                    Restrictions = Some<IEnumerable<Restriction>>(restrictionsByRole[role.Id].ToList())
                })
                .ToList();
        }

        public async Task<UserAccess> GetUserAccessAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var roles = await context.WithAccessAsync(
                await context.FindRolesOfUserAsync(userId, cancellationToken),
                cancellationToken);

            var grants = await (
                    from userGrant in context.Set<Dictionary<string, object>>("UserGrants")
                    where EF.Property<Guid>(userGrant, "UserId") == userId
                    join grant in context.Grants
                        on EF.Property<Guid>(userGrant, "GrantId") equals EF.Property<Guid>(grant, "Id")
                    orderby grant.Name
                    select grant)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var restrictions = await (
                    from userRestriction in context.Set<Dictionary<string, object>>("UserRestrictions")
                    where EF.Property<Guid>(userRestriction, "UserId") == userId
                    join restriction in context.Restrictions
                        on EF.Property<Guid>(userRestriction, "RestrictionId") equals EF.Property<Guid>(restriction, "Id")
                    orderby restriction.Name
                    select restriction)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return new UserAccess(roles, grants, restrictions);
        }

        public async Task ReplaceRoleAccessAsync(
            Guid roleId,
            AccessSelection selection,
            CancellationToken cancellationToken = default)
        {
            await context.ReplaceLinksAsync(
                "RoleGrants",
                "RoleId",
                "GrantId",
                roleId,
                await context.ResolveGrantIdsAsync(selection.Grants, cancellationToken),
                cancellationToken);

            await context.ReplaceLinksAsync(
                "RoleRestrictions",
                "RoleId",
                "RestrictionId",
                roleId,
                await context.ResolveRestrictionIdsAsync(selection.Restrictions, cancellationToken),
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task ReplaceUserAccessAsync(
            Guid userId,
            IReadOnlyCollection<Guid> roleIds,
            AccessSelection selection,
            CancellationToken cancellationToken = default)
        {
            await context.ReplaceLinksAsync("UserRoles", "UserId", "RoleId", userId, roleIds, cancellationToken);

            await context.ReplaceLinksAsync(
                "UserGrants",
                "UserId",
                "GrantId",
                userId,
                await context.ResolveGrantIdsAsync(selection.Grants, cancellationToken),
                cancellationToken);

            await context.ReplaceLinksAsync(
                "UserRestrictions",
                "UserId",
                "RestrictionId",
                userId,
                await context.ResolveRestrictionIdsAsync(selection.Restrictions, cancellationToken),
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
        }

        private async Task<IReadOnlyCollection<Guid>> ResolveGrantIdsAsync(
            IReadOnlyList<string> names,
            CancellationToken cancellationToken)
        {
            var wanted = names.Distinct(StringComparer.Ordinal).ToList();

            var found = await context.Grants
                .Where(grant => wanted.Contains(grant.Name))
                .Select(grant => EF.Property<Guid>(grant, "Id"))
                .ToListAsync(cancellationToken);

            return found.Count == wanted.Count
                ? found
                : throw new ArgumentException("A grant does not exist.", nameof(names));
        }

        private async Task<IReadOnlyCollection<Guid>> ResolveRestrictionIdsAsync(
            IReadOnlyList<string> names,
            CancellationToken cancellationToken)
        {
            var wanted = names.Distinct(StringComparer.Ordinal).ToList();

            var found = await context.Restrictions
                .Where(restriction => wanted.Contains(restriction.Name))
                .Select(restriction => EF.Property<Guid>(restriction, "Id"))
                .ToListAsync(cancellationToken);

            return found.Count == wanted.Count
                ? found
                : throw new ArgumentException("A restriction does not exist.", nameof(names));
        }

        private async Task ReplaceLinksAsync(
            string table,
            string ownerColumn,
            string targetColumn,
            Guid ownerId,
            IReadOnlyCollection<Guid> targetIds,
            CancellationToken cancellationToken)
        {
            var links = context.Set<Dictionary<string, object>>(table);

            var existing = await links
                .Where(link => EF.Property<Guid>(link, ownerColumn) == ownerId)
                .ToListAsync(cancellationToken);

            var current = existing.Select(link => (Guid)link[targetColumn]).ToHashSet();

            links.RemoveRange(existing.Where(link => !targetIds.Contains((Guid)link[targetColumn])));

            await links.AddRangeAsync(
                targetIds
                    .Where(targetId => !current.Contains(targetId))
                    .Select(targetId => new Dictionary<string, object>
                    {
                        [ownerColumn] = ownerId,
                        [targetColumn] = targetId
                    }),
                cancellationToken);
        }
    }
}
