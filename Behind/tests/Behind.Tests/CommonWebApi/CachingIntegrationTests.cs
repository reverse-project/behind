using Behind.Common.Configuration;
using Behind.Common.WebApi;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Behind.Core.Database;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Testcontainers.PostgreSql;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Behind.Tests.CommonWebApi;

[Trait("Category", "Integration")]
public sealed class CachingIntegrationTests
{
    [Fact]
    public async Task CommittedCommandsInvalidateAggregateQueriesAcrossHostsAndRollbackDoesNotPublish()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "caching.testsettings.json"));
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        await using var valkey = new ContainerBuilder(configuration["Valkey:Image"]
            ?? throw new InvalidOperationException("Valkey:Image must be configured."))
            .WithLabel("behind.test", "query-cache")
            .WithPortBinding(6379, true)
            .WithWaitStrategy(DotNet.Testcontainers.Builders.Wait.ForUnixContainer().UntilCommandIsCompleted("valkey-cli", "ping"))
            .Build();
        await using var postgres = new PostgreSqlBuilder(configuration["Postgres:Image"]
            ?? throw new InvalidOperationException("Postgres:Image must be configured."))
            .WithLabel("behind.test", "query-cache")
            .Build();
        await valkey.StartAsync();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<CoreContextPostgres>()
            .UseNpgsql(postgres.GetConnectionString()).Options;
        using var first = CreateHost($"{valkey.Hostname}:{valkey.GetMappedPublicPort(6379)}");
        using var second = CreateHost($"{valkey.Hostname}:{valkey.GetMappedPublicPort(6379)}");
        await using var one = new CoreContextPostgres(options);
        await one.Database.MigrateAsync();
        var versioningOptions = new DbContextOptionsBuilder<VersioningContextPostgres>();
        versioningOptions.UseVersioningPostgres(postgres.GetConnectionString());
        await using var versioning = new VersioningContextPostgres(versioningOptions.Options);
        await versioning.Database.MigrateAsync();
        var statements = new ConcurrentQueue<string>();
        await using var two = new CoreContextPostgres(new DbContextOptionsBuilder<CoreContextPostgres>()
            .UseNpgsql(postgres.GetConnectionString())
            .LogTo(statements.Enqueue, [RelationalEventId.CommandExecuted]).Options);
        var firstQuery = new RawDatabaseQueries(one, first.Services.GetRequiredService<HybridCache>());
        var secondQuery = new RawDatabaseQueries(two, second.Services.GetRequiredService<HybridCache>());
        static long Grants(IReadOnlyList<IRawDatabaseQueries.RawTableListing> rows) =>
            rows.Single(row => row.Table.Name == "Grants").Rows;
        var baseline = Grants(await firstQuery.ListTables());
        Assert.Equal(baseline, Grants(await secondQuery.ListTables()));
        Assert.Single(statements);
        statements.Clear();

        var commands = new RawDatabaseCommands(one, new FixedAuditActorProvider(AuditActor.Anonymous), TimeProvider.System);
        var id = Guid.NewGuid();
        Assert.IsType<RawWriteResult.Saved>(await commands.Insert("Grants", new Dictionary<string, JsonElement>
        {
            ["Id"] = JsonSerializer.SerializeToElement(id),
            ["Name"] = JsonSerializer.SerializeToElement("not-for-cache@example.test")
        }));
        Assert.Equal(baseline + 1, Grants(await secondQuery.ListTables()));
        Assert.Equal(2, statements.Count);
        Assert.Equal(baseline + 1, Grants(await firstQuery.ListTables()));

        await using (var transaction = await one.Database.BeginTransactionAsync())
        {
            one.Grants.Add(new Grant("Rollback"));
            one.AddAuditEntry(new AuditLogEntry("Grant", Guid.NewGuid().ToString(),
                new AuditLogEntry.Created(), DateTimeOffset.UtcNow), AuditActor.Anonymous);
            await one.SaveChangesAsync();
            Assert.Equal(baseline + 2, Grants(await firstQuery.ListTables()));
            Assert.Equal(baseline + 1, Grants(await secondQuery.ListTables()));
            await transaction.RollbackAsync();
        }
        one.ChangeTracker.Clear();
        Assert.Equal(baseline + 1, Grants(await firstQuery.ListTables()));
        Assert.IsType<RawWriteResult.Deleted>(await commands.Delete("Grants",
            new Dictionary<string, JsonElement> { ["Id"] = JsonSerializer.SerializeToElement(id) }));
        Assert.Equal(baseline, Grants(await secondQuery.ListTables()));

        var user = new HumanUser("Cleanup", [new Email("cleanup@example.test")], None);
        await one.Users.AddAsync(user);
        await one.SaveChangesAsync();
        var userId = one.Entry(user).Property<Guid>("Id").CurrentValue;
        var sessions = new SessionCommands(one);
        var sessionId = await sessions.CreateForUserId(userId, new ValidSession(
            AccessToken.Create(new GuestUser(None)),
            RefreshToken.Create(new GuestUser(None)),
            "127.0.0.1", "cache-test"));
        var cutoff = DateTimeOffset.UtcNow;
        await sessions.InvalidateSession(sessionId, userId, cutoff.AddDays(-1));
        static long Sessions(IReadOnlyList<IRawDatabaseQueries.RawTableListing> rows) =>
            rows.Single(row => row.Table.Name == "Sessions").Rows;
        var sessionCount = Sessions(await firstQuery.ListTables());
        Assert.Equal(sessionCount, Sessions(await secondQuery.ListTables()));
        var auditCount = await one.AuditLogEntries.LongCountAsync();
        await using (var transaction = await one.Database.BeginTransactionAsync())
        {
            Assert.Equal(1, await sessions.DeleteInvalidatedBefore(cutoff));
            Assert.Equal(sessionCount - 1, Sessions(await firstQuery.ListTables()));
            Assert.Equal(sessionCount, Sessions(await secondQuery.ListTables()));
            await transaction.RollbackAsync();
        }
        Assert.Equal(sessionCount, Sessions(await firstQuery.ListTables()));
        Assert.Equal(1, await sessions.DeleteInvalidatedBefore(cutoff));
        Assert.Equal(auditCount, await one.AuditLogEntries.LongCountAsync());
        Assert.Equal(sessionCount - 1, Sessions(await firstQuery.ListTables()));
        Assert.Equal(sessionCount - 1, Sessions(await secondQuery.ListTables()));
    }

    [Fact]
    public async Task ValkeySharesEntriesAcrossHostsAndInvalidatesDistributedEntries()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "caching.testsettings.json"));
        var image = configuration["Valkey:Image"]
            ?? throw new InvalidOperationException("Valkey:Image must be configured.");
        await using var valkey = new ContainerBuilder(image)
            .WithLabel("behind.test", "hybridcache")
            .WithPortBinding(6379, true)
            .WithWaitStrategy(DotNet.Testcontainers.Builders.Wait.ForUnixContainer().UntilCommandIsCompleted("valkey-cli", "ping"))
            .Build();
        await valkey.StartAsync();
        var connection = $"{valkey.Hostname}:{valkey.GetMappedPublicPort(6379)}";
        using var first = CreateHost(connection);
        using var second = CreateHost(connection);
        var firstCache = first.Services.GetRequiredService<HybridCache>();
        var secondCache = second.Services.GetRequiredService<HybridCache>();
        Assert.NotNull(first.Services.GetService<IDistributedCache>());
        var calls = 0;
        ValueTask<string> Factory(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            return ValueTask.FromResult("refreshed");
        }

        var options = new HybridCacheEntryOptions { Flags = HybridCacheEntryFlags.DisableLocalCache };
        await firstCache.SetAsync("shared", "value-1", options);
        Assert.Equal("value-1", await secondCache.GetOrCreateAsync("shared", Factory, options));
        Assert.Equal(0, calls);

        await secondCache.RemoveAsync("shared");

        Assert.Equal("refreshed", await firstCache.GetOrCreateAsync("shared", Factory, options));
        Assert.Equal(1, calls);
        await firstCache.SetAsync("shared", "refreshed", options);
    }

    private static IHost CreateHost(string connection)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{CachingConfiguration.SectionName}:Provider"] = "RedisCompatible",
            [$"{CachingConfiguration.SectionName}:Key"] = "CachingConnection",
            ["ConnectionStrings:CachingConnection"] = connection
        });
        builder.AddCaching();
        return builder.Build();
    }
}
