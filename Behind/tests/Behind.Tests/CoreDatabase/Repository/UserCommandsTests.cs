using Behind.Core.Database.Auditing;
using Behind.Core.Database.Commands;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase.Repository;

public sealed class UserCommandsTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<Guid> SeedUserAsync(string email)
    {
        await using var seed = _factory.CreateContext();
        return await DatabaseSeedHelper.SeedHumanUserAsync(seed, email);
    }

    private async Task<string> DisplayNameOf(Guid userId)
    {
        await using var reader = _factory.CreateContext();
        return await reader.Users
            .Where(user => EF.Property<Guid>(user, "Id") == userId)
            .Select(user => user.DisplayName)
            .SingleAsync();
    }

    [Fact]
    public async Task ChangeDisplayName_ExistingUser_PersistsTrimmedName()
    {
        var userId = await SeedUserAsync("rename@test.com");

        await using (var ctx = _factory.CreateContext())
        {
            Assert.True(await new UserCommands(ctx).ChangeDisplayName(userId, "  Ada  "));
        }

        Assert.Equal("Ada", await DisplayNameOf(userId));
    }

    [Fact]
    public async Task ChangeDisplayName_WhitespaceName_PersistsEmptyName()
    {
        var userId = await SeedUserAsync("blank@test.com");

        await using (var ctx = _factory.CreateContext())
        {
            Assert.True(await new UserCommands(ctx).ChangeDisplayName(userId, "   "));
        }

        Assert.Equal(string.Empty, await DisplayNameOf(userId));
    }

    [Fact]
    public async Task ChangeDisplayName_UnknownUser_ReturnsFalse()
    {
        await using var ctx = _factory.CreateContext();

        Assert.False(await new UserCommands(ctx).ChangeDisplayName(Guid.NewGuid(), "Ada"));
    }

    private async Task<bool> IsVisibleAsync(Guid userId)
    {
        await using var reader = _factory.CreateContext();
        return await reader.Users.AnyAsync(user => EF.Property<Guid>(user, "Id") == userId);
    }

    private async Task<bool> RowExistsAsync(Guid userId)
    {
        await using var reader = _factory.CreateContext();
        return await reader.Users.IgnoreQueryFilters().AnyAsync(user => EF.Property<Guid>(user, "Id") == userId);
    }

    [Fact]
    public async Task Delete_HidesTheUser_ButKeepsTheRow()
    {
        var userId = await SeedUserAsync("delete@test.com");

        await using (var ctx = _factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            await new UserCommands(ctx).Delete(userId);
        }

        Assert.False(await IsVisibleAsync(userId));
        Assert.True(await RowExistsAsync(userId));
    }

    [Fact]
    public async Task Restore_AfterDelete_ShowsTheUserAgain()
    {
        var userId = await SeedUserAsync("restore@test.com");

        await using (var ctx = _factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            await new UserCommands(ctx).Delete(userId);
        }

        await using (var ctx = _factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            await new UserCommands(ctx).Restore(userId);
        }

        Assert.True(await IsVisibleAsync(userId));
    }

    [Fact]
    public async Task HardDelete_AfterDelete_RemovesTheRowAndReturnsTrue()
    {
        var userId = await SeedUserAsync("purge@test.com");

        await using (var ctx = _factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            await new UserCommands(ctx).Delete(userId);
        }

        await using (var ctx = _factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            Assert.True(await new UserCommands(ctx).HardDelete(userId));
        }

        Assert.False(await RowExistsAsync(userId));
    }

    [Fact]
    public async Task HardDelete_UserReferencedByAuditEntries_ReturnsFalseAndKeepsTheRow()
    {
        var userId = await SeedUserAsync("attributed@test.com");

        await using (var seed = _factory.CreateContext())
        {
            await DatabaseSeedHelper.SeedAuditLogEntryAsync(seed, userId);
        }

        await using (var ctx = _factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            await new UserCommands(ctx).Delete(userId);
        }

        await using (var ctx = _factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            Assert.False(await new UserCommands(ctx).HardDelete(userId));
        }

        Assert.True(await RowExistsAsync(userId));
    }
}
