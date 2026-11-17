using Behind.Core.Database;
using Behind.Tests.Helpers;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Sqlite;
using Behind.Common;
using Behind.Core.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using static Behind.Tests.Helpers.ScheduleTestBedExtensions;

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

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Postgres_UpgradesPopulatedSchemaAndPersistsSchedulesAndRuns()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        await using var postgres = new PostgreSqlBuilder(configuration["Postgres:Image"]
            ?? throw new InvalidOperationException("Postgres:Image must be configured.")).Build();
        await postgres.StartAsync();
        await using var context = new CoreContextPostgres(
            new DbContextOptionsBuilder<CoreContextPostgres>().UseNpgsql(postgres.GetConnectionString()).Options);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20261113150946_AddPrivateMessageEmails");
        var roleId = Guid.NewGuid();
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO "Roles" ("Id", "Name", "IsDeveloper") VALUES ({roleId}, {"Existing schedule role"}, {false})
            """);

        await migrator.MigrateAsync("20261115232216_AddSchedules");
        var scheduleId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.FromHours(2));
        context.Schedules.Add(new Schedule(new VersionedKey<Guid>(scheduleId, 1), "Existing schedule", Cron(),
            "test.action", ScheduleParameters.Empty, true, ScheduleNotification.Never, ScheduleParticipants.Empty));
        context.ScheduleRuns.Add(new ScheduleRun(new VersionedKey<Guid>(Guid.NewGuid(), 1), scheduleId,
            "Existing schedule", "test.action", now, now, null, ScheduleRunStatus.Running, "", null,
            ScheduleRunEmail.NotRequested, 0));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Assert.Equal("Existing schedule role", (await context.Roles.SingleAsync()).Name);
        Assert.Equal("Existing schedule", (await context.Schedules.SingleAsync()).Name);
        var run = await context.ScheduleRuns.SingleAsync();
        Assert.Equal(scheduleId, run.ScheduleId);
        Assert.Equal(now.ToUniversalTime(), run.StartedAt);

        await migrator.MigrateAsync("20261115232216_AddSchedules");
        Assert.Single(await context.Schedules.ToListAsync());
        Assert.Single(await context.ScheduleRuns.ToListAsync());
        Assert.Single(await context.Roles.ToListAsync());
    }
}
