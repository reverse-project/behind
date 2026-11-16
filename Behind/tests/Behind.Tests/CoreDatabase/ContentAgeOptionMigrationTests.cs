using Behind.Core.Database.Postgres;
using Behind.Core.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Tests.CoreDatabase;

public sealed class ContentAgeOptionMigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SqliteSeedsStandardAndAllDistinctPositiveHistoricalAgesOnFreshAndPopulatedDatabases(bool upgrade)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = new CoreContextSqlite(
            new DbContextOptionsBuilder<CoreContextSqlite>().UseSqlite(connection).Options);
        if (upgrade)
        {
            await context.GetService<IMigrator>().MigrateAsync("20261112195936_ContentLocales");
            var key = Guid.NewGuid();
            foreach (var (version, age) in new[] { (1, 13), (2, 21), (3, 0), (4, 12), (5, 21), (6, -1), (7, 200) })
            {
                await context.Database.ExecuteSqlAsync($"""
                    INSERT INTO "ContentRestrictions" ("Key", "Version", "EntityType", "EntityId", "MinimumAge")
                    VALUES ({key}, {version}, {"News"}, {key}, {age})
                    """);
            }
        }
        await context.Database.MigrateAsync();
        var rows = await context.ContentAgeOptions.OrderBy(item => item.Age).ToListAsync();
        Assert.Equal(upgrade ? [12, 13, 16, 18, 21, 200] : new[] { 12, 16, 18 }, rows.Select(item => item.Age));
        Assert.All(rows, item => Assert.Equal(item.Age.ToString(), item.Name));
        Assert.All(rows, item => Assert.Equal(1, context.Entry(item).VersionedId.Version));
        Assert.Equal(rows.Count, rows.Select(item => context.Entry(item).VersionedId.Key).Distinct().Count());
        Assert.Equal(upgrade ? 7 : 0, await context.ContentRestrictions.CountAsync());
        var audits = await context.AuditLogEntries.Where(item => item.EntityName == nameof(ContentAgeOption)).ToListAsync();
        Assert.Equal(rows.Count, audits.Count);
        Assert.All(audits, item => Assert.IsType<AuditLogEntry.Created>(item.Action));
        Assert.All(rows, item => Assert.Contains(audits, audit =>
            audit.EntityKey == $"{context.Entry(item).VersionedId.Key}:1"));
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task SqliteNameUpgradeBackfillsEveryVersionAndPreservesExistingData()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = new CoreContextSqlite(
            new DbContextOptionsBuilder<CoreContextSqlite>().UseSqlite(connection).Options);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20261113105942_ContentAgeOptions");
        var key = Guid.NewGuid();
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO "ContentAgeOptions" ("Key", "Version", "Age") VALUES ({key}, {1}, {25}), ({key}, {2}, {26})
            """);
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO "ContentRestrictions" ("Key", "Version", "EntityType", "EntityId", "MinimumAge")
            VALUES ({key}, {1}, {"News"}, {key}, {25})
            """);
        var deletedId = Guid.NewGuid();
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO "AuditLogEntries" ("Id", "EntityName", "EntityKey", "Action", "Timestamp", "UserId", "BehalfOfId")
            VALUES ({deletedId}, {"ContentAgeOption"}, {$"{key}:1"}, {"Deleted"}, {"2026-01-01 00:00:00+00:00"}, {null}, {null})
            """);
        await context.Database.MigrateAsync();
        var history = await context.ContentAgeOptions.IgnoreQueryFilters().Where(item => EF.Property<Guid>(item, "Key") == key)
            .OrderBy(item => EF.Property<long>(item, "Version")).ToListAsync();
        Assert.Equal(new[] { "25", "26" }, history.Select(item => item.Name));
        Assert.Equal(new[] { 25, 26 }, history.Select(item => item.Age));
        Assert.Equal(25, Assert.Single(await context.ContentRestrictions.ToListAsync()).MinimumAge);
        Assert.Equal(4, await context.AuditLogEntries.CountAsync(item => item.EntityName == nameof(ContentAgeOption)));
        Assert.Contains(await context.AuditLogEntries.ToListAsync(),
            item => item.EntityKey == $"{key}:1" && item.Action is AuditLogEntry.Deleted);
        await context.Database.ExecuteSqlAsync($"""UPDATE "ContentAgeOptions" SET "Name" = {"Custom"} WHERE "Key" = {key} AND "Version" = {2}""");
        await context.Database.MigrateAsync();
        context.ChangeTracker.Clear();
        Assert.Equal("Custom", (await context.ContentAgeOptions.IgnoreQueryFilters()
            .SingleAsync(item => EF.Property<Guid>(item, "Key") == key && EF.Property<long>(item, "Version") == 2)).Name);
    }

    [Fact]
    public void PostgresMigrationSeedsHistoricalAgesAndMatchesCurrentModel()
    {
        using var context = new CoreContextPostgresFactory().CreateDbContext([]);
        var sql = context.GetService<IMigrator>().GenerateScript(
            "20261112195911_ContentLocales", "20261113105945_ContentAgeOptions");
        Assert.Contains("CREATE TABLE \"ContentAgeOptions\"", sql);
        Assert.Contains("PRIMARY KEY (\"Key\", \"Version\")", sql);
        Assert.Contains("IX_ContentAgeOptions_Age", sql);
        Assert.Contains("UNION SELECT \"MinimumAge\" FROM \"ContentRestrictions\" WHERE \"MinimumAge\" > 0", sql);
        Assert.Contains("SELECT 12 AS \"Age\"", sql);
        Assert.Contains("UNION SELECT 16", sql);
        Assert.Contains("UNION SELECT 18", sql);
        Assert.Contains("'ContentAgeOption'", sql);
        Assert.Contains("'Created'", sql);
        var namesSql = context.GetService<IMigrator>().GenerateScript(
            "20261113105945_ContentAgeOptions", "20261113120001_ContentAgeOptionNames");
        Assert.Contains("UPDATE \"ContentAgeOptions\" SET \"Name\" = CAST(\"Age\" AS text)", namesSql);
        Assert.Contains("ALTER COLUMN \"Name\" SET NOT NULL", namesSql);
        Assert.DoesNotContain("ContentRestrictions", namesSql);
        Assert.False(context.Database.HasPendingModelChanges());
    }
}
