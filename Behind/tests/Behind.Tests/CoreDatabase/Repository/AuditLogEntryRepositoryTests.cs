using System.Text.Json;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase.Repository;

public sealed class AuditLogEntryRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    // -----------------------------------------------------------------------
    // TryFindAsync — User resolution
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TryFindAsync_NoUserId_UserIsGuestUser()
    {
        await using var seed = _factory.CreateContext();
        var id = await DatabaseSeedHelper.SeedAuditLogEntryAsync(seed);

        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<AuditLogEntry, Guid>)ctx).TryFindAsync(id);

        result.Match(() => Assert.Fail("Expected Some"), entry =>
            Assert.IsType<GuestUser>(entry.User));
    }

    [Fact]
    public async Task TryFindAsync_SystemUserId_UserIsSystemUser()
    {
        await using var seed = _factory.CreateContext();
        var id = await DatabaseSeedHelper.SeedAuditLogEntryAsync(seed, userId: DomainConstants.SystemUserId);

        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<AuditLogEntry, Guid>)ctx).TryFindAsync(id);

        result.Match(() => Assert.Fail("Expected Some"), entry =>
            Assert.IsType<SystemUser>(entry.User));
    }

    [Fact]
    public async Task TryFindAsync_HumanUserId_UserIsHumanUserWithEmail()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "audit-user@test.com");
        var id = await DatabaseSeedHelper.SeedAuditLogEntryAsync(seed, userId: userId);

        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<AuditLogEntry, Guid>)ctx).TryFindAsync(id);

        result.Match(() => Assert.Fail("Expected Some"), entry =>
        {
            var hu = Assert.IsType<HumanUser>(entry.User);
            Assert.Contains(hu.Emails, e => e.Value == "audit-user@test.com");
        });
    }

    // -----------------------------------------------------------------------
    // TryFindAsync — BehalfOf resolution
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TryFindAsync_BehalfOfSet_BehalfOfIsSomeHumanUser()
    {
        await using var seed = _factory.CreateContext();
        var behalfUserId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "behalf@test.com");
        var id = await DatabaseSeedHelper.SeedAuditLogEntryAsync(seed, behalfOfId: behalfUserId);

        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<AuditLogEntry, Guid>)ctx).TryFindAsync(id);

        result.Match(() => Assert.Fail("Expected Some"), entry =>
        {
            var found = false;
            entry.BehalfOf.Match(() => { }, user =>
            {
                found = true;
                var hu = Assert.IsType<HumanUser>(user);
                Assert.Contains(hu.Emails, e => e.Value == "behalf@test.com");
            });
            Assert.True(found);
        });
    }

    [Fact]
    public async Task TryFindAsync_NoBehalfOf_BehalfOfIsNone()
    {
        await using var seed = _factory.CreateContext();
        var id = await DatabaseSeedHelper.SeedAuditLogEntryAsync(seed);

        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<AuditLogEntry, Guid>)ctx).TryFindAsync(id);

        result.Match(() => Assert.Fail("Expected Some"), entry =>
        {
            var isNone = true;
            entry.BehalfOf.Match(() => { }, _ => isNone = false);
            Assert.True(isNone);
        });
    }

    // -----------------------------------------------------------------------
    // TryFindAsync — not found
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TryFindAsync_UnknownId_ReturnsNone()
    {
        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<AuditLogEntry, Guid>)ctx).TryFindAsync(Guid.NewGuid());
        var isNone = true;
        result.Match(() => { }, _ => isNone = false);
        Assert.True(isNone);
    }

    // -----------------------------------------------------------------------
    // AddAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AddAsync_Persists_AuditLogEntry()
    {
        var ts = DateTimeOffset.UtcNow;
        Guid id;
        await using (var ctx = _factory.CreateContext())
        {
            var entry = new AuditLogEntry("User", "u-1", new AuditLogEntry.Created(), ts);
            var repo = (IRepository<AuditLogEntry, Guid>)ctx;
            await repo.AddAsync(entry);
            id = ctx.Entry(entry).Property<Guid>("Id").CurrentValue;
            await ctx.SaveChangesAsync();
        }

        await using var read = _factory.CreateContext();
        var found = false;
        (await ((IRepository<AuditLogEntry, Guid>)read).TryFindAsync(id))
            .Match(() => { }, e =>
            {
                found = true;
                Assert.Equal("User", e.EntityName);
                Assert.IsType<AuditLogEntry.Created>(e.Action);
            });
        Assert.True(found);
    }
    
    [Fact]
    public async Task AddAsync_PersistsUpdatedAuditAction()
    {
        var ts = DateTimeOffset.UtcNow;
        Guid id;
        var action = new AuditLogEntry.Updated([
            new PropertyDifference("Name", "Old", "New")
        ]);

        await using (var ctx = _factory.CreateContext())
        {
            var entry = new AuditLogEntry("User", "u-1", action, ts);
            var repo = (IRepository<AuditLogEntry, Guid>)ctx;
            await repo.AddAsync(entry);
            id = ctx.Entry(entry).Property<Guid>("Id").CurrentValue;
            await ctx.SaveChangesAsync();
        }

        await using var read = _factory.CreateContext();
        var found = false;
        (await ((IRepository<AuditLogEntry, Guid>)read).TryFindAsync(id))
            .Match(() => { }, e =>
            {
                found = true;
                var updated = Assert.IsType<AuditLogEntry.Updated>(e.Action);
                var difference = Assert.Single(updated.Differences);
                Assert.Equal("Name", difference.PropertyName);
                Assert.Equal("Old", difference.OldValue?.ToString());
                Assert.Equal("New", difference.NewValue?.ToString());
            });

        Assert.True(found);
    }

    [Fact]
    public async Task AddAsync_StoresCreatedAuditActionAsPlainText()
    {
        Guid id;

        await using (var ctx = _factory.CreateContext())
        {
            var entry = new AuditLogEntry("User", "u-1", new AuditLogEntry.Created(), DateTimeOffset.UtcNow);
            await ctx.AuditLogEntries.AddAsync(entry);
            id = ctx.Entry(entry).Property<Guid>("Id").CurrentValue;
            await ctx.SaveChangesAsync();
        }

        await using var read = _factory.CreateContext();

        string storedAction = await read.Database
            .SqlQueryRaw<string>(
                """
                SELECT Action AS Value
                FROM AuditLogEntries
                WHERE Id = {0}
                """,
                id)
            .SingleAsync();

        Assert.Equal(nameof(AuditLogEntry.Created), storedAction);
    }

    [Fact]
    public async Task AddAsync_StoresUpdatedAuditActionAsJson()
    {
        Guid id;

        await using (var ctx = _factory.CreateContext())
        {
            var action = new AuditLogEntry.Updated([
                new PropertyDifference("Name", "Old", "New")
            ]);
            var entry = new AuditLogEntry("User", "u-1", action, DateTimeOffset.UtcNow);
            await ctx.AuditLogEntries.AddAsync(entry);
            id = ctx.Entry(entry).Property<Guid>("Id").CurrentValue;
            await ctx.SaveChangesAsync();
        }

        await using var read = _factory.CreateContext();

        string storedAction = await read.Database
            .SqlQueryRaw<string>(
                """
                SELECT Action AS Value
                FROM AuditLogEntries
                WHERE Id = {0}
                """,
                id)
            .SingleAsync();

        Assert.StartsWith("{", storedAction.TrimStart());

        using JsonDocument document = JsonDocument.Parse(storedAction);
        JsonElement root = document.RootElement;
        JsonElement difference = root.GetProperty("differences")[0];

        Assert.Equal(nameof(AuditLogEntry.Updated), root.GetProperty("value").GetString());
        Assert.Equal("Name", difference.GetProperty("propertyName").GetString());
        Assert.Equal("Old", difference.GetProperty("oldValue").GetString());
        Assert.Equal("New", difference.GetProperty("newValue").GetString());
    }
    
    // -----------------------------------------------------------------------
    // GetKey
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetKey_AfterAdd_ReturnsNonEmptyGuid()
    {
        await using var ctx = _factory.CreateContext();
        var entry = new AuditLogEntry("E", "k", new AuditLogEntry.Created(), DateTimeOffset.UtcNow);
        await ctx.AuditLogEntries.AddAsync(entry);
        var key = ((IRepository<AuditLogEntry, Guid>)ctx).GetKey(entry);
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
            id = await DatabaseSeedHelper.SeedAuditLogEntryAsync(seed);
        }

        await using (var ctx = _factory.CreateContext())
        {
            var tracked = await ctx.AuditLogEntries
                .Where(e => EF.Property<Guid>(e, "Id") == id)
                .FirstAsync();
            ((IRepository<AuditLogEntry, Guid>)ctx).Delete(tracked);
            await ctx.SaveChangesAsync();
        }

        await using var verify = _factory.CreateContext();
        var isNone = true;
        (await ((IRepository<AuditLogEntry, Guid>)verify).TryFindAsync(id))
            .Match(() => { }, _ => isNone = false);
        Assert.True(isNone);
    }
}
