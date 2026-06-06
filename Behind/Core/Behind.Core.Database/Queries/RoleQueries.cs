using Behind.Common;
using static Behind.Common.FunctionalExtensions;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Queries;

public sealed class RoleQueries(AbstractCoreContext context) : IRoleQueries
{
    public async Task<Option<IRoleQueries.RoleResult>> GetRoleByName(string name,
        CancellationToken cancellationToken = default)
    {
        var searchQuery = from _role in context.Roles
            where _role.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)
            select new
            {
                Id = EF.Property<Guid>(_role, "Id"),
                Role = _role
            };

        var role = await searchQuery.FirstOrDefaultAsync(cancellationToken);

        if (role is null)
        {
            return None;
        }

        var grantsQuery = from _grant in context.Grants
            where EF.Property<Guid>(_grant, "RoleId") == role.Id
            select _grant;

        var grants = await grantsQuery.AsNoTracking().ToListAsync(cancellationToken);

        return new IRoleQueries.RoleResult(role.Id, role.Role with { Grants = grants });
    }
}