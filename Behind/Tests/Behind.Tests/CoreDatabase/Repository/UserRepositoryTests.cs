using Behind.Core.Database;
using Behind.Core.Database.Sqlite;
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
        using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "email@test.com");

        using var ctx = _factory.CreateContext();
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
        using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "noroles@test.com");

        using var ctx = _factory.CreateContext();
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
        using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "onerole@test.com");
        var roleId = await DatabaseSeedHelper.SeedUserRoleAsync(seed, "editor");
        await DatabaseSeedHelper.AssignRoleToUserAsync(seed, userId, roleId);

        using var ctx = _factory.CreateContext();
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
        using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "withgrant@test.com");
        var roleId = await DatabaseSeedHelper.SeedUserRoleAsync(seed, "admin");
        var grantId = await DatabaseSeedHelper.SeedGrantAsync(seed, "delete");
        await DatabaseSeedHelper.AssignRoleToUserAsync(seed, userId, roleId);
        await DatabaseSeedHelper.AssignGrantToRoleAsync(seed, roleId, grantId);

        using var ctx = _factory.CreateContext();
        var result = await ((IRepository<HumanUser, Guid>)ctx).TryFindAsync(userId);

        result.Match(() => Assert.Fail("Expected Some"), user =>
        {
            user.Roles.Match(() => Assert.Fail("Expected roles"), roles =>
            {
                var role = roles.Single();
                role.Grants.Match(() => Assert.Fail("Expected grants"), grants =>
                {
                    Assert.Single(grants);
                    Assert.Equal("delete", grants.First().Name);
                });
            });
        });
    }

    [Fact]
    public async Task TryFindAsync_UnknownId_ReturnsNone()
    {
        using var ctx = _factory.CreateContext();
        var result = await ((IRepository<HumanUser, Guid>)ctx).TryFindAsync(Guid.NewGuid());
        var isNone = true;
        result.Match(() => { }, _ => isNone = false);
        Assert.True(isNone);
    }

    [Fact]
    public async Task AddAsync_Persists_User()
    {
        Guid userId;
        using (var ctx = _factory.CreateContext())
        {
            var user = new HumanUser("Test User", [new Email("add-persist@test.com")], None);
            await ctx.Users.AddAsync(user);
            userId = ctx.Entry(user).Property<Guid>("Id").CurrentValue;

            var email = new Email("add-persist@test.com");
            await ctx.Emails.AddAsync(email);
            ctx.Entry(email).Property("UserId").CurrentValue = userId;

            await ctx.SaveChangesAsync();
        }

        using var read = _factory.CreateContext();
        var found = false;
        (await ((IRepository<HumanUser, Guid>)read).TryFindAsync(userId))
            .Match(() => { }, u =>
            {
                found = true;
                Assert.Equal("add-persist@test.com", u.Emails.First().Value);
            });
        Assert.True(found);
    }

    [Fact]
    public async Task GetKey_AfterAdd_ReturnsNonEmptyGuid()
    {
        using var ctx = _factory.CreateContext();
        var user = new HumanUser("Test User", [new Email("gk@test.com")], None);
        await ctx.Users.AddAsync(user);
        var key = ((IRepository<HumanUser, Guid>)ctx).GetKey(user);
        Assert.NotEqual(Guid.Empty, key);
    }

    [Fact]
    public async Task Delete_RemovesUser_FromDatabase()
    {
        Guid userId;
        using (var seed = _factory.CreateContext())
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "del-user@test.com");

        using (var ctx = _factory.CreateContext())
        {
            // Two FK relationships point from Emails to Users:
            //   Emails.UserId      → Users.Id  (Cascade)
            //   Emails.HumanUserId → Users.Id  (Restrict / no action)
            // The second one has no cascade, so we must remove Emails first.
            // We use raw SQL so we never materialise a HumanUser (whose
            // parameterless constructor throws).
            await ctx.Database.ExecuteSqlAsync(
                $"DELETE FROM Emails WHERE HumanUserId = {userId}");
            await ctx.Database.ExecuteSqlAsync(
                $"DELETE FROM Emails WHERE UserId = {userId}");

            // Attach a valid stub purely so the IRepository.Delete pattern works.
            var stub = new HumanUser("Test User", [new Email("stub@stub.com")], None);
            var entry = ctx.Entry(stub);
            entry.Property<Guid>("Id").CurrentValue = userId;
            // Detach any stub emails EF Core picked up via the navigation
            foreach (var e in ctx.ChangeTracker.Entries<Email>().ToList())
                e.State = EntityState.Detached;
            entry.State = EntityState.Deleted;
            await ctx.SaveChangesAsync();
        }

        using var verify = _factory.CreateContext();
        var isNone = true;
        (await ((IRepository<HumanUser, Guid>)verify).TryFindAsync(userId))
            .Match(() => { }, _ => isNone = false);
        Assert.True(isNone);
    }
}
