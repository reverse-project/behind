using Behind.Core.Database.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Behind.Tests.CoreDatabase;

[Trait("Category", "Integration")]
public sealed class ContentAgeOptionPostgresIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SeedsAgesAndUtcAuditTicksOnFreshAndPopulatedDatabases(bool upgrade)
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        var image = configuration["Postgres:Image"]
            ?? throw new InvalidOperationException("Postgres:Image must be configured in postgresql.testsettings.json.");
        await using var postgres = new PostgreSqlBuilder(image)
            .WithLabel("behind.test", "content-age-options")
            .Build();
        await postgres.StartAsync();
        await using var context = new CoreContextPostgres(
            new DbContextOptionsBuilder<CoreContextPostgres>().UseNpgsql(postgres.GetConnectionString()).Options);
        if (upgrade)
        {
            await context.GetService<IMigrator>().MigrateAsync("20261112195911_ContentLocales");
            var key = Guid.NewGuid();
            foreach (var (version, age) in new[] { (1L, 13), (2L, 21), (3L, 0), (4L, 12), (5L, 21), (6L, -1), (7L, 200) })
            {
                await context.Database.ExecuteSqlAsync($"""
                    INSERT INTO "ContentRestrictions" ("Key", "Version", "EntityType", "EntityId", "MinimumAge")
                    VALUES ({key}, {version}, {"News"}, {key}, {age})
                    """);
            }
        }

        var before = DateTimeOffset.UtcNow;
        await context.Database.MigrateAsync();
        var after = DateTimeOffset.UtcNow;
        var rows = await context.ContentAgeOptions.OrderBy(item => item.Age).ToListAsync();
        Assert.Equal(upgrade ? [12, 13, 16, 18, 21, 200] : new[] { 12, 16, 18 }, rows.Select(item => item.Age));
        Assert.All(rows, item => Assert.Equal(item.Age.ToString(), item.Name));
        Assert.All(rows, item => Assert.Equal(1, context.Entry(item).VersionedId.Version));
        Assert.Equal(upgrade ? 7 : 0, await context.ContentRestrictions.CountAsync());
        var audits = await context.AuditLogEntries.Where(item => item.EntityName == nameof(ContentAgeOption)).ToListAsync();
        Assert.Equal(rows.Count, audits.Count);
        Assert.All(audits, item =>
        {
            Assert.IsType<AuditLogEntry.Created>(item.Action);
            Assert.Equal(TimeSpan.Zero, item.Timestamp.Offset);
            Assert.InRange(item.Timestamp, before, after);
        });
        Assert.All(rows, item => Assert.Contains(audits, audit =>
            audit.EntityKey == $"{context.Entry(item).VersionedId.Key}:1"));
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());

        await context.Database.MigrateAsync();

        Assert.Equal(rows.Count, await context.ContentAgeOptions.CountAsync());
        Assert.Equal(audits.Count, await context.AuditLogEntries.CountAsync(item => item.EntityName == nameof(ContentAgeOption)));
    }
}
