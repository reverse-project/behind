using Behind.Core.Database;
using Behind.Tests.Helpers;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Tests.CoreDatabase;

public sealed class ScheduleMigrationTests
{
    [Fact]
    public async Task Sqlite_UpgradesTheExistingSchemaAndKeepsExistingRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new CoreContextSqlite(
            new DbContextOptionsBuilder<CoreContextSqlite>().UseSqlite(connection).Options);
        await context.GetService<IMigrator>().MigrateAsync("20261113150944_AddPrivateMessageEmails");
        await DatabaseSeedHelper.SeedLegacyBuiltInRolesAsync(context);
        var roles = await context.Roles.CountAsync();

        await context.Database.MigrateAsync();

        Assert.Equal(roles, await context.Roles.CountAsync());
        Assert.Empty(await context.Schedules.ToListAsync());
        Assert.Empty(await context.ScheduleRuns.ToListAsync());
        Assert.False(context.Database.HasPendingModelChanges());
        foreach (var type in new[] { typeof(Schedule), typeof(ScheduleRun) })
        {
            var table = context.Model.FindEntityType(type)!;
            Assert.Equal(["Key", "Version"], table.FindPrimaryKey()!.Properties.Select(property => property.Name));
            Assert.DoesNotContain(table.GetProperties(), property => property.Name is "CreatedAt" or "UpdatedAt");
        }
    }

    [Fact]
    public void Postgres_GeneratesTheSchemaWithoutPendingModelChanges()
    {
        using var context = new CoreContextPostgres(
            new DbContextOptionsBuilder<CoreContextPostgres>().UseNpgsql("Host=localhost;Database=behind").Options);
        var sql = context.GetService<IMigrator>().GenerateScript(
            "20261113150946_AddPrivateMessageEmails", "20261115232216_AddSchedules");

        Assert.Contains("CREATE TABLE \"Schedules\"", sql);
        Assert.Contains("CREATE TABLE \"ScheduleRuns\"", sql);
        Assert.Contains("\"StartedAt\" bigint", sql);
        Assert.False(context.Database.HasPendingModelChanges());
    }
}
