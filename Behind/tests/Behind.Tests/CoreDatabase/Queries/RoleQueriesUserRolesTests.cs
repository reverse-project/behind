using Behind.Core.Database.Queries;
using Behind.Tests.Helpers;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class RoleQueriesUserRolesTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<IReadOnlyList<Role>> RolesOfAsync(Guid userId)
    {
        await using var ctx = _factory.CreateContext();
        return await new RoleQueries(ctx).GetRolesByUserId(userId);
    }

    private static IReadOnlyList<string> GrantNames(Role role) =>
        role.Grants.Match(() => (IReadOnlyList<string>)[], grants => grants.Select(g => g.Name).Order().ToList());

    [Fact]
    public async Task GetRolesByUserId_UserWithRolesAndGrants_ReturnsRolesWithTheirGrants()
    {
        Guid userId;
        await using (var seed = _factory.CreateContext())
        {
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "roles-owner@test.com");
            var admin = await DatabaseSeedHelper.SeedUserRoleAsync(seed, "role-admin");
            var viewer = await DatabaseSeedHelper.SeedUserRoleAsync(seed, "role-viewer");
            var read = await DatabaseSeedHelper.SeedGrantAsync(seed, "role-read");
            var write = await DatabaseSeedHelper.SeedGrantAsync(seed, "role-write");
            await DatabaseSeedHelper.AssignRoleToUserAsync(seed, userId, admin);
            await DatabaseSeedHelper.AssignRoleToUserAsync(seed, userId, viewer);
            await DatabaseSeedHelper.AssignGrantToRoleAsync(seed, admin, read);
            await DatabaseSeedHelper.AssignGrantToRoleAsync(seed, admin, write);
        }

        var roles = await RolesOfAsync(userId);

        Assert.Equal(["role-admin", "role-viewer"], roles.Select(r => r.Name).Order());
        Assert.Equal(["role-read", "role-write"], GrantNames(roles.Single(r => r.Name == "role-admin")));
        Assert.Empty(GrantNames(roles.Single(r => r.Name == "role-viewer")));
    }

    [Fact]
    public async Task GetRoleByName_RoleWithGrants_ReturnsRoleWithItsGrants()
    {
        await using (var seed = _factory.CreateContext())
        {
            var role = await DatabaseSeedHelper.SeedUserRoleAsync(seed, "named-role");
            var other = await DatabaseSeedHelper.SeedUserRoleAsync(seed, "named-other");
            var read = await DatabaseSeedHelper.SeedGrantAsync(seed, "named-read");
            var write = await DatabaseSeedHelper.SeedGrantAsync(seed, "named-write");
            await DatabaseSeedHelper.AssignGrantToRoleAsync(seed, role, read);
            await DatabaseSeedHelper.AssignGrantToRoleAsync(seed, other, write);
        }

        await using var ctx = _factory.CreateContext();
        var found = await new RoleQueries(ctx).GetRoleByName("NAMED-ROLE");

        var result = found.Match(() => throw new Xunit.Sdk.XunitException("None"), r => r);
        Assert.Equal("named-role", result.Role.Name);
        Assert.Equal(["named-read"], GrantNames(result.Role));
    }

    [Fact]
    public async Task GetRoleByName_UnknownRole_ReturnsNone()
    {
        await using var ctx = _factory.CreateContext();

        Assert.Equal(None, await new RoleQueries(ctx).GetRoleByName("missing-role"));
    }

    [Fact]
    public async Task GetRolesByUserId_OtherUsersRoles_AreNotReturned()
    {
        Guid userId;
        await using (var seed = _factory.CreateContext())
        {
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "roles-none@test.com");
            var otherId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "roles-other@test.com");
            var role = await DatabaseSeedHelper.SeedUserRoleAsync(seed, "role-other");
            await DatabaseSeedHelper.AssignRoleToUserAsync(seed, otherId, role);
        }

        Assert.Empty(await RolesOfAsync(userId));
    }
}
