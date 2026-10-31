using Behind.Core.Database;
using Behind.Core.Database.Sqlite;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase.Repository;

public sealed class WallOfShameEntryRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    // -----------------------------------------------------------------------
    // TryFindAsync — User resolution
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TryFindAsync_SystemUserIdAsUserId_UserIsSystemUser()
    {
        await using var seed = _factory.CreateContext();
        var id = await DatabaseSeedHelper.SeedWallOfShameEntryAsync(
            seed,
            userId: DomainConstants.SystemUserId,
            givenById: DomainConstants.SystemUserId);

        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<WallOfShameEntry, Guid>)ctx).TryFindAsync(id);

        result.Match(() => Assert.Fail("Expected Some"), entry =>
            Assert.IsType<SystemUser>(entry.User));
    }

    [Fact]
    public async Task TryFindAsync_HumanUserIdAsUserId_UserIsHumanUserWithEmail()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "banned@test.com");
        var id = await DatabaseSeedHelper.SeedWallOfShameEntryAsync(
            seed,
            userId: userId,
            givenById: DomainConstants.SystemUserId);

        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<WallOfShameEntry, Guid>)ctx).TryFindAsync(id);

        result.Match(() => Assert.Fail("Expected Some"), entry =>
        {
            var hu = Assert.IsType<HumanUser>(entry.User);
            Assert.Contains(hu.Emails, e => e.Value == "banned@test.com");
        });
    }

    [Fact]
    public async Task TryFindAsync_SystemUserIdAsGivenById_GivenByIsSystemUser()
    {
        await using var seed = _factory.CreateContext();
        var id = await DatabaseSeedHelper.SeedWallOfShameEntryAsync(
            seed,
            userId: DomainConstants.SystemUserId,
            givenById: DomainConstants.SystemUserId);

        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<WallOfShameEntry, Guid>)ctx).TryFindAsync(id);

        result.Match(() => Assert.Fail("Expected Some"), entry =>
            Assert.IsType<SystemUser>(entry.GivenBy));
    }

    [Fact]
    public async Task TryFindAsync_HumanUserIdAsGivenById_GivenByIsHumanUserWithEmail()
    {
        await using var seed = _factory.CreateContext();
        var adminId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "admin@test.com");
        var id = await DatabaseSeedHelper.SeedWallOfShameEntryAsync(
            seed,
            userId: DomainConstants.SystemUserId,
            givenById: adminId);

        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<WallOfShameEntry, Guid>)ctx).TryFindAsync(id);

        result.Match(() => Assert.Fail("Expected Some"), entry =>
        {
            var hu = Assert.IsType<HumanUser>(entry.GivenBy);
            Assert.Contains(hu.Emails, e => e.Value == "admin@test.com");
        });
    }

    // -----------------------------------------------------------------------
    // TryFindAsync — Expires
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TryFindAsync_WithExpiresAt_ExpiresIsSome()
    {
        var expiry = new DateTimeOffset(2030, 6, 1, 0, 0, 0, TimeSpan.Zero);
        await using var seed = _factory.CreateContext();
        var id = await DatabaseSeedHelper.SeedWallOfShameEntryAsync(
            seed,
            userId: DomainConstants.SystemUserId,
            givenById: DomainConstants.SystemUserId,
            expiresAt: expiry);

        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<WallOfShameEntry, Guid>)ctx).TryFindAsync(id);

        result.Match(() => Assert.Fail("Expected Some"), entry =>
        {
            var found = false;
            entry.Expires.Match(() => { }, dt =>
            {
                found = true;
                Assert.Equal(expiry, dt);
            });
            Assert.True(found);
        });
    }

    [Fact]
    public async Task TryFindAsync_NoExpiresAt_ExpiresIsNone()
    {
        await using var seed = _factory.CreateContext();
        var id = await DatabaseSeedHelper.SeedWallOfShameEntryAsync(
            seed,
            userId: DomainConstants.SystemUserId,
            givenById: DomainConstants.SystemUserId);

        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<WallOfShameEntry, Guid>)ctx).TryFindAsync(id);

        result.Match(() => Assert.Fail("Expected Some"), entry =>
        {
            var isNone = true;
            entry.Expires.Match(() => { }, _ => isNone = false);
            Assert.True(isNone);
        });
    }

    // -----------------------------------------------------------------------
    // TryFindAsync — Reason
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TryFindAsync_PreservesReason()
    {
        await using var seed = _factory.CreateContext();
        var id = await DatabaseSeedHelper.SeedWallOfShameEntryAsync(
            seed,
            userId: DomainConstants.SystemUserId,
            givenById: DomainConstants.SystemUserId,
            reason: "flooding");

        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<WallOfShameEntry, Guid>)ctx).TryFindAsync(id);

        result.Match(() => Assert.Fail("Expected Some"), entry =>
            Assert.Equal("flooding", entry.Reason.Value));
    }

    // -----------------------------------------------------------------------
    // TryFindAsync — not found
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TryFindAsync_UnknownId_ReturnsNone()
    {
        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<WallOfShameEntry, Guid>)ctx).TryFindAsync(Guid.NewGuid());
        var isNone = true;
        result.Match(() => { }, _ => isNone = false);
        Assert.True(isNone);
    }

    // -----------------------------------------------------------------------
    // AddAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AddAsync_Persists_WallOfShameEntry()
    {
        Guid id;
        await using (var ctx = _factory.CreateContext())
        {
            await DatabaseSeedHelper.EnsureBuiltInUsersAsync(ctx);

            var entry = new WallOfShameEntry(new Reason("spamming"));
            var repo = (IRepository<WallOfShameEntry, Guid>)ctx;
            await repo.AddAsync(entry);
            ctx.Entry(entry).Property("UserId").CurrentValue = DomainConstants.SystemUserId;
            ctx.Entry(entry).Property("GivenById").CurrentValue = DomainConstants.SystemUserId;
            id = ctx.Entry(entry).Property<Guid>("Id").CurrentValue;
            await ctx.SaveChangesAsync();
        }

        await using var read = _factory.CreateContext();
        var found = false;
        (await ((IRepository<WallOfShameEntry, Guid>)read).TryFindAsync(id))
            .Match(() => { }, e =>
            {
                found = true;
                Assert.Equal("spamming", e.Reason.Value);
            });
        Assert.True(found);
    }

    // -----------------------------------------------------------------------
    // GetKey
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetKey_AfterAdd_ReturnsNonEmptyGuid()
    {
        await using var ctx = _factory.CreateContext();

        await DatabaseSeedHelper.EnsureBuiltInUsersAsync(ctx);

        var entry = new WallOfShameEntry(new Reason("test"));
        await ctx.WallOfShameEntries.AddAsync(entry);
        ctx.Entry(entry).Property("UserId").CurrentValue = DomainConstants.SystemUserId;
        ctx.Entry(entry).Property("GivenById").CurrentValue = DomainConstants.SystemUserId;

        var key = ((IRepository<WallOfShameEntry, Guid>)ctx).GetKey(entry);
        Assert.NotEqual(Guid.Empty, key);
    }

    // -----------------------------------------------------------------------
    // Delete
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Delete_RemovesEntry_FromDatabase()
    {
        Guid id;
        await using (var seed = _factory.CreateContext())
        {
            id = await DatabaseSeedHelper.SeedWallOfShameEntryAsync(
                seed,
                userId: DomainConstants.SystemUserId,
                givenById: DomainConstants.SystemUserId);
        }

        await using (var ctx = _factory.CreateContext())
        {
            var tracked = await ctx.WallOfShameEntries
                .Where(e => EF.Property<Guid>(e, "Id") == id)
                .FirstAsync();
            ((IRepository<WallOfShameEntry, Guid>)ctx).Delete(tracked);
            await ctx.SaveChangesAsync();
        }

        await using var verify = _factory.CreateContext();
        var isNone = true;
        (await ((IRepository<WallOfShameEntry, Guid>)verify).TryFindAsync(id))
            .Match(() => { }, _ => isNone = false);
        Assert.True(isNone);
    }
}
