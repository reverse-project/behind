using Behind.Core.Database.Auditing;
using Behind.Common.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Queries;
using Behind.Core.Database;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

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
    [Trait("Category", "Integration")]
    public async Task PostgresUnsetSwitchPinOnlyRemovesTheSelectedCredentialAndUpdatesUserListing()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        var image = configuration["Postgres:Image"] ?? throw new InvalidOperationException("Postgres:Image is required.");
        await using var container = new PostgreSqlBuilder(image).Build();
        await container.StartAsync();
        var options = new DbContextOptionsBuilder<CoreContextPostgres>().UseNpgsql(container.GetConnectionString())
            .AddInterceptors(Auditing()).Options;
        await using var db = new CoreContextPostgres(options);
        await db.Database.MigrateAsync();
        await VerifyUnsetSwitchPin(db);
    }

    [Fact]
    public async Task UnsetSwitchPinOnlyRemovesTheSelectedCredentialAndUpdatesUserListing()
    {
        await using var db = _factory.CreateAuditedContext(AuditActor.Anonymous);
        await VerifyUnsetSwitchPin(db);
    }

    private static AuditSaveChangesInterceptor Auditing() =>
        new(() => new FixedAuditActorProvider(AuditActor.Anonymous), TimeProvider.System);

    private static async Task VerifyUnsetSwitchPin(AbstractCoreContext db)
    {
        var target = new HumanUser("Managed target", [new Email("target@example.test")], None);
        var other = new HumanUser("Managed peer", [new Email("peer@example.test")], None);
        db.UserSwitchPins.AddRange(new UserSwitchPin(target, new PinHash("target-hash")), new UserSwitchPin(other, new PinHash("peer-hash")));
        await db.SaveChangesAsync();

        var targetId = db.GetKey(target);
        var queries = new UserManagementQueries(db);
        async Task<bool> HasPin(Guid id) =>
            (await queries.TryFindUser(id, DeletionState.Active, DateTimeOffset.UtcNow)).Match(() => false, user => user.HasSwitchPin);

        Assert.True(await HasPin(targetId));
        var commands = new UserCommands(db);
        await commands.UnsetSwitchPin(targetId);
        await commands.UnsetSwitchPin(targetId);
        Assert.False(await HasPin(targetId));
        Assert.True(await HasPin(db.GetKey(other)));
        using (db.SoftDelete.IncludeDeleted())
        {
            var unsetPin = await db.UserSwitchPins.SingleAsync(pin => EF.Property<Guid>(pin, "UserId") == targetId);
            Assert.True(await db.Entry(unsetPin).IsDeletedAsync());
            db.Restores.Mark(unsetPin);
            db.Entry(unsetPin).Property(pin => pin.PinHash).CurrentValue = new PinHash("restored-hash");
        }
        await db.SaveChangesAsync();
        Assert.True(await HasPin(targetId));
        using (db.SoftDelete.IncludeDeleted())
        {
            var restoredPin = await db.UserSwitchPins.SingleAsync(pin => EF.Property<Guid>(pin, "UserId") == targetId);
            Assert.False(await db.Entry(restoredPin).IsDeletedAsync());
        }
    }

    [Fact]
    public async Task DisplayNameSaveDoesNotRestoreStaleEmailVisibility()
    {
        var userId = await SeedUserAsync("privacy@test.com");
        await using var stale = _factory.CreateContext();
        var snapshot = await stale.Users.SingleAsync(user => EF.Property<Guid>(user, "Id") == userId);
        await using (var privacy = _factory.CreateContext())
        {
            var current = await privacy.Users.SingleAsync(user => EF.Property<Guid>(user, "Id") == userId);
            privacy.Entry(current).Property(user => user.HideEmail).CurrentValue = !snapshot.HideEmail;
            await privacy.SaveChangesAsync();
        }
        Assert.True(await new UserCommands(stale).ChangeDisplayName(userId, "New name"));
        await using var reader = _factory.CreateContext();
        var saved = await reader.Users.SingleAsync(user => EF.Property<Guid>(user, "Id") == userId);
        Assert.Equal(!snapshot.HideEmail, saved.HideEmail);
        Assert.Equal("New name", saved.DisplayName);
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
    public async Task HardDelete_AfterDelete_RemovesTheRowAndReportsDeleted()
    {
        var userId = await SeedUserAsync("purge@test.com");

        await using (var ctx = _factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            await new UserCommands(ctx).Delete(userId);
        }

        await using (var ctx = _factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            Assert.Equal(IUserCommands.HardDeleteOutcome.Deleted, await new UserCommands(ctx).HardDelete(userId));
        }

        Assert.False(await RowExistsAsync(userId));
    }

    [Fact]
    public async Task HardDelete_UserReferencedByAuditEntries_ReportsRejectionAndKeepsTheRow()
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
            Assert.Equal(IUserCommands.HardDeleteOutcome.Rejected, await new UserCommands(ctx).HardDelete(userId));
        }

        Assert.True(await RowExistsAsync(userId));
    }
}
