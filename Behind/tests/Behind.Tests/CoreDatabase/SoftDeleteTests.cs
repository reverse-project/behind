using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Sqlite;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class SoftDeleteTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private CoreContextSqlite Audited() => _factory.CreateAuditedContext(AuditActor.Anonymous);

    private async Task SoftDeleteUserAsync(Guid userId, CoreContextSqlite? context = null)
    {
        await using var owned = context is null ? Audited() : null;
        var ctx = context ?? owned!;
        await ctx.GetRepository<HumanUser, Guid>().DeleteAsync(userId);
        await ctx.SaveChangesAsync();
    }

    private async Task HardDeleteUserAsync(Guid userId)
    {
        await using var ctx = Audited();
        await ctx.GetHardDeleteRepository<HumanUser, Guid>().HardDeleteAsync(userId);
        await ctx.SaveChangesAsync();
    }

    private static Task<bool> UserExistsAsync(CoreContextSqlite ctx, Guid userId) =>
        ctx.Users.AnyAsync(u => EF.Property<Guid>(u, "Id") == userId);

    private static Task<bool> UserRowExistsAsync(CoreContextSqlite ctx, Guid userId) =>
        ctx.Users.IgnoreQueryFilters().AnyAsync(u => EF.Property<Guid>(u, "Id") == userId);

    private async Task<List<AuditLogEntry.AuditAction>> ActionsAsync(string entityName, Guid key)
    {
        await using var ctx = _factory.CreateContext();
        return await ctx.AuditLogEntries
            .Where(a => a.EntityName == entityName && a.EntityKey == key.ToString())
            .Select(a => a.Action)
            .ToListAsync();
    }

    [Fact]
    public async Task Delete_User_KeepsTheRow_AndRecordsDeletedAuditEntry()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "soft@test.com");

        await SoftDeleteUserAsync(userId);

        await using var verify = _factory.CreateContext();
        Assert.True(await UserRowExistsAsync(verify, userId));
        Assert.Contains(await ActionsAsync("User", userId), a => a is AuditLogEntry.Deleted);
    }

    [Fact]
    public async Task Delete_User_HidesUserFromDefaultQueries()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "hidden@test.com");

        await SoftDeleteUserAsync(userId);

        await using var verify = _factory.CreateContext();
        Assert.False(await UserExistsAsync(verify, userId));
    }

    [Fact]
    public async Task User_WithoutDeletedAuditEntry_StaysVisible()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "visible@test.com");
        await DatabaseSeedHelper.SeedAuditLogEntryAsync(seed);

        Assert.True(await UserExistsAsync(seed, userId));
    }

    [Fact]
    public async Task Delete_User_CascadesToItsEmails()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "primary@test.com");
        await DatabaseSeedHelper.SeedEmailAsync(seed, userId, "secondary@test.com");

        await SoftDeleteUserAsync(userId);

        await using var verify = _factory.CreateContext();
        Assert.False(await verify.Emails.AnyAsync(e => EF.Property<Guid>(e, "UserId") == userId));
        Assert.Equal(
            2,
            await verify.Emails.IgnoreQueryFilters().CountAsync(e => EF.Property<Guid>(e, "UserId") == userId));
    }

    [Fact]
    public async Task Delete_User_WithTrackedEmails_CascadesToThem()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "tracked@test.com");

        await using (var ctx = Audited())
        {
            await ctx.Emails.Where(e => EF.Property<Guid>(e, "UserId") == userId).ToListAsync();
            await SoftDeleteUserAsync(userId, ctx);
        }

        await using var verify = _factory.CreateContext();
        Assert.False(await verify.Emails.AnyAsync(e => EF.Property<Guid>(e, "UserId") == userId));
    }

    [Fact]
    public async Task Delete_User_PhysicallyRemovesItsNonSoftDeletableDependents()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "sessions@test.com");
        var session = new ValidSession(
            AccessToken.Create(new GuestUser(None), DateTimeOffset.UtcNow, TimeSpan.FromMinutes(15)),
            RefreshToken.Create(new GuestUser(None), DateTimeOffset.UtcNow, TimeSpan.FromDays(7)),
            "10.0.0.1",
            "agent");
        var sessionId = await DatabaseSeedHelper.SeedSessionAsync(seed, session, userId);

        await SoftDeleteUserAsync(userId);

        await using var verify = _factory.CreateContext();
        Assert.False(await verify.Set<ValidSession>().AnyAsync(s => EF.Property<Guid>(s, "Id") == sessionId));
    }

    [Fact]
    public async Task IncludeDeleted_ShowsDeletedRecords_UntilScopeIsDisposed()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "admin-view@test.com");
        await SoftDeleteUserAsync(userId);

        await using var ctx = _factory.CreateContext();

        using (ctx.SoftDelete.IncludeDeleted())
        {
            Assert.True(await UserExistsAsync(ctx, userId));
            Assert.True(await ctx.Emails.AnyAsync(e => e.Value == "admin-view@test.com"));
        }

        Assert.False(await UserExistsAsync(ctx, userId));
    }

    [Fact]
    public async Task IncludeDeleted_OnOneContext_DoesNotAffectAnotherContext()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "isolated@test.com");
        await SoftDeleteUserAsync(userId);

        await using var admin = _factory.CreateContext();
        await using var regular = _factory.CreateContext();

        using (admin.SoftDelete.IncludeDeleted())
        {
            Assert.True(await UserExistsAsync(admin, userId));
            Assert.False(await UserExistsAsync(regular, userId));
        }
    }

    [Theory]
    [InlineData(typeof(Grant))]
    [InlineData(typeof(UserRole))]
    public async Task Delete_Aggregate_IsSoftDeletedAndVisibleWithSwitch(Type aggregate)
    {
        await using var seed = _factory.CreateContext();
        var id = aggregate == typeof(Grant)
            ? await DatabaseSeedHelper.SeedGrantAsync(seed, "to-delete")
            : await DatabaseSeedHelper.SeedUserRoleAsync(seed, "to-delete");

        await using (var ctx = Audited())
        {
            await (aggregate == typeof(Grant)
                ? ctx.GetRepository<Grant, Guid>().DeleteAsync(id)
                : ctx.GetRepository<UserRole, Guid>().DeleteAsync(id));
            await ctx.SaveChangesAsync();
        }

        await using var verify = _factory.CreateContext();
        Assert.False(await verify.Grants.AnyAsync(g => g.Name == "to-delete"));
        Assert.False(await verify.Roles.AnyAsync(r => r.Name == "to-delete"));

        using (verify.SoftDelete.IncludeDeleted())
        {
            var found = aggregate == typeof(Grant)
                ? await verify.Grants.AnyAsync(g => g.Name == "to-delete")
                : await verify.Roles.AnyAsync(r => r.Name == "to-delete");
            Assert.True(found);
        }
    }

    [Fact]
    public async Task HardDelete_User_RemovesTheRowPermanently_AndAuditsIt()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "purge@test.com");

        await HardDeleteUserAsync(userId);

        await using var verify = _factory.CreateContext();
        using (verify.SoftDelete.IncludeDeleted())
        {
            Assert.False(await UserRowExistsAsync(verify, userId));
        }

        var actions = await ActionsAsync("User", userId);
        Assert.Contains(actions, a => a is AuditLogEntry.HardDeleted);
        Assert.DoesNotContain(actions, a => a is AuditLogEntry.Deleted);
    }

    [Fact]
    public async Task HardDelete_User_PermanentlyRemovesItsEmails_AndAuditsThem()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "purge-emails@test.com");
        var emailId = await DatabaseSeedHelper.SeedEmailAsync(seed, userId, "purge-second@test.com");

        await HardDeleteUserAsync(userId);

        await using var verify = _factory.CreateContext();
        using (verify.SoftDelete.IncludeDeleted())
        {
            Assert.False(await verify.Emails.AnyAsync(e => EF.Property<Guid>(e, "UserId") == userId));
        }

        Assert.Contains(await ActionsAsync("Email", emailId), a => a is AuditLogEntry.HardDeleted);
    }

    [Fact]
    public async Task HardDelete_AfterSoftDelete_RemovesTheRow_AndKeepsBothAuditEntries()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "two-step@test.com");
        var emailId = await DatabaseSeedHelper.SeedEmailAsync(seed, userId, "two-step-second@test.com");
        await SoftDeleteUserAsync(userId);

        await HardDeleteUserAsync(userId);

        await using var verify = _factory.CreateContext();
        using (verify.SoftDelete.IncludeDeleted())
        {
            Assert.False(await UserRowExistsAsync(verify, userId));
            Assert.False(await verify.Emails.AnyAsync(e => EF.Property<Guid>(e, "Id") == emailId));
        }

        var actions = await ActionsAsync("User", userId);
        Assert.Contains(actions, a => a is AuditLogEntry.Deleted);
        Assert.Contains(actions, a => a is AuditLogEntry.HardDeleted);
        Assert.Contains(await ActionsAsync("Email", emailId), a => a is AuditLogEntry.HardDeleted);
    }

    [Fact]
    public async Task HardDelete_UserReferencedByAuditEntries_IsRejected()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "attributed@test.com");
        await DatabaseSeedHelper.SeedAuditLogEntryAsync(seed, userId);

        await Assert.ThrowsAsync<DbUpdateException>(() => HardDeleteUserAsync(userId));

        await using var verify = _factory.CreateContext();
        Assert.True(await UserExistsAsync(verify, userId));
    }

    [Fact]
    public async Task HardDelete_ByAggregate_RemovesTheRowPermanently()
    {
        await using var seed = _factory.CreateContext();
        var grantId = await DatabaseSeedHelper.SeedGrantAsync(seed, "purge-grant");

        await using (var ctx = Audited())
        {
            var grant = await ctx.Grants.SingleAsync(g => g.Name == "purge-grant");
            ctx.GetHardDeleteRepository<Grant, Guid>().HardDelete(grant);
            await ctx.SaveChangesAsync();
        }

        await using var verify = _factory.CreateContext();
        using (verify.SoftDelete.IncludeDeleted())
        {
            Assert.False(await verify.Grants.AnyAsync(g => g.Name == "purge-grant"));
        }

        Assert.Contains(await ActionsAsync("Grant", grantId), a => a is AuditLogEntry.HardDeleted);
    }

    [Fact]
    public async Task HardDelete_DoesNotLeakIntoLaterSoftDeletesOnTheSameContext()
    {
        await using var seed = _factory.CreateContext();
        var purgedId = await DatabaseSeedHelper.SeedGrantAsync(seed, "purged");
        var keptId = await DatabaseSeedHelper.SeedGrantAsync(seed, "soft-kept");

        await using (var ctx = Audited())
        {
            await ctx.GetHardDeleteRepository<Grant, Guid>().HardDeleteAsync(purgedId);
            await ctx.SaveChangesAsync();

            await ctx.GetRepository<Grant, Guid>().DeleteAsync(keptId);
            await ctx.SaveChangesAsync();
        }

        await using var verify = _factory.CreateContext();
        using (verify.SoftDelete.IncludeDeleted())
        {
            Assert.False(await verify.Grants.AnyAsync(g => g.Name == "purged"));
            Assert.True(await verify.Grants.AnyAsync(g => g.Name == "soft-kept"));
        }
    }

    [Fact]
    public async Task Delete_User_IsAuditedAsDeleted_NotAsUpdate()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "audited@test.com");

        await SoftDeleteUserAsync(userId);

        var actions = await ActionsAsync("User", userId);
        Assert.Contains(actions, a => a is AuditLogEntry.Deleted);
        Assert.DoesNotContain(actions, a => a is AuditLogEntry.Updated);
    }

    [Fact]
    public void SaveChanges_Synchronously_IsNotSupported()
    {
        using var ctx = _factory.CreateContext();

        Assert.Throws<NotSupportedException>(() => ctx.SaveChanges());
    }
}
