using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class BuiltInRolesTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static IReadOnlyList<string> ExpectedGrantNames(BuiltInRole role) =>
        role.Grants.Select(grant => grant.Grant.Name).Order().ToList();

    private static IReadOnlyList<string> GrantNames(Role role) =>
        role.Grants.Match(() => (IReadOnlyList<string>)[], grants => grants.Select(g => g.Name).Order().ToList());

    private static IRoleQueries.RoleResult Found(Option<IRoleQueries.RoleResult> result) =>
        result.Match(() => throw new InvalidOperationException("The role was not seeded."), role => role);

    [Fact]
    public async Task EnsureBuiltInRoles_SeedsAdministratorsWithAllGrants()
    {
        await using (var seed = _factory.CreateContext())
        {
            await seed.EnsureBuiltInRolesAsync();
        }

        await using var ctx = _factory.CreateContext();
        var found = await new RoleQueries(ctx).GetRoleByName(DomainConstants.AdministratorsRole);

        var administrators = Found(found);
        Assert.Equal(BuiltInAccess.Administrators.Id, administrators.Id);
        Assert.Equal(ExpectedGrantNames(BuiltInAccess.Administrators), GrantNames(administrators.Role));
    }

    [Fact]
    public async Task EnsureBuiltInRoles_SeedsUsersWithReadOnEveryArea()
    {
        await using (var seed = _factory.CreateContext())
        {
            await seed.EnsureBuiltInRolesAsync();
        }

        await using var ctx = _factory.CreateContext();
        var found = await new RoleQueries(ctx).GetRoleByName(DomainConstants.UsersRole);

        var users = Found(found);
        Assert.Equal(BuiltInAccess.Users.Id, users.Id);
        Assert.Equal(ExpectedGrantNames(BuiltInAccess.Users), GrantNames(users.Role));
    }

    [Fact]
    public void BuiltInAccess_KeepsTheAuditLogAwayFromUsers()
    {
        var names = BuiltInAccess.Users.Grants.Select(grant => grant.Grant.Name).ToList();

        Assert.DoesNotContain("AuditLog.Read", names);
        Assert.Contains("User.Read", names);
        Assert.Contains("AuditLog.Read", BuiltInAccess.Administrators.Grants.Select(grant => grant.Grant.Name));
        Assert.Contains("AuditLog.Read", BuiltInAccess.Developers.Grants.Select(grant => grant.Grant.Name));
    }

    [Fact]
    public async Task EnsureBuiltInRoles_SeedsDevelopersWithConfigurationReadAndRawDatabaseManage()
    {
        await using (var seed = _factory.CreateContext())
        {
            await seed.EnsureBuiltInRolesAsync();
        }

        await using var ctx = _factory.CreateContext();
        var found = await new RoleQueries(ctx).GetRoleByName(DomainConstants.DevelopersRole);

        var developers = Found(found);
        Assert.Equal(BuiltInAccess.Developers.Id, developers.Id);
        Assert.Contains("Configuration.Read", GrantNames(developers.Role));
        Assert.Contains("RawDatabase.Manage", GrantNames(developers.Role));
        Assert.Equal(ExpectedGrantNames(BuiltInAccess.Developers), GrantNames(developers.Role));
    }

    [Fact]
    public async Task EnsureBuiltInRoles_KeepsGrantEditsMadeToABuiltInRole()
    {
        await using var ctx = _factory.CreateContext();
        await ctx.EnsureBuiltInRolesAsync();
        var configurationRead = BuiltInAccess.Grants.Single(grant => grant.Grant.Name == "Configuration.Read");
        var userRead = BuiltInAccess.Grants.Single(grant => grant.Grant.Name == "User.Read");
        var roleGrants = ctx.Set<Dictionary<string, object>>("RoleGrants");
        await roleGrants.AddAsync(new Dictionary<string, object>
        {
            ["RoleId"] = BuiltInAccess.Users.Id,
            ["GrantId"] = configurationRead.Id
        });
        roleGrants.RemoveRange(await roleGrants
            .Where(roleGrant => EF.Property<Guid>(roleGrant, "RoleId") == BuiltInAccess.Users.Id
                                && EF.Property<Guid>(roleGrant, "GrantId") == userRead.Id)
            .ToListAsync());
        await ctx.SaveChangesAsync();

        await ctx.EnsureBuiltInRolesAsync();

        var users = Found(await new RoleQueries(ctx).GetRoleByName(DomainConstants.UsersRole));
        Assert.Contains("Configuration.Read", GrantNames(users.Role));
        Assert.DoesNotContain("User.Read", GrantNames(users.Role));
    }

    [Fact]
    public async Task EnsureBuiltInRoles_GivesANewlyAddedBuiltInGrantToTheRolesThatListIt()
    {
        await using (var seed = _factory.CreateContext())
        {
            await seed.EnsureBuiltInRolesAsync();
            var sessionRead = BuiltInAccess.Grants.Single(grant => grant.Grant.Name == "Session.Read");
            await seed.Grants
                .Where(grant => EF.Property<Guid>(grant, "Id") == sessionRead.Id)
                .ExecuteDeleteAsync();
        }

        await using var ctx = _factory.CreateContext();
        await ctx.EnsureBuiltInRolesAsync();

        var users = Found(await new RoleQueries(ctx).GetRoleByName(DomainConstants.UsersRole));
        Assert.Equal(ExpectedGrantNames(BuiltInAccess.Users), GrantNames(users.Role));
    }

    [Fact]
    public async Task EnsureBuiltInRoles_SeedsOneRestrictionPerBuiltInGrant()
    {
        await using var ctx = _factory.CreateContext();

        await ctx.EnsureBuiltInRolesAsync();
        await ctx.EnsureBuiltInRolesAsync();

        var names = await ctx.Restrictions.Select(restriction => restriction.Name).ToListAsync();
        Assert.Equal(BuiltInAccess.GrantNames.Order().ToList(), names.Order().ToList());
    }

    [Fact]
    public async Task EnsureBuiltInRoles_IsIdempotent()
    {
        await using var ctx = _factory.CreateContext();

        await ctx.EnsureBuiltInRolesAsync();
        await ctx.EnsureBuiltInRolesAsync();

        Assert.Equal(3, await ctx.Roles.CountAsync());
        Assert.Equal(41, await ctx.Grants.CountAsync());
        Assert.Equal(BuiltInAccess.Roles.Sum(role => role.Grants.Count), await ctx.Set<Dictionary<string, object>>("RoleGrants").CountAsync());
    }

    [Fact]
    public async Task AssignBuiltIn_GrantsTheRoleAndItsGrants()
    {
        Guid userId;
        await using (var seed = _factory.CreateContext())
        {
            await seed.EnsureBuiltInRolesAsync();
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "elevated@test.com");
            await new RoleCommands(seed).AssignBuiltIn(userId, BuiltInAccess.Administrators);
        }

        await using var ctx = _factory.CreateContext();
        var roles = await new RoleQueries(ctx).GetRolesByUserId(userId);

        var role = Assert.Single(roles);
        Assert.Equal(DomainConstants.AdministratorsRole, role.Name);
        Assert.Equal(ExpectedGrantNames(BuiltInAccess.Administrators), GrantNames(role));
    }

    [Fact]
    public async Task AssignBuiltIn_CalledTwice_AssignsOnce()
    {
        await using var ctx = _factory.CreateContext();
        await ctx.EnsureBuiltInRolesAsync();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(ctx, "twice@test.com");
        var commands = new RoleCommands(ctx);

        await commands.AssignBuiltIn(userId, BuiltInAccess.Administrators);
        await commands.AssignBuiltIn(userId, BuiltInAccess.Administrators);

        Assert.Equal(1, await ctx.Set<Dictionary<string, object>>("UserRoles").CountAsync());
    }

    [Fact]
    public async Task AssignBuiltIn_WithoutSeededRoles_SeedsThemAndMakesRoleVisible()
    {
        await using var ctx = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(ctx, "unseeded@test.com");
        var commands = new RoleCommands(ctx);

        await commands.AssignBuiltIn(userId, BuiltInAccess.Administrators);

        var roles = await new RoleQueries(ctx).GetRolesByUserId(userId);
        Assert.Equal([DomainConstants.AdministratorsRole], roles.Select(role => role.Name));
    }

    [Fact]
    public async Task EnsureUsersHaveUsersRole_AssignsOnlyToUsersWithoutItAndSkipsBuiltInUsers()
    {
        await using var ctx = _factory.CreateContext();
        await ctx.EnsureBuiltInUsersAsync();
        await ctx.EnsureBuiltInRolesAsync();
        var first = await DatabaseSeedHelper.SeedHumanUserAsync(ctx, "first@test.com");
        var second = await DatabaseSeedHelper.SeedHumanUserAsync(ctx, "second@test.com");
        await new RoleCommands(ctx).AssignBuiltIn(second, BuiltInAccess.Users);

        await ctx.EnsureUsersHaveUsersRoleAsync();
        await ctx.EnsureUsersHaveUsersRoleAsync();

        var assigned = await ctx.Set<Dictionary<string, object>>("UserRoles")
            .Select(userRole => EF.Property<Guid>(userRole, "UserId"))
            .ToListAsync();
        Assert.Equal(new[] { first, second }.Order(), assigned.Order());
    }
}
