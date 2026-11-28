using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Postgres;
using Behind.Core.Domain;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Behind.Tests.CoreDatabase;

[Trait("Category", "Integration")]
public sealed class ServiceUserPostgresIntegrationTests
{
    private static readonly DateTimeOffset AuditAt = new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero).AddTicks(1234567);
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FreshAndUpgradePreserveUsersAndSupportServiceActors(bool upgrade)
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        var image = configuration["Postgres:Image"]
            ?? throw new InvalidOperationException("Postgres:Image must be configured in postgresql.testsettings.json.");
        await using var postgres = new PostgreSqlBuilder(image).WithLabel("behind.test", "service-users").Build();
        await postgres.StartAsync();
        var connection = postgres.GetConnectionString();
        var options = new DbContextOptionsBuilder<CoreContextPostgres>()
            .UseNpgsql(connection)
            .AddInterceptors(new AuditSaveChangesInterceptor(
                () => new FixedAuditActorProvider(AuditActor.Anonymous), TimeProvider.System))
            .Options;
        await using var context = new CoreContextPostgres(options);
        if (upgrade)
        {
            await context.GetService<IMigrator>().MigrateAsync("20261125184523_VersionSubaccounts");
        }
        else
        {
            await context.Database.MigrateAsync();
        }

        var human = new HumanUser("Existing", [new Email("service-user-test@example.test")], None);
        await context.Users.AddAsync(human);
        var humanId = context.Entry(human).Property<Guid>("Id").CurrentValue;
        await context.SaveChangesAsync();
        await context.EnsureBuiltInRolesAsync(AccessCatalog.Core);
        await context.ReplaceUserAccessAsync(humanId, [BuiltInAccess.Users.Id], new AccessSelection([], []));
        await context.Database.MigrateAsync();
        Assert.True(await context.Users.AnyAsync(user => EF.Property<Guid>(user, "Id") == humanId));
        Assert.Contains((await context.GetUserAccessAsync(humanId)).Roles, role => role.Name == BuiltInAccess.Users.Role.Name);
        var store = new ServiceUserStore(context, TimeProvider.System);
        var credential = (await store.Create("integration", CancellationToken.None))
            .Match(() => throw new InvalidOperationException(), value => value);
        var id = credential.User.Id;
        Assert.False((await store.Authenticate(credential.ApiKey, CancellationToken.None)).IsNone());
        var rotated = (await store.Rotate(id, 1, CancellationToken.None))
            .Match(() => throw new InvalidOperationException(), value => value);
        Assert.True((await store.Authenticate(credential.ApiKey, CancellationToken.None)).IsNone());
        Assert.False((await store.Authenticate(rotated.ApiKey, CancellationToken.None)).IsNone());
        var actorOptions = new DbContextOptionsBuilder<CoreContextPostgres>()
            .UseNpgsql(connection)
            .AddInterceptors(new AuditSaveChangesInterceptor(
                () => new FixedAuditActorProvider(AuditActor.ForUser(id)), new FixedTimeProvider(AuditAt)))
            .Options;
        await using var actorContext = new CoreContextPostgres(actorOptions);
        var news = new News("Service authored", "summary", "content", false);
        await actorContext.NewsEntries.AddAsync(news);
        var newsId = Guid.NewGuid();
        actorContext.Entry(news).StampVersionedKey(newsId, 1);
        actorContext.Entry(news).Property<Guid>(NewsConfiguration.AuthorIdProperty).CurrentValue = id;
        await actorContext.SaveChangesAsync();
        var audit = await actorContext.AuditLogEntries.AsNoTracking()
            .Where(entry => entry.EntityName == nameof(News))
            .Select(entry => new { entry.Timestamp, UserId = EF.Property<Guid?>(entry, "UserId") })
            .SingleAsync();
        Assert.Equal(id, audit.UserId);
        Assert.Equal(TimeSpan.Zero, audit.Timestamp.Offset);
        Assert.Equal(AuditAt.UtcTicks, audit.Timestamp.UtcTicks);
        Assert.Contains((await context.GetUserAccessAsync(id)).Roles, role => role.Name == BuiltInAccess.Users.Role.Name);
        await using var firstContext = new CoreContextPostgres(options);
        await using var secondContext = new CoreContextPostgres(options);
        var rotations = await Task.WhenAll(
            new ServiceUserStore(firstContext, TimeProvider.System).Rotate(id, 2, CancellationToken.None),
            new ServiceUserStore(secondContext, TimeProvider.System).Rotate(id, 2, CancellationToken.None));
        Assert.Single(rotations, result => !result.IsNone());
        Assert.Single(rotations, result => result.IsNone());
        Assert.True((await store.Authenticate(rotated.ApiKey, CancellationToken.None)).IsNone());
        Assert.True(await store.Delete(id, 3, CancellationToken.None));
        Assert.False(await context.NewsEntries.AnyAsync(value => EF.Property<Guid>(value, VersionedColumns.Key) == newsId));
        Assert.True(await store.Restore(id, 3, CancellationToken.None));
        Assert.True(await context.NewsEntries.AnyAsync(value => EF.Property<Guid>(value, VersionedColumns.Key) == newsId));
        var successfulRotation = rotations.Single(result => !result.IsNone()).Match(
            () => throw new InvalidOperationException(), value => value);
        Assert.True((await store.Authenticate(successfulRotation.ApiKey, CancellationToken.None)).IsNone());
        Assert.True(await store.SetEnabled(id, 4, true, CancellationToken.None));
        var restored = (await store.Rotate(id, 5, CancellationToken.None)).Match(
            () => throw new InvalidOperationException(), value => value);
        Assert.False((await store.Authenticate(restored.ApiKey, CancellationToken.None)).IsNone());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
        await context.Database.MigrateAsync();
        Assert.Single(await context.ServiceUsers.ToListAsync());
    }
}
