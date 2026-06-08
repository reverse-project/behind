using Behind.Core.Database;
using Behind.Core.Database.Sqlite;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase.Repository;

public sealed class UserRoleRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task TryFindAsync_RoleWithNoGrants_ReturnsSomeWithGrantsNone()
    {
        await using var seed = _factory.CreateContext();
        var roleId = await DatabaseSeedHelper.SeedUserRoleAsync(seed, "no-grants-role");

        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<UserRole, Guid>)ctx).TryFindAsync(roleId);

        var found = false;
        result.Match(() => { }, role =>
        {
            found = true;
            Assert.Equal("no-grants-role", role.Name);
            var grantsIsNone = true;
            role.Grants.Match(() => { }, _ => grantsIsNone = false);
            Assert.True(grantsIsNone);
        });
        Assert.True(found);
    }

    [Fact]
    public async Task TryFindAsync_RoleWithOneGrant_ReturnsPopulatedGrant()
    {
        await using var seed = _factory.CreateContext();
        var roleId = await DatabaseSeedHelper.SeedUserRoleAsync(seed, "one-grant-role");
        var grantId = await DatabaseSeedHelper.SeedGrantAsync(seed, "write");
        await DatabaseSeedHelper.AssignGrantToRoleAsync(seed, roleId, grantId);

        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<UserRole, Guid>)ctx).TryFindAsync(roleId);

        var found = false;
        result.Match(() => { }, role =>
        {
            found = true;
            var grantFound = false;
            role.Grants.Match(() => { }, grants =>
            {
                grantFound = true;
                Assert.Single(grants);
                Assert.Contains(grants, g => g.Name == "write");
            });
            Assert.True(grantFound);
        });
        Assert.True(found);
    }

    [Fact]
    public async Task TryFindAsync_RoleWithMultipleGrants_ReturnsAllGrants()
    {
        await using var seed = _factory.CreateContext();
        var roleId = await DatabaseSeedHelper.SeedUserRoleAsync(seed, "multi-grant-role");
        var g1 = await DatabaseSeedHelper.SeedGrantAsync(seed, "read-mg");
        var g2 = await DatabaseSeedHelper.SeedGrantAsync(seed, "write-mg");
        await DatabaseSeedHelper.AssignGrantToRoleAsync(seed, roleId, g1);
        await DatabaseSeedHelper.AssignGrantToRoleAsync(seed, roleId, g2);

        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<UserRole, Guid>)ctx).TryFindAsync(roleId);

        result.Match(() => Assert.Fail("Expected Some"), role =>
        {
            role.Grants.Match(() => Assert.Fail("Expected grants"), grants =>
            {
                var list = grants.ToList();
                Assert.Equal(2, list.Count);
            });
        });
    }

    [Fact]
    public async Task TryFindAsync_UnknownId_ReturnsNone()
    {
        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<UserRole, Guid>)ctx).TryFindAsync(Guid.NewGuid());
        var isNone = true;
        result.Match(() => { }, _ => isNone = false);
        Assert.True(isNone);
    }

    [Fact]
    public async Task AddAsync_Persists_Role()
    {
        Guid id;
        await using (var ctx = _factory.CreateContext())
        {
            var role = new UserRole("persisted-role");
            var repo = (IRepository<UserRole, Guid>)ctx;
            await repo.AddAsync(role);
            id = ctx.Entry(role).Property<Guid>("Id").CurrentValue;
            await ctx.SaveChangesAsync();
        }

        await using var read = _factory.CreateContext();
        var found = false;
        (await ((IRepository<UserRole, Guid>)read).TryFindAsync(id))
            .Match(() => { }, r => { found = true; Assert.Equal("persisted-role", r.Name); });
        Assert.True(found);
    }

    [Fact]
    public async Task GetKey_AfterAdd_ReturnsNonEmptyGuid()
    {
        await using var ctx = _factory.CreateContext();
        var role = new UserRole("gk-role");
        await ctx.Roles.AddAsync(role);
        var key = ((IRepository<UserRole, Guid>)ctx).GetKey(role);
        Assert.NotEqual(Guid.Empty, key);
    }

    [Fact]
    public async Task Delete_RemovesRole_FromDatabase()
    {
        Guid id;
        await using (var seed = _factory.CreateContext())
            id = await DatabaseSeedHelper.SeedUserRoleAsync(seed, "del-role");

        await using (var ctx = _factory.CreateContext())
        {
            var tracked = await ctx.Roles
                .Where(r => EF.Property<Guid>(r, "Id") == id)
                .FirstAsync();
            ((IRepository<UserRole, Guid>)ctx).Delete(tracked);
            await ctx.SaveChangesAsync();
        }

        await using var verify = _factory.CreateContext();
        var isNone = true;
        (await ((IRepository<UserRole, Guid>)verify).TryFindAsync(id))
            .Match(() => { }, _ => isNone = false);
        Assert.True(isNone);
    }
}
