using Behind.Common;
using Behind.Common.Database;
using static Behind.Common.FunctionalExtensions;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public partial class AbstractCoreContext : IRepository<HumanUser, Guid>
{
    public DbSet<HumanUser> Users => Set<HumanUser>();
    
    public Guid GetKey(HumanUser entity) => Entry(entity).Property<Guid>("Id").CurrentValue;

    async Task<Option<HumanUser>> IRepository<HumanUser, Guid>.TryFindAsync(
        Guid key,
        CancellationToken cancellationToken
    )
    {
        var query = from user in Users
                    where EF.Property<Guid>(user, "Id") == key
                    select new
                    {
                        user.DisplayName,
                        Emails = Emails
                            .Where(e => EF.Property<Guid>(e, "UserId") == EF.Property<Guid>(user, "Id"))
                            .Select(e => e.Value)
                            .ToList()
                    };

        var userResult = await query.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        if (userResult is null)
        {
            return None;
        }

        var roleIds = await Set<Dictionary<string, object>>("UserRoles")
            .Where(ur => EF.Property<Guid>(ur, "UserId") == key)
            .Select(ur => EF.Property<Guid>(ur, "RoleId"))
            .ToListAsync(cancellationToken);

        Option<IEnumerable<Role>> roles = None;

        if (roleIds.Count > 0)
        {
            var roleData = await Roles
                .Where(r => roleIds.Contains(EF.Property<Guid>(r, "Id")))
                .Select(r => new { Id = EF.Property<Guid>(r, "Id"), Role = r })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var roleGrantMappings = await Set<Dictionary<string, object>>("RoleGrants")
                .Where(rg => roleIds.Contains(EF.Property<Guid>(rg, "RoleId")))
                .Select(rg => new
                {
                    RoleId = EF.Property<Guid>(rg, "RoleId"),
                    GrantId = EF.Property<Guid>(rg, "GrantId")
                })
                .ToListAsync(cancellationToken);

            var grantIdList = roleGrantMappings.Select(x => x.GrantId).Distinct().ToList();
            var grantData = await Grants
                .Where(g => grantIdList.Contains(EF.Property<Guid>(g, "Id")))
                .Select(g => new { Id = EF.Property<Guid>(g, "Id"), Grant = g })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var grantLookup = grantData.ToDictionary(x => x.Id, x => x.Grant);
            var roleGrantMap = roleGrantMappings
                .GroupBy(x => x.RoleId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.GrantId).ToList());

            var populatedRoles = roleData.Select(rd =>
            {
                var roleGrantIds = roleGrantMap.TryGetValue(rd.Id, out var ids) ? ids : [];
                Option<IEnumerable<Grant>> roleGrants = roleGrantIds.Count > 0
                    ? Some((IEnumerable<Grant>)roleGrantIds.Select(gid => grantLookup[gid]).ToList())
                    : None;
                return (Role)(rd.Role with { Grants = roleGrants });
            }).ToList();

            roles = Some((IEnumerable<Role>)populatedRoles);
        }

        return Some(new HumanUser(
            userResult.DisplayName,
            userResult.Emails.Select(v => new Email(v)),
            roles
        ));
    }

    async Task IRepository<HumanUser, Guid>.AddAsync(HumanUser aggregate, CancellationToken cancellationToken) =>
        await Users.AddAsync(aggregate, cancellationToken);

    void IRepository<HumanUser, Guid>.Update(HumanUser aggregate) =>
        this.UpdateValuesByKey([GetKey(aggregate)], aggregate);
    
    void IRepository<HumanUser, Guid>.Update(Guid key, HumanUser aggregate) =>
        this.UpdateValuesByKey([key], aggregate);

    void IRepository<HumanUser, Guid>.Delete(HumanUser aggregate) =>
        Users.ToImmutableEntityRepository(this).RemoveImmutable(aggregate);

    Task IRepository<HumanUser, Guid>.DeleteAsync(Guid key, CancellationToken cancellationToken) =>
        this.DeleteByKeyAsync<HumanUser>([key], cancellationToken);
}
