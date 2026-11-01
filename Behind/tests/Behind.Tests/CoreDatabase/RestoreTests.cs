using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Sqlite;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class RestoreTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private CoreContextSqlite Audited() => _factory.CreateAuditedContext(AuditActor.Anonymous);

    private async Task DeleteUserAsync(Guid userId)
    {
        await using var ctx = Audited();
        await ctx.GetRepository<HumanUser, Guid>().DeleteAsync(userId);
        await ctx.SaveChangesAsync();
    }

    private async Task RestoreUserAsync(Guid userId)
    {
        await using var ctx = Audited();
        await ctx.GetRestoreRepository<HumanUser, Guid>().RestoreAsync(userId);
        await ctx.SaveChangesAsync();
    }

    private async Task<bool> UserVisibleAsync(Guid userId)
    {
        await using var ctx = _factory.CreateContext();
        return await ctx.Users.AnyAsync(user => EF.Property<Guid>(user, "Id") == userId);
    }

    private async Task<bool> EmailVisibleAsync(Guid userId)
    {
        await using var ctx = _factory.CreateContext();
        return await ctx.Emails.AnyAsync(email => EF.Property<Guid>(email, "UserId") == userId);
    }

    private async Task<List<AuditLogEntry.AuditAction>> ActionsAsync(string entityName, Guid key)
    {
        await using var ctx = _factory.CreateContext();
        return await ctx.AuditLogEntries
            .Where(audit => audit.EntityName == entityName && audit.EntityKey == key.ToString())
            .Select(audit => audit.Action)
            .ToListAsync();
    }

    [Fact]
    public async Task Restore_DeletedUser_MakesItVisibleAgain()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "restore@test.com");
        await DeleteUserAsync(userId);
        Assert.False(await UserVisibleAsync(userId));

        await RestoreUserAsync(userId);

        Assert.True(await UserVisibleAsync(userId));
    }

    [Fact]
    public async Task Restore_DeletedUser_RestoresItsCascadedEmails()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "primary-restore@test.com");
        await DatabaseSeedHelper.SeedEmailAsync(seed, userId, "secondary-restore@test.com");
        await DeleteUserAsync(userId);
        Assert.False(await EmailVisibleAsync(userId));

        await RestoreUserAsync(userId);

        await using var verify = _factory.CreateContext();
        Assert.Equal(2, await verify.Emails.CountAsync(email => EF.Property<Guid>(email, "UserId") == userId));
    }

    [Fact]
    public async Task Restore_DeletedUser_KeepsTheDeletionAuditTrail_AndRecordsRestored()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "trail@test.com");
        await DeleteUserAsync(userId);

        await RestoreUserAsync(userId);

        var actions = await ActionsAsync("User", userId);
        Assert.Single(actions, action => action is AuditLogEntry.Deleted);
        Assert.Single(actions, action => action is AuditLogEntry.Restored);
    }

    [Fact]
    public async Task Restore_ActiveUser_DoesNothing()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "active@test.com");

        await RestoreUserAsync(userId);

        Assert.True(await UserVisibleAsync(userId));
        Assert.DoesNotContain(await ActionsAsync("User", userId), action => action is AuditLogEntry.Restored);
    }

    [Fact]
    public async Task Restore_UnknownUser_DoesNothing()
    {
        var unknown = Guid.NewGuid();

        await RestoreUserAsync(unknown);

        Assert.Empty(await ActionsAsync("User", unknown));
    }

    [Fact]
    public async Task Delete_AfterRestore_HidesTheUserAgain()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "again@test.com");
        await DeleteUserAsync(userId);
        await RestoreUserAsync(userId);

        await DeleteUserAsync(userId);

        Assert.False(await UserVisibleAsync(userId));
        Assert.False(await EmailVisibleAsync(userId));
    }

    [Fact]
    public async Task Restore_UserDeletedTwice_NeedsTwoRestores()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "twice@test.com");
        await DeleteUserAsync(userId);
        await RestoreUserAsync(userId);
        await DeleteUserAsync(userId);

        await RestoreUserAsync(userId);

        Assert.True(await UserVisibleAsync(userId));
    }

    [Fact]
    public async Task Restore_WithoutAuditing_DoesNotThrow()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "plain@test.com");

        await using var ctx = _factory.CreateContext();
        await ctx.GetRestoreRepository<HumanUser, Guid>().RestoreAsync(userId);
        await ctx.SaveChangesAsync();

        Assert.True(await UserVisibleAsync(userId));
    }
}
