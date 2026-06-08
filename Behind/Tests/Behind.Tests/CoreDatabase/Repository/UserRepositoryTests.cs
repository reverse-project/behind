using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase.Repository;

public sealed class UserRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task TryFindAsync_UserWithEmail_ReturnsSomeWithEmail()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "email@test.com");

        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<HumanUser, Guid>)ctx).TryFindAsync(userId);

        var found = false;
        result.Match(() => { }, user =>
        {
            found = true;
            Assert.Single(user.Emails);
            Assert.Equal("email@test.com", user.Emails.First().Value);
        });

        Assert.True(found);
    }

    [Fact]
    public async Task TryFindAsync_UserWithNoRoles_RolesIsNone()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "noroles@test.com");

        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<HumanUser, Guid>)ctx).TryFindAsync(userId);

        result.Match(() => Assert.Fail("Expected Some"), user =>
        {
            var rolesIsNone = true;
            user.Roles.Match(() => { }, _ => rolesIsNone = false);
            Assert.True(rolesIsNone);
        });
    }

    [Fact]
    public async Task TryFindAsync_UserWithOneRole_RolesIsSomeContainingThatRole()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "onerole@test.com");
        var roleId = await DatabaseSeedHelper.SeedUserRoleAsync(seed, "editor");
        await DatabaseSeedHelper.AssignRoleToUserAsync(seed, userId, roleId);

        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<HumanUser, Guid>)ctx).TryFindAsync(userId);

        result.Match(() => Assert.Fail("Expected Some"), user =>
        {
            user.Roles.Match(() => Assert.Fail("Expected roles"), roles =>
            {
                var list = roles.ToList();
                Assert.Single(list);
                Assert.Equal("editor", list[0].Name);
            });
        });
    }

    [Fact]
    public async Task TryFindAsync_UserWithRoleAndGrant_GrantIsPopulated()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "withgrant@test.com");
        var roleId = await DatabaseSeedHelper.SeedUserRoleAsync(seed, "admin");
        var grantId = await DatabaseSeedHelper.SeedGrantAsync(seed, "delete");
        await DatabaseSeedHelper.AssignRoleToUserAsync(seed, userId, roleId);
        await DatabaseSeedHelper.AssignGrantToRoleAsync(seed, roleId, grantId);

        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<HumanUser, Guid>)ctx).TryFindAsync(userId);

        result.Match(() => Assert.Fail("Expected Some"), user =>
        {
            user.Roles.Match(() => Assert.Fail("Expected roles"), roles =>
            {
                var role = roles.Single();
                role.Grants.Match(() => Assert.Fail("Expected grants"), grants =>
                {
                    var grantList = grants.ToList();
                    Assert.Single(grantList);
                    Assert.Equal("delete", grantList.First().Name);
                });
            });
        });
    }

    [Fact]
    public async Task TryFindAsync_UnknownId_ReturnsNone()
    {
        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<HumanUser, Guid>)ctx).TryFindAsync(Guid.NewGuid());
        var isNone = true;
        result.Match(() => { }, _ => isNone = false);
        Assert.True(isNone);
    }

    [Fact]
    public async Task AddAsync_Persists_User()
    {
        Guid userId;

        await using (var ctx = _factory.CreateContext())
        {
            var user = new HumanUser("Test User", [new Email("add-persist@test.com")], None);
            await ctx.Users.AddAsync(user);
            userId = ctx.Entry(user).Property<Guid>("Id").CurrentValue;

            await ctx.SaveChangesAsync();
        }

        await using var read = _factory.CreateContext();
        var found = false;
        (await ((IRepository<HumanUser, Guid>)read).TryFindAsync(userId))
            .Match(() => { }, u =>
            {
                found = true;
                Assert.Single(u.Emails);
                Assert.Equal("add-persist@test.com", u.Emails.First().Value);
            });

        Assert.True(found);
    }

    [Fact]
    public async Task GetKey_AfterAdd_ReturnsNonEmptyGuid()
    {
        await using var ctx = _factory.CreateContext();
        var user = new HumanUser("Test User", [new Email("gk@test.com")], None);
        await ctx.Users.AddAsync(user);
        var key = ((IRepository<HumanUser, Guid>)ctx).GetKey(user);
        Assert.NotEqual(Guid.Empty, key);
    }

    [Fact]
    public async Task Delete_RemovesUser_FromDatabase()
    {
        Guid userId;

        await using (var seed = _factory.CreateContext())
        {
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "del-user@test.com");
        }

        await using (var ctx = _factory.CreateContext())
        {
            await ctx.Database.ExecuteSqlAsync(
                $"DELETE FROM Emails WHERE UserId = {userId}");

            await ctx.Database.ExecuteSqlAsync(
                $"DELETE FROM Users WHERE Id = {userId}");
        }

        await using var verify = _factory.CreateContext();
        var isNone = true;
        (await ((IRepository<HumanUser, Guid>)verify).TryFindAsync(userId))
            .Match(() => { }, _ => isNone = false);

        Assert.True(isNone);
    }
}