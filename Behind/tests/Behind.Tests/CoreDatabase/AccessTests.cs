using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class AccessTests : IDisposable
{
    private const string Email = "access@test.com";

    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<Guid> SeedUserWithAsync(params BuiltInRole[] roles)
    {
        await using var seed = _factory.CreateContext();
        await seed.EnsureBuiltInRolesAsync();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, Email);

        foreach (var role in roles)
        {
            await DatabaseSeedHelper.AssignRoleToUserAsync(seed, userId, role.Id);
        }

        return userId;
    }

    private async Task<UserAccess> AccessOfAsync(Guid userId)
    {
        await using var ctx = _factory.CreateContext();
        return await new AccessQueries(ctx).GetUserAccess(userId);
    }

    private async Task SetUserAccessAsync(
        Guid userId,
        IReadOnlyCollection<Guid> roleIds,
        IReadOnlyList<string> grants,
        IReadOnlyList<string> restrictions)
    {
        await using var ctx = _factory.CreateContext();
        await new AccessCommands(ctx).SetUserAccess(userId, roleIds, new AccessSelection(grants, restrictions));
    }

    private async Task<bool> HasGrantAsync(string grant)
    {
        await using var ctx = _factory.CreateContext();
        return await new UserQueries(ctx).HasGrant(Email, grant);
    }

    [Fact]
    public async Task GetUserAccess_CombinesRoleGrantsWithDirectGrantsAndRestrictions()
    {
        var userId = await SeedUserWithAsync(BuiltInAccess.Users);
        await SetUserAccessAsync(userId, [BuiltInAccess.Users.Id], ["User.Manage"], ["Session.Read"]);

        var access = await AccessOfAsync(userId);

        Assert.Equal(["User.Manage"], access.Grants);
        Assert.Equal(["Session.Read"], access.Restrictions);
        Assert.Equal(BuiltInAccess.Users.Role.Name, Assert.Single(access.Roles).Name);
        Assert.Contains((Grant)"User.Manage", access.CalculatedGrants);
        Assert.DoesNotContain((Grant)"Session.Read", access.CalculatedGrants);
        Assert.Contains((Grant)"User.Read", access.CalculatedGrants);
    }

    [Fact]
    public async Task GetUserAccess_UserWithoutAnything_IsEmpty()
    {
        var userId = await SeedUserWithAsync();

        var access = await AccessOfAsync(userId);

        Assert.Empty(access.Roles);
        Assert.Empty(access.CalculatedGrants);
    }

    [Fact]
    public async Task HasGrant_DirectUserGrant_ReturnsTrue()
    {
        var userId = await SeedUserWithAsync();
        await SetUserAccessAsync(userId, [], ["Role.Manage"], []);

        Assert.True(await HasGrantAsync("Role.Manage"));
        Assert.False(await HasGrantAsync("User.Manage"));
    }

    [Fact]
    public async Task HasGrant_UserRestrictionRemovesAGrantGivenByARole()
    {
        var userId = await SeedUserWithAsync(BuiltInAccess.Administrators);
        await SetUserAccessAsync(userId, [BuiltInAccess.Administrators.Id], [], ["User.Manage"]);

        Assert.False(await HasGrantAsync("User.Manage"));
        Assert.True(await HasGrantAsync("User.Read"));
    }

    [Fact]
    public async Task HasGrant_RoleRestrictionRemovesAGrantGivenByAnotherRole()
    {
        await SeedUserWithAsync(BuiltInAccess.Administrators, BuiltInAccess.Users);
        await using (var ctx = _factory.CreateContext())
        {
            await new AccessCommands(ctx).SetRoleAccess(
                BuiltInAccess.Users.Id,
                new AccessSelection(BuiltInAccess.Users.Grants.Select(grant => grant.Grant.Name).ToList(), ["User.Manage"]));
        }

        Assert.False(await HasGrantAsync("User.Manage"));
        Assert.True(await HasGrantAsync("Role.Manage"));
    }

    [Fact]
    public async Task SetRoleAccess_ReplacesGrantsAndRestrictions()
    {
        await SeedUserWithAsync();
        await using var ctx = _factory.CreateContext();
        var commands = new AccessCommands(ctx);

        await commands.SetRoleAccess(BuiltInAccess.Users.Id, new AccessSelection(["User.Read"], ["Session.Read"]));
        await commands.SetRoleAccess(BuiltInAccess.Users.Id, new AccessSelection(["AuditLog.Read"], ["Role.Read"]));

        await using var read = _factory.CreateContext();
        var found = await new RoleQueries(read).GetRoleById(BuiltInAccess.Users.Id);
        var role = found.Match(() => throw new InvalidOperationException(), result => result.Role);
        Assert.Equal(["AuditLog.Read"], role.GrantNames);
        Assert.Equal(["Role.Read"], role.RestrictionNames);
    }

    [Fact]
    public async Task SetUserAccess_ReplacesTheAssignedRoles()
    {
        var userId = await SeedUserWithAsync(BuiltInAccess.Users, BuiltInAccess.Developers);

        await SetUserAccessAsync(userId, [BuiltInAccess.Users.Id, BuiltInAccess.Administrators.Id], [], []);

        var access = await AccessOfAsync(userId);
        Assert.Equal(
            [DomainConstants.AdministratorsRole, DomainConstants.UsersRole],
            access.Roles.Select(role => role.Name).Order().ToList());
    }

    [Fact]
    public async Task SetUserAccess_UnknownGrant_Throws()
    {
        var userId = await SeedUserWithAsync();
        await using var ctx = _factory.CreateContext();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new AccessCommands(ctx).SetUserAccess(userId, [], new AccessSelection(["Nope.Read"], [])));
    }

    [Fact]
    public async Task SetUserAccess_UnknownRestriction_Throws()
    {
        var userId = await SeedUserWithAsync();
        await using var ctx = _factory.CreateContext();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new AccessCommands(ctx).SetUserAccess(userId, [], new AccessSelection([], ["Nope.Read"])));
    }

    [Fact]
    public async Task ListRoles_ReturnsEveryRoleOrderedByNameWithItsAccess()
    {
        await SeedUserWithAsync();
        await using var ctx = _factory.CreateContext();
        await new AccessCommands(ctx).SetRoleAccess(BuiltInAccess.Users.Id, new AccessSelection([], ["User.Read"]));

        await using var read = _factory.CreateContext();
        var roles = await new RoleQueries(read).ListRoles();

        Assert.Equal(
            [DomainConstants.AdministratorsRole, DomainConstants.DevelopersRole, DomainConstants.UsersRole],
            roles.Select(result => result.Role.Name).ToList());
        Assert.Equal(["User.Read"], roles[2].Role.RestrictionNames);
        Assert.Equal(BuiltInAccess.Administrators.Id, roles[0].Id);
    }

    [Fact]
    public async Task GetRoleById_UnknownId_ReturnsNone()
    {
        await using var ctx = _factory.CreateContext();

        Assert.Equal(None, await new RoleQueries(ctx).GetRoleById(Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateRole_NewName_PersistsAndReturnsItsId()
    {
        await using var ctx = _factory.CreateContext();

        var created = await new RoleCommands(ctx).Create("Support");

        var id = created.Match(() => throw new InvalidOperationException(), value => value);
        await using var read = _factory.CreateContext();
        var found = await new RoleQueries(read).GetRoleById(id);
        Assert.Equal("Support", found.Match(() => "", result => result.Role.Name));
    }

    [Fact]
    public async Task CreateRole_ExistingNameIgnoringCase_ReturnsNone()
    {
        await SeedUserWithAsync();
        await using var ctx = _factory.CreateContext();

        Assert.Equal(None, await new RoleCommands(ctx).Create("administrators"));
    }

    [Fact]
    public async Task DeleteRole_HidesTheRoleAndItsGrantsFromUsers()
    {
        var userId = await SeedUserWithAsync(BuiltInAccess.Users);
        Guid roleId;
        await using (var ctx = _factory.CreateContext())
        {
            roleId = (await new RoleCommands(ctx).Create("Support")).Match(() => Guid.Empty, value => value);
            await new AccessCommands(ctx).SetRoleAccess(roleId, new AccessSelection(["Role.Manage"], []));
            await new AccessCommands(ctx).SetUserAccess(
                userId,
                [BuiltInAccess.Users.Id, roleId],
                AccessSelection.Empty);
        }

        Assert.True(await HasGrantAsync("Role.Manage"));

        await using (var ctx = _factory.CreateContext())
        {
            await new RoleCommands(ctx).Delete(roleId);
        }

        Assert.False(await HasGrantAsync("Role.Manage"));
        await using var read = _factory.CreateContext();
        Assert.Equal(None, await new RoleQueries(read).GetRoleById(roleId));
    }

    [Fact]
    public async Task Restrictions_CanBeAddedAndFoundThroughTheRepository()
    {
        Guid id;
        await using (var ctx = _factory.CreateContext())
        {
            var restriction = new Restriction("custom");
            await ((IRepository<Restriction, Guid>)ctx).AddAsync(restriction);
            id = ctx.GetKey(restriction);
            await ctx.SaveChangesAsync();
        }

        await using var read = _factory.CreateContext();
        var found = await ((IRepository<Restriction, Guid>)read).TryFindAsync(id);
        Assert.Equal("custom", found.Match(() => "", restriction => restriction.Name));
        Assert.Equal(1, await read.Restrictions.CountAsync());
    }
}
